using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Clip.Imaging;

namespace Clip.Recording;

public sealed record GifOptions(TimeSpan Start, TimeSpan End, int Width, int FramesPerSecond)
{
    public GifPlan Validate(VideoInfo source)
    {
        var length = End - Start;
        if (Start < TimeSpan.Zero || End > source.Duration || length < TimeSpan.FromSeconds(.1) || length > TimeSpan.FromSeconds(30))
            throw new ArgumentException("원본 범위 안에서 0.1~30초 구간을 지정해 주세요.");
        if (FramesPerSecond is < 5 or > 30) throw new ArgumentException("초당 프레임 수는 5~30이어야 합니다.");
        int maximum = Math.Min(1280, Math.Min(source.Width, (int)Math.Floor(1280.0 * source.Width / source.Height)));
        if (maximum < 1) throw new ArgumentException("이 영상 비율은 GIF 최대 크기 안에서 축소할 수 없습니다.");
        if (Width < 1 || Width > maximum) throw new ArgumentException($"가로 크기는 1~{maximum}px로 지정해 주세요. 비율 유지, 확대 없음.");
        int height = Math.Max(1, (int)Math.Round(Width * (double)source.Height / source.Width));
        int centiseconds = (int)Math.Round(length.TotalMilliseconds / 10);
        int count = Math.Min((int)Math.Ceiling(length.TotalSeconds * FramesPerSecond - 1e-9), centiseconds / 2);
        return new(Width, height, Math.Max(1, count), centiseconds);
    }
}
public sealed record GifPlan(int Width, int Height, int Frames, int Centiseconds)
{
    public int Boundary(int index) => (int)Math.Round((double)index * Centiseconds / Frames);
    public int Delay(int index) => Boundary(index + 1) - Boundary(index);
}
public sealed record GifResult(string Path, int Width, int Height, int Frames, TimeSpan Duration, long Bytes);

public static class GifExporter
{
    public static Task<VideoInfo> ProbeAsync(string path) => Task.Run(() => { using var reader = new VideoReader(path); return reader.Info; });
    public static Task<GifResult> ExportAsync(string source, string directory, GifOptions options, IProgress<int>? progress, CancellationToken cancellation) =>
        Task.Factory.StartNew(() => Export(source, directory, options, progress, cancellation), cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    public static GifResult Export(string source, string directory, GifOptions options, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var reader = new VideoReader(source);
        var plan = options.Validate(reader.Info); reader.ConfigureOutput(plan.Width, plan.Height);
        reader.Seek(options.Start);
        string path = Path.Combine(directory, $"Clip-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.gif");
        AtomicFile.Write(path, stream =>
        {
            using var gif = new GifStreamWriter(stream, plan.Width, plan.Height);
            var current = reader.Read(cancellation) ?? throw new InvalidDataException("원본에 영상 프레임이 없습니다.");
            var next = reader.Read(cancellation);
            for (int i = 0; i < plan.Frames; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                long timestamp = options.Start.Ticks + plan.Boundary(i) * TimeSpan.TicksPerMillisecond * 10;
                while (next is not null && next.Timestamp <= timestamp) { current = next; next = reader.Read(cancellation); }
                gif.WriteFrame(current.Image, plan.Delay(i));
                progress?.Report((i + 1) * 100 / plan.Frames);
            }
            cancellation.ThrowIfCancellationRequested(); gif.Complete();
        });
        return new(path, plan.Width, plan.Height, plan.Frames, TimeSpan.FromMilliseconds(plan.Centiseconds * 10), new FileInfo(path).Length);
    }
}
