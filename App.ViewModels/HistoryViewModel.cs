using System.Collections.ObjectModel;
using App.Data;
using App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly DataLoggingService _loggingService;

    [ObservableProperty] private string _selectedTag = string.Empty;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private string _statusText = "Ready";

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public List<string> AvailableTags { get; } = new()
    {
        "(All)",
        "Temperature",
        "Pressure",
        "MotorSpeed"
    };

    public HistoryViewModel(DataLoggingService loggingService)
    {
        _loggingService = loggingService;
        SelectedTag = "(All)";
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        StatusText = "Loading…";
        Entries.Clear();

        try
        {
            string? tagFilter = SelectedTag == "(All)" ? null : SelectedTag;

            var results = await _loggingService.GetRecentEntriesAsync(
                count: 500,
                tagFilter: tagFilter,
                from: FromDate,
                to: ToDate.AddDays(1)
            );

            foreach (var entry in results)
                Entries.Add(entry);

            StatusText = $"{Entries.Count} entries loaded.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedTag = "(All)";
        FromDate    = DateTime.Today.AddDays(-1);
        ToDate      = DateTime.Today.AddDays(1);
    }
}
