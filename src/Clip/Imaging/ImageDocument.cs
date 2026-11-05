using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clip.Imaging;

public enum Tool { Select, Number, Rectangle, Text, Mosaic, Crop, RemoveRows, RemoveColumns }
public sealed record Mark(Tool Kind, Rect Bounds, Color Color, double Width = 3, string Text = "", int Number = 0, double FontSize = 24);
public sealed record DocumentState(BitmapSource Base, Mark[] Marks, int NextNumber, long Revision);

public sealed class ImageDocument
{
    private DocumentState state;
    private readonly List<DocumentState> undo = new();
    private readonly List<DocumentState> redo = new();
    private long nextRevision = 1;
    private long savedRevision;
    public BitmapSource Base => state.Base;
    public IReadOnlyList<Mark> Marks => state.Marks;
    public int Width => Base.PixelWidth;
    public int Height => Base.PixelHeight;
    public int NextNumber => state.NextNumber;
    public bool CanUndo => undo.Count != 0;
    public bool CanRedo => redo.Count != 0;
    public bool IsDirty => state.Revision != savedRevision;
    public event Action? Changed;

    public ImageDocument(BitmapSource bitmap, bool unsaved)
    {
        state = new(Normalize(bitmap), Array.Empty<Mark>(), 1, 0);
        savedRevision = unsaved ? -1 : 0;
    }
    public static BitmapSource Normalize(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(source.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * source.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        var result = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
    public static BitmapSource Load(string path, int decodeWidth = 0)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
        image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }
    private void Push(DocumentState next)
    {
        undo.Add(state);
        if (undo.Count > 30) undo.RemoveAt(0);
        redo.Clear(); state = next with { Revision = nextRevision++ }; Changed?.Invoke();
    }
    public void Add(Mark mark)
    {
        if (mark.Kind == Tool.Number) mark = mark with { Number = state.NextNumber };
        Push(state with { Marks = state.Marks.Append(mark).ToArray(), NextNumber = state.NextNumber + (mark.Kind == Tool.Number ? 1 : 0) });
    }
    public void Replace(int index, Mark mark)
    {
        if (index < 0 || index >= Marks.Count || Marks[index] == mark) return;
        var copy = state.Marks.ToArray(); copy[index] = mark; Push(state with { Marks = copy });
    }
    public void Delete(int index)
    {
        if (index >= 0 && index < Marks.Count) Push(state with { Marks = state.Marks.Where((_, i) => i != index).ToArray() });
    }
    public void Undo()
    {
        if (!CanUndo) return;
        redo.Add(state); state = undo[^1]; undo.RemoveAt(undo.Count - 1); Changed?.Invoke();
    }
    public void Redo()
    {
        if (!CanRedo) return;
        undo.Add(state); state = redo[^1]; redo.RemoveAt(redo.Count - 1); Changed?.Invoke();
    }
    public void MarkSaved() { savedRevision = state.Revision; Changed?.Invoke(); }
    public void Crop(Int32Rect rect)
    {
        if (rect.Width < 1 || rect.Height < 1 || rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > Width || rect.Y + rect.Height > Height)
            throw new ArgumentOutOfRangeException(nameof(rect));
        var cropped = new CroppedBitmap(Render(), rect); cropped.Freeze();
        Push(new(Normalize(cropped), Array.Empty<Mark>(), 1, 0));
    }
    public void RemoveStrip(bool horizontal, int start, int length)
    {
        int extent = horizontal ? Height : Width;
        if (start < 0 || start >= extent || length < 1 || length >= extent || length > extent - start)
            throw new ArgumentOutOfRangeException(nameof(length), "띠를 제거한 뒤 최소 1픽셀은 남아야 합니다.");
        var source = Normalize(Render());
        int width = Width - (horizontal ? 0 : length), height = Height - (horizontal ? length : 0);
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        // 남겨지는 두 영역만 복사한다. 경계는 이미지의 정수 픽셀 기준이다.
        if (horizontal)
        {
            if (start > 0) source.CopyPixels(new Int32Rect(0, 0, Width, start), pixels, stride, 0);
            int remaining = Height - start - length;
            if (remaining > 0) source.CopyPixels(new Int32Rect(0, start + length, Width, remaining), pixels, stride, start * stride);
        }
        else
        {
            if (start > 0) source.CopyPixels(new Int32Rect(0, 0, start, Height), pixels, stride, 0);
            int remaining = Width - start - length;
            if (remaining > 0) source.CopyPixels(new Int32Rect(start + length, 0, remaining, Height), pixels, stride, start * 4);
        }
        var output = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        output.Freeze(); Push(new(output, Array.Empty<Mark>(), 1, 0));
    }
    public BitmapSource Render()
    {
        BitmapSource result = Base;
        // 순서대로 합성: 모자이크는 앞서 그린 내용까지 포함한다.
        foreach (var mark in Marks)
        {
            if (mark.Kind == Tool.Mosaic) result = Pixelate(result, PixelRect(mark.Bounds, Width, Height), Math.Max(6, (int)(mark.Width * 4)));
            else
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) { dc.DrawImage(result, new Rect(0, 0, Width, Height)); DrawMark(dc, mark); }
                var output = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
                output.Render(visual); output.Freeze(); result = output;
            }
        }
        return result;
    }
    public static void DrawMark(DrawingContext dc, Mark m)
    {
        var brush = new SolidColorBrush(m.Color); brush.Freeze();
        var pen = new Pen(brush, m.Width);
        if (m.Kind == Tool.Rectangle) dc.DrawRectangle(null, pen, m.Bounds);
        if (m.Kind == Tool.Number)
        {
            double radius = m.FontSize * .7;
            dc.DrawEllipse(brush, null, m.Bounds.TopLeft, radius, radius);
            var text = Text(m.Number.ToString(CultureInfo.InvariantCulture), m.FontSize, Brushes.White);
            dc.DrawText(text, new Point(m.Bounds.X - text.Width / 2, m.Bounds.Y - text.Height / 2));
        }
        if (m.Kind == Tool.Text) dc.DrawText(Text(m.Text, m.FontSize, brush), m.Bounds.TopLeft);
    }
    private static FormattedText Text(string text, double size, Brush brush) => new(text, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface("Malgun Gothic"), size, brush, 1);
    public static Rect HitBounds(Mark mark)
    {
        if (mark.Kind == Tool.Number) { double r = mark.FontSize * .7; return new Rect(mark.Bounds.X - r, mark.Bounds.Y - r, r * 2, r * 2); }
        if (mark.Kind == Tool.Text) { var t = Text(mark.Text, mark.FontSize, Brushes.Black); return new Rect(mark.Bounds.TopLeft, new Size(Math.Max(12, t.Width), Math.Max(12, t.Height))); }
        return mark.Bounds;
    }
    public static Int32Rect PixelRect(Rect rect, int width, int height)
    {
        int x = Math.Clamp((int)Math.Floor(rect.Left), 0, width - 1), y = Math.Clamp((int)Math.Floor(rect.Top), 0, height - 1);
        int right = Math.Clamp((int)Math.Ceiling(rect.Right), x + 1, width), bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), y + 1, height);
        return new(x, y, right - x, bottom - y);
    }
    public static BitmapSource Pixelate(BitmapSource source, Int32Rect rect, int block)
    {
        if (block < 1) throw new ArgumentOutOfRangeException(nameof(block));
        var normalized = Normalize(source); int stride = normalized.PixelWidth * 4;
        byte[] pixels = new byte[stride * normalized.PixelHeight]; normalized.CopyPixels(pixels, stride, 0);
        for (int y = rect.Y; y < rect.Y + rect.Height; y += block)
            for (int x = rect.X; x < rect.X + rect.Width; x += block)
            {
                int right = Math.Min(x + block, rect.X + rect.Width), bottom = Math.Min(y + block, rect.Y + rect.Height);
                long[] sum = new long[4]; int count = (right - x) * (bottom - y);
                for (int yy = y; yy < bottom; yy++) for (int xx = x; xx < right; xx++)
                    for (int c = 0; c < 4; c++) sum[c] += pixels[yy * stride + xx * 4 + c];
                for (int yy = y; yy < bottom; yy++) for (int xx = x; xx < right; xx++)
                    for (int c = 0; c < 4; c++) pixels[yy * stride + xx * 4 + c] = (byte)(sum[c] / count);
            }
        var output = BitmapSource.Create(normalized.PixelWidth, normalized.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        output.Freeze(); return output;
    }
    public string SaveNew(string directory)
    {
        string path = Path.Combine(directory, $"Clip-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.png");
        SavePng(Render(), path); MarkSaved(); return path;
    }
    public static void SavePng(BitmapSource bitmap, string path) => AtomicFile.Write(path, stream =>
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
    });
}
