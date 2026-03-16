using App.Models;
using Microsoft.EntityFrameworkCore;

namespace App.Data;

public class AppDbContext : DbContext
{
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TagName).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Value).IsRequired();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.TagName);
        });
    }
}
