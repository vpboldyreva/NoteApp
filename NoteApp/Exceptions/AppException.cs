namespace NoteApp.Exceptions;

/// <summary>
/// Base exception — all app errors inherit from this.
/// Automatically writes to log file on creation.
/// </summary>
public class AppException : Exception
{
    public string Command { get; }
    public string? UserLogin { get; }

    public AppException(string message, string command = "", string? userLogin = null)
        : base(message)
    {
        Command = command;
        UserLogin = userLogin;
        Logger.Log(this);
    }
}

public class AuthException : AppException
{
    public AuthException(string message, string command = "", string? userLogin = null)
        : base(message, command, userLogin) { }
}

public class AccessDeniedException : AppException
{
    public AccessDeniedException(string message, string command = "", string? userLogin = null)
        : base(message, command, userLogin) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message, string command = "", string? userLogin = null)
        : base(message, command, userLogin) { }
}

public class ValidationException : AppException
{
    public ValidationException(string message, string command = "", string? userLogin = null)
        : base(message, command, userLogin) { }
}

public class DatabaseException : AppException
{
    public DatabaseException(string message, string command = "", string? userLogin = null)
        : base(message, command, userLogin) { }
}

/// <summary>
/// Static logger used by all exceptions.
/// </summary>
public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "app.log");

    public static void Log(AppException ex)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] [{ex.GetType().Name}] " +
                   $"{ex.Message}. Команда: {ex.Command}. Пользователь: {ex.UserLogin ?? "anonymous"}";
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* log write failure must not crash the app */ }
    }

    public static void LogInfo(string message, string command = "")
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}. Команда: {command}";
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { }
    }

    public static string[] ReadLogs(int limit = 50)
    {
        if (!File.Exists(LogPath)) return Array.Empty<string>();
        var lines = File.ReadAllLines(LogPath);
        return lines.TakeLast(limit).ToArray();
    }
}
