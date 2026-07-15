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
    public void All_six_analyses_are_available()
    {
        Assert.Equal(
            new[] { "steiner", "ricketts", "tweed", "mcnamara", "downs", "jarabak" },
            AnalysisTemplates.All.Select(t => t.Code));
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

    // ---------- Nouvelles formules (Sprint 5) ----------

    [Fact]
    public void Jarabak_sum_and_ratio_are_computed_without_calibration()
    {
        // Selle 90° (N-S-Ar), S-Go vertical 64, N-Me vertical 100 → ratio 64 %.
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["N"] = new(100, 0),
            ["S"] = new(0, 0),
            ["Ar"] = new(0, 60),
            ["Go"] = new(0, 64),
            ["Me"] = new(100, 100),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Jarabak, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.Equal(90, results["Saddle"].Value!.Value, precision: 6);
        Assert.Equal(64, results["FacialHeightRatio"].Value!.Value, precision: 6); // |S-Go|/|N-Me|×100
        Assert.Equal(
            results["Saddle"].Value!.Value + results["Articular"].Value!.Value + results["Gonial"].Value!.Value,
            results["SumAngles"].Value!.Value, precision: 6);
    }

    [Fact]
    public void Downs_convexity_is_zero_for_straight_profile_and_hidden_helper_is_flagged()
    {
        // N, A, Pog alignés verticalement → N-A-Pog = 180° → convexité 0°.
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["N"] = new(0, 0),
            ["A"] = new(0, 50),
            ["Pog"] = new(0, 100),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Downs, landmarks, null, null)
            .ToDictionary(r => r.Code);

        Assert.Equal(0, results["Convexity"].Value!.Value, precision: 6);
        Assert.True(results["NAPog"].Hidden);
        Assert.False(results["Convexity"].Hidden);
    }

    [Fact]
    public void McNamara_nasion_perpendicular_distance_is_measured_along_frankfort()
    {
        // Francfort horizontal → perpendiculaire de N verticale (x = 0).
        // A à x = 4 px, spacing 0,5 → 2 mm.
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["Po"] = new(-50, 10),
            ["Or"] = new(50, 10),
            ["N"] = new(0, 0),
            ["A"] = new(4, 60),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.McNamara, landmarks, 0.5, 0.5)
            .ToDictionary(r => r.Code);

        Assert.Equal(2, results["A-NPerp"].Value!.Value, precision: 6);
    }

    [Fact]
    public void McNamara_differential_requires_calibration()
    {
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["Co"] = new(0, 0),
            ["A"] = new(80, 60),
            ["Gn"] = new(30, 120),
        };

        var uncalibrated = CephalometryService
            .ComputeResults(AnalysisTemplates.McNamara, landmarks, null, null)
            .ToDictionary(r => r.Code);
        Assert.Equal(MeasureStatus.NotComputable, uncalibrated["MaxMandDiff"].Status);

        var calibrated = CephalometryService
            .ComputeResults(AnalysisTemplates.McNamara, landmarks, 1, 1)
            .ToDictionary(r => r.Code);
        Assert.Equal(
            calibrated["Co-Gn"].Value!.Value - calibrated["Co-A"].Value!.Value,
            calibrated["MaxMandDiff"].Value!.Value, precision: 6);
    }

    [Fact]
    public void Ricketts_convexity_uses_point_to_line_distance()
    {
        // Droite N-Pog verticale (x=0), A à 5 px → 5 mm avec spacing 1.
        var landmarks = new Dictionary<string, ImagePoint>
        {
            ["N"] = new(0, 0),
            ["Pog"] = new(0, 100),
            ["A"] = new(5, 40),
        };

        var results = CephalometryService
            .ComputeResults(AnalysisTemplates.Ricketts, landmarks, 1, 1)
            .ToDictionary(r => r.Code);

        Assert.Equal(5, results["ConvexityA"].Value!.Value, precision: 6);
    }

    // ---------- Superposition ----------

    [Fact]
    public void Similarity_transform_maps_registration_points_exactly()
    {
        var s1 = new ImagePoint(10, 10);
        var n1 = new ImagePoint(10, 30);   // segment vertical, longueur 20
        var s0 = new ImagePoint(100, 50);
        var n0 = new ImagePoint(140, 50);  // segment horizontal, longueur 40 → rotation + échelle ×2

        var transform = SimilarityTransform.FromTwoPoints(s1, n1, s0, n0);

        var mappedS = transform.Apply(s1);
        var mappedN = transform.Apply(n1);
        Assert.Equal(s0.X, mappedS.X, precision: 9);
        Assert.Equal(s0.Y, mappedS.Y, precision: 9);
        Assert.Equal(n0.X, mappedN.X, precision: 9);
        Assert.Equal(n0.Y, mappedN.Y, precision: 9);

        // Un point hors axe suit la rotation (90° horaire ici) et l'échelle ×2.
        var mapped = transform.Apply(new ImagePoint(20, 10)); // 10 px à droite de S1
        Assert.Equal(100, mapped.X, precision: 9);
        Assert.Equal(30, mapped.Y, precision: 9);
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
