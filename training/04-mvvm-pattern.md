# 04 — The MVVM Pattern

MVVM stands for **Model — View — ViewModel**. It's the standard architecture for WPF apps.
Understanding this pattern is the single most important thing for WPF development.

---

## The Problem It Solves

Without a pattern, you'd write code like this in your XAML code-behind:

```csharp
// Bad — mixed UI and logic
private void StartButton_Click(object sender, RoutedEventArgs e)
{
    _plcService.StartAsync();
    StartButton.Content = "Stop";
    StatusLabel.Text = "Connected";
    // querying database directly here, updating controls directly...
}
```

Problems:
- Hard to test (can't test UI code without running the UI)
- Tightly coupled (changing the button name breaks the code)
- Hard to reuse (logic is stuck inside the UI)

---

## The Solution: Three Layers

```
┌─────────────────────────────────────┐
│            VIEW (.xaml)             │  ← What the user sees
│   TextBlock, Button, Chart, Grid    │  ← No logic here
│   Binds to ViewModel properties     │
└──────────────┬──────────────────────┘
               │  Data binding (automatic)
               ↕
┌──────────────▼──────────────────────┐
│           VIEWMODEL (.cs)           │  ← All the logic
│   Properties the View binds to      │  ← Commands for buttons
│   Calls Model/Data services         │  ← No UI knowledge
└──────────────┬──────────────────────┘
               │  Method calls
               ↕
┌──────────────▼──────────────────────┐
│        MODEL (data + services)      │  ← Pure data and rules
│   PlcDataPoint, LogEntry            │  ← Database, file reading
│   PlcSimulatorService               │  ← No UI knowledge
└─────────────────────────────────────┘
```

---

## Concrete Example: The Live Dashboard

### Model — `PlcDataPoint.cs`
```csharp
// Just data — no logic, no UI
public class PlcDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double MotorSpeed { get; set; }
}
```

### ViewModel — `MainViewModel.cs`
```csharp
public partial class MainViewModel : ObservableObject
{
    // Properties the View binds to
    [ObservableProperty] private double _currentTemperature;
    [ObservableProperty] private string _statusText = "Stopped";

    // Command the Start/Stop button calls
    [RelayCommand]
    private async Task ToggleAcquisitionAsync()
    {
        if (_plcService.IsRunning)
        {
            await _plcService.StopAsync();
            StatusText = "Stopped";
        }
        else
        {
            await _plcService.StartAsync();
            StatusText = "Connected — Acquiring data";
        }
    }

    // Called by the event from PlcSimulatorService
    private void OnDataReceived(object? sender, PlcDataPoint point)
    {
        _dispatcher.BeginInvoke(() =>
        {
            CurrentTemperature = point.Temperature;  // UI updates automatically
        });
    }
}
```

### View — `MainWindow.xaml`
```xml
<!-- The View just describes what to show and where to bind -->
<TextBlock Text="{Binding CurrentTemperature, StringFormat={}{0:F2}}"
           FontSize="44" FontWeight="Bold" />

<Button Content="{Binding StartStopLabel}"
        Command="{Binding ToggleAcquisitionCommand}" />
```

The View has **no C# logic**. It just declares "this TextBlock shows CurrentTemperature".
When CurrentTemperature changes in the ViewModel, the screen updates automatically.

---

## How `[ObservableProperty]` Works

`CommunityToolkit.Mvvm` provides source generators that write boilerplate code for you.

You write:
```csharp
[ObservableProperty]
private double _currentTemperature;
```

The toolkit generates:
```csharp
public double CurrentTemperature
{
    get => _currentTemperature;
    set
    {
        if (_currentTemperature == value) return;
        _currentTemperature = value;
        OnPropertyChanged("CurrentTemperature");  // tells the UI "this changed!"
    }
}
```

`OnPropertyChanged()` fires an event. WPF's binding system listens for it and updates the screen.

**Naming rule:** Private field `_currentTemperature` → public property `CurrentTemperature`
(underscore removed, first letter capitalised).

---

## How `[RelayCommand]` Works

You write:
```csharp
[RelayCommand]
private async Task ToggleAcquisitionAsync()
{
    // ...
}
```

The toolkit generates a property called `ToggleAcquisitionCommand` that implements `ICommand`.
The View binds the button to it:

```xml
<Button Command="{Binding ToggleAcquisitionCommand}" />
```

When the user clicks the button, `ToggleAcquisitionAsync()` runs.

**Naming rule:** Method `ToggleAcquisitionAsync()` → command `ToggleAcquisitionCommand`
(`Async` suffix removed, `Command` added).

---

## The DataContext

Every WPF control has a `DataContext` property — it's the object that bindings look at.

```csharp
// App.WPF/App.xaml.cs
mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
```

This says: "MainWindow, when you see `{Binding CurrentTemperature}` in XAML,
look for a property called `CurrentTemperature` on `MainViewModel`."

DataContext is **inherited** — all controls inside `MainWindow` automatically share its DataContext
unless you explicitly override it.

---

## Why MVVM is Worth Learning

| Without MVVM | With MVVM |
|---|---|
| Logic mixed in XAML code-behind | Logic in testable ViewModels |
| Changing UI breaks logic | UI and logic are independent |
| Hard to add features | Add a property + bind it in XAML |
| No reuse | ViewModels can be shared or reused |

In this project, you could completely redesign the UI layout without touching a single ViewModel.
The logic stays the same — only the XAML changes.
