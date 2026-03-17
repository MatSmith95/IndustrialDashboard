using CommunityToolkit.Mvvm.ComponentModel;

namespace App.ViewModels;

public partial class TagRow : ObservableObject
{
    public long TagId { get; init; }
    public string TagName { get; init; } = string.Empty;
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public long Points { get; init; }
    public string ColorHex { get; set; } = "#FF4500";

    [ObservableProperty] private bool _isSelected;
}
