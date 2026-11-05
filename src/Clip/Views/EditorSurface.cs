using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip.Imaging;

namespace Clip.Views;

public sealed class EditorSurface : FrameworkElement
{
    private ImageDocument? document;
    private BitmapSource? rendered;
    private Point start;
    private bool dragging;
    private Rect? pending;
    private Mark? moving;
    private int selected = -1;
    private Tool tool;
    private double zoom = 1;
    public event Action? StateChanged;
    public event Action<Point>? TextRequested;
    public Color MarkColor { get; set; } = Color.FromRgb(239, 68, 68);
    public double StrokeWidth { get; set; } = 3;
    public double TextSize { get; set; } = 24;
    public Tool Tool { get => tool; set { tool = value; CancelPending(); selected = -1; Cursor = value == Tool.Select ? Cursors.Arrow : Cursors.Cross; InvalidateVisual(); StateChanged?.Invoke(); } }
    public bool HasSelection => document is not null && selected >= 0 && selected < document.Marks.Count;
    public bool HasCrop => tool == Tool.Crop && pending.HasValue;
    public bool IsStripTool => tool is Tool.RemoveRows or Tool.RemoveColumns;
    public Int32Rect? PendingStrip => IsStripTool && pending is { Width: >= 1, Height: >= 1 } r && document is not null
        ? ImageDocument.PixelRect(r, document.Width, document.Height) : null;
    public bool CanRemoveStrip => PendingStrip is { } r && document is not null &&
        (tool == Tool.RemoveRows ? r.Height < document.Height : r.Width < document.Width);
    public ImageDocument? Document
    {
        get => document;
        set
        {
            if (document is not null) document.Changed -= Refresh;
            document = value; selected = -1; pending = null;
            if (document is not null) document.Changed += Refresh;
            Refresh();
        }
    }
    public double Zoom { get => zoom; set { zoom = Math.Clamp(value, .02, 4); UpdateSize(); } }
    public EditorSurface()
    {
        Focusable = true; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += Up;
        LostMouseCapture += (_, _) => { if (dragging) CancelPending(); };
    }
    private void UpdateSize() { Width = (document?.Width ?? 1) * zoom; Height = (document?.Height ?? 1) * zoom; InvalidateVisual(); }
    private void Refresh()
    {
        pending = null; moving = null; dragging = false; ReleaseMouseCapture();
        rendered = document?.Render();
        if (document is null || selected >= document.Marks.Count) selected = -1;
        UpdateSize(); StateChanged?.Invoke();
    }
    private Point Position(MouseEventArgs e)
    {
        var p = e.GetPosition(this); return new(Math.Clamp(Math.Round(p.X / zoom), 0, document!.Width), Math.Clamp(Math.Round(p.Y / zoom), 0, document.Height));
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        if (document is null) return; Focus(); start = Position(e); e.Handled = true;
        if (tool == Tool.Number) { document.Add(new(Tool.Number, new Rect(start, new Size(0, 0)), MarkColor, StrokeWidth, FontSize: TextSize)); return; }
        if (tool == Tool.Text) { TextRequested?.Invoke(start); return; }
        if (tool == Tool.Select)
        {
            selected = -1;
            for (int i = document.Marks.Count - 1; i >= 0; i--)
            { var hit = ImageDocument.HitBounds(document.Marks[i]); hit.Inflate(5 / zoom, 5 / zoom); if (hit.Contains(start)) { selected = i; break; } }
            moving = HasSelection ? document.Marks[selected] : null;
        }
        pending = null; dragging = true; CaptureMouse(); InvalidateVisual(); StateChanged?.Invoke();
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!dragging || document is null) return;
        var end = Position(e);
        pending = new Rect(start, end);
        if (tool == Tool.RemoveRows) pending = new Rect(0, Math.Min(start.Y, end.Y), document.Width, Math.Abs(end.Y - start.Y));
        if (tool == Tool.RemoveColumns) pending = new Rect(Math.Min(start.X, end.X), 0, Math.Abs(end.X - start.X), document.Height);
        if (tool == Tool.Select && moving is not null)
        {
            var r = moving.Bounds; r.Offset(end.X - start.X, end.Y - start.Y); pending = r;
        }
        InvalidateVisual(); StateChanged?.Invoke();
    }
    private void Up(object sender, MouseButtonEventArgs e)
    {
        if (!dragging || document is null) return;
        Move(sender, e); dragging = false; ReleaseMouseCapture();
        if (tool == Tool.Select && moving is not null && pending is { } moved) document.Replace(selected, moving with { Bounds = moved });
        else if ((tool == Tool.Rectangle || tool == Tool.Mosaic) && pending is { Width: >= 1, Height: >= 1 } r)
            document.Add(new(tool, r, MarkColor, StrokeWidth));
        if (tool != Tool.Crop && !IsStripTool) pending = null;
        moving = null; InvalidateVisual(); StateChanged?.Invoke();
    }
    public bool CancelPending()
    {
        bool result = pending.HasValue || dragging || selected >= 0;
        pending = null; moving = null; dragging = false; selected = -1; ReleaseMouseCapture(); InvalidateVisual(); StateChanged?.Invoke(); return result;
    }
    public void ApplyCrop()
    {
        if (document is null || pending is not { } r || tool != Tool.Crop) return;
        pending = null; selected = -1; document.Crop(ImageDocument.PixelRect(r, document.Width, document.Height));
    }
    public bool ApplyStrip()
    {
        if (!CanRemoveStrip || PendingStrip is not { } r || document is null) return false;
        bool horizontal = tool == Tool.RemoveRows;
        document.RemoveStrip(horizontal, horizontal ? r.Y : r.X, horizontal ? r.Height : r.Width);
        return true;
    }
    public void DeleteSelected() { if (HasSelection) { document!.Delete(selected); selected = -1; InvalidateVisual(); } }
    public void MoveSelected(int dx, int dy)
    {
        if (!HasSelection) return; var mark = document!.Marks[selected]; var r = mark.Bounds; r.Offset(dx, dy); document.Replace(selected, mark with { Bounds = r });
    }
    public void AddText(Point point, string text)
    { if (document is not null && !string.IsNullOrWhiteSpace(text)) document.Add(new(Tool.Text, new Rect(point, new Size(0, 0)), MarkColor, StrokeWidth, text, FontSize: TextSize)); }
    protected override void OnRender(DrawingContext dc)
    {
        if (document is null || rendered is null) return;
        dc.PushTransform(new ScaleTransform(zoom, zoom));
        var full = new Rect(0, 0, document.Width, document.Height);
        dc.DrawRectangle(Brushes.White, null, full); dc.DrawImage(rendered, full);
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5 / zoom) { DashStyle = DashStyles.Dash };
        if (HasSelection) dc.DrawRectangle(null, pen, ImageDocument.HitBounds(document.Marks[selected]));
        if (pending is { } r)
        {
            if (tool == Tool.Crop) dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new RectangleGeometry(r)));
            if (IsStripTool) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 239, 68, 68)), null, r);
            dc.DrawRectangle(null, pen, r);
        }
        dc.Pop();
    }
}
