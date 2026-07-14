using Ortho.Domain.Entities;

namespace Ortho.Application.Abstractions;

/// <summary>Utilisateur actuellement connecté ; alimente notamment l'audit trail.</summary>
public interface ICurrentUser
{
    AppUser? User { get; }
    string Name { get; }
}

public class CurrentUserContext : ICurrentUser
{
    public AppUser? User { get; set; }
    public string Name => User?.Username ?? Environment.UserName;
}
