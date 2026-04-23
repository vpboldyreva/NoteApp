namespace NoteApp.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;   // varchar(255)
    public string Body { get; set; } = string.Empty;    // TEXT
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
