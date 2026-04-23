using System.Text.Json;

namespace NoteApp.Services;

public class VersionInfo
{
    public string Version { get; set; } = "1.0.0";
    public Dictionary<string, string> ConfigHashes { get; set; } = new();
}

public class UpdateService
{
    private readonly string _localVersionFile;
    private readonly string _remoteVersionFile; // In real app — URL; here simulate with local file

    public UpdateService(string? localVersionFile = null, string? remoteVersionFile = null)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localVersionFile = localVersionFile ?? Path.Combine(baseDir, "version.json");
        _remoteVersionFile = remoteVersionFile ?? Path.Combine(baseDir, "version_remote.json");

        EnsureLocalVersionExists();
    }

    public void CheckAndUpdate()
    {
        if (!File.Exists(_remoteVersionFile))
        {
            Console.WriteLine("Сервер обновлений недоступен. Пропускаем проверку.");
            return;
        }

        var local = LoadVersion(_localVersionFile);
        var remote = LoadVersion(_remoteVersionFile);

        if (local.Version == remote.Version)
        {
            Console.WriteLine($"Версия актуальна: v{local.Version}");
            return;
        }

        bool isMajor = IsMajorUpdate(local.Version, remote.Version);

        Console.Write($"Доступно обновление v{remote.Version}. Установить? (y/n): ");
        var answer = Console.ReadLine()?.Trim().ToLower();

        if (answer != "y") return;

        if (isMajor)
        {
            Console.WriteLine("Мажорное обновление — откат невозможен после установки.");
            ApplyUpdate(local, remote);
        }
        else
        {
            Console.WriteLine($"Минорное обновление v{local.Version} → v{remote.Version}.");
            ApplyUpdate(local, remote);
        }
    }

    private void ApplyUpdate(VersionInfo local, VersionInfo remote)
    {
        int updated = 0;
        foreach (var (file, remoteHash) in remote.ConfigHashes)
        {
            var localHash = local.ConfigHashes.GetValueOrDefault(file, "");
            if (localHash != remoteHash)
            {
                // Simulate config update
                Console.WriteLine($"  Обновлён файл конфигурации: {file}");
                updated++;
            }
        }

        // Update local version
        local.Version = remote.Version;
        local.ConfigHashes = remote.ConfigHashes;
        File.WriteAllText(_localVersionFile, JsonSerializer.Serialize(local, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"Обновление завершено. Обновлено файлов: {updated}. Версия: v{remote.Version}");
        Exceptions.Logger.LogInfo($"Обновление до v{remote.Version}, файлов: {updated}", "update");
    }

    public static bool IsMajorUpdate(string from, string to)
    {
        var fParts = from.Split('.');
        var tParts = to.Split('.');
        return fParts[0] != tParts[0];
    }

    private VersionInfo LoadVersion(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VersionInfo>(json) ?? new VersionInfo();
    }

    private void EnsureLocalVersionExists()
    {
        if (!File.Exists(_localVersionFile))
        {
            var v = new VersionInfo { Version = "1.0.0" };
            File.WriteAllText(_localVersionFile, JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
