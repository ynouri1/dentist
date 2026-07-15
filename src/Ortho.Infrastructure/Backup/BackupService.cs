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

    /// <summary>
    /// Restaure un export zip : snapshot de sécurité de la base actuelle, puis
    /// remplacement des données (base + objets + clés). L'appelant doit ensuite
    /// réappliquer les migrations. Les clés DPAPI ne sont restaurables que par
    /// le même compte Windows.
    /// </summary>
    public void RestoreFrom(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.GetEntry("ortho.db") is null)
            throw new InvalidOperationException("Archive invalide : ortho.db absent.");

        SqliteConnection.ClearAllPools();
        SnapshotDatabase();

        // L'object store est remplacé intégralement pour ne pas mélanger deux états.
        var objectsDirectory = Path.Combine(options.DataDirectory, "objects");
        if (Directory.Exists(objectsDirectory))
            Directory.Delete(objectsDirectory, recursive: true);

        var root = Path.GetFullPath(options.DataDirectory);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) // répertoire
                continue;

            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Entrée d'archive invalide : {entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        // Fichiers WAL/SHM d'une autre session : obsolètes après restauration.
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            var path = Path.Combine(options.DataDirectory, "ortho.db" + sidecar);
            if (File.Exists(path) && archive.GetEntry("ortho.db" + sidecar.TrimStart('-')) is null)
                File.Delete(path);
        }

        Log.Information("Sauvegarde restaurée depuis {Archive}", archivePath);
    }

    /// <summary>Export du diagnostic : uniquement les journaux, jamais de données patient.</summary>
    public string ExportLogsTo(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var archivePath = Path.Combine(targetDirectory, $"ortho-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var logsDirectory = Path.Combine(options.DataDirectory, "logs");
        if (Directory.Exists(logsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(logsDirectory))
            {
                // ReadWrite : le fichier du jour est encore ouvert par Serilog.
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var entry = archive.CreateEntry("logs/" + Path.GetFileName(file));
                using var target = entry.Open();
                source.CopyTo(target);
            }
        }

        Log.Information("Diagnostic exporté : {Path}", archivePath);
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
