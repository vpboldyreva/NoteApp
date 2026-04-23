# NoteApp — Консольная система заметок

Курсовая работа | Тестирование и отладка ПО | ДВГУПС

## Технологии

- .NET 8, C#
- SQLite + Entity Framework Core
- BCrypt.Net для хэширования паролей
- xUnit для тестирования

## Сборка и запуск

```bash
cd NoteApp
dotnet build
dotnet run -- help
```

## Запуск тестов

```bash
cd NoteApp.Tests
dotnet test
```

## Команды

### Авторизация
```
login --user <login> --password <pass>
logout
```

### Заметки
```
note create --title "Заголовок" --body "Текст заметки"
note list
note get --id 1
note update --id 1 --title "Новый заголовок"
note update --id 1 --body "Новый текст"
note delete --id 1
```

### WatchDog (admin / manager)
```
watchdog start
watchdog stop
watchdog status
watchdog config --metric cpu --enabled false
watchdog config --metric ram --enabled true
watchdog logs
watchdog logs --limit 10
```

### Администрирование (admin)
```
user create --login newuser --password pass123 --role user
user create --login mgr --password pass123 --role manager
user delete --id 2
user role --id 2 --role admin
logs
logs --limit 20
```

## Роли

| Роль | Права |
|------|-------|
| user | Создание, просмотр, редактирование, удаление своих заметок |
| manager | Всё, что user + управление WatchDog конфигурацией |
| admin | Полный доступ + управление пользователями + просмотр логов |

## Структура проекта

```
NoteApp.sln
├── NoteApp/
│   ├── Commands/       CommandRouter — разбор аргументов CLI
│   ├── Data/           AppDbContext, DbContextFactory
│   ├── Exceptions/     AppException и наследники, Logger
│   ├── Models/         User, Note, WatchdogLog, ActionLog
│   ├── Services/       AuthService, NoteService, WatchdogService, UpdateService
│   ├── Session/        SessionManager — буфер сессии
│   └── Program.cs
└── NoteApp.Tests/
    ├── AuthServiceTests.cs
    ├── DatabaseTests.cs
    ├── NoteServiceTests.cs
    └── UpdateServiceTests.cs
```

## Ветки Git

| Ветка | Назначение |
|-------|-----------|
| master | Стабильная версия |
| feature/notes | Команды заметок |
| feature/admin | Команды администрирования |
| feature/watchdog | WatchDog мониторинг |
| feature/auto-update | Система обновлений |
