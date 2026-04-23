using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Models;
using NoteApp.Services;
using NoteApp.Session;
using Xunit;

namespace NoteApp.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        // Each test class gets its own session file to avoid parallel conflicts
        SessionManager.SessionFile = Path.Combine(
            Path.GetTempPath(), $".session_auth_{Guid.NewGuid()}");

        _db = TestDbContextFactory.CreateInMemory($"AuthDb_{Guid.NewGuid()}");
        _authService = new AuthService(_db);

        // Seed test user
        _db.Users.Add(new User
        {
            Login = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = Roles.User
        });
        _db.Users.Add(new User
        {
            Login = "adminuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("adminpass"),
            Role = Roles.Admin
        });
        _db.SaveChanges();

        // Clear any existing session between tests
        SessionManager.Clear();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Login_ValidCredentials_ReturnsSession()
    {
        var session = _authService.Login("testuser", "password123");

        Assert.NotNull(session);
        Assert.Equal("testuser", session.UserLogin);
        Assert.Equal(Roles.User, session.Role);
        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.True(session.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void Login_WrongPassword_ThrowsAuthException()
    {
        Assert.Throws<AuthException>(() =>
            _authService.Login("testuser", "wrongpassword"));
    }

    [Fact]
    public void Login_NonExistentUser_ThrowsAuthException()
    {
        Assert.Throws<AuthException>(() =>
            _authService.Login("nobody", "anypassword"));
    }

    [Fact]
    public void Login_EmptyPassword_ThrowsAuthException()
    {
        Assert.Throws<AuthException>(() =>
            _authService.Login("testuser", ""));
    }

    [Fact]
    public void Login_InactiveUser_ThrowsAuthException()
    {
        var user = _db.Users.First(u => u.Login == "testuser");
        user.IsActive = false;
        _db.SaveChanges();

        Assert.Throws<AuthException>(() =>
            _authService.Login("testuser", "password123"));
    }

    // ── Session persistence ───────────────────────────────────────────────────

    [Fact]
    public void Login_SetsCurrentSession()
    {
        _authService.Login("testuser", "password123");

        Assert.True(SessionManager.IsAuthenticated);
        Assert.NotNull(SessionManager.Current);
        Assert.Equal("testuser", SessionManager.Current!.UserLogin);
    }

    [Fact]
    public void Logout_ClearsSession()
    {
        _authService.Login("testuser", "password123");
        _authService.Logout();

        Assert.False(SessionManager.IsAuthenticated);
        Assert.Null(SessionManager.Current);
    }

    // ── Role checks ───────────────────────────────────────────────────────────

    [Fact]
    public void RequireAuth_WithoutSession_ThrowsAuthException()
    {
        SessionManager.Clear();
        Assert.Throws<AuthException>(() => AuthService.RequireAuth("test cmd"));
    }

    [Fact]
    public void RequireRole_AdminOnAdminCommand_Passes()
    {
        _authService.Login("adminuser", "adminpass");
        // Should not throw
        AuthService.RequireRole(Roles.Admin, "admin cmd");
    }

    [Fact]
    public void RequireRole_UserOnAdminCommand_ThrowsAccessDenied()
    {
        _authService.Login("testuser", "password123");
        Assert.Throws<AccessDeniedException>(() =>
            AuthService.RequireRole(Roles.Admin, "admin cmd"));
    }

    [Fact]
    public void RequireRole_ManagerOnManagerCommand_Passes()
    {
        _db.Users.Add(new User
        {
            Login = "mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mgrpass"),
            Role = Roles.Manager
        });
        _db.SaveChanges();

        _authService.Login("mgr", "mgrpass");
        // Should not throw
        AuthService.RequireRole(Roles.Manager, "watchdog config");
    }

    // ── User management (admin) ────────────────────────────────────────────────

    [Fact]
    public void CreateUser_AsAdmin_CreatesUser()
    {
        _authService.Login("adminuser", "adminpass");

        var user = _authService.CreateUser("newuser", "newpass", Roles.User);

        Assert.NotNull(user);
        Assert.Equal("newuser", user.Login);
        Assert.Equal(Roles.User, user.Role);
        Assert.True(_db.Users.Any(u => u.Login == "newuser"));
    }

    [Fact]
    public void CreateUser_AsRegularUser_ThrowsAccessDenied()
    {
        _authService.Login("testuser", "password123");
        Assert.Throws<AccessDeniedException>(() =>
            _authService.CreateUser("x", "y", Roles.User));
    }

    [Fact]
    public void CreateUser_DuplicateLogin_ThrowsValidation()
    {
        _authService.Login("adminuser", "adminpass");
        Assert.Throws<ValidationException>(() =>
            _authService.CreateUser("testuser", "pass", Roles.User));
    }

    [Fact]
    public void CreateUser_InvalidRole_ThrowsValidation()
    {
        _authService.Login("adminuser", "adminpass");
        Assert.Throws<ValidationException>(() =>
            _authService.CreateUser("newlogin", "pass", "superadmin"));
    }

    public void Dispose()
    {
        SessionManager.Clear();
        _db.Dispose();
    }
}
