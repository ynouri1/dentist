namespace Ortho.Domain.Entities;

public enum UserRole
{
    Praticien = 0,
    Assistante = 1,
}

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    /// <summary>Hash PBKDF2-SHA256 ; jamais le mot de passe en clair.</summary>
    public byte[] PasswordHash { get; set; } = [];
    public byte[] PasswordSalt { get; set; } = [];

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
