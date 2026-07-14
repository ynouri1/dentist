using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Patients;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Backup;
using Ortho.Infrastructure.Persistence;

namespace Ortho.Integration.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N"));

    private readonly string _exportDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N") + "-export");

    private ServiceProvider BuildProvider()
    {
        var provider = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(_dataDirectory))
            .BuildServiceProvider();

        using var db = provider.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext();
        db.Database.Migrate();
        return provider;
    }

    [Fact]
    public void Snapshot_returns_null_when_database_does_not_exist_yet()
    {
        var backup = new BackupService(new OrthoDataOptions(_dataDirectory));
        Assert.Null(backup.SnapshotDatabase());
    }

    [Fact]
    public async Task Snapshot_copies_the_database_file()
    {
        await using var provider = BuildProvider();
        await provider.GetRequiredService<PatientService>()
            .CreateAsync(new PatientDraft { FirstName = "Sami", LastName = "Gharbi" });

        var snapshot = provider.GetRequiredService<BackupService>().SnapshotDatabase();

        Assert.NotNull(snapshot);
        Assert.True(File.Exists(snapshot));
        Assert.True(new FileInfo(snapshot).Length > 0);
    }

    [Fact]
    public async Task Export_zips_database_and_keys_but_not_logs_or_internal_backups()
    {
        await using var provider = BuildProvider();
        await provider.GetRequiredService<PatientService>()
            .CreateAsync(new PatientDraft { FirstName = "Sami", LastName = "Gharbi" });

        var backup = provider.GetRequiredService<BackupService>();
        backup.SnapshotDatabase(); // crée backups/ qui doit être exclu de l'export
        Directory.CreateDirectory(Path.Combine(_dataDirectory, "logs"));
        await File.WriteAllTextAsync(Path.Combine(_dataDirectory, "logs", "app.log"), "log");

        var archivePath = backup.ExportTo(_exportDirectory);

        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("ortho.db", entries);
        Assert.Contains(entries, e => e.StartsWith("keys/"));
        Assert.DoesNotContain(entries, e => e.StartsWith("logs/"));
        Assert.DoesNotContain(entries, e => e.StartsWith("backups/"));
    }

    public void Dispose()
    {
        foreach (var directory in new[] { _dataDirectory, _exportDirectory })
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Fichiers encore verrouillés : le répertoire temporaire sera nettoyé par l'OS.
            }
        }
    }
}
