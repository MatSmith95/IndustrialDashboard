using System.Windows;
using System.Windows.Controls;
using App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace IndustrialDashboard;

public partial class MainWindow : Window
{
    public IServiceProvider? ServiceProvider { get; set; }
    private HistoryView? _historyView;
    private WinCcView? _winCcView;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl tc) return;

        if (tc.SelectedItem is TabItem selected)
        {
            if (ReferenceEquals(selected, HistoryTab))
                EnsureHistoryView();
            else if (ReferenceEquals(selected, WinCcTab))
                EnsureWinCcView();
        }
    }

    private void EnsureHistoryView()
    {
        if (_historyView is not null || ServiceProvider is null) return;
        var vm = ServiceProvider.GetRequiredService<HistoryViewModel>();
        _historyView = new HistoryView { DataContext = vm };
        HistoryContainer.Children.Add(_historyView);
    }

    private void EnsureWinCcView()
    {
        if (_winCcView is not null || ServiceProvider is null) return;
        var vm = ServiceProvider.GetRequiredService<WinCcViewModel>();
        _winCcView = new WinCcView { DataContext = vm };
        WinCcContainer.Children.Add(_winCcView);
    }
}
