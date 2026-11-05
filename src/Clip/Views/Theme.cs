using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Clip.Views;

public static class Theme
{
    public static readonly Brush Background = Brush("#16191F"), Panel = Brush("#1D2129"), Elevated = Brush("#282D37");
    public static readonly Brush Canvas = Brush("#101217"), Text = Brush("#F1F3F7"), Muted = Brush("#A4AEBD");
    public static readonly Brush Line = Brush("#363E4B"), Accent = Brush("#2869E8"), AccentSoft = Brush("#21395C"), AccentText = Brush("#9BC1FF");
    public static readonly FontFamily Font = new("Segoe UI, Malgun Gothic");
    private static Brush Brush(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    public static void Apply(Application application)
    {
        foreach (var (key, value) in new[] { ("Background", Background), ("Panel", Panel), ("Elevated", Elevated), ("Canvas", Canvas), ("Text", Text), ("Muted", Muted), ("Line", Line), ("Accent", Accent), ("AccentSoft", AccentSoft), ("AccentText", AccentText) })
            application.Resources["Clip" + key] = value;
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Clip;component/Views/Controls.xaml", UriKind.Relative) });
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, args) =>
        {
            if (sender is not Window window || window.AllowsTransparency) return;
            if (window.Icon is null && Application.Current.MainWindow?.Icon is { } icon) window.Icon = icon;
            int dark = 1; var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        }));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public static Button Button(string text, Action click, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Font };
        if (primary) { button.Background = Accent; button.Foreground = Brushes.White; }
        button.Click += (_, _) => click(); return button;
    }
    public static Button ActionButton(string text, string icon, Action click, bool primary = false)
    {
        var button = Button(text, click, primary);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Icon(icon); glyph.Margin = new Thickness(0, 0, 8, 0); content.Children.Add(glyph);
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        button.Content = content; AutomationProperties.SetName(button, text); return button;
    }
    public static Button ToolButton(string name, string label, string icon, Action click)
    {
        var button = ActionButton(label, icon, click);
        button.Width = 100; button.Height = 40; button.Padding = new Thickness(10, 6, 8, 6); button.Margin = new Thickness(0, 2, 0, 2);
        button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(1);
        button.FontSize = 12; button.ToolTip = name; AutomationProperties.SetName(button, name); return button;
    }
    public static Path Icon(string name, double size = 18)
    {
        string data = name switch
        {
            "capture" => "M 3,8 L 3,3 8,3 M 16,3 L 21,3 21,8 M 21,16 L 21,21 16,21 M 8,21 L 3,21 3,16 M 8,8 L 16,8 16,16 8,16 Z",
            "video" => "M 3,6 L 16,6 16,18 3,18 Z M 16,10 L 22,7 22,17 16,14",
            "image" => "M 3,3 L 21,3 21,21 3,21 Z M 3,17 L 9,11 14,16 17,13 21,17 M 16,7 L 17,7",
            "settings" => "M 4,6 L 20,6 M 4,12 L 20,12 M 4,18 L 20,18 M 8,3 L 8,9 M 16,9 L 16,15 M 10,15 L 10,21",
            "select" => "M 5,3 L 5,19 10,14 14,21 17,19 13,12 20,11 Z",
            "number" => "M 9,3 L 7,21 M 17,3 L 15,21 M 4,8 L 21,8 M 3,16 L 20,16",
            "rectangle" => "M 3,5 L 21,5 21,19 3,19 Z",
            "text" => "M 4,6 L 4,3 20,3 20,6 M 12,3 L 12,21 M 8,21 L 16,21",
            "mosaic" => "M 3,3 L 10,3 10,10 3,10 Z M 14,3 L 21,3 21,10 14,10 Z M 3,14 L 10,14 10,21 3,21 Z M 14,14 L 21,14 21,21 14,21 Z",
            "crop" => "M 6,2 L 6,18 22,18 M 2,6 L 18,6 18,22",
            "rows" => "M 3,3 L 21,3 21,7 3,7 Z M 3,17 L 21,17 21,21 3,21 Z M 5,12 L 19,12",
            "columns" => "M 3,3 L 7,3 7,21 3,21 Z M 17,3 L 21,3 21,21 17,21 Z M 12,5 L 12,19",
            "copy" => "M 8,8 L 21,8 21,21 8,21 Z M 16,8 L 16,3 3,3 3,16 8,16",
            "save" => "M 4,3 L 17,3 21,7 21,21 3,21 3,3 Z M 7,3 L 7,9 16,9 16,3 M 7,21 L 7,14 17,14 17,21",
            "undo" => "M 8,4 L 3,9 8,14 M 3,9 L 14,9 C 23,9 23,20 14,20 L 9,20",
            "redo" => "M 16,4 L 21,9 16,14 M 21,9 L 10,9 C 1,9 1,20 10,20 L 15,20",
            "refresh" => "M 20,8 C 17,1 6,1 3,10 M 20,3 L 20,8 15,8 M 4,16 C 7,23 18,23 21,14 M 4,21 L 4,16 9,16",
            _ => "M 4,4 L 20,20 M 20,4 L 4,20"
        };
        var path = new Path { Data = Geometry.Parse(data), Width = size, Height = size, Stretch = Stretch.Uniform, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1) });
        return path;
    }
    public static TextBlock Label(string text, double size = 13, Brush? color = null) => new() { Text = text, FontFamily = Font, FontSize = size, Foreground = color ?? Text, VerticalAlignment = VerticalAlignment.Center };
}
