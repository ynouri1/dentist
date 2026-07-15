using System;
using System.IO;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Application.Abstractions;
using Ortho.Application.Users;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Backup;
using Ortho.Infrastructure.Persistence;
using Ortho.UI.ViewModels;
using Ortho.UI.Views;
using Serilog;

namespace Ortho.UI;

public partial class App : Avalonia.Application
{
    private IServiceProvider _services = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ortho");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(dataDirectory, "logs", "ortho-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90)
            .CreateLogger();

        _services = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(dataDirectory))
            .AddSingleton<Ortho.Reporting.ReportService>()
            .AddSingleton<MainViewModel>()
            .BuildServiceProvider();

        // Applique les migrations au démarrage : la base est toujours au bon schéma.
        // Un snapshot est pris avant toute migration pour pouvoir revenir en arrière.
        using (var db = _services.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext())
        {
            if (db.Database.GetPendingMigrations().Any())
                _services.GetRequiredService<BackupService>().SnapshotDatabase();
            db.Database.Migrate();
        }

        // Crash reporting local : tout crash finit dans les journaux (export via Diagnostic).
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Crash non géré");
            Log.CloseAndFlush();
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception de tâche non observée");
            args.SetObserved();
        };

        Log.Information("Application démarrée, données dans {DataDirectory}", dataDirectory);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateLoginWindow(desktop);
            desktop.Exit += (_, _) => Log.CloseAndFlush();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private LoginWindow CreateLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var users = _services.GetRequiredService<UserService>();
        var loginViewModel = new LoginViewModel(users)
        {
            Mode = users.HasAnyUserAsync().GetAwaiter().GetResult()
                ? LoginMode.Login
                : LoginMode.FirstRun,
        };
        var loginWindow = new LoginWindow { DataContext = loginViewModel };

        // Le code de secours est montré une seule fois, à la création du compte.
        loginViewModel.RecoveryCodeGenerated = code => InfoDialog.ShowAsync(
            loginWindow,
            Ortho.UI.Localization.L.Get("RecoveryCodeTitle"),
            Ortho.UI.Localization.L.Get("RecoveryCodeMessage"),
            code);

        loginWindow.RestoreRequested = async archivePath =>
        {
            try
            {
                _services.GetRequiredService<BackupService>().RestoreFrom(archivePath);
                using (var db = _services.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext())
                    db.Database.Migrate();

                loginViewModel.Mode = users.HasAnyUserAsync().GetAwaiter().GetResult()
                    ? LoginMode.Login
                    : LoginMode.FirstRun;
                loginViewModel.Error = null;
                await InfoDialog.ShowAsync(
                    loginWindow,
                    Ortho.UI.Localization.L.Get("RestoreBackup"),
                    Ortho.UI.Localization.L.Get("RestoreDone"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Restauration impossible");
                loginViewModel.Error = Ortho.UI.Localization.L.F("RestoreFailed", ex.Message);
            }
        };

        loginViewModel.Succeeded = user =>
        {
            _services.GetRequiredService<CurrentUserContext>().User = user;

            var mainViewModel = _services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainViewModel };
            mainViewModel.LockRequested = () => LockSession(mainWindow);

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            loginWindow.Close();
        };

        return loginWindow;
    }

    private void LockSession(MainWindow mainWindow)
    {
        var currentUser = _services.GetRequiredService<CurrentUserContext>();
        var audit = _services.GetRequiredService<IAuditTrail>();
        _ = audit.RecordAsync("auth.lock", nameof(CurrentUserContext), currentUser.Name);

        var unlockViewModel = new LoginViewModel(_services.GetRequiredService<UserService>())
        {
            Mode = LoginMode.Unlock,
            Username = currentUser.User?.Username ?? "",
        };
        var unlockWindow = new LoginWindow { DataContext = unlockViewModel };

        unlockViewModel.Succeeded = _ =>
        {
            mainWindow.Show();
            unlockWindow.Close();
        };

        mainWindow.Hide();
        unlockWindow.Show();
    }
}
