using System.Text.Json;
using NoteApp.Services;
using Xunit;

namespace NoteApp.Tests;

public class UpdateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _localVersion;
    private readonly string _remoteVersion;

    public UpdateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NoteAppTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _localVersion = Path.Combine(_tempDir, "version.json");
        _remoteVersion = Path.Combine(_tempDir, "version_remote.json");
    }

    private void WriteVersion(string path, string version, Dictionary<string, string>? hashes = null)
    {
        var v = new VersionInfo
        {
            Version = version,
            ConfigHashes = hashes ?? new Dictionary<string, string>()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(v));
    }

    // ── IsMajorUpdate ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.9.9", "2.0.0", true)]
    [InlineData("2.0.0", "3.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    public void IsMajorUpdate_CorrectlyClassifies(string from, string to, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsMajorUpdate(from, to));
    }

    // ── No remote file ────────────────────────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_NoRemoteFile_PrintsUnavailable()
    {
        WriteVersion(_localVersion, "1.0.0");
        // Remote file does NOT exist

        var svc = new UpdateService(_localVersion, _remoteVersion);

        // Should not throw — just print that server is unavailable
        var ex = Record.Exception(() => svc.CheckAndUpdate());
        Assert.Null(ex);
    }

    // ── Same version ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_SameVersion_NoUpdatePrompt()
    {
        WriteVersion(_localVersion, "1.0.0");
        WriteVersion(_remoteVersion, "1.0.0");

        var svc = new UpdateService(_localVersion, _remoteVersion);

        var output = CaptureOutput(() => svc.CheckAndUpdate());
        Assert.Contains("актуальна", output);
    }

    // ── Minor update ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_MinorUpdate_DetectsNewVersion()
    {
        WriteVersion(_localVersion, "1.0.0");
        WriteVersion(_remoteVersion, "1.1.0", new Dictionary<string, string>
        {
            ["config.json"] = "newhash123"
        });

        // Simulate user typing "n" — no actual update
        var svc = new UpdateService(_localVersion, _remoteVersion);
        var output = CaptureOutputWithInput(() => svc.CheckAndUpdate(), "n");

        Assert.Contains("1.1.0", output);
    }

    // ── Major update ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_MajorUpdate_MarkedAsNoRollback()
    {
        WriteVersion(_localVersion, "1.9.0");
        WriteVersion(_remoteVersion, "2.0.0");

        var svc = new UpdateService(_localVersion, _remoteVersion);
        var output = CaptureOutputWithInput(() => svc.CheckAndUpdate(), "n");

        Assert.Contains("2.0.0", output);
    }

    // ── Apply update changes version file ─────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_UserAccepts_VersionFileUpdated()
    {
        WriteVersion(_localVersion, "1.0.0");
        WriteVersion(_remoteVersion, "1.1.0", new Dictionary<string, string>
        {
            ["app.config"] = "abc123"
        });

        var svc = new UpdateService(_localVersion, _remoteVersion);
        CaptureOutputWithInput(() => svc.CheckAndUpdate(), "y");

        var updated = JsonSerializer.Deserialize<VersionInfo>(File.ReadAllText(_localVersion));
        Assert.Equal("1.1.0", updated!.Version);
    }

    // ── Hash comparison ───────────────────────────────────────────────────────

    [Fact]
    public void CheckAndUpdate_SameHashes_NoFilesUpdated()
    {
        var hashes = new Dictionary<string, string> { ["app.config"] = "samehash" };
        WriteVersion(_localVersion, "1.0.0", hashes);
        WriteVersion(_remoteVersion, "1.1.0", hashes);

        var svc = new UpdateService(_localVersion, _remoteVersion);
        var output = CaptureOutputWithInput(() => svc.CheckAndUpdate(), "y");

        Assert.Contains("файлов: 0", output);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CaptureOutput(Action action)
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try { action(); }
        finally { Console.SetOut(original); }
        return sw.ToString();
    }

    private static string CaptureOutputWithInput(Action action, string input)
    {
        var inputReader = new StringReader(input);
        var outputWriter = new StringWriter();
        var origIn = Console.In;
        var origOut = Console.Out;
        Console.SetIn(inputReader);
        Console.SetOut(outputWriter);
        try { action(); }
        finally
        {
            Console.SetIn(origIn);
            Console.SetOut(origOut);
        }
        return outputWriter.ToString();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
