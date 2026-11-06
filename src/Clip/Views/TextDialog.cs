using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clip.Views;

public sealed class TextDialog : Window
{
    private readonly TextBox input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 20, Height = 105, Padding = new Thickness(10), VerticalContentAlignment = VerticalAlignment.Top };
    public string Value => input.Text;
    public TextDialog(Window owner)
    {
        Owner = owner; Title = "Clip · 텍스트"; Width = 450; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background;
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(Theme.Label("텍스트 입력 · Ctrl+Enter로 추가")); input.Margin = new Thickness(0, 12, 0, 16); stack.Children.Add(input);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Theme.Button("취소", () => DialogResult = false)); row.Children.Add(Theme.Button("추가", () => DialogResult = true, true)); stack.Children.Add(row); Content = stack;
        Loaded += (_, _) => input.Focus(); PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; } if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { DialogResult = true; e.Handled = true; } };
    }
}
