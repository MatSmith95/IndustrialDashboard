# 05 — XAML and the UI

XAML (Extensible Application Markup Language) is an XML-based language for describing WPF UIs.
Instead of writing C# to create buttons and layouts, you declare them in XAML.

---

## Basic Syntax

```xml
<!-- A button -->
<Button Content="Click me" Width="100" Height="30" />

<!-- A text block -->
<TextBlock Text="Hello" FontSize="16" Foreground="Red" />

<!-- Nesting — a Border containing a TextBlock -->
<Border Background="Navy" CornerRadius="8" Padding="10">
    <TextBlock Text="Inside the border" Foreground="White" />
</Border>
```

Every element maps to a C# class. `<Button>` creates a `System.Windows.Controls.Button`.
Attributes like `Content="Click me"` set properties on that object.

---

## Layout Controls

WPF has several containers for positioning elements:

### Grid — most flexible, uses rows and columns

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="60" />   <!-- Fixed 60px -->
        <RowDefinition Height="*" />    <!-- Takes remaining space -->
        <RowDefinition Height="Auto" /> <!-- As tall as its content -->
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="200" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <!-- Place items using Grid.Row and Grid.Column -->
    <TextBlock Grid.Row="0" Grid.Column="0" Text="Header" />
    <Button    Grid.Row="1" Grid.Column="1" Content="Click" />
</Grid>
```

**In this project:** `MainWindow.xaml` uses a Grid with 3 rows: header (60px), content (*), status bar (32px).

### StackPanel — stacks items vertically or horizontally

```xml
<StackPanel Orientation="Horizontal">
    <TextBlock Text="52.34" FontSize="44" />
    <TextBlock Text="°C" FontSize="18" VerticalAlignment="Bottom" Margin="6,0,0,6"/>
</StackPanel>
```

**In this project:** Used for the value cards to put the unit (°C, bar, RPM) to the right of the number.

### Border — adds background, corner radius, padding

```xml
<Border Background="#FF0F3460" CornerRadius="10" Padding="20" Margin="6">
    <!-- content here -->
</Border>
```

**In this project:** Every card is a Border with the dark blue card colour.

---

## Data Binding

This is the heart of WPF. Instead of setting a value directly, you *bind* to a ViewModel property.

```xml
<!-- Static — fixed text, never changes -->
<TextBlock Text="Hello" />

<!-- Binding — reads from DataContext.CurrentTemperature, updates when it changes -->
<TextBlock Text="{Binding CurrentTemperature}" />

<!-- Binding with formatting — shows 2 decimal places -->
<TextBlock Text="{Binding CurrentTemperature, StringFormat={}{0:F2}}" />

<!-- Two-way binding — reads AND writes back to ViewModel -->
<TextBox Text="{Binding TagSearch, UpdateSourceTrigger=PropertyChanged}" />
```

### Binding Modes

| Mode | Direction | Used for |
|------|-----------|----------|
| `OneWay` | ViewModel → UI | Display-only (TextBlock, chart) |
| `TwoWay` | ViewModel ↔ UI | Input controls (TextBox, Slider) |
| `OneTime` | ViewModel → UI (once) | Values that never change |

### UpdateSourceTrigger

For `TextBox`, by default the binding only updates the ViewModel when the control loses focus.
`UpdateSourceTrigger=PropertyChanged` means "update every time a character is typed" — needed for the search box.

---

## Commands

Buttons don't directly call methods. They bind to commands.

```xml
<!-- Button calls ToggleAcquisitionCommand when clicked -->
<Button Content="{Binding StartStopLabel}"
        Command="{Binding ToggleAcquisitionCommand}" />
```

The `[RelayCommand]` attribute on the ViewModel creates the command automatically.

---

## Styles

Styles let you define a look once and reuse it everywhere.

```xml
<!-- Defined in App.xaml (global) -->
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="#FFE94560" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="Padding"    Value="20,10" />
</Style>

<!-- Applied to a button -->
<Button Style="{StaticResource PrimaryButton}" Content="Click me" />
```

`x:Key` gives it a name. `{StaticResource PrimaryButton}` references it by name.

---

## Resources

Resources are shared objects — colours, brushes, styles — defined once and referenced anywhere.

```xml
<!-- In App.xaml -->
<Color x:Key="AccentColor">#FFE94560</Color>
<SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />

<!-- Used anywhere in any XAML file -->
<Border Background="{StaticResource AccentBrush}" />
<TextBlock Foreground="{StaticResource TextPrimaryBrush}" />
```

**In this project:** All colours follow a dark theme defined in `App.xaml`. If you want to change the red accent colour, change it in one place and every button/border updates.

---

## Data Templates

Define how a data object should look when displayed in a list.

```xml
<!-- In WinCcView.xaml -->
<DataGrid.Columns>
    <!-- Template column shows a coloured rectangle -->
    <DataGridTemplateColumn Header="Color" Width="50">
        <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
                <!-- For each TagRow, show a coloured rectangle -->
                <Rectangle Fill="{Binding ColorHex, Converter={StaticResource HexBrush}}"
                           Width="24" Height="14" />
            </DataTemplate>
        </DataGridTemplateColumn.CellTemplate>
    </DataGridTemplateColumn>
</DataGrid.Columns>
```

Each row in the DataGrid is a `TagRow` object. The template describes how to render it.

---

## XAML Namespaces (`xmlns`)

At the top of every XAML file you see namespace declarations:

```xml
<UserControl
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"  <!-- Standard WPF controls -->
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"             <!-- XAML language features -->
    xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF"
    xmlns:vm="clr-namespace:App.ViewModels;assembly=App.ViewModels"
    xmlns:local="clr-namespace:IndustrialDashboard">
```

- `xmlns` (no prefix) — default namespace, used for all standard WPF controls (`<Button>`, `<Grid>`, etc.)
- `x:` — XAML language features (`x:Key`, `x:Class`, `x:Name`, `x:Static`)
- `lvc:` — LiveCharts controls (`<lvc:CartesianChart>`)
- `vm:` — our ViewModels namespace (for `DataType`, `x:Static`)
- `local:` — our WPF project namespace (for `RangeSlider`, `HexToColorBrushConverter`)

---

## x:Name — Giving Controls a Name

```xml
<lvc:CartesianChart x:Name="OverlayChart" ... />
```

```csharp
// Now reachable in code-behind
OverlayChart.Visibility = Visibility.Collapsed;
```

Use sparingly — if you're accessing controls by name in code-behind, you're probably fighting MVVM.
Sometimes necessary for things that can't be done in XAML alone (like toggling chart visibility).

---

## Converters

Sometimes the ViewModel has data in one form but the UI needs it in another.
A **converter** transforms the value.

```csharp
// App.WPF/Converters.cs
public class HexToColorBrushConverter : IValueConverter
{
    public object Convert(object value, ...)
    {
        // value = "#FF4500" (a hex colour string)
        // return = SolidColorBrush(OrangeRed)
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)value));
    }
}
```

```xml
<!-- Applied in XAML -->
<Rectangle Fill="{Binding ColorHex, Converter={StaticResource HexBrush}}" />
```

The binding engine calls the converter automatically. `ColorHex` is a string like `"#FF4500"`.
The converter turns it into a brush the Rectangle can use.
