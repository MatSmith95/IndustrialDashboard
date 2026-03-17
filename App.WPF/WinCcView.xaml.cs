using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinCcVm = App.ViewModels.WinCcViewModel;

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
            // Wire IsSplitMode changes to swap chart visibility
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
        // Find the two children of the Border in row 1
        if (FindName("OverlayChart") is FrameworkElement overlay)
            overlay.Visibility = isSplit ? Visibility.Collapsed : Visibility.Visible;
        if (FindName("SplitChart") is FrameworkElement split)
            split.Visibility = isSplit ? Visibility.Visible : Visibility.Collapsed;
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
