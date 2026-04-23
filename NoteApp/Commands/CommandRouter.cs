using NoteApp.Data;
using NoteApp.Exceptions;
using NoteApp.Services;
using NoteApp.Session;

namespace NoteApp.Commands;

public class CommandRouter
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly NoteService _noteService;
    private readonly WatchdogService _watchdogService;

    public CommandRouter(AppDbContext db, WatchdogService watchdog)
    {
        _db = db;
        _authService = new AuthService(db);
        _noteService = new NoteService(db);
        _watchdogService = watchdog;
    }

    public void Execute(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        var cmd = args[0].ToLower();

        try
        {
            switch (cmd)
            {
                case "login":
                    HandleLogin(args);
                    break;
                case "logout":
                    _authService.Logout();
                    Console.WriteLine("Сессия завершена.");
                    break;
                case "note":
                    HandleNote(args);
                    break;
                case "watchdog":
                    HandleWatchdog(args);
                    break;
                case "user":
                    HandleUser(args);
                    break;
                case "logs":
                    HandleLogs(args);
                    break;
                case "help":
                    PrintHelp();
                    break;
                default:
                    Console.WriteLine($"Неизвестная команда: {cmd}. Используйте: help");
                    break;
            }
        }
        catch (AppException ex)
        {
            Console.Error.WriteLine($"[ОШИБКА] {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[КРИТИЧЕСКАЯ ОШИБКА] {ex.Message}");
            Logger.LogInfo($"Необработанное исключение: {ex.Message}", cmd);
        }
    }

    // ─── LOGIN ───────────────────────────────────────────────────────────────
    private void HandleLogin(string[] args)
    {
        var user = GetArg(args, "--user");
        var pass = GetArg(args, "--password");

        if (user == null || pass == null)
        {
            Console.Error.WriteLine("Использование: login --user <login> --password <pass>");
            return;
        }

        var session = _authService.Login(user, pass);
        Console.WriteLine($"Добро пожаловать, {session.UserLogin}! Роль: {session.Role}");
    }

    // ─── NOTE ────────────────────────────────────────────────────────────────
    private void HandleNote(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Использование: note <create|list|get|update|delete>"); return; }

        switch (args[1].ToLower())
        {
            case "create":
            {
                var title = GetArg(args, "--title");
                var body = GetArg(args, "--body");
                if (title == null || body == null)
                {
                    Console.Error.WriteLine("Использование: note create --title <title> --body <body>");
                    throw new ValidationException("Отсутствуют обязательные параметры --title и/или --body", "note create",
                        SessionManager.Current?.UserLogin);
                }
                var note = _noteService.Create(title, body);
                Console.WriteLine($"Заметка создана. ID: {note.Id}");
                break;
            }
            case "list":
            {
                var notes = _noteService.List();
                if (!notes.Any()) { Console.WriteLine("Заметок нет."); break; }
                foreach (var n in notes)
                    Console.WriteLine($"[{n.Id}] {n.Title} ({n.CreatedAt:yyyy-MM-dd HH:mm})");
                break;
            }
            case "get":
            {
                var id = GetIntArg(args, "--id");
                if (id == null) { Console.Error.WriteLine("Использование: note get --id <id>"); return; }
                var n = _noteService.Get(id.Value);
                Console.WriteLine($"ID: {n.Id}\nЗаголовок: {n.Title}\nТело: {n.Body}\nСоздано: {n.CreatedAt:yyyy-MM-dd HH:mm}");
                break;
            }
            case "update":
            {
                var id = GetIntArg(args, "--id");
                if (id == null) { Console.Error.WriteLine("Использование: note update --id <id> [--title <t>] [--body <b>]"); return; }
                var note = _noteService.Update(id.Value, GetArg(args, "--title"), GetArg(args, "--body"));
                Console.WriteLine($"Заметка id={note.Id} обновлена.");
                break;
            }
            case "delete":
            {
                var id = GetIntArg(args, "--id");
                if (id == null) { Console.Error.WriteLine("Использование: note delete --id <id>"); return; }
                Console.Write($"Удалить заметку id={id}? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() != "y") { Console.WriteLine("Отменено."); return; }
                _noteService.Delete(id.Value);
                Console.WriteLine("Заметка удалена.");
                break;
            }
            default:
                Console.WriteLine($"Неизвестная подкоманда: {args[1]}");
                break;
        }
    }

    // ─── WATCHDOG ────────────────────────────────────────────────────────────
    private void HandleWatchdog(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Использование: watchdog <start|stop|status|config|logs>"); return; }

        switch (args[1].ToLower())
        {
            case "start":   _watchdogService.Start(); break;
            case "stop":    _watchdogService.Stop();  break;
            case "status":  _watchdogService.Status(); break;
            case "config":
            {
                var metric = GetArg(args, "--metric");
                var enabledStr = GetArg(args, "--enabled");
                if (metric == null || enabledStr == null)
                {
                    Console.Error.WriteLine("Использование: watchdog config --metric <cpu|ram|hdd> --enabled <true|false>");
                    return;
                }
                if (!bool.TryParse(enabledStr, out var enabled))
                {
                    Console.Error.WriteLine("Параметр --enabled должен быть true или false");
                    return;
                }
                _watchdogService.Configure(metric, enabled);
                break;
            }
            case "logs":
            {
                var limit = GetIntArg(args, "--limit") ?? 20;
                _watchdogService.ShowLogs(limit);
                break;
            }
            default:
                Console.WriteLine($"Неизвестная подкоманда watchdog: {args[1]}");
                break;
        }
    }

    // ─── USER (admin) ─────────────────────────────────────────────────────────
    private void HandleUser(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Использование: user <create|delete|role>"); return; }

        switch (args[1].ToLower())
        {
            case "create":
            {
                var login = GetArg(args, "--login");
                var pass = GetArg(args, "--password");
                var role = GetArg(args, "--role") ?? "user";
                if (login == null || pass == null)
                {
                    Console.Error.WriteLine("Использование: user create --login <l> --password <p> [--role user|admin|manager]");
                    return;
                }
                var u = _authService.CreateUser(login, pass, role);
                Console.WriteLine($"Пользователь создан. ID: {u.Id}, Логин: {u.Login}, Роль: {u.Role}");
                break;
            }
            case "delete":
            {
                var id = GetIntArg(args, "--id");
                if (id == null) { Console.Error.WriteLine("Использование: user delete --id <id>"); return; }
                _authService.DeleteUser(id.Value);
                Console.WriteLine($"Пользователь id={id} деактивирован.");
                break;
            }
            case "role":
            {
                var id = GetIntArg(args, "--id");
                var role = GetArg(args, "--role");
                if (id == null || role == null)
                {
                    Console.Error.WriteLine("Использование: user role --id <id> --role <user|admin|manager>");
                    return;
                }
                _authService.ChangeRole(id.Value, role);
                Console.WriteLine($"Роль пользователя id={id} изменена на {role}.");
                break;
            }
            default:
                Console.WriteLine($"Неизвестная подкоманда user: {args[1]}");
                break;
        }
    }

    // ─── LOGS ─────────────────────────────────────────────────────────────────
    private void HandleLogs(string[] args)
    {
        AuthService.RequireRole(Models.Roles.Admin, "logs");
        var limit = GetIntArg(args, "--limit") ?? 50;
        var lines = Exceptions.Logger.ReadLogs(limit);
        if (!lines.Any()) { Console.WriteLine("Лог пуст."); return; }
        foreach (var l in lines) Console.WriteLine(l);
    }

    // ─── HELP ─────────────────────────────────────────────────────────────────
    private static void PrintHelp()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║                   NoteApp — Справка команд                   ║
╚══════════════════════════════════════════════════════════════╝

АВТОРИЗАЦИЯ
  login --user <login> --password <pass>   Войти в систему
  logout                                   Выйти из системы

ЗАМЕТКИ
  note create --title <t> --body <b>       Создать заметку
  note list                                Список своих заметок
  note get --id <id>                       Просмотр заметки
  note update --id <id> [--title] [--body] Обновить заметку
  note delete --id <id>                    Удалить заметку

WATCHDOG (admin / manager)
  watchdog start                           Запустить мониторинг
  watchdog stop                            Остановить мониторинг
  watchdog status                          Статус сервиса
  watchdog config --metric <cpu|ram|hdd> --enabled <true|false>
  watchdog logs [--limit <n>]              Просмотр метрик

АДМИНИСТРИРОВАНИЕ (admin)
  user create --login <l> --password <p> [--role user|admin|manager]
  user delete --id <id>
  user role --id <id> --role <role>
  logs [--limit <n>]                       Просмотр логов безопасности

ОБЩЕЕ
  help                                     Эта справка
");
    }

    // ─── ARG PARSING HELPERS ──────────────────────────────────────────────────
    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int? GetIntArg(string[] args, string key)
    {
        var val = GetArg(args, key);
        if (val != null && int.TryParse(val, out var result)) return result;
        return null;
    }
}
