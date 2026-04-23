namespace NoteApp.Models;

public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<ActionLog> ActionLogs { get; set; } = new List<ActionLog>();
}

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
    public const string Manager = "manager";

    public static bool IsValid(string role) =>
        role == User || role == Admin || role == Manager;
}
