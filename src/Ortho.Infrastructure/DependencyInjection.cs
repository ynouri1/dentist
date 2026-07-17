using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Abstractions;
using Ortho.Application.Documents;
using Ortho.Application.Patients;
using Ortho.Application.Users;
using Ortho.Application.Cephalometry;
using Ortho.Application.Imaging;
using Ortho.Application.Training;
using Ortho.Infrastructure.Audit;
using Ortho.Infrastructure.Backup;
using Ortho.Infrastructure.Imaging;
using Ortho.Infrastructure.Persistence;
using Ortho.Infrastructure.Security;
using Ortho.Infrastructure.Storage;

namespace Ortho.Infrastructure;

public record OrthoDataOptions(string DataDirectory);

public static class DependencyInjection
{
    public static IServiceCollection AddOrthoInfrastructure(this IServiceCollection services, OrthoDataOptions options)
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(options.DataDirectory);

        var secrets = new LocalSecretStore(Path.Combine(options.DataDirectory, "keys"));
        var databasePassword = Convert.ToHexString(secrets.GetOrCreate("database", 32));
        var objectStoreKey = secrets.GetOrCreate("objects", 32);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(options.DataDirectory, "ortho.db"),
            Password = databasePassword,
        }.ToString();

        services.AddDbContextFactory<OrthoDbContext>(builder => builder.UseSqlite(connectionString));

        services.AddSingleton<IObjectStore>(
            new EncryptedFileObjectStore(Path.Combine(options.DataDirectory, "objects"), objectStoreKey));
        services.AddSingleton<IPatientRepository, PatientRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IAuditTrail, DbAuditTrail>();
        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddSingleton<PatientService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<IMedicalImageDecoder, MedicalImageDecoder>();
        services.AddSingleton<ImagingService>();
        services.AddSingleton<ICephRepository, CephRepository>();
        services.AddSingleton<CephalometryService>();
        services.AddSingleton<ILandmarkDetector>(
            new OnnxLandmarkDetector(Path.Combine(options.DataDirectory, "models")));
        services.AddSingleton<TrainingDataExporter>();
        services.AddSingleton(new BackupService(options));

        return services;
    }
}
