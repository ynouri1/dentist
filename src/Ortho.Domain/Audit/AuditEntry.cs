namespace Ortho.Domain.Audit;

/// <summary>
/// Trace immuable des accès et modifications aux données patients.
/// Exigence de confidentialité médicale et fondation du futur dossier MDR.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
}
