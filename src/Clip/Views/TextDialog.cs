using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clip.Views;

public sealed class TextDialog : Window
{
    private readonly TextBox input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 20, Height = 152, Padding = new Thickness(12), VerticalContentAlignment = VerticalAlignment.Top };
    public string Value => input.Text;
    public TextDialog(Window owner)
    {
        Owner = owner; Title = "Clip · 텍스트"; Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Panel;
        var root = new DockPanel(); Content = root;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Theme.Button("취소", () => DialogResult = false)); row.Children.Add(Theme.Button("추가", () => DialogResult = true, true));
        foreach (Button button in row.Children) { button.MinWidth = 88; button.Height = 40; }
        var footer = new Border { Child = row, Background = Theme.Background, Padding = new Thickness(28, 16, 20, 16), BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var stack = new StackPanel { Margin = new Thickness(28) }; root.Children.Add(stack);
        var title = Theme.Label("텍스트 입력", 24); title.FontWeight = FontWeights.SemiBold; stack.Children.Add(title);
        var hint = Theme.Label("Ctrl+Enter로 추가", 12, Theme.Muted); hint.Margin = new Thickness(0, 8, 0, 20); stack.Children.Add(hint); stack.Children.Add(input);
        Loaded += (_, _) => input.Focus(); PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; } if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { DialogResult = true; e.Handled = true; } };
    }
}
