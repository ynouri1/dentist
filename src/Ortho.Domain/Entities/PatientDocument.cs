namespace Ortho.Domain.Entities;

public enum DocumentCategory
{
    Document = 0,
    PhotoIntraOrale = 1,
    PhotoExtraOrale = 2,
    Radiographie = 3,
    Examen = 4,
}

public class PatientDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }

    /// <summary>Clé dans l'object store chiffré ; le binaire ne vit jamais en base.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
