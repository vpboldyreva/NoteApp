using NoteApp.Commands;
using NoteApp.Data;
using NoteApp.Services;

// ── Startup ──────────────────────────────────────────────────────────────────
var db = DbContextFactory.Create();

// Seed default admin if first run
var authService = new AuthService(db);
authService.EnsureAdminExists();

// Check for config updates on startup (ТЗ п. 6)
var updateService = new UpdateService();
updateService.CheckAndUpdate();

// Start command routing
using var watchdog = new WatchdogService(db);
var router = new CommandRouter(db, watchdog);

router.Execute(args);
