using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using Clip.Platform;
using Clip.Recording;

namespace Clip.Views;

public sealed class RecordingWindow : Window
{
    private readonly Button start, stop;
    private readonly TextBlock state = Theme.Label("녹화 준비", 21);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool active, closing, excluded;
    public event Action? StartRequested, StopRequested, CancelRequested;
    public RecordingWindow(MonitorArea area, bool shortcutAvailable, Func<TimeSpan> elapsed)
    {
        Title = "Clip 녹화"; Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; Background = Theme.Background; Foreground = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
        panel.Children.Add(state);
        var size = ScreenRecorder.EncodedSize(area);
        var detail = Theme.Label($"선택 {area.Width} × {area.Height}px · 최대 30fps · 소리 없음\n마우스 포인터는 녹화하지 않습니다.", 12, Theme.Muted);
        detail.Margin = new Thickness(0, 14, 0, 12); panel.Children.Add(detail);
        if (size.Width != area.Width || size.Height != area.Height)
            panel.Children.Add(Theme.Label($"MP4는 {size.Width} × {size.Height}px로 저장합니다.\n오른쪽·아래에 최대 1px 검정 여백이 붙습니다.", 12, Theme.Muted));
        var hint = Theme.Label(shortcutAvailable ? "Ctrl+Shift+9: 시작 / 정지·저장" : "단축키 충돌: 이 창이나 트레이 메뉴로 정지하세요.", 12, Theme.Muted);
        hint.Margin = new Thickness(0, 12, 0, 12); panel.Children.Add(hint);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        start = Theme.Button("녹화 시작", () => StartRequested?.Invoke(), true);
        stop = Theme.Button("정지·저장", () => StopRequested?.Invoke()); stop.IsEnabled = false;
        buttons.Children.Add(start); buttons.Children.Add(stop); panel.Children.Add(buttons);
        panel.Children.Add(Theme.Label("녹화 중 이 창을 닫으면 정지 후 저장합니다.", 11, Theme.Muted));
        SourceInitialized += (_, _) => excluded = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) && Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
        timer.Tick += (_, _) => state.Text = "녹화 중  " + elapsed().ToString(@"hh\:mm\:ss");
        Closing += (_, e) =>
        {
            if (closing) return;
            e.Cancel = true;
            // Closing 안에서 다시 Close를 호출하지 않도록 현재 알림이 끝난 뒤 처리한다.
            Dispatcher.BeginInvoke(() => { if (active) StopRequested?.Invoke(); else CancelRequested?.Invoke(); });
        };
    }
    public void Begin()
    {
        active = true; start.IsEnabled = false; stop.IsEnabled = true; state.Text = "녹화 시작 중…";
        // 이전 Windows 또는 캡처 제외 실패 시 조작 창 자체가 영상에 찍히지 않게 숨긴다.
        if (!excluded) Hide();
    }
    public void Started() { if (active && stop.IsEnabled) timer.Start(); }
    public void Stopping() { timer.Stop(); stop.IsEnabled = false; state.Text = "MP4 저장 중…"; }
    public void Finish() { timer.Stop(); closing = true; Close(); }
}
