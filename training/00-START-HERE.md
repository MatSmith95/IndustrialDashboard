# 📚 Industrial Dashboard — Training Guide

Welcome. This folder explains every part of the project from the ground up.
You don't need prior WPF or C# experience — each file builds on the last.

---

## Reading Order

| File | What it covers |
|------|----------------|
| `01-project-structure.md` | How the solution is organised, what each folder does, how they connect |
| `02-no-program-cs.md` | Why there's no `Program.cs` — how a WPF app starts |
| `03-csharp-fundamentals.md` | C# features used throughout: records, async/await, interfaces, generics, nullable |
| `04-mvvm-pattern.md` | The MVVM design pattern — Models, ViewModels, Views and why it matters |
| `05-xaml-and-ui.md` | How the UI is built in XAML — layouts, bindings, styles, data templates |
| `06-data-and-sqlite.md` | How data is stored and queried — EF Core, SQLite, DataLoggingService |
| `07-live-charts.md` | How the live charts work — LiveChartsCore, series, axes, bindings |
| `08-wincc-log-viewer.md` | Deep dive on the WinCC feature — reading .db3 files, FILETIME, the viewer UI |
| `09-dependency-injection.md` | What DI is, how `App.xaml.cs` wires everything together |
| `10-common-patterns.md` | Patterns you'll see everywhere: commands, observable properties, converters |

---

## Quick orientation — where to find things in Visual Studio

```
Solution Explorer
├── App.Models         ← Pure data classes (no UI, no DB logic)
├── App.Comms          ← Talks to hardware (or simulates it)
├── App.Data           ← Database and file reading
├── App.ViewModels     ← All the logic the UI needs
└── App.WPF            ← Everything you can see on screen
    ├── App.xaml       ← Application-level resources and startup
    ├── MainWindow.xaml← The main window frame + tabs
    ├── training/      ← You are here
    └── *.xaml         ← Individual screens/controls
```

---

## The question "where does the app start?"

There is no `Program.cs` in a WPF app. The entry point is `App.xaml.cs` → `OnStartup()`.
See **`02-no-program-cs.md`** for the full explanation.
