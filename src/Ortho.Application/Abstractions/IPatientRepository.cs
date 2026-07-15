using Ortho.Domain.Entities;

namespace Ortho.Application.Abstractions;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Recherche par nom, prénom, numéro de dossier ou téléphone. Terme vide = patients récents.</summary>
    Task<IReadOnlyList<Patient>> SearchAsync(string? term, int take = 100, CancellationToken ct = default);

    Task AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);

    /// <summary>Prochain numéro de dossier pour l'année en cours (ex. P-2026-0042).</summary>
    Task<string> NextFileNumberAsync(CancellationToken ct = default);

    Task AddConsultationAsync(Consultation consultation, CancellationToken ct = default);
    Task AddDocumentAsync(PatientDocument document, CancellationToken ct = default);

    Task AddImageAsync(MedicalImage image, CancellationToken ct = default);
    Task<MedicalImage?> GetImageAsync(Guid imageId, CancellationToken ct = default);
    Task UpdateImageAsync(MedicalImage image, CancellationToken ct = default);
}
