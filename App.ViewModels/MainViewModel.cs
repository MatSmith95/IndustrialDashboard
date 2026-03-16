using System.Collections.ObjectModel;
using System.Windows.Threading;
using App.Comms;
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

public partial class MainViewModel : ObservableObject
{
    private const int MaxPoints = 60;

    private readonly IPlcService _plcService;
    private readonly DataLoggingService _loggingService;
    private readonly Dispatcher _dispatcher;

    // ── Live values ───────────────────────────────────────────────────────────
    [ObservableProperty] private double _currentTemperature;
    [ObservableProperty] private double _currentPressure;
    [ObservableProperty] private double _currentMotorSpeed;
    [ObservableProperty] private string _statusText = "Stopped";
    [ObservableProperty] private string _startStopLabel = "Start Acquisition";

    // ── Chart data ────────────────────────────────────────────────────────────
    private readonly ObservableCollection<ObservableValue> _tempValues    = new();
    private readonly ObservableCollection<ObservableValue> _pressValues   = new();
    private readonly ObservableCollection<ObservableValue> _motorValues   = new();

    public ISeries[] Series { get; }

    public MainViewModel(IPlcService plcService, DataLoggingService loggingService, Dispatcher dispatcher)
    {
        _plcService     = plcService;
        _loggingService = loggingService;
        _dispatcher     = dispatcher;

        _plcService.DataReceived += OnDataReceived;

        Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Name   = "Temperature (°C)",
                Values = _tempValues,
                Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                Fill   = null,
                GeometrySize = 0
            },
            new LineSeries<ObservableValue>
            {
                Name   = "Pressure (bar)",
                Values = _pressValues,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                Fill   = null,
                GeometrySize = 0
            },
            new LineSeries<ObservableValue>
            {
                Name   = "Motor Speed (RPM)",
                Values = _motorValues,
                Stroke = new SolidColorPaint(SKColors.LimeGreen, 2),
                Fill   = null,
                GeometrySize = 0
            }
        };
    }

    [RelayCommand]
    private async Task ToggleAcquisitionAsync()
    {
        if (_plcService.IsRunning)
        {
            await _plcService.StopAsync();
            StatusText     = "Stopped";
            StartStopLabel = "Start Acquisition";
        }
        else
        {
            await _plcService.StartAsync();
            StatusText     = "Connected — Acquiring data";
            StartStopLabel = "Stop Acquisition";
        }
    }

    private void OnDataReceived(object? sender, PlcDataPoint point)
    {
        // Log to DB on background thread (fire-and-forget with error swallow for demo)
        _ = _loggingService.LogDataPointAsync(point);

        // Marshal UI updates to dispatcher
        _dispatcher.BeginInvoke(() =>
        {
            CurrentTemperature = Math.Round(point.Temperature, 2);
            CurrentPressure    = Math.Round(point.Pressure, 4);
            CurrentMotorSpeed  = point.MotorSpeed;

            AppendValue(_tempValues,  point.Temperature);
            AppendValue(_pressValues, point.Pressure);
            AppendValue(_motorValues, point.MotorSpeed);
        });
    }

    private static void AppendValue(ObservableCollection<ObservableValue> collection, double value)
    {
        if (collection.Count >= MaxPoints)
            collection.RemoveAt(0);
        collection.Add(new ObservableValue(value));
    }
}
