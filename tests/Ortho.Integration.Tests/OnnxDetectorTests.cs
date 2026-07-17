using Ortho.Infrastructure.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ortho.Integration.Tests;

/// <summary>
/// Valide de bout en bout que OnnxLandmarkDetector charge et infère un VRAI modèle
/// ONNX (fixture déterministe générée par ml/tools/make_reference_model.py). Ferme
/// la boucle entre le pipeline Python et l'inférence embarquée .NET.
/// </summary>
public class OnnxDetectorTests
{
    private static string ReferenceModelDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Models");

    private static bool ModelPresent =>
        File.Exists(Path.Combine(ReferenceModelDirectory, "landmarks.onnx"));

    [Fact]
    public async Task Reference_model_is_loaded_and_predicts_expected_positions()
    {
        Assert.True(ModelPresent, "Fixture landmarks.onnx absente — exécutez make_reference_model.py.");

        using var detector = new OnnxLandmarkDetector(ReferenceModelDirectory);
        Assert.True(detector.IsAvailable);

        // Image 256×256 : les coordonnées décodées coïncident avec la taille d'entrée du modèle.
        using var image = new Image<Rgb24>(256, 256);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        var detected = await detector.DetectAsync(stream.ToArray(), ["S", "N", "Go"]);

        // Pics déterministes du modèle de référence (cf. sortie de make_reference_model.py).
        var byCode = detected.ToDictionary(d => d.Code);
        Assert.Equal(2.0, byCode["S"].Point.X, precision: 3);
        Assert.Equal(2.0, byCode["S"].Point.Y, precision: 3);
        Assert.Equal(30.0, byCode["N"].Point.X, precision: 3);
        Assert.Equal(22.0, byCode["N"].Point.Y, precision: 3);
        Assert.Equal(114.0, byCode["Go"].Point.X, precision: 3);
        Assert.Equal(82.0, byCode["Go"].Point.Y, precision: 3);
        Assert.All(detected, d => Assert.True(d.Confidence > 0));
    }

    [Fact]
    public async Task Only_requested_codes_are_returned()
    {
        Assert.True(ModelPresent);
        using var detector = new OnnxLandmarkDetector(ReferenceModelDirectory);

        using var image = new Image<Rgb24>(256, 256);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        var detected = await detector.DetectAsync(stream.ToArray(), ["N"]);
        Assert.Single(detected);
        Assert.Equal("N", detected[0].Code);
    }
}
