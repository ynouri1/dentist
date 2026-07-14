using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ortho.Infrastructure;
using Ortho.Infrastructure.Backup;
using Ortho.Infrastructure.Persistence;
using Ortho.UI.ViewModels;
using Ortho.UI.Views;
using Serilog;

namespace Ortho.UI;

public partial class App : Avalonia.Application
{
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

        var services = new ServiceCollection()
            .AddOrthoInfrastructure(new OrthoDataOptions(dataDirectory))
            .AddSingleton<MainViewModel>()
            .BuildServiceProvider();

        // Applique les migrations au démarrage : la base est toujours au bon schéma.
        // Un snapshot est pris avant toute migration pour pouvoir revenir en arrière.
        using (var db = services.GetRequiredService<IDbContextFactory<OrthoDbContext>>().CreateDbContext())
        {
            if (db.Database.GetPendingMigrations().Any())
                services.GetRequiredService<BackupService>().SnapshotDatabase();
            db.Database.Migrate();
        }

        Log.Information("Application démarrée, données dans {DataDirectory}", dataDirectory);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainViewModel>(),
            };
            desktop.Exit += (_, _) => Log.CloseAndFlush();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
