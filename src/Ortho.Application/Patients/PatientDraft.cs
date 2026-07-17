using Ortho.Domain.Entities;

namespace Ortho.Application.Patients;

/// <summary>Données saisies dans le formulaire patient, avant validation.</summary>
public record PatientDraft
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateOnly? BirthDate { get; init; }
    public Sex Sex { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
    public bool ResearchConsent { get; init; }
}
