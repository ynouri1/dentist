using Ortho.Application.Abstractions;
using Ortho.Domain.Entities;

namespace Ortho.Application.Patients;

public class ValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public class PatientService(IPatientRepository repository, IAuditTrail audit)
{
    public Task<Patient?> GetAsync(Guid id, CancellationToken ct = default)
        => repository.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Patient>> SearchAsync(string? term, CancellationToken ct = default)
        => repository.SearchAsync(term, ct: ct);

    public async Task<Patient> CreateAsync(PatientDraft draft, CancellationToken ct = default)
    {
        Validate(draft);

        var now = DateTime.UtcNow;
        var patient = new Patient
        {
            FileNumber = await repository.NextFileNumberAsync(ct),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        Apply(draft, patient);

        await repository.AddAsync(patient, ct);
        await audit.RecordAsync("patient.create", nameof(Patient), patient.Id.ToString(), patient.FileNumber, ct);
        return patient;
    }

    public async Task<Patient> UpdateAsync(Guid id, PatientDraft draft, CancellationToken ct = default)
    {
        Validate(draft);

        var patient = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Patient introuvable : {id}");

        Apply(draft, patient);
        patient.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateAsync(patient, ct);
        await audit.RecordAsync("patient.update", nameof(Patient), patient.Id.ToString(), patient.FileNumber, ct);
        return patient;
    }

    public async Task<Consultation> AddConsultationAsync(
        Guid patientId, DateTime date, string? reason, string? notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(notes))
            throw new ValidationException(["Le motif ou les notes de la consultation sont obligatoires."]);

        var consultation = new Consultation
        {
            PatientId = patientId,
            Date = date,
            Reason = Normalize(reason),
            Notes = Normalize(notes),
            CreatedAtUtc = DateTime.UtcNow,
        };

        await repository.AddConsultationAsync(consultation, ct);
        await audit.RecordAsync("consultation.create", nameof(Consultation), consultation.Id.ToString(),
            $"patient {patientId}", ct);
        return consultation;
    }

    private static void Apply(PatientDraft draft, Patient patient)
    {
        patient.FirstName = draft.FirstName.Trim();
        patient.LastName = draft.LastName.Trim();
        patient.BirthDate = draft.BirthDate;
        patient.Sex = draft.Sex;
        patient.Phone = Normalize(draft.Phone);
        patient.Email = Normalize(draft.Email);
        patient.Address = Normalize(draft.Address);
        patient.Notes = Normalize(draft.Notes);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(PatientDraft draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.LastName))
            errors.Add("Le nom est obligatoire.");
        if (string.IsNullOrWhiteSpace(draft.FirstName))
            errors.Add("Le prénom est obligatoire.");
        if (draft.BirthDate is { } birth && birth > DateOnly.FromDateTime(DateTime.Today))
            errors.Add("La date de naissance ne peut pas être dans le futur.");

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}
