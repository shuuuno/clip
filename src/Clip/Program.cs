using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Platform;
using Clip.Views;
using Clip.Recording;
using Forms = System.Windows.Forms;
using Velopack;
using CommunityToolkit.WinUI.Notifications;

namespace Clip;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 실행 중인 인스턴스의 편집을 보호한다. 업데이트 적용은 단일 인스턴스 확인 뒤에만 한다.
        VelopackApp.Build().SetAutoApplyOnStartup(false).OnBeforeUninstallFastCallback(_ => ToastNotificationManagerCompat.Uninstall()).Run();
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        using var mutex = new Mutex(true, "Local\\Clip.Capture.SingleInstance.v1", out bool first);
        if (!first)
        {
            try { using var signal = EventWaitHandle.OpenExisting("Local\\Clip.Capture.Activate.v1"); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            return;
        }
        try { AppUpdates.ApplyPendingAtStartup(args); new ClipApplication(args).Run(); }
        catch (Exception e) { MessageBox.Show("Clip 실행 중 문제가 발생했습니다.\n" + e.Message, "Clip", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { mutex.ReleaseMutex(); }
    }
}

public sealed class ClipApplication : Application
{
    private readonly SettingsStore store;
    private readonly StartupRegistration startup;
    private readonly bool startHidden;
    private bool restoreCaptureWindow;
    private CaptureNotifications? notifications;
    private readonly AppUpdates updates = new();
    private HotkeyService? hotkey;
    private HotkeyService? recordHotkey;
    private bool recordHotkeyRegistered;
    private RecordingWindow? recordingWindow;
    private ScreenRecorder? recorder;
    private MonitorArea? recordingArea;
    private bool stopRequested, quitAfterRecording;
    private string? recordingStopReason;
    private readonly DispatcherTimer recordingTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool hotkeyRegistered, selecting, quitting;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? icon;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? wait;
    private Views.MainWindow window = null!;
    private readonly List<CaptureOverlay> overlays = new();
    private readonly DispatcherTimer watchdog = new() { Interval = TimeSpan.FromSeconds(60) };
    public ClipSettings Settings { get; private set; }
    public bool StartAtLogin => startup.IsEnabled;
    public ClipApplication(string[] args)
    {
        store = new(SettingsStore.ResolveDataDirectory(args)); Settings = store.Load(); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        startup = new(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Clip.exe"), store.DataDirectory);
        startHidden = args.Contains("--startup", StringComparer.Ordinal) || ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); Theme.Apply(this); window = new Views.MainWindow(this); MainWindow = window;
        try { notifications = new CaptureNotifications(path => Dispatcher.BeginInvoke(() => { if (!quitting && !selecting && recordingWindow is null && window.IsEnabled) window.OpenCapture(path); })); }
        catch (Exception) { window.SetStatus("Windows 알림을 준비하지 못했습니다. 캡처는 라이브러리에서 열 수 있습니다."); }
        hotkey = new(); hotkey.Pressed += () => _ = CaptureAsync(); hotkeyRegistered = hotkey.Set(Settings.Modifiers, Settings.KeyCode);
        recordHotkey = new(); recordHotkey.Pressed += ToggleRecording; recordHotkeyRegistered = recordHotkey.Set(6, 57);
        icon = MakeIcon(); tray = new() { Icon = icon, Text = "Clip · 화면 캡처", Visible = true };
        window.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("새 캡처", null, (_, _) => Dispatcher.InvokeAsync(() => _ = CaptureAsync()));
        menu.Items.Add("영역 녹화 / 정지·저장", null, (_, _) => Dispatcher.InvokeAsync(ToggleRecording));
        menu.Items.Add("라이브러리", null, (_, _) => Dispatcher.InvokeAsync(ShowMain));
        menu.Items.Add("설정", null, (_, _) => Dispatcher.InvokeAsync(() => { if (recordingWindow is null && !selecting) { window.ShowLibrary(); window.ShowSettings(); } }));
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Clip 종료", null, (_, _) => Dispatcher.InvokeAsync(Quit)); tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.InvokeAsync(ShowMain);
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Clip.Capture.Activate.v1");
        wait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => { if (!quitting) Dispatcher.BeginInvoke(ShowMain); }, null, Timeout.Infinite, false);
        watchdog.Tick += (_, _) => CancelCapture();
        recordingTimer.Tick += (_, _) => { if (tray is not null && recorder is not null) tray.Text = "Clip · 녹화 " + recorder.Elapsed.ToString(@"hh\:mm\:ss") + " · 우클릭으로 정지"; };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        if (startHidden) new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        else window.Show();
        if (!hotkeyRegistered) window.SetStatus("캡처 단축키가 다른 앱과 충돌합니다. 새 캡처 버튼을 쓰거나 설정에서 바꿔 주세요.");
        else if (store.Warning is not null) window.SetStatus(store.Warning);
        _ = updates.CheckAsync(message => { if (!quitting) window.SetStatus(message); });
    }
    public string? ApplySettings(ClipSettings next, bool startAtLogin)
    {
        if (recordHotkeyRegistered && next.Modifiers == 6 && next.KeyCode == 57) return "Ctrl+Shift+9는 녹화 시작·정지 단축키입니다.";
        bool changed = next.Modifiers != Settings.Modifiers || next.KeyCode != Settings.KeyCode || !hotkeyRegistered;
        if (changed && !hotkey!.Set(next.Modifiers, next.KeyCode)) return "이미 사용 중인 단축키입니다. 기존 설정을 유지했습니다.";
        StartupEntry? oldStartup = null; bool startupChanged = false;
        try
        {
            oldStartup = startup.Read();
            startup.SetEnabled(startAtLogin);
            startupChanged = !Equals(oldStartup, startup.Read());
            store.Save(next); Settings = next; hotkeyRegistered = true; return null;
        }
        catch (Exception e)
        {
            string rollback = "";
            if (startupChanged)
                try { startup.Restore(oldStartup); } catch (Exception restoreError) { rollback = " 자동 실행 설정 복원 실패: " + restoreError.Message; }
            if (changed) hotkeyRegistered = hotkey!.Set(Settings.Modifiers, Settings.KeyCode);
            return "설정 저장 실패: " + e.Message + rollback;
        }
    }
    public Task CaptureAsync() => SelectRegionAsync(false);
    private async Task SelectRegionAsync(bool recording)
    {
        if (selecting || quitting || recordingWindow is not null || !window.IsEnabled) return;
        if (!recording && Settings.OpenAfterCapture && !window.ConfirmDiscard()) return;
        restoreCaptureWindow = window.IsVisible; selecting = true; window.Hide();
        try
        {
            await Task.Delay(180);
            if (quitting || !selecting) return;
            var monitors = Native.Monitors(); var frames = monitors.Select(m => (Monitor: m, Image: Native.Capture(m))).ToList();
            var pointer = Native.CursorPosition(); CaptureOverlay? active = null;
            foreach (var frame in frames)
            {
                var overlay = new CaptureOverlay(frame.Monitor, frame.Image, recording);
                overlay.Selected += CompleteCapture; overlay.Cancelled += CancelCapture;
                overlay.RegionSelected += PrepareRecording;
                overlay.SelectionStarted += chosen => { watchdog.Stop(); foreach (var other in overlays) if (other != chosen) other.ResetSelection(); };
                overlays.Add(overlay); overlay.Show();
                if (new Rect(frame.Monitor.X, frame.Monitor.Y, frame.Monitor.Width, frame.Monitor.Height).Contains(pointer)) active = overlay;
            }
            (active ?? overlays.FirstOrDefault())?.Activate(); watchdog.Start();
        }
        catch (Exception e) { CloseOverlays(); window.ShowLibrary(); window.SetStatus("캡처하지 못했습니다. 다시 시도해 주세요. " + e.Message); }
    }
    private async void CompleteCapture(BitmapSource bitmap)
    {
        CloseOverlays(); bool openAfterCapture = Settings.OpenAfterCapture;
        string path;
        try
        {
            path = new Clip.Imaging.ImageDocument(bitmap, true).SaveNew(Settings.CaptureDirectory);
            window.RefreshLibrary();
        }
        catch (Exception error)
        {
            // 저장 실패 때 기존 편집의 폐기 확인을 거치고 새 캡처 복사를 재시도한다.
            window.ShowLibrary();
            if (openAfterCapture || window.ConfirmDiscard()) window.LoadCapture(bitmap, false);
            await window.CopyBitmapAsync(bitmap);
            window.SetStatus("자동 저장 실패 · 클립보드 복사를 시도했습니다. " + error.Message);
            return;
        }
        bool copied = await window.CopyBitmapAsync(bitmap, "캡처를 복사하고 라이브러리에 저장했습니다.");
        if (quitting) return;
        if (openAfterCapture && !selecting && recordingWindow is null) window.OpenCapture(path, false);
        else
        {
            try { notifications?.Show(path, copied); }
            catch (Exception) { window.SetStatus("Windows 알림을 표시하지 못했습니다. 캡처는 라이브러리에 저장됐습니다."); }
        }
    }
    private void CancelCapture() { CloseOverlays(); if (restoreCaptureWindow) window.ShowLibrary(); window.SetStatus("캡처를 취소했습니다. 파일은 생성하지 않았습니다."); }
    private void CloseOverlays() { watchdog.Stop(); foreach (var overlay in overlays) overlay.Close(); overlays.Clear(); selecting = false; }
    private void ShowMain()
    {
        if (selecting) return;
        if (recordingWindow is not null) { if (recorder is null || recordingWindow.IsVisible) { recordingWindow.Show(); recordingWindow.Activate(); } return; }
        window.ShowLibrary();
    }
    public void ToggleRecording()
    {
        if (quitting) return;
        if (recorder is not null) { StopRecording(); return; }
        if (recordingWindow is not null) { _ = RunRecordingAsync(); return; }
        _ = SelectRegionAsync(true);
    }
    private void PrepareRecording(MonitorArea area)
    {
        CloseOverlays();
        try
        {
            ScreenRecorder.EncodedSize(area); recordingArea = area;
            recordingWindow = new RecordingWindow(area, recordHotkeyRegistered, () => recorder?.Elapsed ?? TimeSpan.Zero);
            recordingWindow.StartRequested += () => _ = RunRecordingAsync(); recordingWindow.StopRequested += StopRecording;
            recordingWindow.CancelRequested += CancelRecording; recordingWindow.Show();
        }
        catch (Exception e) { recordingWindow?.Finish(); recordingWindow = null; window.ShowLibrary(); window.SetStatus(e.Message); }
    }
    private void CancelRecording()
    {
        if (recorder is not null) return;
        recordingWindow?.Finish(); recordingWindow = null; recordingArea = null; window.ShowLibrary(); window.SetStatus("녹화를 취소했습니다.");
    }
    private async Task RunRecordingAsync()
    {
        if (recorder is not null || recordingWindow is null || recordingArea is null) return;
        recorder = new ScreenRecorder(); stopRequested = false; recordingStopReason = null; recordingWindow.Begin();
        try
        {
            // 선택 화면이 사라진 뒤 첫 프레임을 잡는다.
            await Task.Delay(180);
            var session = recorder;
            var result = await session.Start(recordingArea, Settings.CaptureDirectory, () => Dispatcher.BeginInvoke(() => { if (!stopRequested && ReferenceEquals(recorder, session) && recordingWindow is not null) { recordingWindow.Started(); recordingTimer.Start(); } }));
            window.RefreshLibrary(); window.SetStatus((recordingStopReason is null ? "" : recordingStopReason + " · ") + $"MP4 저장 완료 · {result.Duration:hh\\:mm\\:ss} · {Path.GetFileName(result.Path)}");
        }
        catch (Exception e) { window.SetStatus("녹화 저장 실패 · " + e.Message + " (Windows 미디어 기능과 저장 폴더를 확인해 주세요.)"); }
        finally
        {
            recordingTimer.Stop(); if (tray is not null) tray.Text = "Clip · 화면 캡처";
            recorder.Dispose(); recorder = null; recordingWindow?.Finish(); recordingWindow = null; recordingArea = null;
            if (quitAfterRecording) { quitAfterRecording = false; Quit(); } else window.ShowLibrary();
        }
    }
    private void StopRecording() { if (recorder is null || stopRequested) return; stopRequested = true; recordingTimer.Stop(); recordingWindow?.Stopping(); recorder.RequestStop(); }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (quitting) return;
        if (selecting) CancelCapture();
        if (recorder is not null) { recordingStopReason = "모니터 구성이 바뀌어 정지했습니다"; StopRecording(); }
        else if (recordingWindow is not null) CancelRecording();
    });
    public void Quit()
    {
        if (window.CancelExportForExit(Quit)) return;
        if (recorder is not null) { quitAfterRecording = true; StopRecording(); return; }
        recordingWindow?.Finish(); recordingWindow = null;
        if (quitting) return; if (!window.ConfirmDiscard()) return; quitting = true; CloseOverlays(); window.FinishClose(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        quitting = true; watchdog.Stop(); recordingTimer.Stop(); wait?.Unregister(null); activation?.Dispose(); hotkey?.Dispose(); recordHotkey?.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        notifications?.Dispose(); updates.Dispose(); icon?.Dispose(); base.OnExit(e);
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    private static System.Drawing.Icon MakeIcon()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32); using (var g = System.Drawing.Graphics.FromImage(bmp))
        { g.Clear(System.Drawing.Color.FromArgb(37, 99, 235)); using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 3); g.DrawLines(pen, new[] { new System.Drawing.Point(22, 8), new System.Drawing.Point(9, 8), new System.Drawing.Point(9, 24), new System.Drawing.Point(22, 24) }); }
        IntPtr handle = bmp.GetHicon(); try { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
    }
}
