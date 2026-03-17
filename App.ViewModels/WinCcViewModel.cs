using System.Collections.ObjectModel;
using System.Windows.Media;
using App.Data;
using App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace App.ViewModels;

public partial class WinCcViewModel : ObservableObject
{
    // ── View mode ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isConfigureMode = true;
    [ObservableProperty] private string _configureGraphLabel = "▶ Plot Graph";

    // ── File paths ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _segmentDbPath = string.Empty;
    [ObservableProperty] private string _tagDbPath = string.Empty;

    // ── Status ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusText = "Select segment and tag .db3 files, then Load Tags.";
    [ObservableProperty] private bool _isLoading;

    // ── Configure view ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _tagSearch = string.Empty;
    [ObservableProperty] private string _dateRangeText = string.Empty;
    public ObservableCollection<TagRow> AllTags { get; } = new();
    public ObservableCollection<TagRow> FilteredTags { get; } = new();

    // ── Chart mode ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSplitMode;
    [ObservableProperty] private string _chartModeLabel = "Switch to Split View";

    // ── Signals panel ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isPanelVisible = true;
    [ObservableProperty] private string _panelToggleLabel = "▼ Hide Signals";

    // ── Range slider ───────────────────────────────────────────────────────────
    [ObservableProperty] private double _sliderMin;
    [ObservableProperty] private double _sliderMax;
    [ObservableProperty] private double _sliderLower;
    [ObservableProperty] private double _sliderUpper;
    [ObservableProperty] private string _sliderLowerLabel = string.Empty;
    [ObservableProperty] private string _sliderUpperLabel = string.Empty;

    // ── Cursor values ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _cursorText = string.Empty;

    // ── Chart data ─────────────────────────────────────────────────────────────
    private Dictionary<long, string> _tagMap = new();
    public ObservableCollection<TagConfig> Tags { get; } = new();

    public ISeries[] OverlaySeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[] OverlayXAxes { get; private set; } = BuildDefaultXAxes();
    public Axis[] OverlayYAxes { get; private set; } = Array.Empty<Axis>();
    public ObservableCollection<GroupChartVm> GroupCharts { get; } = new();

    // ── File picking ───────────────────────────────────────────────────────────
    public void SetSegmentDb(string path) => SegmentDbPath = path;
    public void SetTagDb(string path)     => TagDbPath = path;

    // ── Commands ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadTagsAsync()
    {
        if (string.IsNullOrWhiteSpace(TagDbPath) || string.IsNullOrWhiteSpace(SegmentDbPath))
        {
            StatusText = "Please select both .db3 files first.";
            return;
        }

        IsLoading = true;
        StatusText = "Loading tags…";
        AllTags.Clear();
        FilteredTags.Clear();
        Tags.Clear();
        TagSearch = string.Empty;

        try
        {
            _tagMap = await Task.Run(() => WinCcReader.LoadTagMap(TagDbPath));
            var availTags = await Task.Run(() => WinCcReader.GetAvailableTags(SegmentDbPath, _tagMap));
            var stats     = await Task.Run(() => WinCcReader.GetTagStats(SegmentDbPath));
            var (minDate, maxDate) = await Task.Run(() => WinCcReader.GetDateRange(SegmentDbPath));

            DateRangeText = $"File range: {minDate:dd MMM yyyy HH:mm:ss} → {maxDate:dd MMM yyyy HH:mm:ss}";

            SliderMin   = minDate.Ticks;
            SliderMax   = maxDate.Ticks;
            SliderLower = minDate.Ticks;
            SliderUpper = maxDate.Ticks;
            UpdateSliderLabels();

            int colIdx = 0;
            foreach (var (id, name) in availTags)
            {
                stats.TryGetValue(id, out var s);
                var row = new TagRow
                {
                    TagId    = id,
                    TagName  = name,
                    MinValue = s.Min,
                    MaxValue = s.Max,
                    Points   = s.Count,
                    ColorHex = TagConfig.ColorPalette[colIdx % TagConfig.ColorPalette.Length]
                };
                AllTags.Add(row);
                FilteredTags.Add(row);
                colIdx++;
            }

            StatusText = $"{AllTags.Count} tags loaded. Select tags then click ▶ Plot Graph.";
            IsConfigureMode = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    partial void OnTagSearchChanged(string value)
    {
        FilteredTags.Clear();
        var q = value.Trim();
        foreach (var t in AllTags)
        {
            if (string.IsNullOrEmpty(q) || t.TagName.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredTags.Add(t);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var t in FilteredTags) t.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var t in AllTags) t.IsSelected = false;
    }

    [RelayCommand]
    private async Task PlotAsync()
    {
        var selected = AllTags.Where(t => t.IsSelected).ToList();
        if (!selected.Any()) { StatusText = "Select at least one tag."; return; }

        IsLoading = true;
        StatusText = "Loading data…";
        IsConfigureMode = false;

        try
        {
            var tagIds = selected.Select(t => t.TagId).ToList();
            var from   = new DateTime((long)SliderLower);
            var to     = new DateTime((long)SliderUpper);

            var data = await Task.Run(() =>
                WinCcReader.LoadSegmentData(SegmentDbPath, _tagMap, tagIds, from, to, 100_000));

            // Build TagConfigs (or reuse existing)
            var existingById = Tags.ToDictionary(t => t.TagId);
            Tags.Clear();

            var grouped = data.GroupBy(d => d.TagId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var row in selected)
            {
                existingById.TryGetValue(row.TagId, out var existing);
                var cfg = existing ?? new TagConfig
                {
                    TagId    = row.TagId,
                    TagName  = row.TagName,
                    IsVisible = true,
                    ColorHex = row.ColorHex,
                    GroupId  = 1
                };

                cfg.Points = grouped.TryGetValue(row.TagId, out var pts)
                    ? pts.Select(p => new ObservablePoint(p.Timestamp.Ticks, p.Value)).ToList()
                    : new List<ObservablePoint>();

                // Always auto-scale from data
                cfg.AutoScale();
                Tags.Add(cfg);
            }

            SyncGroupScales(Tags.ToList());
            RebuildCharts(Tags.Where(t => t.IsVisible).ToList());

            long total = grouped.Values.Sum(v => v.LongCount());
            StatusText = $"Plotted {total:N0} points across {selected.Count} tags.";
            ConfigureGraphLabel = "⚙ Configure";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleConfigureGraph()
    {
        IsConfigureMode = !IsConfigureMode;
        ConfigureGraphLabel = IsConfigureMode ? "▶ Plot Graph" : "⚙ Configure";
    }

    [RelayCommand]
    private void ToggleChartMode()
    {
        IsSplitMode = !IsSplitMode;
        ChartModeLabel = IsSplitMode ? "Overlay View" : "Split View";
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsPanelVisible = !IsPanelVisible;
        PanelToggleLabel = IsPanelVisible ? "▼ Hide Signals" : "▲ Show Signals";
    }

    // ── Slider ─────────────────────────────────────────────────────────────────

    partial void OnSliderLowerChanged(double value) => UpdateSliderLabels();
    partial void OnSliderUpperChanged(double value) => UpdateSliderLabels();

    private void UpdateSliderLabels()
    {
        if (SliderMax > SliderMin)
        {
            SliderLowerLabel = new DateTime((long)SliderLower).ToString("dd MMM HH:mm:ss");
            SliderUpperLabel = new DateTime((long)SliderUpper).ToString("dd MMM HH:mm:ss");
        }
    }

    // ── Cursor value (called from code-behind on MouseMove) ────────────────────

    public void UpdateCursorValues(long xTick, IEnumerable<TagConfig> visibleTags)
    {
        // Find nearest actual X across all tags
        var timeStamp = new DateTime(xTick).ToString("HH:mm:ss.fff");

        // For each tag find the interpolated/nearest Y value at xTick
        var tagValues = new List<(string Name, double Y, double Distance)>();

        foreach (var tag in visibleTags.Where(t => t.IsVisible && t.Points.Count > 0))
        {
            // Binary search for closest point
            var pts = tag.Points;
            int lo = 0, hi = pts.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (pts[mid].X!.Value < xTick) lo = mid; else hi = mid;
            }
            // Pick closer of lo/hi
            var nearest = Math.Abs(pts[lo].X!.Value - xTick) < Math.Abs(pts[hi].X!.Value - xTick)
                ? pts[lo] : pts[hi];

            if (nearest.Y.HasValue)
                tagValues.Add((tag.TagName, nearest.Y.Value, Math.Abs(nearest.X!.Value - xTick)));
        }

        if (!tagValues.Any()) { CursorText = string.Empty; return; }

        // Sort by distance to find the two nearest to cursor Y
        // Since we don't have the screen Y, we just take the two with smallest X distance
        // and show all with closest time — then caller can pass cursorY ratio for true nearest
        var nearest2 = tagValues.OrderBy(t => t.Distance).Take(2).ToList();
        CursorText = $"{timeStamp}  |  " + string.Join("  |  ", nearest2.Select(t => $"{t.Name}: {t.Y:F4}"));
    }

    // ── Chart building ─────────────────────────────────────────────────────────

    private void RebuildCharts(List<TagConfig> visibleTags)
    {
        BuildOverlay(visibleTags);
        BuildSplit(visibleTags);
        OnPropertyChanged(nameof(OverlaySeries));
        OnPropertyChanged(nameof(OverlayXAxes));
        OnPropertyChanged(nameof(OverlayYAxes));
    }

    private void BuildOverlay(List<TagConfig> tags)
    {
        var series = new List<ISeries>();
        var yAxes  = new List<Axis>();
        int axisIdx = 0;

        foreach (var group in tags.GroupBy(t => t.GroupId).OrderBy(g => g.Key))
        {
            var first = group.First();
            yAxes.Add(new Axis
            {
                Name        = $"G{first.GroupId}",
                LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x3A, 0x3A, 0x50)) { StrokeThickness = 0.5f },
                MinLimit    = first.MinY,
                MaxLimit    = first.MaxY
            });
            foreach (var tag in group)
            {
                series.Add(new LineSeries<ObservablePoint>
                {
                    Name           = tag.TagName,
                    Values         = tag.Points,
                    Stroke         = new SolidColorPaint(HexToSkColor(tag.ColorHex), 1.5f),
                    Fill           = null,
                    GeometrySize   = 0,
                    LineSmoothness = 0,
                    ScalesYAt      = axisIdx,
                    XToolTipLabelFormatter = _ => string.Empty  // disable tooltip
                });
            }
            axisIdx++;
        }

        OverlaySeries = series.ToArray();
        OverlayXAxes  = BuildDefaultXAxes();
        OverlayYAxes  = yAxes.ToArray();
    }

    private void BuildSplit(List<TagConfig> tags)
    {
        GroupCharts.Clear();
        foreach (var group in tags.GroupBy(t => t.GroupId).OrderBy(g => g.Key))
        {
            var first = group.First();
            var series = group.Select(tag => (ISeries)new LineSeries<ObservablePoint>
            {
                Name           = tag.TagName,
                Values         = tag.Points,
                Stroke         = new SolidColorPaint(HexToSkColor(tag.ColorHex), 1.5f),
                Fill           = null,
                GeometrySize   = 0,
                LineSmoothness = 0,
                XToolTipLabelFormatter = _ => string.Empty
            }).ToArray();

            GroupCharts.Add(new GroupChartVm
            {
                GroupId = first.GroupId,
                Series  = series,
                XAxes   = BuildDefaultXAxes(),
                YAxes   = new[]
                {
                    new Axis
                    {
                        LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
                        SeparatorsPaint = new SolidColorPaint(new SKColor(0x3A, 0x3A, 0x50)) { StrokeThickness = 0.5f },
                        MinLimit    = first.MinY,
                        MaxLimit    = first.MaxY
                    }
                }
            });
        }
    }

    private static void SyncGroupScales(List<TagConfig> tags)
    {
        foreach (var group in tags.GroupBy(t => t.GroupId))
        {
            double min = group.Min(t => t.MinY);
            double max = group.Max(t => t.MaxY);
            foreach (var t in group) { t.MinY = min; t.MaxY = max; }
        }
    }

    private static Axis[] BuildDefaultXAxes() => new[]
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x3A, 0x3A, 0x50)) { StrokeThickness = 0.5f },
            Labeler = value => new DateTime((long)value).ToString("HH:mm:ss")
        }
    };

    private static SKColor HexToSkColor(string hex)
    {
        try { var c = (Color)ColorConverter.ConvertFromString(hex); return new SKColor(c.R, c.G, c.B); }
        catch { return SKColors.White; }
    }
}

public class GroupChartVm
{
    public int GroupId { get; init; }
    public ISeries[] Series { get; init; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; init; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; init; } = Array.Empty<Axis>();
    public string Label => $"G{GroupId}";
}
