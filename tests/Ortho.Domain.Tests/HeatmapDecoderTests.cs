using Ortho.Application.Cephalometry;

namespace Ortho.Domain.Tests;

public class HeatmapDecoderTests
{
    [Fact]
    public void DecodePeak_maps_argmax_cell_to_scaled_image_coordinates()
    {
        // Heatmap 4×4, pic dans la cellule (col 3, ligne 1). Image 400×400.
        var channel = new float[16];
        channel[1 * 4 + 3] = 0.9f;

        var (point, confidence) = HeatmapDecoder.DecodePeak(channel, 4, 4, 400, 400);

        // Centre de cellule : (3 + 0.5)/4 × 400 = 350 ; (1 + 0.5)/4 × 400 = 150.
        Assert.Equal(350, point.X, precision: 6);
        Assert.Equal(150, point.Y, precision: 6);
        Assert.Equal(0.9, confidence, precision: 6);
    }

    [Fact]
    public void DecodePeak_handles_anisotropic_scaling()
    {
        var channel = new float[4]; // 2×2
        channel[3] = 1.0f;          // col 1, ligne 1

        var (point, _) = HeatmapDecoder.DecodePeak(channel, 2, 2, 800, 200);

        Assert.Equal((1 + 0.5) / 2 * 800, point.X, precision: 6);
        Assert.Equal((1 + 0.5) / 2 * 200, point.Y, precision: 6);
    }

    [Fact]
    public void DecodePeak_clamps_confidence_between_zero_and_one()
    {
        var channel = new float[] { -2f, 5f, 0f, 0f };
        var (_, confidence) = HeatmapDecoder.DecodePeak(channel, 2, 2, 10, 10);
        Assert.Equal(1.0, confidence);
    }

    [Fact]
    public void DecodePeak_rejects_channel_too_short()
    {
        Assert.Throws<ArgumentException>(() => HeatmapDecoder.DecodePeak(new float[3], 2, 2, 10, 10));
    }
}
