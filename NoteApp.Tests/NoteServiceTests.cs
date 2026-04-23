using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Models;
using NoteApp.Services;
using NoteApp.Session;
using Xunit;

namespace NoteApp.Tests;

public class NoteServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NoteService _noteService;
    private readonly AuthService _authService;
    private User _user = null!;
    private User _otherUser = null!;

    public NoteServiceTests()
    {
        // Unique session file per test class to avoid parallel file conflicts
        SessionManager.SessionFile = Path.Combine(
            Path.GetTempPath(), $".session_note_{Guid.NewGuid()}");

        _db = TestDbContextFactory.CreateInMemory($"NoteDb_{Guid.NewGuid()}");
        _noteService = new NoteService(_db);
        _authService = new AuthService(_db);

        _user = new User { Login = "noteuser", PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"), Role = Roles.User };
        _otherUser = new User { Login = "other", PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"), Role = Roles.User };
        _db.Users.AddRange(_user, _otherUser);
        _db.SaveChanges();

        SessionManager.Clear();
    }

    private void LoginAsUser() => _authService.Login("noteuser", "pass");
    private void LoginAsOther() => _authService.Login("other", "pass");

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidNote_ReturnsNoteWithId()
    {
        LoginAsUser();
        var note = _noteService.Create("My Title", "My body text");

        Assert.True(note.Id > 0);
        Assert.Equal("My Title", note.Title);
        Assert.Equal("My body text", note.Body);
        Assert.Equal(_user.Id, note.UserId);
    }

    [Fact]
    public void Create_WithoutSession_ThrowsAuth()
    {
        SessionManager.Clear();
        Assert.Throws<AuthException>(() => _noteService.Create("T", "B"));
    }

    [Fact]
    public void Create_EmptyTitle_ThrowsValidation()
    {
        LoginAsUser();
        Assert.Throws<ValidationException>(() => _noteService.Create("", "body"));
    }

    [Fact]
    public void Create_TitleTooLong_ThrowsValidation()
    {
        LoginAsUser();
        var longTitle = new string('A', 256);
        Assert.Throws<ValidationException>(() => _noteService.Create(longTitle, "body"));
    }

    [Fact]
    public void Create_TitleAt255Chars_Succeeds()
    {
        LoginAsUser();
        var title = new string('A', 255);
        var note = _noteService.Create(title, "body");
        Assert.Equal(255, note.Title.Length);
    }

    [Fact]
    public void Create_EmptyBody_ThrowsValidation()
    {
        LoginAsUser();
        Assert.Throws<ValidationException>(() => _noteService.Create("Title", ""));
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public void List_ReturnsOnlyOwnNotes()
    {
        LoginAsUser();
        _noteService.Create("Note1", "Body1");
        _noteService.Create("Note2", "Body2");

        LoginAsOther();
        _noteService.Create("OtherNote", "Body");

        LoginAsUser();
        var notes = _noteService.List();

        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(_user.Id, n.UserId));
    }

    [Fact]
    public void List_EmptyForNewUser_ReturnsEmpty()
    {
        LoginAsUser();
        var notes = _noteService.List();
        Assert.Empty(notes);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_ExistingOwnNote_ReturnsNote()
    {
        LoginAsUser();
        var created = _noteService.Create("Fetch me", "body");
        var fetched = _noteService.Get(created.Id);

        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Fetch me", fetched.Title);
    }

    [Fact]
    public void Get_NonExistentId_ThrowsNotFound()
    {
        LoginAsUser();
        Assert.Throws<NotFoundException>(() => _noteService.Get(99999));
    }

    [Fact]
    public void Get_OtherUserNote_ThrowsNotFound()
    {
        LoginAsOther();
        var othersNote = _noteService.Create("Other's", "body");

        LoginAsUser();
        // Should not be accessible — treated as not found
        Assert.Throws<NotFoundException>(() => _noteService.Get(othersNote.Id));
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Title_UpdatesCorrectly()
    {
        LoginAsUser();
        var note = _noteService.Create("Old title", "body");
        var updated = _noteService.Update(note.Id, "New title", null);

        Assert.Equal("New title", updated.Title);
        Assert.Equal("body", updated.Body); // body unchanged
    }

    [Fact]
    public void Update_Body_UpdatesCorrectly()
    {
        LoginAsUser();
        var note = _noteService.Create("title", "old body");
        var updated = _noteService.Update(note.Id, null, "new body");

        Assert.Equal("title", updated.Title);
        Assert.Equal("new body", updated.Body);
    }

    [Fact]
    public void Update_NoFields_ThrowsValidation()
    {
        LoginAsUser();
        var note = _noteService.Create("t", "b");
        Assert.Throws<ValidationException>(() => _noteService.Update(note.Id, null, null));
    }

    [Fact]
    public void Update_NonExistentNote_ThrowsNotFound()
    {
        LoginAsUser();
        Assert.Throws<NotFoundException>(() => _noteService.Update(99999, "t", null));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_OwnNote_RemovesFromDb()
    {
        LoginAsUser();
        var note = _noteService.Create("delete me", "body");
        _noteService.Delete(note.Id);

        Assert.Throws<NotFoundException>(() => _noteService.Get(note.Id));
    }

    [Fact]
    public void Delete_NonExistentNote_ThrowsNotFound()
    {
        LoginAsUser();
        Assert.Throws<NotFoundException>(() => _noteService.Delete(99999));
    }

    public void Dispose()
    {
        SessionManager.Clear();
        _db.Dispose();
    }
}
