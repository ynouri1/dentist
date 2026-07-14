using Microsoft.EntityFrameworkCore;
using Ortho.Application.Abstractions;
using Ortho.Domain.Entities;

namespace Ortho.Infrastructure.Persistence;

public class PatientRepository(IDbContextFactory<OrthoDbContext> contextFactory) : IPatientRepository
{
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Patients
            .Include(p => p.Consultations.OrderByDescending(c => c.Date))
            .Include(p => p.Documents)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string? term, int take = 100, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        IQueryable<Patient> query = db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.LastName, pattern) ||
                EF.Functions.Like(p.FirstName, pattern) ||
                EF.Functions.Like(p.FileNumber, pattern) ||
                (p.Phone != null && EF.Functions.Like(p.Phone, pattern)));
        }

        return await query
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Patient patient, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Patient patient, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Patients.Update(patient);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> NextFileNumberAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var year = DateTime.Today.Year;
        var prefix = $"P-{year}-";
        var count = await db.Patients.CountAsync(p => p.FileNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}
