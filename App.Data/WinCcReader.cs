using App.Models;
using Microsoft.Data.Sqlite;

namespace App.Data;

/// <summary>
/// Reads WinCC Unified Logging .db3 files.
/// Tag file:     LoggingTag         — pk_Key (long), Name (string)
/// Segment file: LoggedProcessValue — pk_TimeStamp (long FILETIME), pk_fk_id (long), Value (double)
/// FILETIME: 100ns ticks since 1601-01-01 (no UTC offset — store as-is, display as local)
/// </summary>
public static class WinCcReader
{
    private static DateTime FileTimeToDateTime(long ticks)
        => new DateTime(1601, 1, 1).AddTicks(ticks);

    /// <summary>Load tag map from LoggingTag table. Returns pk_Key -> Name.</summary>
    public static Dictionary<long, string> LoadTagMap(string tagDbPath)
    {
        var map = new Dictionary<long, string>();
        using var con = new SqliteConnection($"Data Source={tagDbPath};Mode=ReadOnly");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT pk_Key, Name FROM LoggingTag";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!r.IsDBNull(0) && !r.IsDBNull(1))
                map[r.GetInt64(0)] = r.GetString(1);
        }
        return map;
    }

    /// <summary>Get all distinct tag IDs present in the segment file, with names from the map.</summary>
    public static List<(long Id, string Name)> GetAvailableTags(string segmentDbPath, Dictionary<long, string> tagMap)
    {
        var result = new List<(long, string)>();
        using var con = new SqliteConnection($"Data Source={segmentDbPath};Mode=ReadOnly");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT pk_fk_id FROM LoggedProcessValue ORDER BY pk_fk_id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            long id = r.GetInt64(0);
            tagMap.TryGetValue(id, out string? name);
            result.Add((id, name ?? $"Tag_{id}"));
        }
        return result;
    }

    /// <summary>Get per-tag stats: pk_fk_id -> (min, max, count).</summary>
    public static Dictionary<long, (double Min, double Max, long Count)> GetTagStats(string segmentDbPath)
    {
        var result = new Dictionary<long, (double, double, long)>();
        using var con = new SqliteConnection($"Data Source={segmentDbPath};Mode=ReadOnly");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT pk_fk_id, MIN(Value), MAX(Value), COUNT(*) FROM LoggedProcessValue GROUP BY pk_fk_id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!r.IsDBNull(0) && !r.IsDBNull(1) && !r.IsDBNull(2) && !r.IsDBNull(3))
                result[r.GetInt64(0)] = (r.GetDouble(1), r.GetDouble(2), r.GetInt64(3));
        }
        return result;
    }

    /// <summary>Get the min/max timestamp range from the segment file.</summary>
    public static (DateTime Min, DateTime Max) GetDateRange(string segmentDbPath)
    {
        using var con = new SqliteConnection($"Data Source={segmentDbPath};Mode=ReadOnly");
        con.Open();

        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT MIN(pk_TimeStamp), MAX(pk_TimeStamp) FROM LoggedProcessValue";
        using var r = cmd.ExecuteReader();
        if (r.Read() && !r.IsDBNull(0) && !r.IsDBNull(1))
            return (FileTimeToDateTime(r.GetInt64(0)), FileTimeToDateTime(r.GetInt64(1)));

        return (DateTime.MinValue, DateTime.MaxValue);
    }

    /// <summary>Load process values from the segment file, filtered by tag IDs.</summary>
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

        var conditions = new List<string>();
        var tagIdList = filterTagIds?.ToList();

        if (tagIdList?.Count > 0)
            conditions.Add($"pk_fk_id IN ({string.Join(",", tagIdList)})");

        if (from.HasValue && from.Value != DateTime.MinValue)
            conditions.Add($"pk_TimeStamp >= {DateTimeToFileTicks(from.Value)}");

        if (to.HasValue && to.Value != DateTime.MaxValue)
            conditions.Add($"pk_TimeStamp <= {DateTimeToFileTicks(to.Value)}");

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT pk_TimeStamp, pk_fk_id, Value FROM LoggedProcessValue {where} ORDER BY pk_TimeStamp ASC LIMIT {maxRows}";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0) || r.IsDBNull(1) || r.IsDBNull(2)) continue;

            long ts    = r.GetInt64(0);
            long tagId = r.GetInt64(1);
            double val = r.GetDouble(2);

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

    private static long DateTimeToFileTicks(DateTime dt)
        => (dt - new DateTime(1601, 1, 1)).Ticks;
}
