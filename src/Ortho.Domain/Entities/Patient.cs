namespace Ortho.Domain.Entities;

public enum Sex
{
    Inconnu = 0,
    Masculin = 1,
    Feminin = 2,
}

public class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Numéro de dossier lisible, unique au cabinet (ex. P-2026-0001).</summary>
    public string FileNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public Sex Sex { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<Consultation> Consultations { get; set; } = [];
    public List<PatientDocument> Documents { get; set; } = [];

    public string FullName => $"{LastName.ToUpperInvariant()} {FirstName}".Trim();

    public int? AgeAt(DateOnly date)
    {
        if (BirthDate is not { } birth)
            return null;

        var age = date.Year - birth.Year;
        if (date < birth.AddYears(age))
            age--;
        return age;
    }
}
