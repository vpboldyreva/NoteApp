using Microsoft.EntityFrameworkCore;

namespace NoteApp.Data;

public static class DbContextFactory
{
    private static string _dbPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "noteapp.db");

    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    // CreateInMemory is available in NoteApp.Tests project only
}
