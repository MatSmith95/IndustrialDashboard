using System.Windows;
using System.Windows.Controls;
using App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace App.WPF;

public partial class MainWindow : Window
{
    public IServiceProvider? ServiceProvider { get; set; }
    private HistoryView? _historyView;

    public MainWindow()
    {
        InitializeComponent();
    }

    // Lazy-load the history view when tab is selected
    private void OnHistoryTabSelected(object sender, RoutedEventArgs e)
    {
        if (_historyView is null && ServiceProvider is not null)
        {
            var historyVm = ServiceProvider.GetRequiredService<HistoryViewModel>();
            _historyView = new HistoryView { DataContext = historyVm };
            HistoryContainer.Children.Add(_historyView);
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        HistoryTab.Selected += OnHistoryTabSelected;
    }
}
