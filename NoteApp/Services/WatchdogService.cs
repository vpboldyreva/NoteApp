using System.Diagnostics;
using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Models;
using NoteApp.Session;

namespace NoteApp.Services;

public class WatchdogService : IDisposable
{
    private readonly AppDbContext _db;
    private Timer? _timer;
    private bool _running;
    private readonly object _lock = new();

    public bool IsRunning => _running;

    public WatchdogService(AppDbContext db)
    {
        _db = db;
    }

    public void Start()
    {
        AuthService.RequireRole(Roles.Admin, "watchdog start");

        lock (_lock)
        {
            if (_running)
            {
                Console.WriteLine("WatchDog uzhe zapushchen.");
                return;
            }
            _timer = new Timer(Collect, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _running = true;
        }

        Logger.LogInfo("WatchDog zapushchen", "watchdog start");
        Console.WriteLine("WatchDog zapushchen. Sbor metrik kazhdye 10 sekund.");
    }

    public void Stop()
    {
        AuthService.RequireRole(Roles.Admin, "watchdog stop");

        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _running = false;
        }

        Logger.LogInfo("WatchDog ostanovlen", "watchdog stop");
        Console.WriteLine("WatchDog ostanovlen.");
    }

    public void Status()
    {
        AuthService.RequireRole(Roles.Admin, "watchdog status");
        string status = _running ? "RABOTAET" : "OSTANOVLEN";
        Console.WriteLine("WatchDog: " + status);

        var configs = _db.WatchdogConfigs.ToList();
        foreach (var c in configs)
        {
            string state = c.Enabled ? "vklyuchen" : "vyklyuchen";
            Console.WriteLine("  " + c.Metric + ": " + state);
        }
    }

    public void Configure(string metric, bool enabled)
    {
        AuthService.RequireRole(Roles.Manager, "watchdog config");

        var config = _db.WatchdogConfigs.FirstOrDefault(c => c.Metric == metric);
        if (config == null)
        {
            throw new NotFoundException("Metrika " + metric + " ne naydena. Dopustimye: cpu, ram, hdd", "watchdog config");
        }

        config.Enabled = enabled;
        _db.SaveChanges();

        string state = enabled ? "vklyuchena" : "vyklyuchena";
        Logger.LogInfo("Metrika " + metric + " " + state, "watchdog config");
        Console.WriteLine("Metrika " + metric + ": " + state);
    }

    public void ShowLogs(int limit = 20)
    {
        AuthService.RequireRole(Roles.Admin, "watchdog logs");

        var logs = _db.WatchdogLogs
            .OrderByDescending(l => l.RecordedAt)
            .Take(limit)
            .ToList();

        if (!logs.Any())
        {
            Console.WriteLine("Logov net. Zapustite WatchDog: watchdog start");
            return;
        }

        Console.WriteLine(
            string.Format("{0,-22} {1,-8} {2,-10} {3,-8}",
            "Vremya", "CPU%", "RAM MB", "HDD%"));
        Console.WriteLine(new string('-', 50));

        foreach (var l in logs)
        {
            Console.WriteLine(
                string.Format("{0,-22} {1,-8} {2,-10} {3,-8}",
                l.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                l.CpuUsage.HasValue ? l.CpuUsage.Value.ToString("0.0") : "-",
                l.RamUsageMb.HasValue ? l.RamUsageMb.Value.ToString("0") : "-",
                l.DiskUsagePercent.HasValue ? l.DiskUsagePercent.Value.ToString("0.0") : "-"));
        }
    }

    private void Collect(object? _)
    {
        try
        {
            var configs = _db.WatchdogConfigs.ToDictionary(c => c.Metric, c => c.Enabled);

            var log = new WatchdogLog { RecordedAt = DateTime.UtcNow };

            if (configs.GetValueOrDefault("cpu"))
                log.CpuUsage = GetCpuUsage();

            if (configs.GetValueOrDefault("ram"))
                log.RamUsageMb = GetRamUsageMb();

            if (configs.GetValueOrDefault("hdd"))
                log.DiskUsagePercent = GetDiskUsagePercent();

            _db.WatchdogLogs.Add(log);
            _db.SaveChanges();
        }
        catch (Exception ex)
        {
            Logger.LogInfo("Oshibka sbora metrik: " + ex.Message, "watchdog collect");
        }
    }

    private static double GetCpuUsage()
    {
        using var proc = Process.GetCurrentProcess();
        var start = proc.TotalProcessorTime;
        Thread.Sleep(100);
        proc.Refresh();
        var end = proc.TotalProcessorTime;
        var used = (end - start).TotalMilliseconds;
        var total = 100.0 * Environment.ProcessorCount;
        return Math.Round(used / total * 100, 1);
    }

    private static double GetRamUsageMb()
    {
        using var proc = Process.GetCurrentProcess();
        return Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 1);
    }

    private static double GetDiskUsagePercent()
    {
        try
        {
            var root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "/";
            var drive = new DriveInfo(root);
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return Math.Round((double)used / drive.TotalSize * 100, 1);
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
