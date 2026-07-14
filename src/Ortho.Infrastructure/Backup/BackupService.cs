using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Ortho.Infrastructure.Backup;

/// <summary>
/// Sauvegardes du cabinet. Les fichiers copiés (base SQLCipher, object store)
/// sont déjà chiffrés ; les clés DPAPI sont incluses mais ne sont restaurables
/// que par le même compte Windows — la restauration sur un autre poste passera
/// par un export dédié (prévu avec le module multi-poste).
/// </summary>
public class BackupService(OrthoDataOptions options)
{
    private const int RetainedSnapshots = 10;

    /// <summary>
    /// Copie de la base avant toute migration de schéma. Retourne le chemin du
    /// snapshot, ou null si la base n'existe pas encore (premier lancement).
    /// </summary>
    public string? SnapshotDatabase()
    {
        var databasePath = Path.Combine(options.DataDirectory, "ortho.db");
        if (!File.Exists(databasePath))
            return null;

        // Ferme les connexions du pool pour que le fichier soit copiable et cohérent.
        SqliteConnection.ClearAllPools();

        var backupDirectory = Path.Combine(options.DataDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var destination = Path.Combine(backupDirectory, $"ortho-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");
        File.Copy(databasePath, destination);
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            if (File.Exists(databasePath + sidecar))
                File.Copy(databasePath + sidecar, destination + sidecar);
        }

        Prune(backupDirectory);
        Log.Information("Snapshot de la base créé avant migration : {Path}", destination);
        return destination;
    }

    /// <summary>Export complet (base + documents + clés) en une archive zip.</summary>
    public string ExportTo(string targetDirectory)
    {
        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(targetDirectory);

        var archivePath = Path.Combine(targetDirectory, $"ortho-sauvegarde-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var file in Directory.EnumerateFiles(options.DataDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(options.DataDirectory, file);
            var topFolder = relative.Split(Path.DirectorySeparatorChar, '/')[0];

            // Les logs et les snapshots internes n'ont rien à faire dans un export.
            if (topFolder is "logs" or "backups")
                continue;

            archive.CreateEntryFromFile(file, relative.Replace('\\', '/'));
        }

        Log.Information("Export de sauvegarde créé : {Path}", archivePath);
        return archivePath;
    }

    private static void Prune(string backupDirectory)
    {
        var snapshots = new DirectoryInfo(backupDirectory)
            .GetFiles("ortho-*.db")
            .OrderByDescending(f => f.Name)
            .Skip(RetainedSnapshots);

        foreach (var old in snapshots)
        {
            foreach (var sidecar in new[] { "", "-wal", "-shm" })
            {
                var path = old.FullName + sidecar;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
