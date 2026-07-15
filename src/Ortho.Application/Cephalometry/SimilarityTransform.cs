using Ortho.Application.Imaging;

namespace Ortho.Application.Cephalometry;

/// <summary>
/// Transformation de similitude (rotation + échelle + translation) définie par
/// deux paires de points : sert au recalage des superpositions T0/T1 sur des
/// structures stables (classiquement S et N).
/// </summary>
public readonly record struct SimilarityTransform(double A, double B, double Tx, double Ty)
{
    /// <summary>Transformation qui envoie (sourceA, sourceB) sur (targetA, targetB).</summary>
    public static SimilarityTransform FromTwoPoints(
        ImagePoint sourceA, ImagePoint sourceB, ImagePoint targetA, ImagePoint targetB)
    {
        var (ux, uy) = (sourceB.X - sourceA.X, sourceB.Y - sourceA.Y);
        var (vx, vy) = (targetB.X - targetA.X, targetB.Y - targetA.Y);
        var normSquared = ux * ux + uy * uy;
        if (normSquared == 0)
            return new SimilarityTransform(1, 0, targetA.X - sourceA.X, targetA.Y - sourceA.Y);

        // p' = M (p − sourceA) + targetA, avec M = échelle·rotation exprimée par (a, b).
        var a = (ux * vx + uy * vy) / normSquared;
        var b = (ux * vy - uy * vx) / normSquared;
        return new SimilarityTransform(
            a, b,
            targetA.X - (a * sourceA.X - b * sourceA.Y),
            targetA.Y - (b * sourceA.X + a * sourceA.Y));
    }

    public ImagePoint Apply(ImagePoint p) => new(
        A * p.X - B * p.Y + Tx,
        B * p.X + A * p.Y + Ty);
}
