using Ortho.Domain.Entities;

namespace Ortho.Domain.Tests;

public class PatientTests
{
    [Fact]
    public void FullName_combines_uppercase_last_name_and_first_name()
    {
        var patient = new Patient { FirstName = "Yassine", LastName = "Nouri" };
        Assert.Equal("NOURI Yassine", patient.FullName);
    }

    [Theory]
    [InlineData("2010-06-15", "2026-06-14", 15)] // veille d'anniversaire
    [InlineData("2010-06-15", "2026-06-15", 16)] // jour d'anniversaire
    [InlineData("2010-06-15", "2026-07-14", 16)]
    public void AgeAt_computes_age_from_birth_date(string birth, string at, int expected)
    {
        var patient = new Patient { BirthDate = DateOnly.Parse(birth) };
        Assert.Equal(expected, patient.AgeAt(DateOnly.Parse(at)));
    }

    [Fact]
    public void AgeAt_is_null_without_birth_date()
    {
        Assert.Null(new Patient().AgeAt(DateOnly.FromDateTime(DateTime.Today)));
    }
}
