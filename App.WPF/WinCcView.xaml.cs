using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveChartsCore.SkiaSharpView.WPF;
using Microsoft.Win32;
using WinCcVm = App.ViewModels.WinCcViewModel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace IndustrialDashboard;

public partial class WinCcView : UserControl
{
    public WinCcView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is WinCcVm vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(WinCcVm.IsSplitMode))
                    UpdateChartVisibility(vm.IsSplitMode);
            };
            UpdateChartVisibility(vm.IsSplitMode);
        }
    }

    private void UpdateChartVisibility(bool isSplit)
    {
        if (OverlayChart is not null)
            OverlayChart.Visibility = isSplit ? Visibility.Collapsed : Visibility.Visible;
        if (SplitChart is not null)
            SplitChart.Visibility = isSplit ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Chart_MouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not WinCcVm vm) return;
        if (sender is not CartesianChart chart) return;

        var pos = e.GetPosition(chart);
        if (chart.ActualWidth <= 0) return;

        try
        {
            // Map pixel X to data X using axis min/max
            var xAxes = chart.XAxes?.ToArray();
            if (xAxes == null || xAxes.Length == 0) return;

            // Use the chart's ScaleUIPoint to convert screen coords to data coords
            var dataPoint = chart.ScalePixelsToData(new LiveChartsCore.Drawing.LvcPointD(pos.X, pos.Y));
            long xTick = (long)dataPoint.X;

            vm.UpdateCursorValues(xTick, vm.Tags);
        }
        catch { /* ignore errors during hover */ }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WinCcVm vm) return;
        var dlg = new SaveFileDialog
        {
            Title  = "Export Chart Data as CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"wincc-export-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv"
        };
        if (dlg.ShowDialog() == true)
            await vm.ExportCsvCommand.ExecuteAsync(dlg.FileName);
    }

    private void BrowseSegment_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select WinCC Segment .db3 File",
            Filter = "SQLite Database (*.db3;*.db)|*.db3;*.db|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true && DataContext is WinCcVm vm)
            vm.SetSegmentDb(dlg.FileName);
    }

    private void BrowseTagDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select WinCC Tag Map .db3 File",
            Filter = "SQLite Database (*.db3;*.db)|*.db3;*.db|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true && DataContext is WinCcVm vm)
            vm.SetTagDb(dlg.FileName);
    }
}
