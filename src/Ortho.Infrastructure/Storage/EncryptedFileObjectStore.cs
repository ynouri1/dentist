using System.Security.Cryptography;
using Ortho.Application.Abstractions;

namespace Ortho.Infrastructure.Storage;

/// <summary>
/// Object store local : chaque objet est chiffré AES-256-GCM dans un fichier
/// <c>{root}/{key}.bin</c> au format [nonce 12 o][tag 16 o][ciphertext].
/// </summary>
public class EncryptedFileObjectStore(string rootDirectory, byte[] masterKey) : IObjectStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var plaintext = buffer.ToArray();

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aes = new AesGcm(masterKey, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await file.WriteAsync(nonce, ct);
        await file.WriteAsync(tag, ct);
        await file.WriteAsync(ciphertext, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var data = await File.ReadAllBytesAsync(PathFor(key), ct);

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var ciphertext = data.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(masterKey, TagSize))
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return new MemoryStream(plaintext, writable: false);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(PathFor(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        // Interdit toute sortie du répertoire racine via la clé.
        var safe = key.Replace('\\', '/');
        if (safe.Contains("..") || Path.IsPathRooted(safe))
            throw new ArgumentException($"Clé d'objet invalide : {key}", nameof(key));
        return Path.Combine(rootDirectory, safe + ".bin");
    }
}
