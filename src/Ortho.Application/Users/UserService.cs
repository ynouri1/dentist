using System.Security.Cryptography;
using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.Application.Users;

/// <summary>Compte créé + code de secours en clair — montré une seule fois, jamais stocké.</summary>
public record UserCreation(AppUser User, string RecoveryCode);

public class UserService(IUserRepository users, IAuditTrail audit)
{
    public Task<bool> HasAnyUserAsync(CancellationToken ct = default) => users.AnyAsync(ct);

    public async Task<UserCreation> CreateAsync(
        string username, string displayName, string password, UserRole role, CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            errors.Add("L'identifiant doit faire au moins 3 caractères.");
        if (password.Length < 8)
            errors.Add("Le mot de passe doit faire au moins 8 caractères.");
        if (await users.GetByUsernameAsync(username.Trim().ToLowerInvariant(), ct) is not null)
            errors.Add("Cet identifiant existe déjà.");
        if (errors.Count > 0)
            throw new ValidationException(errors);

        var (hash, salt) = PasswordHasher.Hash(password);
        var recoveryCode = GenerateRecoveryCode();
        var (recoveryHash, recoverySalt) = PasswordHasher.Hash(NormalizeCode(recoveryCode));

        var user = new AppUser
        {
            Username = username.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username.Trim() : displayName.Trim(),
            Role = role,
            PasswordHash = hash,
            PasswordSalt = salt,
            RecoveryCodeHash = recoveryHash,
            RecoveryCodeSalt = recoverySalt,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await users.AddAsync(user, ct);
        await audit.RecordAsync("user.create", nameof(AppUser), user.Id.ToString(), user.Username, ct);
        return new UserCreation(user, recoveryCode);
    }

    /// <summary>Retourne l'utilisateur si les identifiants sont valides, sinon null.</summary>
    public async Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await users.GetByUsernameAsync(username.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            await audit.RecordAsync("auth.failure", nameof(AppUser), username, ct: ct);
            return null;
        }

        await audit.RecordAsync("auth.login", nameof(AppUser), user.Id.ToString(), user.Username, ct);
        return user;
    }

    /// <summary>
    /// Réinitialise le mot de passe avec le code de secours et retourne un
    /// NOUVEAU code (l'ancien est consommé). Null si identifiant/code invalide.
    /// </summary>
    public async Task<string?> ResetPasswordAsync(
        string username, string recoveryCode, string newPassword, CancellationToken ct = default)
    {
        if (newPassword.Length < 8)
            throw new ValidationException(["Le mot de passe doit faire au moins 8 caractères."]);

        var user = await users.GetByUsernameAsync(username.Trim().ToLowerInvariant(), ct);
        var normalizedCode = NormalizeCode(recoveryCode);
        if (user is null || !user.IsActive ||
            user.RecoveryCodeHash is null || user.RecoveryCodeSalt is null ||
            !PasswordHasher.Verify(normalizedCode, user.RecoveryCodeHash, user.RecoveryCodeSalt))
        {
            await audit.RecordAsync("auth.recovery.failure", nameof(AppUser), username, ct: ct);
            return null;
        }

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        var newCode = GenerateRecoveryCode();
        var (recoveryHash, recoverySalt) = PasswordHasher.Hash(NormalizeCode(newCode));

        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.RecoveryCodeHash = recoveryHash;
        user.RecoveryCodeSalt = recoverySalt;
        await users.UpdateAsync(user, ct);

        await audit.RecordAsync("auth.recovery", nameof(AppUser), user.Id.ToString(), user.Username, ct);
        return newCode;
    }

    /// <summary>Tirets et espaces ignorés, casse insensible.</summary>
    private static string NormalizeCode(string code)
        => code.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    /// <summary>Code XXXX-XXXX-XXXX sur un alphabet sans caractères ambigus (pas de O/0/I/1).</summary>
    public static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }
}
