# Industrial Data Dashboard — WPF (.NET 8)

A production-structured WPF application demonstrating a full industrial data acquisition and live dashboard pipeline with simulated PLC data.

---

## Architecture

```
IndustrialDashboard.sln
├── App.Models        — PlcDataPoint, LogEntry (EF Core entity)
├── App.Comms         — IPlcService, PlcSimulatorService (background 500ms timer)
├── App.Data          — AppDbContext (SQLite), DataLoggingService
├── App.ViewModels    — MainViewModel, HistoryViewModel (CommunityToolkit.Mvvm)
└── App.WPF           — MainWindow, HistoryView (WPF, LiveCharts, dark theme)
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Windows 10/11 (WPF requires Windows)
- Visual Studio 2022 (v17.4+) **or** `dotnet` CLI

---

## Setup & Run

### 1. Restore NuGet packages

```cmd
dotnet restore IndustrialDashboard.sln
```

### 2. Build

```cmd
dotnet build IndustrialDashboard.sln --configuration Release
```

### 3. Run

```cmd
dotnet run --project App.WPF\App.WPF.csproj
```

Or open `IndustrialDashboard.sln` in Visual Studio and press **F5**.

> **Database:** `industrial_dashboard.db` (SQLite) is created automatically in the working directory on first launch via `EnsureCreated()`. No migrations needed.

---

## Features

### Live Dashboard tab
- **3 value cards:** Temperature (°C), Pressure (bar), Motor Speed (RPM) — live numeric readout updated every 500ms
- **Live chart:** Rolling 60-point multi-series line chart (LiveChartsCore) — all 3 tags on one chart
- **Start / Stop button:** Toggles the PLC acquisition loop
- **Status bar:** Shows connection state

### Data History tab
- Queries last 500 log entries from SQLite
- Filter by **Tag Name** (Temperature / Pressure / MotorSpeed / All)
- Filter by **date range** (From / To date pickers)
- Scrollable **DataGrid** with sortable columns

---

## Simulated PLC Tags

| Tag | Simulation |
|-----|-----------|
| Temperature | Sin wave: 20–80°C, 30-second period |
| Pressure | Random walk: 0.9–1.1 bar, ±0.01 per scan |
| MotorSpeed | Stepped: 0 → 25 → 50 → 75 → 100 → 0 RPM, changes every 10 scans (5s) |

---

## NuGet Packages

| Package | Version | Project |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.3.2 | ViewModels, WPF |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc2 | WPF |
| LiveChartsCore.SkiaSharpView | 2.0.0-rc2 | ViewModels |
| Microsoft.EntityFrameworkCore | 8.0.0 | Models |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.0 | Data, WPF |
| Microsoft.EntityFrameworkCore.Design | 8.0.0 | Data |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | WPF |
