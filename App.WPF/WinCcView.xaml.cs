using System.Windows.Controls;
using Microsoft.Win32;
using WinCcVm = App.ViewModels.WinCcViewModel;

namespace IndustrialDashboard;

public partial class WinCcView : UserControl
{
    public WinCcView()
    {
        InitializeComponent();
    }

    private void BrowseSegment_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select WinCC Segment .db3 File",
            Filter = "SQLite Database (*.db3;*.db)|*.db3;*.db|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true && DataContext is WinCcVm vm)
            vm.SetSegmentDb(dlg.FileName);
    }

    private void BrowseTagDb_Click(object sender, System.Windows.RoutedEventArgs e)
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
