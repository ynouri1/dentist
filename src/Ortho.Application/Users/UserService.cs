using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.Application.Users;

public class UserService(IUserRepository users, IAuditTrail audit)
{
    public Task<bool> HasAnyUserAsync(CancellationToken ct = default) => users.AnyAsync(ct);

    public async Task<AppUser> CreateAsync(
        string username, string displayName, string password, UserRole role, CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            errors.Add("L'identifiant doit faire au moins 3 caractères.");
        if (password.Length < 8)
            errors.Add("Le mot de passe doit faire au moins 8 caractères.");
        if (await users.GetByUsernameAsync(username.Trim(), ct) is not null)
            errors.Add("Cet identifiant existe déjà.");
        if (errors.Count > 0)
            throw new ValidationException(errors);

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new AppUser
        {
            Username = username.Trim().ToLowerInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username.Trim() : displayName.Trim(),
            Role = role,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await users.AddAsync(user, ct);
        await audit.RecordAsync("user.create", nameof(AppUser), user.Id.ToString(), user.Username, ct);
        return user;
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
}
