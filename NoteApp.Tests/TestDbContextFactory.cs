using Microsoft.EntityFrameworkCore;
using NoteApp.Data;

namespace NoteApp.Tests;

public static class TestDbContextFactory
{
    public static AppDbContext CreateInMemory(string dbName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
