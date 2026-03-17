# 07 — Live Charts

This project uses **LiveChartsCore** for real-time data visualisation.

---

## What is LiveChartsCore?

LiveChartsCore is a .NET charting library built on SkiaSharp (a 2D graphics engine).
It renders inside WPF, Blazor, MAUI, and other platforms.

NuGet packages used:
- `LiveChartsCore.SkiaSharpView.WPF` — the WPF chart controls
- `LiveChartsCore.SkiaSharpView` — the SkiaSharp rendering engine

---

## The Chart Control in XAML

```xml
<lvc:CartesianChart
    Series="{Binding Series}"
    XAxes="{Binding XAxes}"
    YAxes="{Binding YAxes}"
    TooltipPosition="Hidden"
    Background="Transparent" />
```

- `Series` — what to draw (list of line series, each with its data points and colour)
- `XAxes` — how the horizontal axis looks and behaves
- `YAxes` — how the vertical axis looks (one per signal group)
- `TooltipPosition="Hidden"` — disables the built-in tooltip

---

## Defining a Series in the ViewModel

```csharp
// App.ViewModels/MainViewModel.cs
Series = new ISeries[]
{
    new LineSeries<ObservableValue>
    {
        Name   = "Temperature (°C)",
        Values = _tempValues,           // The data — an ObservableCollection
        Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),  // Line colour and thickness
        Fill   = null,                  // No fill under the line
        GeometrySize = 0               // No dots at each data point
    },
    // ...
};
```

`ISeries` is the interface — `LineSeries<T>` implements it. The `<T>` is the data point type.

---

## Data Point Types

LiveCharts supports several data point types:

| Type | When to use |
|------|-------------|
| `ObservableValue` | Simple Y values (index is X) — used in live dashboard |
| `ObservablePoint` | X and Y values — used in WinCC viewer (X = ticks) |
| `DateTimePoint` | DateTime as X — alternative for time-series |

### Live Dashboard (rolling window)

```csharp
// ObservableValue — just a Y value, X is the index (0, 1, 2, ...)
private readonly ObservableCollection<ObservableValue> _tempValues = new();

// Add a new point, remove oldest if over 60
private static void AppendValue(ObservableCollection<ObservableValue> collection, double value)
{
    if (collection.Count >= 60)
        collection.RemoveAt(0);  // Drop oldest — creates the "rolling window"
    collection.Add(new ObservableValue(value));
}
```

### WinCC Viewer (timestamp X axis)

```csharp
// ObservablePoint — X = Timestamp.Ticks (so X axis can show real times)
var points = group
    .OrderBy(d => d.Timestamp)
    .Select(d => new ObservablePoint(d.Timestamp.Ticks, d.Value))
    .ToList();
```

---

## Configuring Axes in Code

Because of namespace issues with XAML-declared axes, we define them in the ViewModel:

```csharp
// App.ViewModels/MainViewModel.cs
public Axis[] XAxes { get; } = new[]
{
    new Axis
    {
        IsVisible = false,              // Hide X axis labels on live chart
        SeparatorsPaint = null          // No grid lines
    }
};

public Axis[] YAxes { get; } = new[]
{
    new Axis
    {
        LabelsPaint = new SolidColorPaint(new SKColor(0x9E, 0x9E, 0x9E)),
        SeparatorsPaint = null
    }
};
```

For the WinCC viewer, the X axis shows real timestamps:

```csharp
// App.ViewModels/WinCcViewModel.cs
new Axis
{
    Labeler = value => new DateTime((long)value).ToString("HH:mm:ss")
    // value = the raw tick number from ObservablePoint.X
    // We convert it back to DateTime and format it
}
```

---

## Multiple Y Axes (Signal Groups)

When tags are in different groups, each group gets its own Y axis with its own scale:

```csharp
// In BuildOverlay()
int axisIdx = 0;
foreach (var group in tags.GroupBy(t => t.GroupId))
{
    // One Axis per group
    yAxes.Add(new Axis
    {
        MinLimit = first.MinY,  // Fixed scale — doesn't auto-fit
        MaxLimit = first.MaxY
    });

    foreach (var tag in group)
    {
        series.Add(new LineSeries<ObservablePoint>
        {
            Values    = tag.Points,
            ScalesYAt = axisIdx  // ← Which Y axis this series uses
        });
    }
    axisIdx++;
}
```

`ScalesYAt = 0` means "use the first Y axis". `ScalesYAt = 1` means "use the second". And so on.
This is how multiple signal groups with different scales coexist on one chart.

---

## Split Mode — Multiple Charts Stacked

In split mode, there's one `CartesianChart` per signal group, stacked vertically:

```xml
<!-- WinCcView.xaml -->
<ItemsControl ItemsSource="{Binding GroupCharts}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type vm:GroupChartVm}">
            <Grid Height="160">
                <lvc:CartesianChart Series="{Binding Series}"
                                   XAxes="{Binding XAxes}"
                                   YAxes="{Binding YAxes}" />
                <TextBlock Text="{Binding Label}" ... />
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`GroupCharts` is an `ObservableCollection<GroupChartVm>`. Each `GroupChartVm` has its own Series, XAxes, and YAxes. The ItemsControl repeats the template for each one, stacking them.

---

## Disabling Tooltips

The built-in tooltip shows all series values on hover — overwhelming with 16 tags.

```xml
<lvc:CartesianChart TooltipPosition="Hidden" ... />
```

We replace it with our own cursor value bar that shows only the 2 nearest traces.

---

## Auto-Scaling

When data is loaded in the WinCC viewer, we auto-scale each tag:

```csharp
// App.ViewModels/TagConfig.cs
public void AutoScale()
{
    var values = Points.Where(p => p.Y.HasValue).Select(p => p.Y!.Value).ToList();
    double min = values.Min();
    double max = values.Max();
    double span = max - min;
    double margin = span > 0 ? span * 0.1 : 1.0;  // 10% padding

    MinY = Math.Round(min - margin, 4);
    MaxY = Math.Round(max + margin, 4);
}
```

Then `MinLimit` and `MaxLimit` are set on the axis — LiveCharts won't auto-fit outside this range.
