using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace IndustrialDashboard;

public partial class RangeSlider : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────────────

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty LowerValueProperty =
        DependencyProperty.Register(nameof(LowerValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty UpperValueProperty =
        DependencyProperty.Register(nameof(UpperValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty LowerLabelProperty =
        DependencyProperty.Register(nameof(LowerLabel), typeof(string), typeof(RangeSlider),
            new PropertyMetadata(""));

    public static readonly DependencyProperty UpperLabelProperty =
        DependencyProperty.Register(nameof(UpperLabel), typeof(string), typeof(RangeSlider),
            new PropertyMetadata(""));

    public double Minimum  { get => (double)GetValue(MinimumProperty);  set => SetValue(MinimumProperty, value); }
    public double Maximum  { get => (double)GetValue(MaximumProperty);  set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => (double)GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => (double)GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public string LowerLabel { get => (string)GetValue(LowerLabelProperty); set => SetValue(LowerLabelProperty, value); }
    public string UpperLabel { get => (string)GetValue(UpperLabelProperty); set => SetValue(UpperLabelProperty, value); }

    // ── Constructor ────────────────────────────────────────────────────────────

    public RangeSlider()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateThumbs();
        SizeChanged += (_, _) => UpdateThumbs();
    }

    // ── Drag handlers ──────────────────────────────────────────────────────────

    private void LowerThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double trackWidth = GetTrackWidth();
        if (trackWidth <= 0) return;
        double delta = (e.HorizontalChange / trackWidth) * (Maximum - Minimum);
        double newVal = Math.Clamp(LowerValue + delta, Minimum, UpperValue);
        LowerValue = newVal;
    }

    private void UpperThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double trackWidth = GetTrackWidth();
        if (trackWidth <= 0) return;
        double delta = (e.HorizontalChange / trackWidth) * (Maximum - Minimum);
        double newVal = Math.Clamp(UpperValue + delta, LowerValue, Maximum);
        UpperValue = newVal;
    }

    // ── Layout ─────────────────────────────────────────────────────────────────

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RangeSlider)d).UpdateThumbs();

    private void UpdateThumbs()
    {
        if (!IsLoaded) return;
        double range = Maximum - Minimum;
        if (range <= 0) return;

        double trackWidth = GetTrackWidth();
        double thumbWidth = LowerThumb.ActualWidth > 0 ? LowerThumb.ActualWidth : 16;

        double lowerPos = ((LowerValue - Minimum) / range) * trackWidth + 10 - thumbWidth / 2;
        double upperPos = ((UpperValue - Minimum) / range) * trackWidth + 10 - thumbWidth / 2;

        LowerThumb.Margin = new Thickness(lowerPos, 0, 0, 0);
        UpperThumb.Margin = new Thickness(upperPos, 0, 0, 0);

        // Highlight bar
        double selLeft  = ((LowerValue - Minimum) / range) * trackWidth + 10;
        double selWidth = ((UpperValue - LowerValue) / range) * trackWidth;
        SelectionHighlight.Margin = new Thickness(selLeft, 0, 0, 0);
        SelectionHighlight.Width  = Math.Max(0, selWidth);

        // Labels
        LowerLabelText.Text = LowerLabel;
        UpperLabelText.Text = UpperLabel;
    }

    private double GetTrackWidth()
        => Math.Max(0, ActualWidth - 20); // 10px margin each side
}
