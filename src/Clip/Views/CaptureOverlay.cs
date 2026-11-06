using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip.Imaging;
using Clip.Platform;

namespace Clip.Views;

public sealed class CaptureOverlay : Window
{
    private readonly MonitorArea monitor;
    private readonly BitmapSource snapshot;
    private readonly Surface surface;
    private Point start, cursor;
    private Int32Rect? selection;
    private bool dragging;
    private readonly bool recording;
    public event Action<BitmapSource>? Selected;
    public event Action<MonitorArea>? RegionSelected;
    public event Action? Cancelled;
    public event Action<CaptureOverlay>? SelectionStarted;
    public CaptureOverlay(MonitorArea monitor, BitmapSource image, bool recording = false)
    {
        this.monitor = monitor; snapshot = image; this.recording = recording;
        Title = "Clip 영역 선택"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Black;
        Left = monitor.X; Top = monitor.Y; Width = monitor.Width; Height = monitor.Height;
        surface = new Surface(this); Content = surface;
        SourceInitialized += (_, _) => Native.Place(this, monitor);
        Loaded += (_, _) => { Native.Place(this, monitor); surface.Focus(); };
        PreviewKeyDown += OnKey;
        MouseRightButtonDown += (_, e) => { e.Handled = true; Cancelled?.Invoke(); };
        surface.MouseMove += (_, e) =>
        {
            cursor = PixelPoint(e.GetPosition(surface));
            if (dragging) selection = SelectionMath.FromPoints(start, cursor, monitor.Width, monitor.Height);
            surface.InvalidateVisual();
        };
        surface.MouseLeftButtonDown += (_, e) =>
        {
            Activate(); surface.Focus(); SelectionStarted?.Invoke(this);
            start = cursor = PixelPoint(e.GetPosition(surface)); selection = null; dragging = true; surface.CaptureMouse(); surface.InvalidateVisual();
        };
        surface.MouseLeftButtonUp += (_, e) =>
        {
            if (!dragging) return;
            dragging = false; surface.ReleaseMouseCapture(); cursor = PixelPoint(e.GetPosition(surface));
            // 움직이지 않은 클릭은 의도하지 않은 1px 캡처로 확정하지 않는다.
            selection = cursor == start ? null : SelectionMath.FromPoints(start, cursor, monitor.Width, monitor.Height);
            surface.InvalidateVisual(); e.Handled = true;
            if (!recording) CompleteSelection();
        };
    }
    public void ResetSelection() { selection = null; dragging = false; surface.ReleaseMouseCapture(); surface.InvalidateVisual(); }
    private Point PixelPoint(Point position)
    {
        var screen = surface.PointToScreen(position);
        return SelectionMath.ScreenToLocal(screen, monitor);
    }
    private void CompleteSelection()
    {
        if (dragging || selection is not { } chosen) return;
        selection = null;
        if (recording) RegionSelected?.Invoke(SelectionMath.ToScreen(chosen, monitor));
        else { var image = new CroppedBitmap(snapshot, chosen); image.Freeze(); Selected?.Invoke(image); }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Cancelled?.Invoke(); return; }
        if (e.Key == Key.Enter && selection is not null && !dragging) { e.Handled = true; CompleteSelection(); return; }
        int dx = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0, dy = e.Key == Key.Up ? -1 : e.Key == Key.Down ? 1 : 0;
        if (selection is { } rect && (dx != 0 || dy != 0))
        {
            selection = SelectionMath.Nudge(rect, dx, dy, (Keyboard.Modifiers & ModifierKeys.Shift) != 0, monitor.Width, monitor.Height);
            surface.InvalidateVisual(); e.Handled = true;
        }
    }
    private sealed class Surface : FrameworkElement
    {
        private readonly CaptureOverlay owner;
        public Surface(CaptureOverlay owner) { this.owner = owner; Focusable = true; Cursor = Cursors.Cross; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); }
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight; if (w <= 0 || h <= 0) return;
            var full = new Rect(0, 0, w, h); dc.DrawImage(owner.snapshot, full);
            double sx = w / owner.monitor.Width, sy = h / owner.monitor.Height;
            var shade = new SolidColorBrush(Color.FromArgb(145, 8, 14, 24));
            if (owner.selection is { } s)
            {
                var r = new Rect(s.X * sx, s.Y * sy, s.Width * sx, s.Height * sy);
                dc.DrawGeometry(shade, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(r)));
                dc.DrawRectangle(null, new Pen(Brushes.White, 1), r);
                DrawText(dc, $"{s.Width} × {s.Height} px", new Point(Math.Clamp(r.Left, 12, Math.Max(12, w - 220)), Math.Clamp(r.Bottom + 12, 12, Math.Max(12, h - 100))), 16);
            }
            else dc.DrawRectangle(shade, null, full);
            var p = new Point(owner.cursor.X * sx, owner.cursor.Y * sy);
            var cross = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), .6);
            dc.DrawLine(cross, new Point(p.X, 0), new Point(p.X, h)); dc.DrawLine(cross, new Point(0, p.Y), new Point(w, p.Y));
            int px = Math.Clamp((int)owner.cursor.X, 0, owner.monitor.Width - 1), py = Math.Clamp((int)owner.cursor.Y, 0, owner.monitor.Height - 1);
            int lx = Math.Clamp(px - 7, 0, Math.Max(0, owner.monitor.Width - 15)), ly = Math.Clamp(py - 7, 0, Math.Max(0, owner.monitor.Height - 15));
            var crop = new CroppedBitmap(owner.snapshot, new Int32Rect(lx, ly, Math.Min(15, owner.monitor.Width), Math.Min(15, owner.monitor.Height)));
            var loupe = new Rect(Math.Clamp(p.X + 24, 12, Math.Max(12, w - 132)), Math.Clamp(p.Y + 24, 12, Math.Max(12, h - 180)), 120, 120);
            dc.DrawImage(crop, loupe); dc.DrawRectangle(null, new Pen(Brushes.White, 2), loupe);
            double cx = loupe.X + (px - lx + .5) * 8, cy = loupe.Y + (py - ly + .5) * 8;
            dc.DrawLine(new Pen(Brushes.OrangeRed, 1), new Point(cx, loupe.Top), new Point(cx, loupe.Bottom));
            dc.DrawLine(new Pen(Brushes.OrangeRed, 1), new Point(loupe.Left, cy), new Point(loupe.Right, cy));
            string hint = owner.recording ? "녹화할 영역 드래그   ·   Enter 선택 완료   ·   Esc / 우클릭 취소   ·   한 모니터 안에서 선택" : "드래그 후 마우스를 놓으면 캡처·복사 완료   ·   Esc / 우클릭 취소";
            var bar = new Rect(Math.Max(12, (w - 760) / 2), Math.Max(12, h - 66), Math.Min(760, w - 24), 44);
            dc.DrawRoundedRectangle(Theme.Panel, new Pen(Theme.Line, 1), bar, 4, 4); DrawText(dc, hint, new Point(bar.X + 16, bar.Y + 12), 12);
        }
        private void DrawText(DrawingContext dc, string text, Point point, double size) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Theme.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Theme.Text, VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
    }
}

public static class SelectionMath
{
    public static Point ScreenToLocal(Point screen, MonitorArea monitor) => new(
        Math.Clamp(Math.Round(screen.X - monitor.X), 0, monitor.Width),
        Math.Clamp(Math.Round(screen.Y - monitor.Y), 0, monitor.Height));
    public static MonitorArea ToScreen(Int32Rect rect, MonitorArea monitor)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || (long)rect.X + rect.Width > monitor.Width || (long)rect.Y + rect.Height > monitor.Height)
            throw new ArgumentOutOfRangeException(nameof(rect));
        return new(checked(monitor.X + rect.X), checked(monitor.Y + rect.Y), rect.Width, rect.Height);
    }
    public static Int32Rect FromPoints(Point a, Point b, int width, int height)
    {
        int left = Math.Clamp((int)Math.Floor(Math.Min(a.X, b.X)), 0, width - 1), top = Math.Clamp((int)Math.Floor(Math.Min(a.Y, b.Y)), 0, height - 1);
        int right = Math.Clamp((int)Math.Round(Math.Max(a.X, b.X)), left + 1, width), bottom = Math.Clamp((int)Math.Round(Math.Max(a.Y, b.Y)), top + 1, height);
        return new(left, top, right - left, bottom - top);
    }
    public static Int32Rect Nudge(Int32Rect r, int dx, int dy, bool resize, int width, int height) => resize
        ? new(r.X, r.Y, Math.Clamp(r.Width + dx, 1, width - r.X), Math.Clamp(r.Height + dy, 1, height - r.Y))
        : new(Math.Clamp(r.X + dx, 0, width - r.Width), Math.Clamp(r.Y + dy, 0, height - r.Height), r.Width, r.Height);
}
