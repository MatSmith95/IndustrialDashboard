# 10 — Common Patterns

This file explains patterns you'll see repeatedly throughout the project.

---

## Pattern 1: ObservableProperty + OnPropertyChanged

WPF only updates the UI when you raise `PropertyChanged`. `[ObservableProperty]` does this automatically.

```csharp
// What you write:
[ObservableProperty] private double _currentTemperature;

// What gets generated (simplified):
public double CurrentTemperature
{
    get => _currentTemperature;
    set
    {
        if (_currentTemperature == value) return;  // Skip if unchanged
        _currentTemperature = value;
        OnPropertyChanged(nameof(CurrentTemperature));  // Notify the UI
    }
}
```

**When to use:** Any property the UI binds to that can change at runtime.

---

## Pattern 2: partial void On{Property}Changed

When an `[ObservableProperty]` changes, the toolkit looks for a matching `partial void` method you can implement:

```csharp
// WinCcViewModel.cs
[ObservableProperty] private double _sliderLower;
[ObservableProperty] private double _sliderUpper;

// These run automatically when the properties change
partial void OnSliderLowerChanged(double value) => UpdateSliderLabels();
partial void OnSliderUpperChanged(double value) => UpdateSliderLabels();

private void UpdateSliderLabels()
{
    SliderLowerLabel = new DateTime((long)SliderLower).ToString("dd MMM HH:mm:ss");
    SliderUpperLabel = new DateTime((long)SliderUpper).ToString("dd MMM HH:mm:ss");
}
```

Method naming: `On{PropertyName}Changed(T newValue)`.

---

## Pattern 3: RelayCommand with Loading State

Commands that take time typically set a loading state:

```csharp
[RelayCommand]
private async Task LoadTagsAsync()
{
    IsLoading = true;        // Show progress bar in UI
    StatusText = "Loading…"; // Show status message

    try
    {
        var result = await Task.Run(() => SomeHeavyWork());
        // Update collections / properties with result
        StatusText = $"Done — {result.Count} items loaded.";
    }
    catch (Exception ex)
    {
        StatusText = $"Error: {ex.Message}";
    }
    finally
    {
        IsLoading = false;  // Always hide loading — even on error
    }
}
```

The `finally` block runs whether the try succeeded or threw an exception.

---

## Pattern 4: Background Thread → UI Thread

WPF controls can only be updated from the UI thread.
Background tasks must dispatch back to it.

```csharp
// App.ViewModels/MainViewModel.cs
private void OnDataReceived(object? sender, PlcDataPoint point)
{
    // This runs on the background timer thread
    _ = _loggingService.LogDataPointAsync(point);  // Stay on background thread for DB

    // But UI updates must go to the dispatcher
    _dispatcher.BeginInvoke(() =>
    {
        // Now on UI thread — safe to update properties
        CurrentTemperature = point.Temperature;
        AppendValue(_tempValues, point.Temperature);
    });
}
```

`Task.Run()` is the reverse — moves work FROM the UI thread TO a background thread:

```csharp
var result = await Task.Run(() => WinCcReader.LoadSegmentData(...));
// LoadSegmentData runs on a background thread
// After it finishes, execution returns to the UI thread automatically
```

---

## Pattern 5: Null Coalescing and Safety

```csharp
// ?? — "if null, use this default"
string tagName = tagMap.TryGetValue(id, out var name) ? name : $"Tag_{id}";

// ?.  — "only call if not null"
DataReceived?.Invoke(this, point);  // Does nothing if no subscribers

// ??= — "if null, assign this"
_serviceProvider ??= services.BuildServiceProvider();
```

---

## Pattern 6: Filtering with ObservableCollection

The configure view uses two collections — all tags and filtered tags:

```csharp
public ObservableCollection<TagRow> AllTags { get; } = new();     // Full set
public ObservableCollection<TagRow> FilteredTags { get; } = new(); // What's shown

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
```

The DataGrid binds to `FilteredTags`. Searching updates `FilteredTags` without modifying `AllTags`.
`OrdinalIgnoreCase` = case-insensitive matching.

---

## Pattern 7: Factory Pattern (DbContextFactory)

Instead of sharing one `DbContext`, we use a factory that creates fresh instances:

```csharp
// Registered as:
services.AddDbContextFactory<AppDbContext>(options => ...);

// Used as:
public async Task LogDataPointAsync(PlcDataPoint point)
{
    await using var db = await _dbFactory.CreateDbContextAsync();  // Fresh instance
    db.LogEntries.Add(...);
    await db.SaveChangesAsync();
    // db is disposed here — connection closed
}
```

`await using` = `using` that works with async disposal.

---

## Pattern 8: Guard Clauses

Check invalid conditions at the top and return early. Avoids deeply nested `if` blocks.

```csharp
[RelayCommand]
private async Task LoadTagsAsync()
{
    // Guard clause — exit immediately if conditions aren't met
    if (string.IsNullOrWhiteSpace(TagDbPath) || string.IsNullOrWhiteSpace(SegmentDbPath))
    {
        StatusText = "Please select both .db3 files first.";
        return;  // Stop here
    }

    // From here, we know both paths are valid
    IsLoading = true;
    // ...
}
```

---

## Pattern 9: Converter

Transform a value for display without changing the ViewModel.

```csharp
// App.WPF/Converters.cs
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    // true → hidden, false → visible  (opposite of the built-in BoolToVis)
}
```

```xml
<!-- Button only visible in graph mode (IsConfigureMode = false) -->
<Button Visibility="{Binding IsConfigureMode, Converter={StaticResource InvBoolToVis}}" />
```

One converter, used anywhere the logic applies.

---

## Pattern 10: SKColor from Hex

LiveCharts uses SkiaSharp colours (`SKColor`), but WPF uses `System.Windows.Media.Color`.
The converter bridges them:

```csharp
private static SKColor HexToSkColor(string hex)
{
    try
    {
        // WPF can parse "#FF4500" → Color
        var c = (Color)ColorConverter.ConvertFromString(hex);
        // Convert to SKColor (SkiaSharp)
        return new SKColor(c.R, c.G, c.B);
    }
    catch { return SKColors.White; }  // Fallback if hex is invalid
}
```

This is used when building chart series — each series gets a `SolidColorPaint(SKColor)` for its line colour.

---

## Where to Find Each Pattern in the Code

| Pattern | File |
|---------|------|
| `[ObservableProperty]` | `MainViewModel.cs`, `WinCcViewModel.cs`, `TagConfig.cs` |
| `partial void On{X}Changed` | `WinCcViewModel.cs` (slider labels) |
| Loading state | `WinCcViewModel.cs` → `LoadTagsAsync`, `PlotAsync` |
| Background → UI thread | `MainViewModel.cs` → `OnDataReceived` |
| `Task.Run` | `WinCcViewModel.cs` → all async commands |
| Null safety | `PlcSimulatorService.cs` → `DataReceived?.Invoke` |
| Guard clauses | `WinCcViewModel.cs` → `LoadTagsAsync`, `PlotAsync` |
| Converter | `Converters.cs` → `HexToColorBrushConverter`, `InverseBoolToVisibilityConverter` |
| ObservableCollection filter | `WinCcViewModel.cs` → `OnTagSearchChanged` |
| Factory | `DataLoggingService.cs` → `_dbFactory.CreateDbContextAsync()` |
