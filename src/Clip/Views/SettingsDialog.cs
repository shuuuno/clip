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
    private readonly TextBlock status = Theme.Label("설정은 이 기기에만 저장됩니다.", 12, Theme.Muted);
    private uint modifiers, key;
    private bool recording;
    private readonly Func<ClipSettings, string?> apply;
    public SettingsDialog(Window owner, ClipSettings settings, Func<ClipSettings, string?> apply)
    {
        this.apply = apply; modifiers = settings.Modifiers; key = settings.KeyCode;
        Owner = owner; Title = "Clip · 설정"; Width = 610; Height = 390; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Brushes.White;
        var stack = new StackPanel { Margin = new Thickness(24) }; stack.Children.Add(Theme.Label("설정", 24));
        var label = Theme.Label("PNG 저장 폴더", 14); label.Margin = new Thickness(0, 22, 0, 8); stack.Children.Add(label);
        var row = new DockPanel(); var browse = Theme.Button("폴더 선택", Browse); DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        folder = new TextBox { Text = settings.CaptureDirectory, FontSize = 13, Padding = new Thickness(8), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }; row.Children.Add(folder); stack.Children.Add(row);
        var note = Theme.Label("폴더를 바꿔도 기존 이미지는 이동하지 않습니다.", 12, Theme.Muted); note.Margin = new Thickness(0, 7, 0, 20); stack.Children.Add(note);
        stack.Children.Add(Theme.Label("캡처 단축키", 14)); shortcut = Theme.Button(KeyName(modifiers, key), () => { }); shortcut.Click += (_, _) => { recording = true; shortcut.Content = "새 조합을 누르세요 · Esc 취소"; shortcut.Focus(); }; shortcut.HorizontalAlignment = HorizontalAlignment.Left; shortcut.Margin = new Thickness(0, 8, 0, 18); stack.Children.Add(shortcut);
        status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0, 0, 0, 15); stack.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; actions.Children.Add(Theme.Button("취소", () => DialogResult = false)); actions.Children.Add(Theme.Button("저장", Save, true)); stack.Children.Add(actions); Content = stack;
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
        if ((m & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) { status.Text = "Ctrl 또는 Alt를 포함한 조합을 사용하세요."; return; }
        modifiers = ((m & ModifierKeys.Control) != 0 ? 2u : 0) | ((m & ModifierKeys.Shift) != 0 ? 4u : 0) | ((m & ModifierKeys.Alt) != 0 ? 1u : 0) | ((m & ModifierKeys.Windows) != 0 ? 8u : 0);
        key = (uint)KeyInterop.VirtualKeyFromKey(k); recording = false; shortcut.Content = KeyName(modifiers, key);
    }
    private void Browse()
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Clip 이미지 저장 폴더", InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : "" };
        if (picker.ShowDialog(this) == true) folder.Text = picker.FolderName;
    }
    private void Save()
    {
        try
        {
            if (!Path.IsPathFullyQualified(folder.Text)) throw new IOException("전체 폴더 경로를 입력하세요.");
            string path = Path.GetFullPath(folder.Text); Directory.CreateDirectory(path);
            string probe = Path.Combine(path, ".clip-write-" + Guid.NewGuid().ToString("N"));
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { stream.WriteByte(0); }
            string? error = apply(new(path, modifiers, key)); if (error is not null) { status.Text = error; return; }
            DialogResult = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { status.Text = "저장할 수 없습니다: " + e.Message; }
    }
}
