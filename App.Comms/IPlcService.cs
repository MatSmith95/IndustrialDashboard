using App.Models;

namespace App.Comms;

public interface IPlcService
{
    event EventHandler<PlcDataPoint>? DataReceived;
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
