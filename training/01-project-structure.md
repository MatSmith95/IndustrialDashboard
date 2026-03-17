# 01 — Project Structure

## What is a Solution?

A `.sln` file is a **solution** — a container that groups multiple projects together.
Think of it like a folder of folders, where each inner folder is its own separate program that can talk to the others.

In Visual Studio, you open `IndustrialDashboard.sln` and see all 5 projects in Solution Explorer.

---

## The 5 Projects and What They Do

```
IndustrialDashboard.sln
│
├── App.Models        — "What things are"
├── App.Comms         — "How to talk to hardware"
├── App.Data          — "How to save and read data"
├── App.ViewModels    — "What the screen needs to know"
└── App.WPF           — "What the user sees"
```

### App.Models
Contains plain data classes — no logic, no UI, no database code.

```csharp
// PlcDataPoint.cs — just holds values
public class PlcDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double MotorSpeed { get; set; }
}
```

Every other project can use these classes because they have no dependencies.
**Rule of thumb:** If it's just data, it goes in Models.

### App.Comms
Handles communication with the PLC (or simulates it).

- `IPlcService.cs` — defines *what* a PLC service must do (an interface)
- `PlcSimulatorService.cs` — does it with fake data on a 500ms timer

Why separate this? Because one day you'll replace the simulator with a real S7/OPC-UA driver.
You swap one file, everything else stays the same.

### App.Data
Handles reading and writing data.

- `AppDbContext.cs` — the EF Core database connection
- `DataLoggingService.cs` — writes PLC readings to SQLite
- `WinCcReader.cs` — opens WinCC `.db3` files and reads tag data

### App.ViewModels
Contains all the **logic** the UI needs, but no WPF-specific code.

- `MainViewModel.cs` — live dashboard state (current values, chart data, start/stop)
- `HistoryViewModel.cs` — the history query screen
- `WinCcViewModel.cs` — the WinCC log viewer logic
- `TagConfig.cs` — represents a single tag in the signals table
- `TagRow.cs` — represents a row in the configure/tag-select table

### App.WPF
The actual WPF application — the only project that produces a `.exe`.

- `App.xaml` / `App.xaml.cs` — application startup and shared resources
- `MainWindow.xaml` — the main window with the tab control
- `HistoryView.xaml` — the history data grid screen
- `WinCcView.xaml` — the WinCC log viewer screen
- `RangeSlider.xaml` — the custom dual-thumb time slider control
- `Converters.cs` — helper classes that convert data for the UI

---

## How Projects Reference Each Other

```
App.WPF
  ├── references App.ViewModels
  ├── references App.Models
  ├── references App.Comms
  └── references App.Data

App.ViewModels
  ├── references App.Models
  ├── references App.Comms
  └── references App.Data

App.Data
  └── references App.Models

App.Comms
  └── references App.Models

App.Models
  └── (no references — it's the foundation)
```

The key rule: **lower layers never reference higher layers**.
Models don't know about ViewModels. Data doesn't know about the UI.
This is called a **layered architecture**.

---

## How to Read a `.csproj` File

Each project has a `.csproj` file (the project file). It tells .NET:
1. What framework to target
2. What NuGet packages to install
3. What other projects to reference

```xml
<!-- App.WPF/App.WPF.csproj (simplified) -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>  <!-- Windows-only, WPF -->
    <UseWPF>true</UseWPF>                              <!-- Enables WPF -->
    <OutputType>WinExe</OutputType>                    <!-- Produces a .exe, not a .dll -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.0-rc2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\App.ViewModels\App.ViewModels.csproj" />
  </ItemGroup>
</Project>
```

Notice `App.Models` and `App.Data` target `net8.0-windows` too, because they're referenced by the WPF project and share some Windows-specific types.

---

## The Dependency Chain — Data Flow Example

Here's what happens when a PLC value arrives:

```
PlcSimulatorService (App.Comms)
    ↓ fires DataReceived event with PlcDataPoint (App.Models)
MainViewModel (App.ViewModels)
    ↓ receives it, updates CurrentTemperature, adds to chart collection
    ↓ also calls DataLoggingService (App.Data)
        ↓ writes to SQLite database (App.Data)
MainWindow.xaml (App.WPF)
    ↓ binding sees CurrentTemperature changed
    ↓ TextBlock on screen updates automatically
```

No project breaks its layer rules. Data flows upward through events and bindings.
