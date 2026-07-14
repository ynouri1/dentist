namespace Ortho.Application.Abstractions;

/// <summary>
/// Stockage des binaires (DICOM, photos, STL). L'implémentation MVP est un
/// répertoire local chiffré ; une implémentation S3/MinIO pourra s'y substituer
/// pour la version clinique multi-poste.
/// </summary>
public interface IObjectStore
{
    Task SaveAsync(string key, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
