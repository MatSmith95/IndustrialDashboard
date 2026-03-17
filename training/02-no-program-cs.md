# 02 — Why There's No Program.cs

## Console vs WPF Apps

In a **console application**, execution starts at `Program.cs`:

```csharp
// Console app
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello");
    }
}
```

In a **WPF application**, there is no `Program.cs`. The entry point is generated automatically
by the build system from `App.xaml`. You don't write it — .NET writes it for you.

---

## What Actually Happens When You Run the App

When you press F5 in Visual Studio or run `dotnet run`, .NET:

1. Looks at `App.WPF.csproj` and sees `<OutputType>WinExe</OutputType>`
2. Looks at `App.xaml` and sees `x:Class="IndustrialDashboard.App"`
3. Generates a hidden `Main()` method that creates an instance of your `App` class
4. Calls `app.Run()` which starts the WPF message loop

You can actually see the generated file if you look in `obj/Debug/net8.0-windows/App.g.cs`
(it's auto-generated, don't edit it).

---

## App.xaml — The Application Definition

```xml
<!-- App.WPF/App.xaml -->
<Application x:Class="IndustrialDashboard.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <!-- Global colours, styles, brushes defined here -->
        <!-- Every window and control in the app can use these -->
    </Application.Resources>
</Application>
```

Key things:
- `x:Class="IndustrialDashboard.App"` — links this XAML to the C# class `App.xaml.cs`
- `Application.Resources` — anything defined here is available everywhere in the app
- We removed `StartupUri="MainWindow.xaml"` because we handle startup manually in code

---

## App.xaml.cs — Your Real Entry Point

```csharp
// App.WPF/App.xaml.cs
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    // This is called when the application starts — your real "Main"
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Register everything we need (Dependency Injection setup)
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(...);
        services.AddSingleton<IPlcService, PlcSimulatorService>();
        services.AddSingleton<DataLoggingService>();
        services.AddSingleton<MainViewModel>(...);
        // ...

        _serviceProvider = services.BuildServiceProvider();

        // 2. Ensure the database exists
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();  // Creates tables if they don't exist

        // 3. Create and show the main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    // Called when the application exits
    protected override async void OnExit(ExitEventArgs e)
    {
        // Stop the PLC service cleanly
        var plc = _serviceProvider?.GetService<IPlcService>();
        if (plc?.IsRunning == true)
            await plc.StopAsync();
    }
}
```

### Why `partial class`?
The `partial` keyword means the class is split across two files:
- `App.xaml.cs` — your code
- `App.g.cs` (auto-generated) — the generated `Main()` method and XAML loading code

They get merged together at compile time into one class.

---

## The WPF Message Loop

After `OnStartup()` finishes, WPF enters a **message loop** — it sits and waits for:
- Mouse clicks
- Keyboard input
- Timer events
- Paint requests
- Your code raising property-changed events

This loop runs on the **UI thread** (also called the dispatcher thread).
The app stays running until the last window closes, then `OnExit()` is called.

---

## Why We Don't Use StartupUri

The original template uses `StartupUri="MainWindow.xaml"` which tells WPF
"just open this window when you start". Simple apps use this.

We removed it because we use **Dependency Injection** — our MainWindow needs
services injected into it, so we create it manually in `OnStartup()`.
This gives us full control over what gets created and in what order.

---

## Summary

| Console App | WPF App |
|-------------|---------|
| `Program.cs` → `Main()` | `App.xaml.cs` → `OnStartup()` |
| Runs top to bottom | Runs a message loop until window closes |
| No UI | Full graphical UI |
| `Console.WriteLine()` | WPF controls, bindings, events |
