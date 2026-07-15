namespace Ortho.Application.Imaging;

/// <summary>Point en coordonnées image (pixels).</summary>
public readonly record struct ImagePoint(double X, double Y);

/// <summary>
/// Géométrie des mesures. Les calculs se font en coordonnées physiques :
/// chaque axe est converti avec son propre pixel spacing (les détecteurs
/// anisotropes existent), sinon en pixels bruts (spacing = 1).
/// </summary>
public static class GeometryCalculator
{
    public static double Distance(ImagePoint a, ImagePoint b, double spacingX = 1, double spacingY = 1)
    {
        var dx = (b.X - a.X) * spacingX;
        var dy = (b.Y - a.Y) * spacingY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Angle au sommet <paramref name="vertex"/> formé par les points a et b, en degrés [0;180].</summary>
    public static double AngleDegrees(
        ImagePoint a, ImagePoint vertex, ImagePoint b, double spacingX = 1, double spacingY = 1)
    {
        var (uX, uY) = ((a.X - vertex.X) * spacingX, (a.Y - vertex.Y) * spacingY);
        var (vX, vY) = ((b.X - vertex.X) * spacingX, (b.Y - vertex.Y) * spacingY);

        var normU = Math.Sqrt(uX * uX + uY * uY);
        var normV = Math.Sqrt(vX * vX + vY * vY);
        if (normU == 0 || normV == 0)
            return 0;

        var cos = Math.Clamp((uX * vX + uY * vY) / (normU * normV), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }
}
