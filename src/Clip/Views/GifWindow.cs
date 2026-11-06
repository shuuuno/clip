using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip.Imaging;

namespace Clip.Views;

public sealed class GifWindow : Window
{
    public GifWindow(Window owner, string path)
    {
        Owner = owner; Title = "Clip GIF · " + Path.GetFileName(path); Width = 840; Height = 620; MinWidth = 450; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text;
        var animation = new GifAnimation(path);
        try
        {
            int[] delays = Enumerable.Range(0, animation.Count).Select(animation.Delay).ToArray();
            int[] ends = new int[delays.Length]; for (int i = 0; i < ends.Length; i++) ends[i] = delays[i] + (i == 0 ? 0 : ends[i - 1]);
            var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
            var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            var status = Theme.Label("", 12, Theme.Muted); status.Margin = new Thickness(0, 12, 0, 10); bottom.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; bottom.Children.Add(buttons);
            int w = animation.Width, h = animation.Height;
            var surface = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[checked(w * h * 4)];
            var image = new Image { Stretch = Stretch.Uniform, Source = surface }; root.Children.Add(image);
            int index = -1; bool failed = false; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) }; var clock = new Stopwatch();
            void ShowFrame()
            {
                if (failed) return;
                int time = (int)(clock.ElapsedMilliseconds / 10 % ends[^1]);
                int next = Array.FindIndex(ends, end => end > time); if (next == index) return; index = next;
                // 디코더의 900개 프레임에 렌더링 자원이 남지 않게 표시 버퍼 한 장을 재사용한다.
                try
                {
                    var converted = new FormatConvertedBitmap(animation.ReadFrame(index), PixelFormats.Bgra32, null, 0);
                    converted.CopyPixels(pixels, w * 4, 0); surface.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
                    status.Text = $"{w} × {h}px · {index + 1} / {animation.Count} 프레임 · 반복 재생";
                }
                catch (Exception e) { failed = true; timer.Stop(); clock.Stop(); buttons.IsEnabled = false; status.Text = "GIF 프레임을 읽지 못했습니다. " + e.Message; }
            }
            timer.Tick += (_, _) => ShowFrame();
            buttons.Children.Add(Theme.ActionButton("재생", "play", () => { clock.Start(); timer.Start(); }, true)); buttons.Children.Add(Theme.QuietButton("일시 정지", "pause", () => { clock.Stop(); timer.Stop(); }));
            buttons.Children.Add(Theme.QuietButton("처음부터", "restart", () => { index = -1; clock.Restart(); ShowFrame(); timer.Start(); }));
            Loaded += (_, _) => { clock.Start(); ShowFrame(); if (!failed) timer.Start(); };
            Closed += (_, _) => { timer.Stop(); image.Source = null; animation.Dispose(); };
        }
        catch { animation.Dispose(); throw; }
    }
}
