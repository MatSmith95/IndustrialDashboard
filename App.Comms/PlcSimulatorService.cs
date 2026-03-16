using App.Models;

namespace App.Comms;

public class PlcSimulatorService : IPlcService
{
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _scanCount = 0;
    private double _pressureValue = 1.0;
    private readonly Random _random = new();

    private static readonly double[] MotorSteps = { 0, 25, 50, 75, 100 };
    private int _motorStepIndex = 0;

    public event EventHandler<PlcDataPoint>? DataReceived;
    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;
        _loopTask = Task.Run(() => AcquisitionLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        if (_loopTask is not null)
            await _loopTask.ConfigureAwait(false);
        IsRunning = false;
    }

    private async Task AcquisitionLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, token).ConfigureAwait(false);

                var point = new PlcDataPoint
                {
                    Timestamp   = DateTime.Now,
                    Temperature = SimulateTemperature(),
                    Pressure    = SimulatePressure(),
                    MotorSpeed  = SimulateMotorSpeed()
                };

                DataReceived?.Invoke(this, point);
                _scanCount++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Sin wave: 20–80°C, period ~60 scans (30 seconds)
    private double SimulateTemperature()
    {
        double radians = _scanCount * (2 * Math.PI / 60.0);
        return 50.0 + 30.0 * Math.Sin(radians);
    }

    // Random walk: 0.9–1.1 bar
    private double SimulatePressure()
    {
        double delta = (_random.NextDouble() - 0.5) * 0.02;
        _pressureValue = Math.Clamp(_pressureValue + delta, 0.9, 1.1);
        return Math.Round(_pressureValue, 4);
    }

    // Step through 0/25/50/75/100 RPM, change every 10 scans
    private double SimulateMotorSpeed()
    {
        if (_scanCount % 10 == 0 && _scanCount > 0)
            _motorStepIndex = (_motorStepIndex + 1) % MotorSteps.Length;
        return MotorSteps[_motorStepIndex];
    }
}
