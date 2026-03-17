# 03 — C# Fundamentals Used in This Project

This file covers every C# feature you'll encounter and exactly where it's used.

---

## 1. Properties (`get` / `set`)

C# classes expose data through **properties** rather than raw fields.

```csharp
// App.Models/PlcDataPoint.cs
public class PlcDataPoint
{
    public DateTime Timestamp { get; set; }   // get = read it, set = write it
    public double Temperature { get; set; }
}
```

`{ get; set; }` is shorthand for auto-implemented properties.
Under the hood C# creates a hidden backing field — you don't write it.

---

## 2. Interfaces (`interface`)

An interface defines a **contract** — a list of methods/properties that a class *must* provide,
without saying *how* to provide them.

```csharp
// App.Comms/IPlcService.cs
public interface IPlcService
{
    event EventHandler<PlcDataPoint>? DataReceived;
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
```

Then `PlcSimulatorService` implements it:

```csharp
public class PlcSimulatorService : IPlcService  // "I promise to implement all of IPlcService"
{
    public bool IsRunning { get; private set; }  // ✅ provides this property
    public event EventHandler<PlcDataPoint>? DataReceived;  // ✅ provides this event
    public Task StartAsync(...) { ... }  // ✅ provides this method
    public Task StopAsync() { ... }       // ✅ provides this method
}
```

**Why?** In `App.xaml.cs` we register `IPlcService` → `PlcSimulatorService`.
If you later write a `RealPlcService`, you swap one line and nothing else changes.

---

## 3. async / await

Operations that take time (file I/O, database queries, network) use `async`/`await`
so the UI doesn't freeze while waiting.

```csharp
// App.ViewModels/WinCcViewModel.cs
[RelayCommand]
private async Task LoadTagsAsync()
{
    IsLoading = true;

    // Task.Run() moves the work to a background thread
    // await means "wait for this to finish, but don't block the UI thread"
    var tags = await Task.Run(() => WinCcReader.GetAvailableTags(SegmentDbPath, _tagMap));

    // This line runs on the UI thread after the background work finishes
    foreach (var (id, name) in tags)
        AllTags.Add(new TagRow { TagId = id, TagName = name });

    IsLoading = false;
}
```

Rules:
- A method using `await` must be marked `async`
- `async` methods return `Task` (or `Task<T>` if they return a value)
- `await` pauses *that method* but lets other code run — the UI stays responsive

---

## 4. Events

Events let one object notify other objects that something happened.

```csharp
// App.Comms/PlcSimulatorService.cs
// Declaring the event
public event EventHandler<PlcDataPoint>? DataReceived;

// Firing the event (inside the 500ms timer loop)
DataReceived?.Invoke(this, point);  // "?" means "only fire if someone is listening"
```

```csharp
// App.ViewModels/MainViewModel.cs
// Subscribing to the event
_plcService.DataReceived += OnDataReceived;  // "+=" means "add my handler to the list"

private void OnDataReceived(object? sender, PlcDataPoint point)
{
    // This runs every time a new PLC reading arrives
    CurrentTemperature = point.Temperature;
}
```

`EventHandler<T>` is a standard delegate type: `void Handler(object? sender, T args)`.

---

## 5. Generics (`<T>`)

Generics let you write code that works with any type, decided at compile time.

```csharp
// ObservableCollection<PlcDataPoint> — a list of PlcDataPoints that notifies the UI when it changes
private readonly ObservableCollection<ObservableValue> _tempValues = new();

// Dictionary<long, string> — keys are longs, values are strings
private Dictionary<long, string> _tagMap = new();
```

`T` is a placeholder. `List<T>` becomes `List<PlcDataPoint>` or `List<string>` depending on what you put in the `<>`.

---

## 6. Nullable Reference Types (`?`)

The `?` after a type means "this could be null — handle it carefully".

```csharp
private ServiceProvider? _serviceProvider;  // might not be assigned yet
public event EventHandler<PlcDataPoint>? DataReceived;  // might have no subscribers

// The null-conditional operator ?. — only calls if not null
DataReceived?.Invoke(this, point);  // safe, won't crash if nobody subscribed
```

Without `?`, the compiler warns you if you forget to check for null.

---

## 7. Pattern Matching (`is`, `switch`)

A modern way to check types and extract values in one step.

```csharp
// App.WPF/WinCcView.xaml.cs
if (DataContext is WinCcVm vm)  // if DataContext is a WinCcViewModel, call it "vm"
{
    vm.SetSegmentDb(dlg.FileName);  // use vm safely — it's guaranteed non-null here
}
```

---

## 8. LINQ (Language Integrated Query)

LINQ lets you query collections with a readable, SQL-like syntax.

```csharp
// App.ViewModels/WinCcViewModel.cs
var selected = AllTags.Where(t => t.IsSelected).ToList();
// "from AllTags, give me all items where IsSelected is true, as a List"

var total = grouped.Values.Sum(v => v.LongCount());
// "sum the count of each group"

var ordered = tags.GroupBy(t => t.GroupId).OrderBy(g => g.Key);
// "group by GroupId, then sort groups by their key"
```

The `=>` is a **lambda** — a small inline function. `t => t.IsSelected` means "a thing called t, return t.IsSelected".

---

## 9. Records and `init` Properties

`init` is like `set` but only allows the property to be set during object creation.

```csharp
// App.ViewModels/TagRow.cs
public class TagRow
{
    public long TagId { get; init; }     // set once when creating, then read-only
    public string TagName { get; init; } = string.Empty;
}

// Creating it:
var row = new TagRow { TagId = 42, TagName = "Temperature" };
// row.TagId = 99;  ← COMPILE ERROR — can't change after creation
```

---

## 10. `partial` classes

A class split across multiple files. WPF uses this heavily — XAML generates one part, you write the other.

```csharp
// App.WPF/MainWindow.xaml.cs — your code
public partial class MainWindow : Window
{
    public IServiceProvider? ServiceProvider { get; set; }
    // ...
}

// App.WPF/obj/.../MainWindow.g.cs — auto-generated from MainWindow.xaml
public partial class MainWindow : Window
{
    private void InitializeComponent() { /* loads the XAML */ }
}
```

At compile time these merge into one complete class.

---

## 11. `using` statements

Two meanings:

```csharp
// 1. Import a namespace (like Python's import)
using System.Collections.ObjectModel;
using App.Models;

// 2. Dispose pattern — automatically clean up when done
using var con = new SqliteConnection($"Data Source={path}");
con.Open();
// When this block ends, con.Dispose() is called automatically
// — even if an exception is thrown
```

---

## 12. String interpolation

```csharp
string name = "Mathew";
int count = 16;
string message = $"Hello {name}, you have {count} tags.";
// → "Hello Mathew, you have 16 tags."

// With formatting
string temp = $"{temperature:F2}";  // 2 decimal places → "52.34"
string date = $"{DateTime.Now:dd MMM yyyy}";  // → "17 Mar 2026"
```
