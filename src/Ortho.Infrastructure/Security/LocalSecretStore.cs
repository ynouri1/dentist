using System.Security.Cryptography;

namespace Ortho.Infrastructure.Security;

/// <summary>
/// Conserve les secrets locaux (mot de passe SQLCipher, clé AES de l'object store).
/// Sous Windows le fichier est protégé par DPAPI (lié au compte utilisateur) ;
/// ailleurs il est stocké brut avec permissions restreintes — à durcir avant
/// un déploiement hors Windows.
/// </summary>
public class LocalSecretStore(string directory)
{
    public byte[] GetOrCreate(string name, int sizeBytes)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".key");

        if (File.Exists(path))
            return Unprotect(File.ReadAllBytes(path));

        var secret = RandomNumberGenerator.GetBytes(sizeBytes);
        File.WriteAllBytes(path, Protect(secret));
        return secret;
    }

    private static byte[] Protect(byte[] data)
        => OperatingSystem.IsWindows()
            ? ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : data;

    private static byte[] Unprotect(byte[] data)
        => OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : data;
}
