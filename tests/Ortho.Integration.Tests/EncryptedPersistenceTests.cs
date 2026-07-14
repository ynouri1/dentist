using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Abstractions;
using Ortho.Application.Patients;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Persistence;

namespace Ortho.Integration.Tests;

public class EncryptedPersistenceTests : IDisposable
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "ortho-tests", Guid.NewGuid().ToString("N"));

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
    public async Task Patient_survives_restart_and_database_file_is_encrypted()
    {
        // Première « session » : création du patient.
        await using (var provider = BuildProvider())
        {
            var patients = provider.GetRequiredService<PatientService>();
            await patients.CreateAsync(new PatientDraft { FirstName = "Yassine", LastName = "Nouri" });
        }

        // Deuxième « session » : le patient est retrouvé (mêmes clés, nouveau conteneur).
        await using (var provider = BuildProvider())
        {
            var patients = provider.GetRequiredService<PatientService>();
            var found = await patients.SearchAsync("Nouri");
            var patient = Assert.Single(found);
            Assert.Equal("NOURI Yassine", patient.FullName);
            Assert.StartsWith($"P-{DateTime.Today.Year}-", patient.FileNumber);
        }

        // Le fichier ne doit PAS commencer par l'en-tête SQLite en clair.
        // ClearAllPools libère le verrou que le pool de connexions garde sur le fichier.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var header = new byte[16];
        await using (var file = new FileStream(
            Path.Combine(_dataDirectory, "ortho.db"),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            _ = await file.ReadAsync(header);
        Assert.NotEqual("SQLite format 3", Encoding.ASCII.GetString(header, 0, 15));
    }

    [Fact]
    public async Task Audit_trail_records_patient_creation()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var created = await patients.CreateAsync(new PatientDraft { FirstName = "Amine", LastName = "Trabelsi" });

        await using var db = await provider
            .GetRequiredService<IDbContextFactory<OrthoDbContext>>()
            .CreateDbContextAsync();
        var entry = Assert.Single(db.AuditEntries.Where(a => a.Action == "patient.create"));
        Assert.Equal(created.Id.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Object_store_roundtrips_and_never_writes_plaintext()
    {
        await using var provider = BuildProvider();
        var store = provider.GetRequiredService<IObjectStore>();

        var plaintext = "contenu-confidentiel-radio-panoramique"u8.ToArray();
        await store.SaveAsync("patients/x/radio1", new MemoryStream(plaintext));

        // Relecture fidèle.
        await using var read = await store.OpenReadAsync("patients/x/radio1");
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        Assert.Equal(plaintext, buffer.ToArray());

        // Le fichier sur disque ne contient pas le contenu en clair.
        var objectFile = Directory
            .GetFiles(Path.Combine(_dataDirectory, "objects"), "*.bin", SearchOption.AllDirectories)
            .Single();
        var raw = await File.ReadAllBytesAsync(objectFile);
        Assert.DoesNotContain(
            Encoding.UTF8.GetString(plaintext),
            Encoding.Latin1.GetString(raw));
    }

    [Fact]
    public async Task Object_store_rejects_path_traversal_keys()
    {
        await using var provider = BuildProvider();
        var store = provider.GetRequiredService<IObjectStore>();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync("../evasion", new MemoryStream([1])));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Fichiers encore verrouillés par SQLite : le répertoire temporaire sera nettoyé par l'OS.
        }
    }
}
