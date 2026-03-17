# 08 — WinCC Log Viewer Deep Dive

The WinCC Log Viewer is the most complex feature. This guide walks through every part.

---

## What it Does

Siemens WinCC Unified stores PLC tag data in SQLite `.db3` files:
- A **tag map file** (`*_TagLoggingDatabase.db3`) — maps numeric IDs to tag names
- **Segment files** (one per time period) — the actual logged values

The viewer:
1. Lets you browse and select both files
2. Loads all available tags with their name, min, max, and point count
3. Lets you select which tags to plot and filter by time range
4. Plots the selected tags on a chart with configurable signal groups
5. Shows cursor values for the two nearest traces on hover

---

## The Database Schema (Siemens Format)

```sql
-- Tag map file
CREATE TABLE LoggingTag (
    pk_Key  INTEGER PRIMARY KEY,  -- Unique numeric ID for the tag
    Name    TEXT                  -- Human-readable name, e.g. "PLC_DB".Motor1.Speed
);

-- Segment file
CREATE TABLE LoggedProcessValue (
    pk_TimeStamp  INTEGER,  -- Windows FILETIME (see below)
    pk_fk_id      INTEGER,  -- References LoggingTag.pk_Key
    Value         REAL      -- The measured value
);
```

---

## Windows FILETIME

Siemens uses Windows FILETIME for timestamps.

**FILETIME** = number of 100-nanosecond (0.0000001 second) intervals since **1st January 1601 00:00:00**.

This is NOT Unix time (which starts from 1970) and NOT .NET ticks (same unit but different epoch).

```csharp
// App.Data/WinCcReader.cs
private static DateTime FileTimeToDateTime(long ticks)
    => new DateTime(1601, 1, 1).AddTicks(ticks);
    // "Start from 1601-01-01 and add ticks"
```

Example:
- `133_800_000_000_000_000` ticks ≈ 17th March 2025
- One second = 10,000,000 ticks (100ns × 10,000,000 = 1 second)

For the X-axis in LiveCharts, we store `.Ticks` (which is the same unit, also 100ns intervals since the same epoch in .NET). So the conversion is direct: `point.Timestamp.Ticks` = the value stored in `pk_TimeStamp`.

---

## The Two-File Design

| File | Purpose | When to open |
|------|---------|--------------|
| Tag map | Maps `pk_fk_id` numbers to tag names | Open once, cache in `_tagMap` dictionary |
| Segment | Contains the actual values | Open for each query |

```csharp
// Step 1: Load tag map from tag file
Dictionary<long, string> _tagMap = WinCcReader.LoadTagMap(tagDbPath);
// _tagMap[101] = "PLC_DB.Motor1.Speed"
// _tagMap[102] = "PLC_DB.Motor1.Current"

// Step 2: Load data from segment file, pass the map to resolve names
List<WinCcDataPoint> data = WinCcReader.LoadSegmentData(segmentDbPath, _tagMap, ...);
```

---

## Getting Tag Statistics Efficiently

When tags are loaded, we show Min/Max/Count in the table WITHOUT loading all data.
SQLite can calculate these itself:

```sql
SELECT pk_fk_id, MIN(Value), MAX(Value), COUNT(*) 
FROM LoggedProcessValue 
GROUP BY pk_fk_id
```

This runs entirely in the database file — much faster than loading 50,000 rows into C# memory.

```csharp
// App.Data/WinCcReader.cs
public static Dictionary<long, (double Min, double Max, long Count)> GetTagStats(string segmentDbPath)
{
    var result = new Dictionary<long, (double, double, long)>();
    // ... runs the SQL above
    // result[101] = (0.0, 100.0, 3600)  ← min, max, count
    return result;
}
```

---

## The Configure / Graph Flow

Two distinct views are shown by toggling `IsConfigureMode`:

```
IsConfigureMode = true  →  Configure view (tag table)
IsConfigureMode = false →  Graph view (charts)
```

```csharp
// WinCcViewModel.cs
[RelayCommand]
private async Task PlotAsync()
{
    var selected = AllTags.Where(t => t.IsSelected).ToList();
    IsLoading = true;
    IsConfigureMode = false;  // Switch to graph view

    var data = await Task.Run(() =>
        WinCcReader.LoadSegmentData(SegmentDbPath, _tagMap, tagIds, from, to, 100_000));

    // Build TagConfig for each selected tag
    foreach (var row in selected)
    {
        var cfg = new TagConfig { ... };
        cfg.Points = data
            .Where(d => d.TagId == row.TagId)
            .Select(d => new ObservablePoint(d.Timestamp.Ticks, d.Value))
            .ToList();
        cfg.AutoScale();  // Compute MinY/MaxY from actual data
        Tags.Add(cfg);
    }

    RebuildCharts(Tags.ToList());
}
```

---

## Signal Groups and Y-Axis Scaling

Each tag has a `GroupId` (1–8). Tags in the same group share the same Y-axis scale.

```csharp
// After auto-scaling individual tags, sync groups
private static void SyncGroupScales(List<TagConfig> tags)
{
    foreach (var group in tags.GroupBy(t => t.GroupId))
    {
        // Find the widest range among all tags in this group
        double min = group.Min(t => t.MinY);
        double max = group.Max(t => t.MaxY);
        // Apply to all tags — they now share the same scale
        foreach (var t in group) { t.MinY = min; t.MaxY = max; }
    }
}
```

**Example:** Group 1 has:
- Temperature: -10 to 80°C
- Pressure: 0.9 to 1.1 bar

After sync, Group 1 Y axis = -10 to 80 (the wider range wins).
The pressure trace will be compressed near the bottom — but that's fine, Group 1 signals are meant to share a scale.

---

## The RangeSlider

`RangeSlider.xaml` is a custom control — two thumbs on one track.

```csharp
// App.WPF/RangeSlider.xaml.cs
public static readonly DependencyProperty LowerValueProperty =
    DependencyProperty.Register(nameof(LowerValue), typeof(double), typeof(RangeSlider), ...);
```

**Dependency Properties** are WPF's special property system that supports binding.
You can't bind to a normal C# property from XAML — it must be a DependencyProperty.

```xml
<!-- Used in WinCcView.xaml -->
<local:RangeSlider
    Minimum="{Binding SliderMin}"
    Maximum="{Binding SliderMax}"
    LowerValue="{Binding SliderLower, Mode=TwoWay}"
    UpperValue="{Binding SliderUpper, Mode=TwoWay}" />
```

When the user drags a thumb, `LowerValue` changes, which updates `SliderLower` in the ViewModel via the two-way binding.

---

## Cursor Value Tracking

When the mouse moves over the chart, we find the two nearest tag values at that X position:

```csharp
public void UpdateCursorValues(long xTick, IEnumerable<TagConfig> visibleTags)
{
    foreach (var tag in visibleTags.Where(t => t.Points.Count > 0))
    {
        // Binary search — efficient way to find the nearest point in a sorted list
        int lo = 0, hi = pts.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (pts[mid].X!.Value < xTick) lo = mid;
            else hi = mid;
        }
        // lo and hi are now the two points straddling xTick
        // Pick whichever is closest
    }

    // Sort by time distance to xTick, take 2 nearest
    var nearest2 = tagValues.OrderBy(t => t.Distance).Take(2).ToList();
    CursorText = $"{timeStamp}  |  " + string.Join("  |  ", nearest2.Select(...));
}
```

Binary search is O(log n) — for 50,000 points it takes about 16 comparisons instead of 50,000.
