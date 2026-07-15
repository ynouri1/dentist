using Ortho.Application.Imaging;

namespace Ortho.Domain.Tests;

public class GeometryCalculatorTests
{
    [Fact]
    public void Distance_in_pixels_uses_pythagoras()
    {
        Assert.Equal(5.0, GeometryCalculator.Distance(new ImagePoint(0, 0), new ImagePoint(3, 4)), precision: 10);
    }

    [Fact]
    public void Distance_applies_anisotropic_pixel_spacing()
    {
        // 30 px × 0,1 mm = 3 mm en X ; 40 px × 0,2 mm = 8 mm en Y → hypoténuse.
        var mm = GeometryCalculator.Distance(
            new ImagePoint(0, 0), new ImagePoint(30, 40), spacingX: 0.1, spacingY: 0.2);
        Assert.Equal(Math.Sqrt(3 * 3 + 8 * 8.0), mm, precision: 10);
    }

    [Theory]
    [InlineData(10, 0, 0, 10, 90.0)]   // axes perpendiculaires
    [InlineData(10, 0, 10, 10, 45.0)]  // diagonale
    [InlineData(10, 0, -10, 0, 180.0)] // demi-tour
    [InlineData(10, 0, 10, 0, 0.0)]    // même direction
    public void Angle_at_vertex_matches_expected_degrees(double ax, double ay, double bx, double by, double expected)
    {
        var vertex = new ImagePoint(0, 0);
        var angle = GeometryCalculator.AngleDegrees(new ImagePoint(ax, ay), vertex, new ImagePoint(bx, by));
        Assert.Equal(expected, angle, precision: 8);
    }

    [Fact]
    public void Angle_with_degenerate_ray_is_zero()
    {
        var p = new ImagePoint(5, 5);
        Assert.Equal(0, GeometryCalculator.AngleDegrees(p, p, new ImagePoint(10, 10)));
    }

    [Fact]
    public void Points_survive_json_roundtrip()
    {
        var points = new[] { new ImagePoint(12.5, 7.25), new ImagePoint(0, 300) };
        var parsed = ImagingService.ParsePoints(ImagingService.SerializePoints(points));
        Assert.Equal(points, parsed);
    }
}
