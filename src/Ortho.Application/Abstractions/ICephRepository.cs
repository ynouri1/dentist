using Ortho.Domain.Entities;

namespace Ortho.Application.Abstractions;

public interface ICephRepository
{
    Task<CephAnalysis?> GetByImageAndTemplateAsync(Guid imageId, string templateCode, CancellationToken ct = default);
    Task<CephAnalysis?> GetAsync(Guid analysisId, CancellationToken ct = default);
    Task AddAsync(CephAnalysis analysis, CancellationToken ct = default);

    /// <summary>Crée ou déplace le landmark <paramref name="landmark"/> (unique par analyse et code).</summary>
    Task UpsertLandmarkAsync(CephLandmark landmark, CancellationToken ct = default);
    Task DeleteLandmarkAsync(Guid analysisId, string code, CancellationToken ct = default);
}
