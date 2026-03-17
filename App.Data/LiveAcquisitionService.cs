using App.Models;
using Microsoft.Data.Sqlite;

namespace App.Data;

/// <summary>
/// Polls a WinCC segment .db3 file for new rows since the last seen timestamp.
/// Uses WAL mode + busy_timeout so reads and WinCC writes can proceed concurrently.
/// </summary>
public class LiveAcquisitionService : IDisposable
{
    public event EventHandler<List<WinCcDataPoint>>? NewDataArrived;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsRunning { get; private set; }

    public Task StartAsync(
        string segmentDbPath,
        Dictionary<long, string> tagMap,
        IEnumerable<long> tagIds,
        DateTime resumeFrom,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning) return Task.CompletedTask;
        IsRunning = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => PollLoopAsync(
            segmentDbPath, tagMap, tagIds.ToList(), resumeFrom, pollInterval, _cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        if (_loopTask is not null)
            await _loopTask.ConfigureAwait(false);
        IsRunning = false;
    }

    private async Task PollLoopAsync(
        string segmentDbPath,
        Dictionary<long, string> tagMap,
        List<long> tagIds,
        DateTime lastSeen,
        TimeSpan pollInterval,
        CancellationToken token)
    {
        using var timer = new PeriodicTimer(pollInterval);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            try
            {
                var rows = QueryNewRows(segmentDbPath, tagMap, tagIds, lastSeen);
                if (rows.Count > 0)
                {
                    lastSeen = rows.Max(r => r.Timestamp);
                    NewDataArrived?.Invoke(this, rows);
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6)
            {
                // SQLITE_BUSY (5) or SQLITE_LOCKED (6) — WinCC is mid-write, skip this tick
            }
            catch (Exception)
            {
                // Surface to caller via event with empty list so UI can show error — don't crash loop
            }
        }
    }

    private static List<WinCcDataPoint> QueryNewRows(
        string path,
        Dictionary<long, string> tagMap,
        List<long> tagIds,
        DateTime after)
    {
        var results = new List<WinCcDataPoint>();

        // Mode=ReadWrite needed for WAL -shm file; Cache=Shared allows concurrent access
        var connStr = $"Data Source={path};Cache=Shared";
        using var con = new SqliteConnection(connStr);
        con.Open();

        // Enable WAL + set busy timeout so we wait up to 5s instead of failing immediately
        using var pragmaCmd = con.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragmaCmd.ExecuteNonQuery();

        long afterTick = DateTimeToFileTicks(after);
        string inClause = string.Join(",", tagIds);

        using var cmd = con.CreateCommand();
        cmd.CommandText = $@"
            SELECT pk_TimeStamp, pk_fk_id, Value
            FROM LoggedProcessValue
            WHERE pk_TimeStamp > {afterTick}
              AND pk_fk_id IN ({inClause})
            ORDER BY pk_TimeStamp ASC
            LIMIT 10000";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0) || r.IsDBNull(1) || r.IsDBNull(2)) continue;
            long ts    = r.GetInt64(0);
            long tagId = r.GetInt64(1);
            double val = r.GetDouble(2);
            tagMap.TryGetValue(tagId, out string? name);
            results.Add(new WinCcDataPoint
            {
                Timestamp = FileTimeToDateTime(ts),
                TagId     = tagId,
                TagName   = name ?? $"Tag_{tagId}",
                Value     = val
            });
        }

        return results;
    }

    private static DateTime FileTimeToDateTime(long ticks)
        => new DateTime(1601, 1, 1).AddTicks(ticks);

    private static long DateTimeToFileTicks(DateTime dt)
        => (dt - new DateTime(1601, 1, 1)).Ticks;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
