using System.Windows;
using System.Windows.Controls;
using App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.WPF;

public partial class MainWindow : Window
{
    public IServiceProvider? ServiceProvider { get; set; }
    private HistoryView? _historyView;
    private WinCcView? _winCcView;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnHistoryTabSelected(object sender, RoutedEventArgs e)
    {
        if (_historyView is null && ServiceProvider is not null)
        {
            var historyVm = ServiceProvider.GetRequiredService<HistoryViewModel>();
            _historyView = new HistoryView { DataContext = historyVm };
            HistoryContainer.Children.Add(_historyView);
        }
    }

    private void OnWinCcTabSelected(object sender, RoutedEventArgs e)
    {
        if (_winCcView is null && ServiceProvider is not null)
        {
            var winCcVm = ServiceProvider.GetRequiredService<WinCcViewModel>();
            _winCcView = new WinCcView { DataContext = winCcVm };
            WinCcContainer.Children.Add(_winCcView);
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        HistoryTab.Selected += OnHistoryTabSelected;
        WinCcTab.Selected   += OnWinCcTabSelected;
    }
}
