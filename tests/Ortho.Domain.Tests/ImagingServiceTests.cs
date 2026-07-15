using Ortho.Application.Imaging;
using Ortho.Application.Patients;

namespace Ortho.Domain.Tests;

public class ImagingServiceTests
{
    [Theory]
    [InlineData(50.0, 200.0, 0.25)]
    [InlineData(10.0, 100.0, 0.1)]
    public void ComputeSpacing_divides_known_length_by_measured_pixels(double mm, double px, double expected)
    {
        Assert.Equal(expected, ImagingService.ComputeSpacing(mm, px), precision: 10);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 0)]
    [InlineData(-5, 100)]
    public void ComputeSpacing_rejects_non_positive_lengths(double mm, double px)
    {
        Assert.Throws<ValidationException>(() => ImagingService.ComputeSpacing(mm, px));
    }
}
