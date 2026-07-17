using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ortho.Application.Abstractions;
using Ortho.Application.Cephalometry;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ortho.Infrastructure.Imaging;

/// <summary>
/// Détecteur de landmarks par modèle ONNX (heatmaps). Le modèle et sa
/// configuration sont déposés dans <c>{dataDir}/models/</c> :
/// <c>landmarks.onnx</c> + <c>landmarks.model.json</c> (taille d'entrée, codes
/// par canal). En leur absence, le détecteur est simplement indisponible et le
/// placement reste manuel — aucune dépendance dure au modèle.
/// </summary>
public class OnnxLandmarkDetector : ILandmarkDetector, IDisposable
{
    private record ModelConfig(int InputWidth, int InputHeight, string[] Codes);

    private readonly InferenceSession? _session;
    private readonly ModelConfig? _config;

    public OnnxLandmarkDetector(string modelsDirectory)
    {
        var modelPath = Path.Combine(modelsDirectory, "landmarks.onnx");
        var configPath = Path.Combine(modelsDirectory, "landmarks.model.json");
        if (!File.Exists(modelPath) || !File.Exists(configPath))
        {
            Log.Information("Aucun modèle IA de landmarks : {ModelPath} absent, placement manuel.", modelPath);
            return;
        }

        try
        {
            _config = JsonSerializer.Deserialize<ModelConfig>(
                File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _session = new InferenceSession(modelPath);
            Log.Information("Modèle IA de landmarks chargé ({Count} points).", _config?.Codes.Length ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Chargement du modèle IA impossible, placement manuel.");
            _session = null;
            _config = null;
        }
    }

    public bool IsAvailable => _session is not null && _config is not null;

    public Task<IReadOnlyList<DetectedLandmark>> DetectAsync(
        byte[] displayPng, IReadOnlyList<string> requestedCodes, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<DetectedLandmark>>(() => Detect(displayPng, requestedCodes), ct);

    private IReadOnlyList<DetectedLandmark> Detect(byte[] displayPng, IReadOnlyList<string> requestedCodes)
    {
        if (_session is null || _config is null)
            return [];

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(displayPng);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            image.Mutate(x => x.Resize(_config.InputWidth, _config.InputHeight));

            // Tenseur d'entrée NCHW, RGB normalisé 0–1.
            var input = new DenseTensor<float>([1, 3, _config.InputHeight, _config.InputWidth]);
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        input[0, 0, y, x] = row[x].R / 255f;
                        input[0, 1, y, x] = row[x].G / 255f;
                        input[0, 2, y, x] = row[x].B / 255f;
                    }
                }
            });

            var inputName = _session.InputMetadata.Keys.First();
            using var results = _session.Run(
                [NamedOnnxValue.CreateFromTensor(inputName, input)]);

            var output = results.First().AsTensor<float>();
            // Sortie attendue : [1, C, hH, hW].
            var channels = output.Dimensions[1];
            var heatmapHeight = output.Dimensions[2];
            var heatmapWidth = output.Dimensions[3];
            var plane = heatmapHeight * heatmapWidth;
            var flat = output.ToArray();

            var wanted = requestedCodes.ToHashSet();
            var detected = new List<DetectedLandmark>();
            for (var c = 0; c < channels && c < _config.Codes.Length; c++)
            {
                var code = _config.Codes[c];
                if (!wanted.Contains(code))
                    continue;

                var (point, confidence) = HeatmapDecoder.DecodePeak(
                    flat.AsSpan(c * plane, plane),
                    heatmapWidth, heatmapHeight, originalWidth, originalHeight);
                detected.Add(new DetectedLandmark(code, point, confidence));
            }

            return detected;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Inférence IA de landmarks impossible.");
            return [];
        }
    }

    public void Dispose() => _session?.Dispose();
}
