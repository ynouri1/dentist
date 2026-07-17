using Ortho.Application.Imaging;

namespace Ortho.Application.Cephalometry;

/// <summary>
/// Décode la sortie d'un modèle de détection par heatmaps : chaque canal est une
/// carte de probabilité (H×W) dont l'argmax donne la position du landmark, remise
/// à l'échelle de l'image d'origine. Fonction pure — c'est le cœur testable de
/// l'inférence, indépendant d'ONNX Runtime.
/// </summary>
public static class HeatmapDecoder
{
    /// <summary>Position du pic d'un canal, remise à l'échelle image, + confiance (valeur du pic).</summary>
    public static (ImagePoint Point, double Confidence) DecodePeak(
        ReadOnlySpan<float> channel, int heatmapWidth, int heatmapHeight,
        int imageWidth, int imageHeight)
    {
        if (heatmapWidth <= 0 || heatmapHeight <= 0)
            throw new ArgumentException("Dimensions de heatmap invalides.");
        if (channel.Length < heatmapWidth * heatmapHeight)
            throw new ArgumentException("Canal de heatmap trop court.");

        var bestIndex = 0;
        var bestValue = channel[0];
        for (var i = 1; i < heatmapWidth * heatmapHeight; i++)
        {
            if (channel[i] > bestValue)
            {
                bestValue = channel[i];
                bestIndex = i;
            }
        }

        var hx = bestIndex % heatmapWidth;
        var hy = bestIndex / heatmapWidth;

        // Centre de la cellule ramené aux coordonnées de l'image affichée.
        var x = (hx + 0.5) * imageWidth / heatmapWidth;
        var y = (hy + 0.5) * imageHeight / heatmapHeight;

        return (new ImagePoint(x, y), Math.Clamp(bestValue, 0, 1));
    }
}
