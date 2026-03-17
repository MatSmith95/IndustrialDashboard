using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.Defaults;

namespace App.ViewModels;

public partial class TagConfig : ObservableObject
{
    public long TagId { get; init; }

    [ObservableProperty] private string _tagName = string.Empty;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private string _colorHex = "#FF4500";
    [ObservableProperty] private int _groupId = 1;
    [ObservableProperty] private double _minY;
    [ObservableProperty] private double _maxY;

    public List<ObservablePoint> Points { get; set; } = new();

    public static readonly string[] ColorPalette =
    {
        "#FF4500", "#1E90FF", "#32CD32", "#FFD700", "#9370DB",
        "#00CED1", "#FF69B4", "#FFA07A", "#40E0D0", "#FF6347",
        "#7B68EE", "#3CB371", "#DA70D6", "#4682B4", "#DC143C", "#BDB76B"
    };

    public static readonly string[] ColorNames =
    {
        "Red-Orange", "Blue", "Green", "Gold", "Purple",
        "Cyan", "Pink", "Salmon", "Turquoise", "Tomato",
        "Slate-Blue", "Sea-Green", "Orchid", "Steel-Blue", "Crimson", "Khaki"
    };

    public static readonly int[] AvailableGroups = { 1, 2, 3, 4, 5, 6, 7, 8 };

    [RelayCommand]
    public void AutoScale()
    {
        if (Points.Count == 0) return;
        var values = Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
        if (values.Count == 0) return;

        double min = values.Min();
        double max = values.Max();
        double span = max - min;
        double margin = span > 0 ? span * 0.1 : Math.Abs(min) * 0.1;
        if (margin < 1e-10) margin = 1.0;

        MinY = Math.Round(min - margin, 4);
        MaxY = Math.Round(max + margin, 4);
    }
}
