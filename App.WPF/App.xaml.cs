using System.Windows;
using App.Comms;
using App.Data;
using App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IndustrialDashboard;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // DB
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite("Data Source=industrial_dashboard.db"));

        // Services
        services.AddSingleton<IPlcService, PlcSimulatorService>();
        services.AddSingleton<DataLoggingService>();

        // ViewModels
        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<IPlcService>(),
            sp.GetRequiredService<DataLoggingService>(),
            Current.Dispatcher));
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<WinCcViewModel>();
        services.AddTransient<LiveAcquisitionService>();

        // Views
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Ensure DB exists
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.ServiceProvider = _serviceProvider;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            var plc = _serviceProvider.GetService<IPlcService>();
            if (plc?.IsRunning == true)
                await plc.StopAsync();
            _serviceProvider.Dispose();
        }
        base.OnExit(e);
    }
}
