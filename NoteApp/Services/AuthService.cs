using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Models;
using NoteApp.Session;

namespace NoteApp.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public SessionData Login(string login, string password)
    {
        var user = _db.Users.FirstOrDefault(u => u.Login == login && u.IsActive);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new AuthException("Неверный логин или пароль", "login", login);

        var session = new SessionData
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            UserLogin = user.Login,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        SessionManager.Save(session);
        Logger.LogInfo($"Пользователь {login} вошёл в систему", "login");

        _db.ActionLogs.Add(new ActionLog
        {
            UserId = user.Id,
            Command = "login",
            Details = $"Успешный вход. Роль: {user.Role}"
        });
        _db.SaveChanges();

        return session;
    }

    public void Logout()
    {
        var session = SessionManager.Current;
        if (session != null)
        {
            _db.ActionLogs.Add(new ActionLog
            {
                UserId = session.UserId,
                Command = "logout",
                Details = "Выход из системы"
            });
            _db.SaveChanges();
            Logger.LogInfo($"Пользователь {session.UserLogin} вышел", "logout");
        }
        SessionManager.Clear();
    }

    // Admin: create user
    public User CreateUser(string login, string password, string role)
    {
        RequireRole(Roles.Admin, "user create");

        if (!Roles.IsValid(role))
            throw new ValidationException($"Неверная роль: {role}. Допустимые: user, admin, manager", "user create");

        if (_db.Users.Any(u => u.Login == login))
            throw new ValidationException($"Пользователь '{login}' уже существует", "user create");

        var user = new User
        {
            Login = login,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        LogAction("user create", $"Создан пользователь {login} с ролью {role}");
        return user;
    }

    // Admin: delete user
    public void DeleteUser(int userId)
    {
        RequireRole(Roles.Admin, "user delete");

        var user = _db.Users.Find(userId)
            ?? throw new NotFoundException($"Пользователь id={userId} не найден", "user delete");

        user.IsActive = false;
        _db.SaveChanges();
        LogAction("user delete", $"Деактивирован пользователь id={userId}");
    }

    // Admin: change user role
    public void ChangeRole(int userId, string newRole)
    {
        RequireRole(Roles.Admin, "user role");

        if (!Roles.IsValid(newRole))
            throw new ValidationException($"Неверная роль: {newRole}", "user role");

        var user = _db.Users.Find(userId)
            ?? throw new NotFoundException($"Пользователь id={userId} не найден", "user role");

        user.Role = newRole;
        _db.SaveChanges();
        LogAction("user role", $"Роль пользователя id={userId} изменена на {newRole}");
    }

    // Seed first admin if no users exist
    public void EnsureAdminExists()
    {
        if (!_db.Users.Any())
        {
            _db.Users.Add(new User
            {
                Login = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = Roles.Admin
            });
            _db.SaveChanges();
            Console.WriteLine("Создан администратор по умолчанию: login=admin, password=admin123");
            Console.WriteLine("Смените пароль после первого входа!");
        }
    }

    // Helpers
    public static void RequireAuth(string command = "")
    {
        if (!SessionManager.TryLoad())
            throw new AuthException("Требуется авторизация. Выполните: login --user <login> --password <pass>", command);
    }

    public static void RequireRole(string requiredRole, string command = "")
    {
        RequireAuth(command);
        var session = SessionManager.Current!;

        bool hasAccess = requiredRole switch
        {
            Roles.Admin => session.Role == Roles.Admin,
            Roles.Manager => session.Role is Roles.Admin or Roles.Manager,
            _ => true
        };

        if (!hasAccess)
            throw new AccessDeniedException(
                $"Недостаточно прав. Требуется роль: {requiredRole}", command, session.UserLogin);
    }

    private void LogAction(string command, string details)
    {
        var session = SessionManager.Current;
        _db.ActionLogs.Add(new ActionLog
        {
            UserId = session?.UserId,
            Command = command,
            Details = details
        });
        _db.SaveChanges();
    }
}
