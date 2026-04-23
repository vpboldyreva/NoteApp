using Microsoft.EntityFrameworkCore;
using NoteApp.Data;
using NoteApp.Models;
using Xunit;

namespace NoteApp.Tests;

public class DatabaseTests : IDisposable
{
    private readonly AppDbContext _db;

    public DatabaseTests()
    {
        _db = TestDbContextFactory.CreateInMemory($"DbTest_{Guid.NewGuid()}");
    }

    // ── Connection ────────────────────────────────────────────────────────────

    [Fact]
    public void Database_CanConnect()
    {
        // EnsureCreated() inside CreateInMemory — just check we can query
        var count = _db.Users.Count();
        Assert.True(count >= 0);
    }

    [Fact]
    public void Database_TablesExist()
    {
        // Verify all entity sets are accessible
        Assert.NotNull(_db.Users);
        Assert.NotNull(_db.Notes);
        Assert.NotNull(_db.WatchdogLogs);
        Assert.NotNull(_db.WatchdogConfigs);
        Assert.NotNull(_db.ActionLogs);
    }

    // ── WatchdogConfig seed ───────────────────────────────────────────────────

    [Fact]
    public void Database_WatchdogConfigsSeeded()
    {
        var configs = _db.WatchdogConfigs.ToList();
        Assert.Equal(3, configs.Count);
        Assert.Contains(configs, c => c.Metric == "cpu");
        Assert.Contains(configs, c => c.Metric == "ram");
        Assert.Contains(configs, c => c.Metric == "hdd");
    }

    // ── User CRUD ─────────────────────────────────────────────────────────────

    [Fact]
    public void Database_CanInsertUser()
    {
        var user = new User
        {
            Login = "dbuser",
            PasswordHash = "hash",
            Role = Roles.User
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        Assert.True(user.Id > 0);
        Assert.Equal("dbuser", _db.Users.Find(user.Id)!.Login);
    }

    [Fact(Skip = "InMemory DB does not enforce unique indexes. This constraint is verified by SQLite in production.")]
    public void Database_UserLoginIsUnique()
    {
        _db.Users.Add(new User { Login = "unique", PasswordHash = "h1", Role = Roles.User });
        _db.SaveChanges();

        _db.Users.Add(new User { Login = "unique", PasswordHash = "h2", Role = Roles.User });

        // In SQLite unique constraint throws on SaveChanges
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Database_CanDeleteUser()
    {
        var user = new User { Login = "todel", PasswordHash = "h", Role = Roles.User };
        _db.Users.Add(user);
        _db.SaveChanges();

        _db.Users.Remove(user);
        _db.SaveChanges();

        Assert.Null(_db.Users.Find(user.Id));
    }

    // ── Note CRUD ─────────────────────────────────────────────────────────────

    [Fact]
    public void Database_CanInsertNote()
    {
        var user = new User { Login = "noteowner", PasswordHash = "h", Role = Roles.User };
        _db.Users.Add(user);
        _db.SaveChanges();

        var note = new Note { Title = "Test note", Body = "Body text", UserId = user.Id };
        _db.Notes.Add(note);
        _db.SaveChanges();

        Assert.True(note.Id > 0);
    }

    [Fact]
    public void Database_NoteDeletedWhenUserDeleted()
    {
        var user = new User { Login = "cascade", PasswordHash = "h", Role = Roles.User };
        _db.Users.Add(user);
        _db.SaveChanges();

        _db.Notes.Add(new Note { Title = "Note", Body = "Body", UserId = user.Id });
        _db.SaveChanges();

        _db.Users.Remove(user);
        _db.SaveChanges();

        var notes = _db.Notes.Where(n => n.UserId == user.Id).ToList();
        Assert.Empty(notes);
    }

    [Fact]
    public void Database_CanQueryNoteById_NotFound()
    {
        var note = _db.Notes.FirstOrDefault(n => n.Id == 99999);
        Assert.Null(note);
    }

    // ── WatchdogLog ───────────────────────────────────────────────────────────

    [Fact]
    public void Database_CanInsertWatchdogLog()
    {
        var log = new WatchdogLog
        {
            CpuUsage = 45.5,
            RamUsageMb = 1024.0,
            DiskUsagePercent = 72.3,
            RecordedAt = DateTime.UtcNow
        };

        _db.WatchdogLogs.Add(log);
        _db.SaveChanges();

        Assert.True(log.Id > 0);
        Assert.Equal(45.5, _db.WatchdogLogs.Find(log.Id)!.CpuUsage);
    }

    public void Dispose() => _db.Dispose();
}
