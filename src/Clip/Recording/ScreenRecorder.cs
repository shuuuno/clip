using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Clip.Platform;

namespace Clip.Recording;

public sealed record RecordingResult(string Path, TimeSpan Duration, long Frames, int Width, int Height);

public sealed class ScreenRecorder : IDisposable
{
    public const int FramesPerSecond = 30;
    private readonly ManualResetEventSlim stop = new(false);
    private long elapsedTicks;
    private int started;
    public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref elapsedTicks));
    public Task<RecordingResult>? Completion { get; private set; }
    public static (int Width, int Height) EncodedSize(MonitorArea area)
    {
        if (area.Width < 2 || area.Height < 2 || area.Width > 3840 || area.Height > 3840)
            throw new ArgumentException("녹화 영역은 가로·세로 각각 2~3840px이어야 합니다.");
        int w = (area.Width + 1) & ~1, h = (area.Height + 1) & ~1;
        if ((long)w * h > 3840L * 2160) throw new ArgumentException("녹화 영역은 3840×2160 상당 크기 이하로 선택해 주세요.");
        return (w, h);
    }
    public Task<RecordingResult> Start(MonitorArea area, string directory, Action? ready = null)
    {
        var size = EncodedSize(area);
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("이미 시작한 녹화입니다.");
        string path = Path.Combine(directory, $"Clip-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.mp4");
        Completion = Task.Factory.StartNew(() => Record(area, size.Width, size.Height, path, ready), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return Completion;
    }
    public void RequestStop() => stop.Set();
    private RecordingResult Record(MonitorArea area, int width, int height, string path, Action? ready)
    {
        // 전용 MTA 작업 스레드 안에서 캡처·COM 인코더·버퍼의 수명을 모두 관리한다.
        using var writer = new Mp4Writer(path, width, height, FramesPerSecond);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        byte[] previous = new byte[checked(width * height * 4)], next = new byte[previous.Length];
        Capture(previous);
        var clock = Stopwatch.StartNew();
        long previousTime = 0;
        ready?.Invoke();
        while (!stop.IsSet)
        {
            long due = previousTime + TimeSpan.TicksPerSecond / FramesPerSecond;
            int delay = (int)Math.Max(0, (due - clock.Elapsed.Ticks) / TimeSpan.TicksPerMillisecond);
            if (stop.Wait(delay)) break;
            Capture(next);
            long timestamp = Math.Max(previousTime + 1, clock.Elapsed.Ticks);
            writer.WriteFrame(previous, previousTime, timestamp - previousTime);
            previousTime = timestamp; (previous, next) = (next, previous);
            Interlocked.Exchange(ref elapsedTicks, timestamp);
        }
        long end = Math.Max(previousTime + TimeSpan.TicksPerSecond / FramesPerSecond, clock.Elapsed.Ticks);
        writer.WriteFrame(previous, previousTime, end - previousTime);
        Interlocked.Exchange(ref elapsedTicks, end);
        return new(writer.Complete(), TimeSpan.FromTicks(end), writer.FramesWritten, width, height);

        void Capture(byte[] pixels)
        {
            graphics.CopyFromScreen(area.X, area.Y, 0, 0, new Size(area.Width, area.Height), CopyPixelOperation.SourceCopy);
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int y = 0; y < height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 4, width * 4);
            }
            finally { bitmap.UnlockBits(data); }
        }
    }
    public void Dispose()
    {
        if (Completion is { IsCompleted: false }) throw new InvalidOperationException("녹화 종료를 기다린 뒤 해제해야 합니다.");
        stop.Dispose();
    }
}
