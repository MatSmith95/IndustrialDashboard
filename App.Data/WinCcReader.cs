using App.Models;
using Microsoft.Data.Sqlite;

namespace App.Data;

/// <summary>
/// Reads WinCC Unified Logging .db3 files.
/// Schema: segment file has LoggedProcessValue (pk_TimeStamp, fk_LoggingTag, Val, Quality)
///         tag file has LoggingTag (id, Name)
/// pk_TimeStamp is Windows FILETIME: 100ns ticks since 1601-01-01 UTC.
/// </summary>
public static class WinCcReader
{
    // Windows FILETIME epoch offset to Unix epoch
    private static readonly DateTime FileTimeEpoch = new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime FileTimeToDateTime(long fileTime)
        => FileTimeEpoch.AddTicks(fileTime).ToLocalTime();

    /// <summary>
    /// Load tag map from the LoggingTag table in the tag .db3 file.
    /// Returns dictionary of id -> name.
    /// </summary>
    public static Dictionary<long, string> LoadTagMap(string tagDbPath)
    {
        var map = new Dictionary<long, string>();
        using var con = new SqliteConnection($"Data Source={tagDbPath};Mode=ReadOnly");
        con.Open();

        // Try common column name patterns for the tag table
        // Siemens uses: id (or pk_LoggingTag), Name (or TagName)
        string? idCol   = FindColumn(con, "LoggingTag", new[] { "id", "pk_LoggingTag", "Id" });
        string? nameCol = FindColumn(con, "LoggingTag", new[] { "Name", "TagName", "name" });

        if (idCol is null || nameCol is null)
            throw new InvalidOperationException("Could not find id/name columns in LoggingTag table.");

        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT [{idCol}], [{nameCol}] FROM LoggingTag";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                map[reader.GetInt64(0)] = reader.GetString(1);
        }
        return map;
    }

    /// <summary>
    /// Load process values from the segment .db3 file, joined with tag names from the map.
    /// Returns list of WinCcDataPoint ordered by timestamp.
    /// </summary>
    public static List<WinCcDataPoint> LoadSegmentData(
        string segmentDbPath,
        Dictionary<long, string> tagMap,
        IEnumerable<long>? filterTagIds = null,
        DateTime? from = null,
        DateTime? to = null,
        int maxRows = 50_000)
    {
        var results = new List<WinCcDataPoint>();

        using var con = new SqliteConnection($"Data Source={segmentDbPath};Mode=ReadOnly");
        con.Open();

        // Discover FK column name
        string? tsCol  = FindColumn(con, "LoggedProcessValue", new[] { "pk_TimeStamp", "TimeStamp", "Timestamp" });
        string? fkCol  = FindColumn(con, "LoggedProcessValue", new[] { "fk_LoggingTag", "FK_LoggingTag", "TagId", "fk_TagId" });
        string? valCol = FindColumn(con, "LoggedProcessValue", new[] { "Val", "Value", "val" });

        if (tsCol is null || fkCol is null || valCol is null)
            throw new InvalidOperationException("Could not find required columns in LoggedProcessValue table.");

        var conditions = new List<string>();
        if (filterTagIds?.Any() == true)
            conditions.Add($"[{fkCol}] IN ({string.Join(",", filterTagIds)})");

        // Convert DateTime to FILETIME for query
        if (from.HasValue)
        {
            long ft = DateTimeToFileTime(from.Value);
            conditions.Add($"[{tsCol}] >= {ft}");
        }
        if (to.HasValue)
        {
            long ft = DateTimeToFileTime(to.Value);
            conditions.Add($"[{tsCol}] <= {ft}");
        }

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT [{tsCol}], [{fkCol}], [{valCol}] FROM LoggedProcessValue {where} ORDER BY [{tsCol}] ASC LIMIT {maxRows}";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2)) continue;

            long ts    = reader.GetInt64(0);
            long tagId = reader.GetInt64(1);
            double val = reader.GetDouble(2);

            tagMap.TryGetValue(tagId, out string? tagName);

            results.Add(new WinCcDataPoint
            {
                Timestamp = FileTimeToDateTime(ts),
                TagId     = tagId,
                TagName   = tagName ?? $"Tag_{tagId}",
                Value     = val
            });
        }

        return results;
    }

    /// <summary>
    /// Get all tag IDs and names available in the segment file (joined with tag map).
    /// </summary>
    public static List<(long Id, string Name)> GetAvailableTags(string segmentDbPath, Dictionary<long, string> tagMap)
    {
        var result = new List<(long, string)>();
        using var con = new SqliteConnection($"Data Source={segmentDbPath};Mode=ReadOnly");
        con.Open();

        string? fkCol = FindColumn(con, "LoggedProcessValue", new[] { "fk_LoggingTag", "FK_LoggingTag", "TagId", "fk_TagId" });
        if (fkCol is null) return result;

        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT [{fkCol}] FROM LoggedProcessValue ORDER BY [{fkCol}]";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long id = reader.GetInt64(0);
            tagMap.TryGetValue(id, out string? name);
            result.Add((id, name ?? $"Tag_{id}"));
        }
        return result;
    }

    private static string? FindColumn(SqliteConnection con, string table, string[] candidates)
    {
        try
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info([{table}])";
            using var r = cmd.ExecuteReader();
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (r.Read()) cols.Add(r.GetString(1));

            foreach (var candidate in candidates)
                if (cols.Contains(candidate))
                    return candidate;
        }
        catch { /* table may not exist */ }
        return null;
    }

    private static long DateTimeToFileTime(DateTime dt)
        => (dt.ToUniversalTime() - FileTimeEpoch).Ticks;
}
