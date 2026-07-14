using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.Application.Documents;

public class DocumentService(IPatientRepository patients, IObjectStore store, IAuditTrail audit)
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".dcm"] = "application/dicom",
        [".pdf"] = "application/pdf",
    };

    public async Task<PatientDocument> ImportAsync(
        Guid patientId, Stream content, string fileName, DocumentCategory category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ValidationException(["Le nom du fichier est obligatoire."]);

        var document = new PatientDocument
        {
            PatientId = patientId,
            FileName = Path.GetFileName(fileName),
            ContentType = ContentTypes.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream"),
            Category = category,
            ImportedAtUtc = DateTime.UtcNow,
        };
        document.StorageKey = $"patients/{patientId:N}/documents/{document.Id:N}";

        await store.SaveAsync(document.StorageKey, content, ct);
        if (content.CanSeek)
            document.SizeBytes = content.Length;

        await patients.AddDocumentAsync(document, ct);
        await audit.RecordAsync("document.import", nameof(PatientDocument), document.Id.ToString(),
            $"{document.FileName} ({document.Category}) patient {patientId}", ct);
        return document;
    }

    public Task<Stream> OpenAsync(PatientDocument document, CancellationToken ct = default)
        => store.OpenReadAsync(document.StorageKey, ct);

    public static bool IsImage(PatientDocument document)
        => document.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
