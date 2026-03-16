using App.Models;
using Microsoft.EntityFrameworkCore;

namespace App.Data;

public class DataLoggingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DataLoggingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task LogDataPointAsync(PlcDataPoint point)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        db.LogEntries.AddRange(
            new LogEntry { Timestamp = point.Timestamp, TagName = "Temperature", Value = point.Temperature },
            new LogEntry { Timestamp = point.Timestamp, TagName = "Pressure",    Value = point.Pressure    },
            new LogEntry { Timestamp = point.Timestamp, TagName = "MotorSpeed",  Value = point.MotorSpeed  }
        );

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<LogEntry>> GetRecentEntriesAsync(int count = 500, string? tagFilter = null, DateTime? from = null, DateTime? to = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        IQueryable<LogEntry> query = db.LogEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tagFilter))
            query = query.Where(e => e.TagName == tagFilter);

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        return await query
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync()
            .ConfigureAwait(false);
    }
}
