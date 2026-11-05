using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clip.Views;

public static class Theme
{
    public static readonly Brush Background = new SolidColorBrush(Color.FromRgb(15, 20, 29));
    public static readonly Brush Panel = new SolidColorBrush(Color.FromRgb(24, 32, 44));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(156, 171, 191));
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    public static void Apply(Application application)
    {
        application.Resources[SystemColors.WindowBrushKey] = Panel;
        application.Resources[SystemColors.WindowTextBrushKey] = Brushes.White;
        var button = new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Panel)); button.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(57, 72, 93))));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8))); button.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0)));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 13.0)); button.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        var template = new ControlTemplate(typeof(Button)); var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.AppendChild(content); template.VisualTree = border; button.Setters.Add(new Setter(Control.TemplateProperty, template));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .35)); button.Triggers.Add(disabled);
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(96, 165, 250)))); button.Triggers.Add(hover);
        application.Resources[typeof(Button)] = button;
        foreach (var type in new[] { typeof(ComboBox), typeof(ComboBoxItem), typeof(TextBox) })
        {
            var input = new Style(type); input.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(24, 32, 44))));
            input.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White)); application.Resources[type] = input;
        }
    }
    public static Button Button(string text, System.Action click, bool primary = false)
    {
        var b = new Button { Content = text }; if (primary) b.Background = Accent; b.Click += (_, _) => click(); return b;
    }
    public static TextBlock Label(string text, double size = 13, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Brushes.White, VerticalAlignment = VerticalAlignment.Center };
}
