using System.Windows.Controls;
using Microsoft.Win32;

namespace App.WPF;

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
        if (dlg.ShowDialog() == true && DataContext is App.ViewModels.WinCcViewModel vm)
            vm.SetSegmentDb(dlg.FileName);
    }

    private void BrowseTagDb_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select WinCC Tag Map .db3 File",
            Filter = "SQLite Database (*.db3;*.db)|*.db3;*.db|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true && DataContext is App.ViewModels.WinCcViewModel vm)
            vm.SetTagDb(dlg.FileName);
    }
}
