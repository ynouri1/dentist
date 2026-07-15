using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Documents;
using Ortho.Application.Patients;
using Ortho.Application.Users;
using Ortho.Domain.Entities;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Persistence;

namespace Ortho.Integration.Tests;

public class UsersAndRecordsTests : IDisposable
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
    public async Task User_creation_and_authentication_lifecycle()
    {
        await using var provider = BuildProvider();
        var users = provider.GetRequiredService<UserService>();

        Assert.False(await users.HasAnyUserAsync());

        await users.CreateAsync("dr.nouri", "Dr Nouri", "S3cret!Pass", UserRole.Praticien);
        Assert.True(await users.HasAnyUserAsync());

        // La casse de l'identifiant est normalisée, le hash n'est jamais le mot de passe.
        var authenticated = await users.AuthenticateAsync("DR.NOURI", "S3cret!Pass");
        Assert.NotNull(authenticated);
        Assert.Equal("Dr Nouri", authenticated.DisplayName);
        Assert.NotEqual("S3cret!Pass"u8.ToArray(), authenticated.PasswordHash);

        Assert.Null(await users.AuthenticateAsync("dr.nouri", "mauvais-mdp"));
    }

    [Fact]
    public async Task User_creation_rejects_short_password_and_duplicate_username()
    {
        await using var provider = BuildProvider();
        var users = provider.GetRequiredService<UserService>();

        await Assert.ThrowsAsync<ValidationException>(
            () => users.CreateAsync("dr.nouri", "Dr Nouri", "court", UserRole.Praticien));

        await users.CreateAsync("dr.nouri", "Dr Nouri", "S3cret!Pass", UserRole.Praticien);
        await Assert.ThrowsAsync<ValidationException>(
            () => users.CreateAsync("dr.nouri", "Doublon", "AutreP@ss123", UserRole.Assistante));
    }

    [Fact]
    public async Task Recovery_code_resets_password_and_is_single_use()
    {
        await using var provider = BuildProvider();
        var users = provider.GetRequiredService<UserService>();

        var creation = await users.CreateAsync("dr.nouri", "Dr Nouri", "AncienMdp1!", UserRole.Praticien);
        Assert.Matches(@"^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", creation.RecoveryCode);

        // Mauvais code → refus audité, mot de passe inchangé.
        Assert.Null(await users.ResetPasswordAsync("dr.nouri", "AAAA-BBBB-CCCC", "NouveauMdp1!"));
        Assert.NotNull(await users.AuthenticateAsync("dr.nouri", "AncienMdp1!"));

        // Bon code (tirets/minuscules tolérés) → nouveau mot de passe + NOUVEAU code.
        var newCode = await users.ResetPasswordAsync(
            "dr.nouri", creation.RecoveryCode.Replace("-", "").ToLowerInvariant(), "NouveauMdp1!");
        Assert.NotNull(newCode);
        Assert.NotEqual(creation.RecoveryCode, newCode);

        Assert.Null(await users.AuthenticateAsync("dr.nouri", "AncienMdp1!"));
        Assert.NotNull(await users.AuthenticateAsync("dr.nouri", "NouveauMdp1!"));

        // L'ancien code est consommé.
        Assert.Null(await users.ResetPasswordAsync("dr.nouri", creation.RecoveryCode, "EncoreUnAutre1!"));
        // Le nouveau fonctionne.
        Assert.NotNull(await users.ResetPasswordAsync("dr.nouri", newCode!, "Definitif1!"));
    }

    [Fact]
    public async Task Consultations_and_documents_are_attached_to_the_patient_record()
    {
        await using var provider = BuildProvider();
        var patients = provider.GetRequiredService<PatientService>();
        var documents = provider.GetRequiredService<DocumentService>();

        var patient = await patients.CreateAsync(new PatientDraft { FirstName = "Lina", LastName = "Mansour" });

        await patients.AddConsultationAsync(patient.Id, new DateTime(2026, 7, 1), "Première consultation", "Classe II");
        await patients.AddConsultationAsync(patient.Id, new DateTime(2026, 7, 10), "Pose bagues", null);

        var photo = Encoding.UTF8.GetBytes("fausse-photo-intra-orale");
        var imported = await documents.ImportAsync(
            patient.Id, new MemoryStream(photo), "molaires.jpg", DocumentCategory.PhotoIntraOrale);
        Assert.Equal("image/jpeg", imported.ContentType);
        Assert.Equal(photo.Length, imported.SizeBytes);

        var record = await patients.GetAsync(patient.Id);
        Assert.NotNull(record);
        Assert.Equal(2, record.Consultations.Count);
        Assert.Equal("Pose bagues", record.Consultations[0].Reason); // triées par date décroissante
        var document = Assert.Single(record.Documents);

        // Le contenu relu depuis l'object store chiffré est identique.
        await using var stream = await documents.OpenAsync(document);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(photo, buffer.ToArray());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataDirectory))
                Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Fichiers encore verrouillés : le répertoire temporaire sera nettoyé par l'OS.
        }
    }
}
