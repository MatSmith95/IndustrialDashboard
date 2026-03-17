# 09 — Dependency Injection

Dependency Injection (DI) is how this app creates and connects all its objects.
It sounds complex but the idea is simple: **you don't create objects yourself — you ask for them**.

---

## The Problem Without DI

Imagine creating objects manually:

```csharp
// Without DI — everything tangled together
var dbContext = new AppDbContext(new DbContextOptions<AppDbContext>());
var loggingService = new DataLoggingService(dbFactory);
var plcService = new PlcSimulatorService();
var mainVm = new MainViewModel(plcService, loggingService, Dispatcher.CurrentDispatcher);
var mainWindow = new MainWindow();
mainWindow.DataContext = mainVm;
mainWindow.Show();
```

Problems:
- If `DataLoggingService` constructor changes, you have to update every place you create it
- Testing is hard — you can't swap `PlcSimulatorService` for a test version
- Object lifetimes are unclear — who owns what?

---

## The Solution: A Service Container

```csharp
// App.WPF/App.xaml.cs — the DI setup
var services = new ServiceCollection();

// Register what we have
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite("Data Source=industrial_dashboard.db"));

services.AddSingleton<IPlcService, PlcSimulatorService>();
services.AddSingleton<DataLoggingService>();

services.AddSingleton<MainViewModel>(sp => new MainViewModel(
    sp.GetRequiredService<IPlcService>(),      // DI provides this
    sp.GetRequiredService<DataLoggingService>(), // DI provides this
    Current.Dispatcher));

services.AddTransient<HistoryViewModel>();
services.AddTransient<WinCcViewModel>();
services.AddSingleton<MainWindow>();

// Build the container
_serviceProvider = services.BuildServiceProvider();
```

Now when you need something:
```csharp
var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
```

DI automatically:
1. Sees that `MainWindow` needs no constructor parameters
2. Creates it
3. Returns it

---

## Lifetimes: Singleton vs Transient

| Lifetime | Meaning | Use when |
|----------|---------|----------|
| `AddSingleton` | One instance, shared everywhere | Services that hold state (PLC connection, main VM) |
| `AddTransient` | New instance every time it's requested | Stateless services, ViewModels for temporary views |
| `AddScoped` | One per "scope" (not used here) | Web requests, short-lived operations |

```csharp
services.AddSingleton<IPlcService, PlcSimulatorService>();
// → Same PlcSimulatorService everywhere. The timer is running in one place.

services.AddTransient<HistoryViewModel>();
// → New HistoryViewModel each time (created fresh when the tab is opened)
```

---

## Constructor Injection

When DI creates an object, it looks at the constructor and automatically provides what's needed:

```csharp
// App.Data/DataLoggingService.cs
public class DataLoggingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    // DI sees this constructor needs IDbContextFactory<AppDbContext>
    // It finds it in the container and passes it in automatically
    public DataLoggingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }
}
```

You registered `AddDbContextFactory<AppDbContext>(...)` earlier, so DI knows how to create one.

---

## Registering Against an Interface

```csharp
services.AddSingleton<IPlcService, PlcSimulatorService>();
//                    ↑ interface  ↑ concrete class
```

Anywhere that asks for `IPlcService`, it gets `PlcSimulatorService`.

```csharp
// MainViewModel only knows about IPlcService — not PlcSimulatorService
public MainViewModel(IPlcService plcService, ...)
{
    _plcService = plcService;  // Could be simulator or real driver — doesn't matter
}
```

This is the **Liskov Substitution Principle** — you can swap the implementation without changing the consumer.

---

## Lazy Loading Views

In `MainWindow.xaml.cs`, the history and WinCC views are created on-demand
(only when the tab is first opened):

```csharp
private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (ReferenceEquals(selected, HistoryTab))
        EnsureHistoryView();
    else if (ReferenceEquals(selected, WinCcTab))
        EnsureWinCcView();
}

private void EnsureHistoryView()
{
    if (_historyView is not null || ServiceProvider is null) return;  // Already created
    var vm = ServiceProvider.GetRequiredService<HistoryViewModel>();  // Ask DI
    _historyView = new HistoryView { DataContext = vm };
    HistoryContainer.Children.Add(_historyView);
}
```

This is called **lazy initialisation** — don't create it until it's needed.
If the user never opens the History tab, `HistoryViewModel` is never instantiated.

---

## Summary

| Manual | DI |
|--------|-----|
| `new PlcSimulatorService()` | `services.AddSingleton<IPlcService, PlcSimulatorService>()` |
| Pass objects manually everywhere | Constructor injection — objects declare what they need |
| One singleton or many instances — your problem | Lifetime managed by the container |
| Changing a class breaks all callers | Change registration in one place |
