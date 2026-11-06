using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Clip.Views;

public sealed class VideoWindow : Window
{
    private GifExportDialog? exporting;
    private bool openingExport, exitPending;
    public VideoWindow(Window owner, string path, string exportDirectory, Action saved)
    {
        Owner = owner; Title = "Clip · " + Path.GetFileName(path); Width = 900; Height = 650; MinWidth = 560; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var panel = new StackPanel(); DockPanel.SetDock(panel, Dock.Bottom); root.Children.Add(panel);
        var status = Theme.Label("영상 여는 중…", 12, Theme.Muted); status.Margin = new Thickness(0, 12, 0, 8); panel.Children.Add(status);
        var controls = new WrapPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(controls);
        controls.IsEnabled = false;
        var media = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform };
        controls.Children.Add(Theme.ActionButton("재생", "play", media.Play, true)); controls.Children.Add(Theme.QuietButton("일시 정지", "pause", media.Pause));
        controls.Children.Add(Theme.QuietButton("처음부터", "restart", () => { media.Position = TimeSpan.Zero; media.Play(); }));
        var seek = new Slider { Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 8), IsEnabled = false, IsMoveToPointEnabled = true };
        System.Windows.Automation.AutomationProperties.SetName(seek, "영상 재생 위치"); panel.Children.Insert(1, seek);
        controls.Children.Add(Theme.Button("GIF 내보내기", () =>
        {
            if (openingExport || exporting is not null) return;
            openingExport = true; media.Pause();
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!IsVisible) return;
                    exporting = new GifExportDialog(this, path, exportDirectory, saved); exporting.ShowDialog();
                }
                finally { exporting = null; openingExport = false; }
            });
        })); root.Children.Add(media);
        bool updating = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            status.Text = media.NaturalDuration.HasTimeSpan
                ? $"{media.Position:hh\\:mm\\:ss} / {media.NaturalDuration.TimeSpan:hh\\:mm\\:ss} · {media.NaturalVideoWidth} × {media.NaturalVideoHeight}px"
                : $"{media.Position:hh\\:mm\\:ss} · {media.NaturalVideoWidth} × {media.NaturalVideoHeight}px";
            if (!seek.IsMouseCaptureWithin) { updating = true; seek.Value = media.Position.TotalSeconds; updating = false; }
        };
        seek.ValueChanged += (_, _) => { if (!updating && media.NaturalDuration.HasTimeSpan) media.Position = TimeSpan.FromSeconds(seek.Value); };
        media.MediaOpened += (_, _) => { controls.IsEnabled = true; if (media.NaturalDuration.HasTimeSpan) { seek.Maximum = media.NaturalDuration.TimeSpan.TotalSeconds; seek.IsEnabled = true; } timer.Start(); };
        media.MediaEnded += (_, _) => media.Pause();
        media.MediaFailed += (_, e) => { timer.Stop(); controls.IsEnabled = false; status.Text = "영상을 열지 못했습니다. " + e.ErrorException.Message; };
        Loaded += (_, _) => { media.Source = new Uri(path); media.Play(); };
        Closing += (_, _) => { timer.Stop(); media.Stop(); media.Close(); media.Source = null; };
    }
    public bool CancelExportForExit(Action retry)
    {
        if (exporting is null) return false;
        if (!exitPending)
        {
            exitPending = true;
            exporting.Closed += (_, _) => Dispatcher.BeginInvoke(() => { exitPending = false; retry(); });
            exporting.RequestClose();
        }
        return true;
    }
}
