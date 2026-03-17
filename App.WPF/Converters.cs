using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IndustrialDashboard;

public class HexToColorBrushConverter : IValueConverter
{
    public static readonly HexToColorBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { /* fall through */ }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts bool to ComboBox index: false=0 (Append), true=1 (Rolling).</summary>
public class BoolToIndexConverter : IValueConverter
{
    public static readonly BoolToIndexConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? 1 : 0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i == 1;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
