using Microsoft.EntityFrameworkCore;
using Ortho.Application.Abstractions;
using Ortho.Domain.Entities;

namespace Ortho.Infrastructure.Persistence;

public class CephRepository(IDbContextFactory<OrthoDbContext> contextFactory) : ICephRepository
{
    public async Task<CephAnalysis?> GetByImageAndTemplateAsync(
        Guid imageId, string templateCode, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.CephAnalyses
            .Include(a => a.Landmarks)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MedicalImageId == imageId && a.TemplateCode == templateCode, ct);
    }

    public async Task<CephAnalysis?> GetAsync(Guid analysisId, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.CephAnalyses
            .Include(a => a.Landmarks)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);
    }

    public async Task AddAsync(CephAnalysis analysis, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpsertLandmarkAsync(CephLandmark landmark, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var existing = await db.CephLandmarks.FirstOrDefaultAsync(
            l => l.AnalysisId == landmark.AnalysisId && l.Code == landmark.Code, ct);

        if (existing is null)
        {
            db.CephLandmarks.Add(landmark);
        }
        else
        {
            existing.X = landmark.X;
            existing.Y = landmark.Y;
            existing.Source = landmark.Source;
            existing.Confidence = landmark.Confidence;
            existing.PlacedAtUtc = landmark.PlacedAtUtc;
        }

        await db.CephAnalyses
            .Where(a => a.Id == landmark.AnalysisId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UpdatedAtUtc, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteLandmarkAsync(Guid analysisId, string code, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await db.CephLandmarks
            .Where(l => l.AnalysisId == analysisId && l.Code == code)
            .ExecuteDeleteAsync(ct);
    }
}
