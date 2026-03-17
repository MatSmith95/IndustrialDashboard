# 06 — Data and SQLite

This project stores logged PLC data in a SQLite database file and reads WinCC log files.

---

## What is SQLite?

SQLite is a lightweight database stored in a single file (`industrial_dashboard.db`).
No server to install, no configuration — it's just a file you can open with any SQLite browser.

---

## Entity Framework Core (EF Core)

EF Core lets you work with the database using C# classes instead of raw SQL.

### Defining the Database Schema

```csharp
// App.Models/LogEntry.cs — one row in the database
public class LogEntry
{
    [Key]                           // This is the primary key (auto-incremented)
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string TagName { get; set; } = string.Empty;
    public double Value { get; set; }
}
```

### The DbContext

The `DbContext` is your database connection and schema definition.

```csharp
// App.Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    // This property = one table in the database
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);                  // primary key
            entity.HasIndex(e => e.Timestamp);         // index for fast date queries
            entity.HasIndex(e => e.TagName);           // index for fast tag filtering
        });
    }
}
```

### Creating the Database

In `App.xaml.cs`:
```csharp
db.Database.EnsureCreated();  // If industrial_dashboard.db doesn't exist, create it with the right tables
```

This runs every time the app starts. If the database already exists, it does nothing.

---

## Writing Data — DataLoggingService

```csharp
// App.Data/DataLoggingService.cs
public async Task LogDataPointAsync(PlcDataPoint point)
{
    // CreateDbContextAsync() creates a fresh database connection
    await using var db = await _dbFactory.CreateDbContextAsync();

    // Add 3 rows — one per tag per scan
    db.LogEntries.AddRange(
        new LogEntry { Timestamp = point.Timestamp, TagName = "Temperature", Value = point.Temperature },
        new LogEntry { Timestamp = point.Timestamp, TagName = "Pressure",    Value = point.Pressure    },
        new LogEntry { Timestamp = point.Timestamp, TagName = "MotorSpeed",  Value = point.MotorSpeed  }
    );

    // Actually write to disk
    await db.SaveChangesAsync();
}
```

`IDbContextFactory` is used instead of a single shared DbContext because we write from a background thread.
Creating a fresh context per operation is the correct approach for multi-threaded writing.

---

## Reading Data — DataLoggingService

```csharp
public async Task<List<LogEntry>> GetRecentEntriesAsync(int count = 500, string? tagFilter = null, ...)
{
    await using var db = await _dbFactory.CreateDbContextAsync();

    IQueryable<LogEntry> query = db.LogEntries.AsNoTracking();  // Read-only, no change tracking

    // Conditionally add filters
    if (!string.IsNullOrWhiteSpace(tagFilter))
        query = query.Where(e => e.TagName == tagFilter);

    if (from.HasValue)
        query = query.Where(e => e.Timestamp >= from.Value);

    // EF Core translates this to SQL:
    // SELECT * FROM LogEntries WHERE TagName = 'Temperature' ORDER BY Timestamp DESC LIMIT 500
    return await query
        .OrderByDescending(e => e.Timestamp)
        .Take(count)
        .ToListAsync();
}
```

The LINQ query is lazy — EF Core doesn't run SQL until `.ToListAsync()` is called.

---

## Dependency Injection for DbContext

```csharp
// App.xaml.cs — registering the factory
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite("Data Source=industrial_dashboard.db"));
```

```csharp
// App.Data/DataLoggingService.cs — constructor injection
public class DataLoggingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DataLoggingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;  // DI provides this automatically
    }
}
```

We register the factory once. DI injects it wherever it's needed. We never call `new AppDbContext()` directly.

---

## Reading WinCC .db3 Files — WinCcReader

WinCC Unified logs data to SQLite `.db3` files with this schema:

| Table | Column | Type | Meaning |
|-------|--------|------|---------|
| `LoggingTag` | `pk_Key` | INTEGER | Unique tag ID |
| `LoggingTag` | `Name` | TEXT | Human-readable tag name |
| `LoggedProcessValue` | `pk_TimeStamp` | INTEGER | Time as Windows FILETIME |
| `LoggedProcessValue` | `pk_fk_id` | INTEGER | References `pk_Key` |
| `LoggedProcessValue` | `Value` | REAL | The measured value |

### Windows FILETIME

Siemens stores timestamps as Windows FILETIME — the number of 100-nanosecond intervals since 1st January 1601.

```csharp
// App.Data/WinCcReader.cs
private static DateTime FileTimeToDateTime(long ticks)
    => new DateTime(1601, 1, 1).AddTicks(ticks);
    // Starts from 1601-01-01 and adds the ticks
    // Result is in local time (no UTC conversion needed)
```

### Reading with Raw SQL (no EF Core)

WinCC files are external — we don't own their schema. So we use `Microsoft.Data.Sqlite` directly:

```csharp
public static Dictionary<long, string> LoadTagMap(string tagDbPath)
{
    var map = new Dictionary<long, string>();

    // Open the file read-only so we can't accidentally modify it
    using var con = new SqliteConnection($"Data Source={tagDbPath};Mode=ReadOnly");
    con.Open();

    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT pk_Key, Name FROM LoggingTag";
    using var r = cmd.ExecuteReader();

    while (r.Read())  // Loop over each row
    {
        if (!r.IsDBNull(0) && !r.IsDBNull(1))
            map[r.GetInt64(0)] = r.GetString(1);  // id → name
    }
    return map;
}
```

### Getting Tag Statistics Efficiently

Instead of loading all data just to find min/max, we ask SQLite to do the maths:

```csharp
cmd.CommandText = "SELECT pk_fk_id, MIN(Value), MAX(Value), COUNT(*) FROM LoggedProcessValue GROUP BY pk_fk_id";
```

This SQL runs in the database file itself — much faster than loading everything into C# and calculating manually.

---

## Where the Database File Lives

`industrial_dashboard.db` is created in the **working directory** when the app runs.

When running via `dotnet run`, that's the project folder.
When running the built `.exe`, it's the same folder as the executable.

You can open it with [DB Browser for SQLite](https://sqlitebrowser.org/) to inspect the data directly.
