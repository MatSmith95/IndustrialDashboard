using System.Collections.ObjectModel;
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
    // Palette for multi-tag series
    private static readonly SKColor[] Palette =
    {
        SKColors.OrangeRed, SKColors.DodgerBlue, SKColors.LimeGreen,
        SKColors.Gold, SKColors.MediumPurple, SKColors.Cyan,
        SKColors.HotPink, SKColors.LightSalmon
    };

    [ObservableProperty] private string _segmentDbPath = string.Empty;
    [ObservableProperty] private string _tagDbPath = string.Empty;
    [ObservableProperty] private string _statusText = "Select segment and tag .db3 files to begin.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today.AddDays(1);

    public ObservableCollection<TagSelectionItem> AvailableTags { get; } = new();
    public ISeries[] Series { get; private set; } = Array.Empty<ISeries>();

    private Dictionary<long, string> _tagMap = new();

    // ── File picking (called from code-behind, not command, to use OpenFileDialog) ──
    public void SetSegmentDb(string path) => SegmentDbPath = path;
    public void SetTagDb(string path)     => TagDbPath = path;

    [RelayCommand]
    private async Task LoadTagsAsync()
    {
        if (string.IsNullOrWhiteSpace(TagDbPath))
        {
            StatusText = "Please select a tag .db3 file first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SegmentDbPath))
        {
            StatusText = "Please select a segment .db3 file first.";
            return;
        }

        IsLoading = true;
        StatusText = "Loading tags…";
        AvailableTags.Clear();

        try
        {
            _tagMap = await Task.Run(() => WinCcReader.LoadTagMap(TagDbPath));
            var tags = await Task.Run(() => WinCcReader.GetAvailableTags(SegmentDbPath, _tagMap));

            foreach (var (id, name) in tags)
                AvailableTags.Add(new TagSelectionItem { TagId = id, TagName = name, IsSelected = false });

            StatusText = $"{AvailableTags.Count} tags found. Select tags then click Load Data.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading tags: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        var selectedTags = AvailableTags.Where(t => t.IsSelected).ToList();
        if (!selectedTags.Any())
        {
            StatusText = "Select at least one tag to plot.";
            return;
        }

        IsLoading = true;
        StatusText = "Loading data…";

        try
        {
            var tagIds = selectedTags.Select(t => t.TagId).ToList();

            var data = await Task.Run(() =>
                WinCcReader.LoadSegmentData(SegmentDbPath, _tagMap, tagIds, FromDate, ToDate));

            // Group by tag and build series
            var grouped = data.GroupBy(d => d.TagId).ToList();
            var newSeries = new List<ISeries>();
            int colourIdx = 0;

            foreach (var group in grouped)
            {
                var points = group
                    .OrderBy(d => d.Timestamp)
                    .Select(d => new DateTimePoint(d.Timestamp, d.Value))
                    .ToList();

                var colour = Palette[colourIdx % Palette.Length];
                colourIdx++;

                newSeries.Add(new LineSeries<DateTimePoint>
                {
                    Name   = group.First().TagName,
                    Values = points,
                    Stroke = new SolidColorPaint(colour, 1.5f),
                    Fill   = null,
                    GeometrySize = 0,
                    LineSmoothness = 0
                });
            }

            Series = newSeries.ToArray();
            OnPropertyChanged(nameof(Series));

            long totalPoints = grouped.Sum(g => g.LongCount());
            StatusText = $"Loaded {totalPoints:N0} data points across {grouped.Count} tag(s). Range: {FromDate:dd MMM} – {ToDate:dd MMM}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading data: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SelectAllTags()
    {
        foreach (var t in AvailableTags) t.IsSelected = true;
    }

    [RelayCommand]
    private void ClearTagSelection()
    {
        foreach (var t in AvailableTags) t.IsSelected = false;
    }
}

public partial class TagSelectionItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public long TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
}
