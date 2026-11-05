using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Clip.Imaging;

namespace Clip.Platform;

public sealed record MonitorArea(int X, int Y, int Width, int Height);
public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    private delegate bool MonitorProc(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    public static Thickness MaximizedBorder(Window window)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        uint value = (uint)Math.Round(96 * dpi.DpiScaleX);
        int padding = GetSystemMetricsForDpi(92, value); // SM_CXPADDEDBORDER
        double x = (GetSystemMetricsForDpi(32, value) + padding) / dpi.DpiScaleX;
        double y = (GetSystemMetricsForDpi(33, value) + padding) / dpi.DpiScaleY;
        return new Thickness(x, y, x, y);
    }
    public static List<MonitorArea> Monitors()
    {
        var result = new List<MonitorArea>();
        MonitorProc callback = (IntPtr m, IntPtr d, ref RECT r, IntPtr p) => { result.Add(new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top)); return true; };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw new Win32Exception();
        return result;
    }
    public static System.Windows.Point CursorPosition()
    {
        GetCursorPos(out var p); return new(p.X, p.Y);
    }
    public static BitmapSource Capture(MonitorArea monitor)
    {
        using var bitmap = new Bitmap(monitor.Width, monitor.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(monitor.X, monitor.Y, 0, 0, new System.Drawing.Size(monitor.Width, monitor.Height), CopyPixelOperation.SourceCopy);
        IntPtr handle = bitmap.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return ImageDocument.Normalize(source);
        }
        finally { DeleteObject(handle); }
    }
    public static void Place(Window window, MonitorArea monitor)
    {
        if (!SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), monitor.X, monitor.Y, monitor.Width, monitor.Height, 0x0040))
            throw new Win32Exception();
    }
}

public sealed class HotkeyService : IDisposable
{
    private readonly HwndSource source;
    private int activeId;
    public event Action? Pressed;
    public HotkeyService()
    {
        source = new HwndSource(new HwndSourceParameters("Clip Hotkey") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        source.AddHook(Hook);
    }
    public bool Set(uint modifiers, uint key)
    {
        int next = activeId == 1 ? 2 : 1;
        if (!Native.RegisterHotKey(source.Handle, next, modifiers | 0x4000, key)) return false;
        if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId);
        activeId = next; return true;
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x0312 && (int)wparam == activeId) { handled = true; Pressed?.Invoke(); }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId);
        source.RemoveHook(Hook); source.Dispose();
    }
}
