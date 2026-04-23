using Microsoft.EntityFrameworkCore;
using NoteApp.Models;

namespace NoteApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<WatchdogLog> WatchdogLogs => Set<WatchdogLog>();
    public DbSet<WatchdogConfig> WatchdogConfigs => Set<WatchdogConfig>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Login).IsUnique();
            e.Property(u => u.Login).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
        });

        // Note
        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(255).IsRequired();
            e.Property(n => n.Body).IsRequired();  // TEXT — no length limit
            e.HasOne(n => n.User)
             .WithMany(u => u.Notes)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // WatchdogConfig seed
        modelBuilder.Entity<WatchdogConfig>().HasData(
            new WatchdogConfig { Id = 1, Metric = "cpu", Enabled = true },
            new WatchdogConfig { Id = 2, Metric = "ram", Enabled = true },
            new WatchdogConfig { Id = 3, Metric = "hdd", Enabled = true }
        );

        // ActionLog
        modelBuilder.Entity<ActionLog>(e =>
        {
            e.HasOne(a => a.User)
             .WithMany(u => u.ActionLogs)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
