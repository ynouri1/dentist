using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ortho.Infrastructure.Persistence;

/// <summary>Utilisée uniquement par « dotnet ef » pour générer les migrations.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrthoDbContext>
{
    public OrthoDbContext CreateDbContext(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();
        var options = new DbContextOptionsBuilder<OrthoDbContext>()
            .UseSqlite("Data Source=design_time.db")
            .Options;
        return new OrthoDbContext(options);
    }
}
