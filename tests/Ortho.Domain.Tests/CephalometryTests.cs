using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Domain.Cephalometry;

namespace Ortho.Domain.Tests;

public class CephalometryTests
{
    // ---------- Intégrité des templates (les analyses sont des données : on les valide) ----------

    [Fact]
    public void All_templates_reference_only_catalogued_landmarks()
    {
        foreach (var template in AnalysisTemplates.All)
        foreach (var code in template.RequiredLandmarks)
            Assert.True(LandmarkCatalog.All.ContainsKey(code),
                $"{template.Code} référence un landmark inconnu : {code}");
    }

    [Fact]
    public void All_templates_have_unique_measure_codes_and_positive_sd()
    {
        foreach (var template in AnalysisTemplates.All)
        {
            Assert.Equal(template.Measures.Count, template.Measures.Select(m => m.Code).Distinct().Count());
            Assert.All(template.Measures, m => Assert.True(m.NormSd > 0, $"{m.Code} : écart-type nul"));
        }
    }

    [Fact]
    public void Derived_measures_reference_previously_defined_measures()
    {
        foreach (var template in AnalysisTemplates.All)
        {
            var seen = new HashSet<string>();
            foreach (var measure in template.Measures)
            {
                foreach (var operand in measure.Operands ?? [])
                    Assert.Contains(operand, seen);
                seen.Add(measure.Code);
            }
        }
    }

    [Fact]
    public void Steiner_and_tweed_have_expected_measures()
    {
        Assert.Equal(11, AnalysisTemplates.Steiner.Measures.Count);
        Assert.Equal(3, AnalysisTemplates.Tweed.Measures.Count);
    }

    // ---------- Calculs Steiner sur géométrie construite ----------

    /// <summary>
    /// Géométrie synthétique : N à l'origine, S au-dessus (y négatif = haut de l'image).
    /// A et B placés pour produire exactement SNA = 82°, SNB = 80° → ANB = 2°.
    /// </summary>
    private static Dictionary<string, ImagePoint> SteinerBase()
    {
        const double d = 100;
        ImagePoint OnRay(double degrees) => new(
            d * Math.Sin(degrees * Math.PI / 180),
            d * Math.Cos(degrees * Math.PI / 180)); // 0° = vers le bas depuis N, sens horaire vers l'avant

        return new Dictionary<string, ImagePoint>
        {
            ["S"] = new(0, -d),   // N→S vers le haut
            ["N"] = new(0, 0),
            ["A"] = OnRay(180 - 82), // angle S-N-A = 82°
            ["B"] = OnRay(180 - 80), // angle S-N-B = 80°
        };
    }

    [Fact]
    public void Steiner_SNA_SNB_and_ANB_match_constructed_angles()
    {
        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Steiner, SteinerBase(), null, null)
            .ToDictionary(r => r.Code);

        Assert.Equal(82, results["SNA"].Value!.Value, precision: 6);
        Assert.Equal(80, results["SNB"].Value!.Value, precision: 6);
        Assert.Equal(2, results["ANB"].Value!.Value, precision: 6);
        Assert.Equal(MeasureStatus.Normal, results["SNA"].Status);
        Assert.Equal(MeasureStatus.Normal, results["ANB"].Status);
    }

    [Fact]
    public void Angles_are_computed_even_without_calibration_but_distances_are_not()
    {
        var landmarks = SteinerBase();
        landmarks["U1E"] = new ImagePoint(50, 30);

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Steiner, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.NotNull(results["SNA"].Value);
        Assert.Null(results["U1-NA-mm"].Value); // mm impossible sans calibration
        Assert.Equal(MeasureStatus.NotComputable, results["U1-NA-mm"].Status);
    }

    [Fact]
    public void Point_to_line_distance_uses_calibration()
    {
        // Droite NA verticale (x = 0) ; U1E à 6 px → 6 × 0,5 mm = 3 mm.
        var landmarks = SteinerBase();
        landmarks["N"] = new ImagePoint(0, 0);
        landmarks["A"] = new ImagePoint(0, 100);
        landmarks["U1E"] = new ImagePoint(6, 50);

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Steiner, landmarks, 0.5, 0.5)
            .ToDictionary(r => r.Code);

        Assert.Equal(3, results["U1-NA-mm"].Value!.Value, precision: 6);
    }

    [Fact]
    public void Missing_landmark_yields_not_computable_without_blocking_others()
    {
        var landmarks = SteinerBase();
        landmarks.Remove("B");

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Steiner, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.NotNull(results["SNA"].Value);
        Assert.Equal(MeasureStatus.NotComputable, results["SNB"].Status);
        Assert.Equal(MeasureStatus.NotComputable, results["ANB"].Status); // dépend de SNB
    }

    // ---------- Calculs Tweed ----------

    [Fact]
    public void Tweed_triangle_sums_to_180_degrees()
    {
        // Francfort horizontal ; plan mandibulaire à 25° ; axe incisif vertical.
        var tan25 = Math.Tan(25 * Math.PI / 180);
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["Po"] = new(0, 0),
            ["Or"] = new(100, 0),
            ["Go"] = new(0, 200),
            ["Me"] = new(100, 200 + 100 * tan25),
            ["L1A"] = new(80, 150),
            ["L1E"] = new(80, 100),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Tweed, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.Equal(25, results["FMA"].Value!.Value, precision: 6);
        Assert.Equal(180, results["FMA"].Value!.Value + results["IMPA"].Value!.Value + results["FMIA"].Value!.Value,
            precision: 6);
    }

    // ---------- Classification par rapport aux normes ----------

    [Theory]
    [InlineData(82.0, MeasureStatus.Normal)]      // pile sur la norme
    [InlineData(84.0, MeasureStatus.Normal)]      // +1 σ
    [InlineData(85.9, MeasureStatus.Borderline)]  // ~+1,95 σ
    [InlineData(87.0, MeasureStatus.Outside)]     // +2,5 σ
    public void Measure_status_reflects_deviation_from_norm(double snaDegrees, MeasureStatus expected)
    {
        const double d = 100;
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["S"] = new(0, -d),
            ["N"] = new(0, 0),
            ["A"] = new(
                d * Math.Sin((180 - snaDegrees) * Math.PI / 180),
                d * Math.Cos((180 - snaDegrees) * Math.PI / 180)),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Steiner, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.Equal(expected, results["SNA"].Status);
    }
}
