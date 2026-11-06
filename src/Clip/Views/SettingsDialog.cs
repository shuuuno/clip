using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Clip.Views;

public sealed class SettingsDialog : Window
{
    private readonly TextBox folder;
    private readonly Button shortcut;
    private readonly CheckBox startAtLogin;
    private readonly TextBlock status = Theme.Label("설정은 이 기기에만 저장됩니다.", 12, Theme.Muted);
    private uint modifiers, key;
    private bool recording;
    private readonly Func<ClipSettings, bool, string?> apply;
    public SettingsDialog(Window owner, ClipSettings settings, bool startupEnabled, Func<ClipSettings, bool, string?> apply)
    {
        this.apply = apply; modifiers = settings.Modifiers; key = settings.KeyCode;
        Owner = owner; Title = "Clip · 설정"; Width = 620; Height = 620; MinWidth = 560; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text; FontFamily = Theme.Font;
        var root = new DockPanel(); Content = root;
        var footer = new Border { Padding = new Thickness(28, 16, 20, 20), BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Background = Theme.Panel }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var footerContent = new StackPanel(); footer.Child = footerContent;
        status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0, 0, 0, 14); footerContent.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; actions.Children.Add(Theme.Button("취소", () => DialogResult = false)); actions.Children.Add(Theme.Button("저장", Save, true)); footerContent.Children.Add(actions);
        var stack = new StackPanel { Margin = new Thickness(28, 24, 28, 24) }; root.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var title = Theme.Label("설정", 24); title.FontWeight = FontWeights.SemiBold; stack.Children.Add(title);
        var intro = Theme.Label("시작 방식, 저장 위치, 단축키", 13, Theme.Muted); intro.Margin = new Thickness(0, 8, 0, 24); stack.Children.Add(intro);
        startAtLogin = new CheckBox { Content = "Windows 로그인 시 시작", IsChecked = startupEnabled, FontSize = 14 }; stack.Children.Add(startAtLogin);
        var startupNote = Theme.Label("창을 띄우지 않고 트레이에서 대기합니다.\n현재 사용자에게만 등록하며 저장 버튼을 눌러야 적용됩니다.", 12, Theme.Muted); startupNote.TextWrapping = TextWrapping.Wrap; startupNote.Margin = new Thickness(50, 8, 0, 0); stack.Children.Add(startupNote);
        stack.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 24, 0, 24) });
        var label = Theme.Label("저장 폴더", 14); label.FontWeight = FontWeights.SemiBold; label.Margin = new Thickness(0, 0, 0, 10); stack.Children.Add(label);
        var row = new DockPanel(); var browse = Theme.Button("폴더 선택", Browse); DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        folder = new TextBox { Text = settings.CaptureDirectory, FontSize = 13, Height = 36, Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        System.Windows.Automation.AutomationProperties.SetName(folder, "저장 폴더"); row.Children.Add(folder); stack.Children.Add(row);
        var note = Theme.Label("PNG · MP4 · GIF를 저장합니다. 기존 파일은 이동하지 않습니다.", 12, Theme.Muted); note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 8, 0, 24); stack.Children.Add(note);
        var shortcutLabel = Theme.Label("캡처 단축키", 14); shortcutLabel.FontWeight = FontWeights.SemiBold; stack.Children.Add(shortcutLabel);
        shortcut = Theme.Button(KeyName(modifiers, key), () => { }); shortcut.Click += (_, _) => { recording = true; shortcut.Content = "새 조합을 누르세요 · Esc 취소"; shortcut.Focus(); }; shortcut.HorizontalAlignment = HorizontalAlignment.Left; shortcut.Margin = new Thickness(0, 10, 0, 0); stack.Children.Add(shortcut);
        var shortcutNote = Theme.Label("버튼을 누른 뒤 Ctrl 또는 Alt가 포함된 조합을 입력하세요.\n녹화 시작·정지: Ctrl+Shift+9 (고정)", 12, Theme.Muted); shortcutNote.TextWrapping = TextWrapping.Wrap; shortcutNote.Margin = new Thickness(0, 8, 0, 0); stack.Children.Add(shortcutNote);
        System.Windows.Automation.AutomationProperties.SetHelpText(shortcut, "캡처 단축키 변경");
        PreviewKeyDown += Record;
    }
    public static string KeyName(uint modifiers, uint key)
    {
        string prefix = ((modifiers & 2) != 0 ? "Ctrl+" : "") + ((modifiers & 4) != 0 ? "Shift+" : "") + ((modifiers & 1) != 0 ? "Alt+" : "") + ((modifiers & 8) != 0 ? "Win+" : "");
        string name = KeyInterop.KeyFromVirtualKey((int)key).ToString(); if (name.Length == 2 && name[0] == 'D') name = name[1..]; return prefix + name;
    }
    private void Record(object sender, KeyEventArgs e)
    {
        if (!recording) return; e.Handled = true; var k = e.Key == Key.System ? e.SystemKey : e.Key;
        if (k == Key.Escape) { recording = false; shortcut.Content = KeyName(modifiers, key); return; }
        if (k is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
        var m = Keyboard.Modifiers;
        if ((m & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) { SetError("Ctrl 또는 Alt를 포함한 조합을 사용하세요."); return; }
        modifiers = ((m & ModifierKeys.Control) != 0 ? 2u : 0) | ((m & ModifierKeys.Shift) != 0 ? 4u : 0) | ((m & ModifierKeys.Alt) != 0 ? 1u : 0) | ((m & ModifierKeys.Windows) != 0 ? 8u : 0);
        key = (uint)KeyInterop.VirtualKeyFromKey(k); recording = false; shortcut.Content = KeyName(modifiers, key);
    }
    private void Browse()
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Clip 저장 폴더", InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : "" };
        if (picker.ShowDialog(this) == true) folder.Text = picker.FolderName;
    }
    private void Save()
    {
        if (recording) { SetError("단축키 조합을 입력하거나 Esc로 취소한 뒤 저장해 주세요."); return; }
        try
        {
            if (!Path.IsPathFullyQualified(folder.Text)) throw new IOException("전체 폴더 경로를 입력하세요.");
            string path = Path.GetFullPath(folder.Text); Directory.CreateDirectory(path);
            string probe = Path.Combine(path, ".clip-write-" + Guid.NewGuid().ToString("N"));
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { stream.WriteByte(0); }
            string? error = apply(new(path, modifiers, key), startAtLogin.IsChecked == true); if (error is not null) { SetError(error); return; }
            DialogResult = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { folder.BorderBrush = Theme.Error; SetError("저장할 수 없습니다: " + e.Message); }
    }
    private void SetError(string message) { status.Foreground = Theme.Error; status.Text = message; }
}
