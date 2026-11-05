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
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Brushes.White;
        var root = new DockPanel { Margin = new Thickness(18) }; Content = root;
        var panel = new StackPanel(); DockPanel.SetDock(panel, Dock.Bottom); root.Children.Add(panel);
        var status = Theme.Label("영상 여는 중…", 12, Theme.Muted); status.Margin = new Thickness(0, 12, 0, 8); panel.Children.Add(status);
        var controls = new WrapPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(controls);
        controls.IsEnabled = false;
        var media = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform };
        controls.Children.Add(Theme.Button("재생", media.Play, true)); controls.Children.Add(Theme.Button("일시 정지", media.Pause));
        controls.Children.Add(Theme.Button("처음부터", () => { media.Position = TimeSpan.Zero; media.Play(); }));
        var seek = new Slider { Minimum = 0, Maximum = 1, Width = 250, Margin = new Thickness(12, 0, 0, 0), IsEnabled = false, IsMoveToPointEnabled = true };
        controls.Children.Add(seek);
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
