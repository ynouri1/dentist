namespace Ortho.Application.Abstractions;

public interface IAuditTrail
{
    Task RecordAsync(string action, string entityType, string entityId, string? details = null, CancellationToken ct = default);
}
