using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Models;
using NoteApp.Session;

namespace NoteApp.Services;

public class NoteService
{
    private readonly AppDbContext _db;

    public NoteService(AppDbContext db)
    {
        _db = db;
    }

    public Note Create(string title, string body)
    {
        AuthService.RequireAuth("note create");

        ValidateTitle(title);
        ValidateBody(body);

        var session = SessionManager.Current!;
        var note = new Note
        {
            Title = title,
            Body = body,
            UserId = session.UserId
        };

        _db.Notes.Add(note);
        _db.SaveChanges();

        Logger.LogInfo($"Заметка создана id={note.Id}", "note create");
        return note;
    }

    public List<Note> List()
    {
        AuthService.RequireAuth("note list");
        var userId = SessionManager.Current!.UserId;
        return _db.Notes.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToList();
    }

    public Note Get(int id)
    {
        AuthService.RequireAuth("note get");
        var userId = SessionManager.Current!.UserId;
        var note = _db.Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId)
            ?? throw new NotFoundException($"Заметка id={id} не найдена", "note get");
        return note;
    }

    public Note Update(int id, string? title, string? body)
    {
        AuthService.RequireAuth("note update");

        if (title == null && body == null)
            throw new ValidationException("Укажите хотя бы одно поле для обновления: --title или --body", "note update");

        var note = Get(id);

        if (title != null)
        {
            ValidateTitle(title);
            note.Title = title;
        }

        if (body != null)
        {
            ValidateBody(body);
            note.Body = body;
        }

        note.UpdatedAt = DateTime.UtcNow;
        _db.SaveChanges();

        Logger.LogInfo($"Заметка id={id} обновлена", "note update");
        return note;
    }

    public void Delete(int id)
    {
        AuthService.RequireAuth("note delete");
        var note = Get(id);
        _db.Notes.Remove(note);
        _db.SaveChanges();
        Logger.LogInfo($"Заметка id={id} удалена", "note delete");
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("Заголовок не может быть пустым", "note");
        if (title.Length > 255)
            throw new ValidationException("Заголовок не может превышать 255 символов", "note");
    }

    private static void ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ValidationException("Тело заметки не может быть пустым", "note");
    }
}
