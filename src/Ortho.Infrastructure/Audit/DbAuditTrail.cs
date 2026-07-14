using Microsoft.EntityFrameworkCore;
using Ortho.Application.Abstractions;
using Ortho.Domain.Audit;
using Ortho.Infrastructure.Persistence;
using Serilog;

namespace Ortho.Infrastructure.Audit;

/// <summary>Journalise en base (trace durable) et dans le log applicatif.</summary>
public class DbAuditTrail(IDbContextFactory<OrthoDbContext> contextFactory, ICurrentUser currentUser) : IAuditTrail
{
    public async Task RecordAsync(string action, string entityType, string entityId, string? details = null, CancellationToken ct = default)
    {
        var entry = new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = currentUser.Name,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        };

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        Log.Information("Audit {Action} {EntityType}/{EntityId} par {User} : {Details}",
            action, entityType, entityId, entry.User, details);
    }
}
