using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.Domain.Tests;

public class PatientServiceTests
{
    private sealed class FakeRepository : IPatientRepository
    {
        public List<Patient> Added { get; } = [];

        public Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<Patient>> SearchAsync(string? term, int take = 100, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Patient>>(Added);

        public Task AddAsync(Patient patient, CancellationToken ct = default)
        {
            Added.Add(patient);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Patient patient, CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> NextFileNumberAsync(CancellationToken ct = default)
            => Task.FromResult($"P-{DateTime.Today.Year}-{Added.Count + 1:D4}");

        public List<Consultation> Consultations { get; } = [];
        public List<PatientDocument> Documents { get; } = [];

        public Task AddConsultationAsync(Consultation consultation, CancellationToken ct = default)
        {
            Consultations.Add(consultation);
            return Task.CompletedTask;
        }

        public Task AddDocumentAsync(PatientDocument document, CancellationToken ct = default)
        {
            Documents.Add(document);
            return Task.CompletedTask;
        }

        public Task<PatientDocument?> GetDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(Documents.FirstOrDefault(d => d.Id == documentId));

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
        {
            Documents.RemoveAll(d => d.Id == documentId);
            return Task.CompletedTask;
        }

        public List<MedicalImage> Images { get; } = [];

        public Task AddImageAsync(MedicalImage image, CancellationToken ct = default)
        {
            Images.Add(image);
            return Task.CompletedTask;
        }

        public Task<MedicalImage?> GetImageAsync(Guid imageId, CancellationToken ct = default)
            => Task.FromResult(Images.FirstOrDefault(i => i.Id == imageId));

        public Task UpdateImageAsync(MedicalImage image, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteImageAsync(Guid imageId, CancellationToken ct = default)
        {
            Images.RemoveAll(i => i.Id == imageId);
            return Task.CompletedTask;
        }

        public List<ImageAnnotation> Annotations { get; } = [];

        public Task AddAnnotationAsync(ImageAnnotation annotation, CancellationToken ct = default)
        {
            Annotations.Add(annotation);
            return Task.CompletedTask;
        }

        public Task DeleteAnnotationAsync(Guid annotationId, CancellationToken ct = default)
        {
            Annotations.RemoveAll(a => a.Id == annotationId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IAuditTrail
    {
        public List<string> Actions { get; } = [];

        public Task RecordAsync(string action, string entityType, string entityId, string? details = null, CancellationToken ct = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private static (PatientService Service, FakeRepository Repo, FakeAudit Audit) CreateService()
    {
        var repo = new FakeRepository();
        var audit = new FakeAudit();
        return (new PatientService(repo, audit), repo, audit);
    }

    [Fact]
    public async Task Create_assigns_file_number_and_records_audit()
    {
        var (service, repo, audit) = CreateService();

        var patient = await service.CreateAsync(new PatientDraft { FirstName = "Amine", LastName = "Ben Salah" });

        Assert.Equal($"P-{DateTime.Today.Year}-0001", patient.FileNumber);
        Assert.Single(repo.Added);
        Assert.Contains("patient.create", audit.Actions);
    }

    [Fact]
    public async Task Create_trims_and_normalizes_optional_fields()
    {
        var (service, _, _) = CreateService();

        var patient = await service.CreateAsync(new PatientDraft
        {
            FirstName = "  Amine ",
            LastName = " Ben Salah ",
            Phone = "   ",
        });

        Assert.Equal("Amine", patient.FirstName);
        Assert.Equal("Ben Salah", patient.LastName);
        Assert.Null(patient.Phone);
    }

    [Theory]
    [InlineData("", "Nouri")]
    [InlineData("Yassine", "")]
    public async Task Create_rejects_missing_mandatory_names(string firstName, string lastName)
    {
        var (service, _, _) = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(new PatientDraft { FirstName = firstName, LastName = lastName }));
    }

    [Fact]
    public async Task AddConsultation_requires_reason_or_notes()
    {
        var (service, _, _) = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.AddConsultationAsync(Guid.NewGuid(), DateTime.Now, reason: "  ", notes: null));
    }

    [Fact]
    public async Task AddConsultation_records_audit()
    {
        var (service, repo, audit) = CreateService();

        await service.AddConsultationAsync(Guid.NewGuid(), DateTime.Now, "Contrôle", "RAS");

        Assert.Single(repo.Consultations);
        Assert.Contains("consultation.create", audit.Actions);
    }

    [Fact]
    public async Task Create_rejects_birth_date_in_the_future()
    {
        var (service, _, _) = CreateService();
        var draft = new PatientDraft
        {
            FirstName = "Amine",
            LastName = "Ben Salah",
            BirthDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(draft));
    }
}
