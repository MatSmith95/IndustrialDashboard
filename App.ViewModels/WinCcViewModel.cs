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
    // ── File / load state ──────────────────────────────────────────────────────
    [ObservableProperty] private string _segmentDbPath = string.Empty;
    [ObservableProperty] private string _tagDbPath = string.Empty;
    [ObservableProperty] private string _statusText = "Select segment and tag .db3 files to begin.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _dateRangeText = "Date range: load tags to detect";

    // ── Chart mode ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSplitMode;     // false = overlay, true = split bands
    [ObservableProperty] private string _chartModeLabel = "Switch to Split View";

    // ── Signals panel collapse ──────────────────────────────────────────────────
    [ObservableProperty] private bool _isPanelVisible = true;
    [ObservableProperty] private string _panelToggleLabel = "▼ Hide Signals";
    [ObservableProperty] private double _panelHeight = 200;

    // ── Time slider ────────────────────────────────────────────────────────────
    [ObservableProperty] private double _sliderMin;
    [ObservableProperty] private double _sliderMax;
    [ObservableProperty] private double _sliderFrom;
    [ObservableProperty] private double _sliderTo;
    [ObservableProperty] private string _sliderFromLabel = "";
    [ObservableProperty] private string _sliderToLabel = "";

    private DateTime _dataMinDate = DateTime.MinValue;
    private DateTime _dataMaxDate = DateTime.MaxValue;

    // ── Tag data ────────────────────────────────────────────────────────────────
    private Dictionary<long, string> _tagMap = new();
    public ObservableCollection<TagConfig> Tags { get; } = new();

    // ── Chart output ───────────────────────────────────────────────────────────
    // Overlay mode — single chart
    public ISeries[] OverlaySeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[] OverlayXAxes { get; private set; } = BuildDefaultXAxes();
    public Axis[] OverlayYAxes { get; private set; } = Array.Empty<Axis>();

    // Split mode — one chart per group
    public ObservableCollection<GroupChartVm> GroupCharts { get; } = new();

    // ── File picking ───────────────────────────────────────────────────────────
    public void SetSegmentDb(string path) => SegmentDbPath = path;
    public void SetTagDb(string path) => TagDbPath = path;

    // ── Commands ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadTagsAsync()
    {
        if (string.IsNullOrWhiteSpace(TagDbPath) || string.IsNullOrWhiteSpace(SegmentDbPath))
        {
            StatusText = "Please select both segment and tag .db3 files.";
            return;
        }

        IsLoading = true;
        StatusText = "Loading tags…";
        Tags.Clear();

        try
        {
            _tagMap = await Task.Run(() => WinCcReader.LoadTagMap(TagDbPath));
            var tags = await Task.Run(() => WinCcReader.GetAvailableTags(SegmentDbPath, _tagMap));
            var (minDate, maxDate) = await Task.Run(() => WinCcReader.GetDateRange(SegmentDbPath));

            _dataMinDate = minDate;
            _dataMaxDate = maxDate;
            DateRangeText = $"Range: {minDate:dd MMM yyyy HH:mm:ss} → {maxDate:dd MMM yyyy HH:mm:ss}";

            // Init slider to full range
            double minTick = minDate.Ticks;
            double maxTick = maxDate.Ticks;
            SliderMin = minTick;
            SliderMax = maxTick;
            SliderFrom = minTick;
            SliderTo = maxTick;
            UpdateSliderLabels();

            int colourIdx = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                var (id, name) = tags[i];
                Tags.Add(new TagConfig
                {
                    TagId    = id,
                    TagName  = name,
                    IsVisible = true,
                    ColorHex = TagConfig.ColorPalette[colourIdx % TagConfig.ColorPalette.Length],
                    GroupId  = 1,
                    MinY     = 0,
                    MaxY     = 100
                });
                colourIdx++;
            }

            StatusText = $"{Tags.Count} tags found. Click 'Plot' to load data.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading tags: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PlotAsync()
    {
        var visibleTags = Tags.Where(t => t.IsVisible).ToList();
        if (!visibleTags.Any()) { StatusText = "No visible tags to plot."; return; }

        IsLoading = true;
        StatusText = "Loading data…";

        try
        {
            var tagIds = visibleTags.Select(t => t.TagId).ToList();
            var from = new DateTime((long)SliderFrom);
            var to   = new DateTime((long)SliderTo);

            var data = await Task.Run(() =>
                WinCcReader.LoadSegmentData(SegmentDbPath, _tagMap, tagIds, from, to, maxRows: 100_000));

            // Distribute points to each TagConfig
            var grouped = data.GroupBy(d => d.TagId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var tag in visibleTags)
            {
                tag.Points = grouped.TryGetValue(tag.TagId, out var pts)
                    ? pts.Select(p => new ObservablePoint(p.Timestamp.Ticks, p.Value)).ToList()
                    : new List<ObservablePoint>();
            }

            // Auto-scale any tag where Min==Max==0 (first load)
            foreach (var tag in visibleTags.Where(t => t.MinY == 0 && t.MaxY == 0))
                tag.AutoScale();

            // Sync group min/max — all tags in same group share the widest range
            SyncGroupScales(visibleTags);

            RebuildCharts(visibleTags);

            long total = grouped.Values.Sum(v => v.LongCount());
            StatusText = $"Loaded {total:N0} points across {visibleTags.Count} tags. Range: {from:HH:mm:ss} – {to:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading data: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleChartMode()
    {
        IsSplitMode = !IsSplitMode;
        ChartModeLabel = IsSplitMode ? "Switch to Overlay View" : "Switch to Split View";
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsPanelVisible = !IsPanelVisible;
        PanelToggleLabel = IsPanelVisible ? "▼ Hide Signals" : "▲ Show Signals";
    }

    partial void OnSliderFromChanged(double value)
    {
        if (value > SliderTo) SliderFrom = SliderTo;
        UpdateSliderLabels();
    }

    partial void OnSliderToChanged(double value)
    {
        if (value < SliderFrom) SliderTo = SliderFrom;
        UpdateSliderLabels();
    }

    private void UpdateSliderLabels()
    {
        if (SliderMax > SliderMin)
        {
            SliderFromLabel = new DateTime((long)SliderFrom).ToString("dd MMM HH:mm:ss");
            SliderToLabel   = new DateTime((long)SliderTo).ToString("dd MMM HH:mm:ss");
        }
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

    private void BuildOverlay(List<TagConfig> visibleTags)
    {
        var series = new List<ISeries>();
        var yAxes  = new List<Axis>();
        var groups = visibleTags.GroupBy(t => t.GroupId).OrderBy(g => g.Key);
        int axisIdx = 0;

        foreach (var group in groups)
        {
            var first = group.First();
            yAxes.Add(new Axis
            {
                Name       = $"G{first.GroupId}",
                LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x3A, 0x3A, 0x50)) { StrokeThickness = 0.5f },
                MinLimit   = first.MinY,
                MaxLimit   = first.MaxY
            });

            foreach (var tag in group)
            {
                series.Add(new LineSeries<ObservablePoint>
                {
                    Name          = tag.TagName,
                    Values        = tag.Points,
                    Stroke        = new SolidColorPaint(HexToSkColor(tag.ColorHex), 1.5f),
                    Fill          = null,
                    GeometrySize  = 0,
                    LineSmoothness = 0,
                    ScalesYAt     = axisIdx
                });
            }
            axisIdx++;
        }

        OverlaySeries = series.ToArray();
        OverlayXAxes  = BuildDefaultXAxes();
        OverlayYAxes  = yAxes.ToArray();
    }

    private void BuildSplit(List<TagConfig> visibleTags)
    {
        GroupCharts.Clear();
        var groups = visibleTags.GroupBy(t => t.GroupId).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var first = group.First();
            var series = group.Select(tag => (ISeries)new LineSeries<ObservablePoint>
            {
                Name          = tag.TagName,
                Values        = tag.Points,
                Stroke        = new SolidColorPaint(HexToSkColor(tag.ColorHex), 1.5f),
                Fill          = null,
                GeometrySize  = 0,
                LineSmoothness = 0
            }).ToArray();

            var yAxis = new Axis
            {
                Name        = $"G{first.GroupId}",
                LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x3A, 0x3A, 0x50)) { StrokeThickness = 0.5f },
                MinLimit    = first.MinY,
                MaxLimit    = first.MaxY
            };

            GroupCharts.Add(new GroupChartVm
            {
                GroupId = first.GroupId,
                Series  = series,
                XAxes   = BuildDefaultXAxes(),
                YAxes   = new[] { yAxis }
            });
        }
    }

    private static void SyncGroupScales(List<TagConfig> tags)
    {
        foreach (var group in tags.GroupBy(t => t.GroupId))
        {
            // Auto-scale any still at 0/0
            foreach (var t in group.Where(t => t.MinY == 0 && t.MaxY == 0))
                t.AutoScale();

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
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SKColor(c.R, c.G, c.B);
        }
        catch { return SKColors.White; }
    }
}

/// <summary>One chart band in split mode.</summary>
public class GroupChartVm
{
    public int GroupId { get; init; }
    public ISeries[] Series { get; init; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; init; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; init; } = Array.Empty<Axis>();
    public string Label => $"G{GroupId}";
}
