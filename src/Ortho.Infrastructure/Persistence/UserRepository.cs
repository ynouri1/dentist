using Microsoft.EntityFrameworkCore;
using Ortho.Application.Abstractions;
using Ortho.Domain.Entities;

namespace Ortho.Infrastructure.Persistence;

public class UserRepository(IDbContextFactory<OrthoDbContext> contextFactory) : IUserRepository
{
    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(ct);
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }
}
