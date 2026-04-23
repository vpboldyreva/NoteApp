using System.Text;
using System.Text.Json;

namespace NoteApp.Session;

public class SessionData
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserLogin { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public static class SessionManager
{
    public static string SessionFile = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, ".session");

    private static readonly object _fileLock = new();
    private static SessionData? _current;

    public static bool IsAuthenticated =>
        _current != null && _current.ExpiresAt > DateTime.UtcNow;

    public static SessionData? Current => IsAuthenticated ? _current : null;

    public static void Save(SessionData session)
    {
        _current = session;
        var json = JsonSerializer.Serialize(session);
        var encrypted = XorEncrypt(json);
        lock (_fileLock)
        {
            try { File.WriteAllBytes(SessionFile, encrypted); }
            catch { }
        }
    }

    public static bool TryLoad()
    {
        if (_current != null && IsAuthenticated) return true;
        if (!File.Exists(SessionFile)) return false;
        try
        {
            byte[] encrypted;
            lock (_fileLock) { encrypted = File.ReadAllBytes(SessionFile); }
            var json = XorDecrypt(encrypted);
            var session = JsonSerializer.Deserialize<SessionData>(json);
            if (session == null || session.ExpiresAt <= DateTime.UtcNow) return false;
            _current = session;
            return true;
        }
        catch { return false; }
    }

    public static void Clear()
    {
        _current = null;
        lock (_fileLock)
        {
            try { if (File.Exists(SessionFile)) File.Delete(SessionFile); }
            catch { }
        }
    }

    private static readonly byte[] Key = Encoding.UTF8.GetBytes("NoteApp_S3ss10n!");

    private static byte[] XorEncrypt(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        for (int i = 0; i < data.Length; i++)
            data[i] ^= Key[i % Key.Length];
        return data;
    }

    private static string XorDecrypt(byte[] data)
    {
        var copy = (byte[])data.Clone();
        for (int i = 0; i < copy.Length; i++)
            copy[i] ^= Key[i % Key.Length];
        return Encoding.UTF8.GetString(copy);
    }
}
