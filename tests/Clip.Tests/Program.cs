using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip;
using Clip.Imaging;
using Clip.Platform;
using Clip.Views;
using Clip.Recording;

internal static class Program
{
    private static int passed;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 5 && args[0] == "--capture-stress") return CaptureStress(args);
        if (args.Length == 3 && args[0] == "--gif-stress") return GifStress(args);
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string work = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        try
        {
            Run("역방향 선택·1픽셀·화면 경계", () =>
            {
                Equal(new Int32Rect(10, 20, 90, 60), SelectionMath.FromPoints(new(100, 80), new(10, 20), 200, 100));
                Equal(new Int32Rect(199, 99, 1, 1), SelectionMath.FromPoints(new(200, 100), new(200, 100), 200, 100));
                var random = new Random(11);
                for (int i = 0; i < 1000; i++)
                {
                    var r = SelectionMath.FromPoints(new(random.Next(-100, 500), random.Next(-100, 400)), new(random.Next(-100, 500), random.Next(-100, 400)), 320, 240);
                    Check(r.X >= 0 && r.Y >= 0 && r.Width >= 1 && r.Height >= 1 && r.X + r.Width <= 320 && r.Y + r.Height <= 240, "유효 픽셀 경계");
                }
            });
            Run("방향키 이동·리사이즈 제한", () =>
            {
                var r = new Int32Rect(5, 5, 10, 10);
                Equal(new Int32Rect(4, 5, 10, 10), SelectionMath.Nudge(r, -1, 0, false, 20, 20));
                Equal(new Int32Rect(5, 5, 11, 10), SelectionMath.Nudge(r, 1, 0, true, 20, 20));
                Equal(new Int32Rect(10, 10, 10, 10), SelectionMath.Nudge(r, 500, 500, false, 20, 20));
                Equal(new Int32Rect(5, 5, 1, 1), SelectionMath.Nudge(r, -500, -500, true, 20, 20));
            });
            Run("모자이크 내부 평균·바깥 픽셀 보존", () =>
            {
                var bitmap = Pattern(8, 8); var before = Bytes(bitmap); var after = Bytes(ImageDocument.Pixelate(bitmap, new(2, 2, 4, 4), 2));
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) for (int c = 0; c < 4; c++)
                {
                    int index = (y * 8 + x) * 4 + c;
                    if (x < 2 || x >= 6 || y < 2 || y >= 6) Equal(before[index], after[index]);
                    else
                    {
                        int left = 2 + (x - 2) / 2 * 2, top = 2 + (y - 2) / 2 * 2, sum = 0;
                        for (int yy = top; yy < top + 2; yy++) for (int xx = left; xx < left + 2; xx++) sum += before[(yy * 8 + xx) * 4 + c];
                        Equal((byte)(sum / 4), after[index]);
                    }
                }
            });
            Run("주석 합성·원본 불변·Undo/Redo·저장 상태", () =>
            {
                var bitmap = Pattern(100, 80); var original = Bytes(bitmap); var doc = new ImageDocument(bitmap, true);
                doc.Add(new(Tool.Rectangle, new(10, 10, 60, 30), Colors.Red, 4));
                Check(!Bytes(doc.Render()).SequenceEqual(original), "주석 픽셀 반영"); Check(Bytes(doc.Base).SequenceEqual(original), "원본 불변");
                doc.MarkSaved(); Check(!doc.IsDirty, "저장 상태"); doc.Add(new(Tool.Number, new(50, 50, 0, 0), Colors.Blue));
                Equal(2, doc.NextNumber); Check(doc.IsDirty, "편집 상태"); doc.Undo(); Equal(1, doc.Marks.Count); Check(!doc.IsDirty, "저장 지점으로 복귀");
                doc.Redo(); Equal(2, doc.Marks.Count); Check(doc.IsDirty, "Redo 상태");
            });
            Run("크롭 합성 결과·좌표·원본/주석 복원", () =>
            {
                var doc = new ImageDocument(Pattern(100, 80), true); doc.Add(new(Tool.Rectangle, new(15, 15, 50, 40), Colors.Red, 3));
                var rendered = Bytes(doc.Render()); doc.Crop(new(10, 10, 60, 50)); Equal(60, doc.Width); Equal(50, doc.Height); Equal(0, doc.Marks.Count);
                var cropped = Bytes(doc.Render()); for (int y = 0; y < 50; y++) for (int x = 0; x < 60; x++) for (int c = 0; c < 4; c++) Equal(rendered[((y + 10) * 100 + x + 10) * 4 + c], cropped[(y * 60 + x) * 4 + c]);
                doc.Undo(); Equal(100, doc.Width); Equal(1, doc.Marks.Count); Check(Bytes(doc.Render()).SequenceEqual(rendered), "크롭 Undo 픽셀");
                Throws<ArgumentOutOfRangeException>(() => doc.Crop(new(99, 79, 2, 2)));
            });
            Run("가로·세로 띠 제거의 모든 경계·1픽셀·투명도 보존", () =>
            {
                int width = 9, height = 7; var raw = Bytes(Pattern(width, height));
                for (int i = 0; i < width * height; i++) raw[i * 4 + 3] = (byte)(i * 3);
                var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, raw, width * 4); source.Freeze();
                foreach (bool horizontal in new[] { true, false })
                {
                    int extent = horizontal ? height : width;
                    for (int start = 0; start < extent; start++) for (int length = 1; length <= extent - start && length < extent; length++)
                    {
                        var doc = new ImageDocument(source, false); doc.RemoveStrip(horizontal, start, length);
                        Equal(width - (horizontal ? 0 : length), doc.Width); Equal(height - (horizontal ? length : 0), doc.Height);
                        byte[] result = Bytes(doc.Render());
                        for (int y = 0; y < doc.Height; y++) for (int x = 0; x < doc.Width; x++) for (int c = 0; c < 4; c++)
                        {
                            int sx = !horizontal && x >= start ? x + length : x, sy = horizontal && y >= start ? y + length : y;
                            Equal(raw[(sy * width + sx) * 4 + c], result[(y * doc.Width + x) * 4 + c]);
                        }
                        Check(Bytes(source).SequenceEqual(raw), "입력 이미지 불변");
                    }
                }
            });
            Run("띠 제거 주석 합성·Undo/Redo·저장 지점·PNG 일치", () =>
            {
                var doc = new ImageDocument(Pattern(100, 80), false);
                doc.Add(new(Tool.Number, new(20, 20, 0, 0), Colors.Blue));
                doc.Add(new(Tool.Mosaic, new(10, 25, 60, 30), Colors.Red)); doc.MarkSaved();
                byte[] before = Bytes(doc.Render()); var originalBase = doc.Base;
                doc.RemoveStrip(true, 30, 20); Equal(0, doc.Marks.Count); Equal(1, doc.NextNumber); Check(doc.IsDirty, "띠 제거는 미저장 편집");
                byte[] after = Bytes(doc.Render());
                for (int y = 0; y < 60; y++) for (int x = 0; x < 100; x++) for (int c = 0; c < 4; c++)
                    Equal(before[((y < 30 ? y : y + 20) * 100 + x) * 4 + c], after[(y * 100 + x) * 4 + c]);
                doc.Undo(); Check(ReferenceEquals(originalBase, doc.Base), "원본 객체 복원"); Equal(2, doc.Marks.Count); Equal(2, doc.NextNumber);
                Check(!doc.IsDirty && Bytes(doc.Render()).SequenceEqual(before), "저장 지점·주석 복원");
                doc.Redo(); Check(Bytes(doc.Render()).SequenceEqual(after), "Redo 픽셀 일치");
                string saved = doc.SaveNew(work); Check(Bytes(ImageDocument.Load(saved)).SequenceEqual(after), "PNG 재열기 일치");
                doc.RemoveStrip(false, 40, 15); doc.Undo(); Check(!doc.IsDirty && Bytes(doc.Render()).SequenceEqual(after), "연속 제거 후 저장 지점 복귀");
            });
            Run("띠 제거 잘못된 구간·전체 제거 차단·상태 보존", () =>
            {
                var doc = new ImageDocument(Pattern(9, 7), false); byte[] before = Bytes(doc.Render());
                foreach (bool horizontal in new[] { true, false })
                {
                    int extent = horizontal ? 7 : 9;
                    foreach (var range in new[] { (-1, 1), (0, 0), (0, -1), (0, extent), (extent, 1), (extent - 1, 2), (int.MaxValue, 1), (1, int.MaxValue) })
                        Throws<ArgumentOutOfRangeException>(() => doc.RemoveStrip(horizontal, range.Item1, range.Item2));
                }
                Check(!doc.IsDirty && !doc.CanUndo && Bytes(doc.Render()).SequenceEqual(before), "실패 시 편집 불변");
                var tiny = new ImageDocument(Pattern(1, 1), false); Throws<ArgumentOutOfRangeException>(() => tiny.RemoveStrip(true, 0, 1));
            });
            Run("원자 저장 실패·기존 파일 보호·임시 파일 제거", () =>
            {
                string path = Path.Combine(work, "atomic.txt"); File.WriteAllText(path, "keep");
                Throws<IOException>(() => AtomicFile.Write(path, s => s.WriteByte(1))); Equal("keep", File.ReadAllText(path));
                Throws<InvalidOperationException>(() => AtomicFile.Write(Path.Combine(work, "failed.txt"), s => { s.WriteByte(1); throw new InvalidOperationException(); }));
                Check(!File.Exists(Path.Combine(work, "failed.txt")), "실패 결과 미노출"); Check(!Directory.EnumerateFiles(work, ".clip-*.tmp").Any(), "임시 파일 정리");
            });
            Run("PNG 독립 저장·재열기·동명 회피", () =>
            {
                var doc = new ImageDocument(Pattern(48, 32), true); doc.Add(new(Tool.Mosaic, new(0, 0, 20, 20), Colors.Red)); var pixels = Bytes(doc.Render());
                string a = doc.SaveNew(work), b = doc.SaveNew(work); Check(a != b, "새 경로"); Check(!doc.IsDirty, "저장 완료");
                Check(Bytes(ImageDocument.Load(a)).SequenceEqual(pixels), "PNG 픽셀 보존");
                File.Delete(a); Check(!File.Exists(a), "로더가 파일 잠금을 보유하지 않음");
                doc.Add(new(Tool.Number, new(10, 10, 0, 0), Colors.Red));
                string blocked = Path.Combine(work, "not-a-directory"); File.WriteAllText(blocked, "blocked");
                Throws<IOException>(() => doc.SaveNew(blocked)); Check(doc.IsDirty && doc.CanUndo, "저장 실패 후 편집 보존");
            });
            Run("손상 설정 보존·설정 재시작 유지", () =>
            {
                var store = new SettingsStore(Path.Combine(work, "config")); var defaults = store.Load(); File.WriteAllText(store.PathName, "broken");
                Equal(defaults, store.Load()); Equal("broken", File.ReadAllText(store.PathName)); Check(store.Warning is not null, "복구 안내");
                var expected = new ClipSettings(work, 6, 51); store.Save(expected); Equal(expected, new SettingsStore(store.DataDirectory).Load());
            });
            Run("전역 단축키 충돌·기존 등록 유지·해제", () =>
            {
                using var a = new HotkeyService(); using var b = new HotkeyService();
                Check(a.Set(3, 0x86), "Ctrl+Alt+F23 시험 등록"); Check(!b.Set(3, 0x86), "중복 충돌");
                Check(b.Set(3, 0x87), "다른 키 등록"); Check(!a.Set(3, 0x87), "변경 충돌"); Check(!b.Set(3, 0x86), "실패 후 기존 등록 유지");
            });
            Run("음수 모니터 원점·물리 픽셀 변환·녹화 짝수 패딩", () =>
            {
                var monitor = new MonitorArea(-2560, -1440, 2560, 1440);
                Equal(new Point(120, 80), SelectionMath.ScreenToLocal(new(-2440, -1360), monitor));
                Equal(new MonitorArea(-2440, -1360, 801, 451), SelectionMath.ToScreen(new(120, 80, 801, 451), monitor));
                Equal(new Point(120, 80), SelectionMath.ScreenToLocal(new(-2440.1, -1359.9), monitor));
                Throws<ArgumentOutOfRangeException>(() => SelectionMath.ToScreen(new(2500, 0, 100, 100), monitor));
                Equal((802, 452), ScreenRecorder.EncodedSize(new(-2440, -1360, 801, 451)));
                Throws<ArgumentException>(() => ScreenRecorder.EncodedSize(new(0, 0, 1, 1)));
                Throws<ArgumentException>(() => ScreenRecorder.EncodedSize(new(0, 0, 3840, 3840)));
            });
            Run("MP4 인코딩·가변 간격 타임스탬프·완료 전 미노출", () =>
            {
                string path = Path.Combine(work, "encoder-pattern.mp4");
                using var writer = new Mp4Writer(path, 320, 240);
                Check(!File.Exists(path), "완료 전 최종 경로 없음");
                byte[] pixels = new byte[320 * 240 * 4];
                long timestamp = 0;
                for (int frame = 0; frame < 60; frame++)
                {
                    for (int y = 0; y < 240; y++) for (int x = 0; x < 320; x++)
                    {
                        int at = (y * 320 + x) * 4; pixels[at] = y >= 120 ? (byte)255 : (byte)0;
                        pixels[at + 1] = x >= frame * 4 && x < frame * 4 + 20 ? (byte)255 : (byte)0;
                        pixels[at + 2] = y < 120 ? (byte)255 : (byte)0; pixels[at + 3] = 255;
                    }
                    long duration = frame < 30 ? TimeSpan.TicksPerSecond / 30 : TimeSpan.TicksPerSecond / 10;
                    writer.WriteFrame(pixels, timestamp, duration); timestamp += duration;
                }
                Throws<ArgumentException>(() => writer.WriteFrame(pixels, 0, 1));
                Equal(path, writer.Complete()); Equal(60L, writer.FramesWritten);
                Check(new FileInfo(path).Length > 1000, "완료 MP4 파일");
                Throws<IOException>(() => new Mp4Writer(path, 320, 240));
            });
            Run("미완료 녹화 정리·실패 경로·기존 파일 보호", () =>
            {
                string folder = Path.Combine(work, "abort"); Directory.CreateDirectory(folder);
                string destination = Path.Combine(folder, "empty.mp4");
                using (var writer = new Mp4Writer(destination, 320, 240))
                    Throws<InvalidOperationException>(() => writer.Complete());
                Check(!Directory.EnumerateFiles(folder).Any(), "미완료 임시 파일 정리");
                string blocked = Path.Combine(folder, "block"); File.WriteAllText(blocked, "keep");
                Throws<IOException>(() => new Mp4Writer(Path.Combine(blocked, "test.mp4"), 320, 240));
                Equal("keep", File.ReadAllText(blocked));
            });
            Run("녹화 완료 이동 실패 시 기존 파일·복구 파일 보존", () =>
            {
                string folder = Path.Combine(work, "move-failure"); Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "keep.mp4");
                using (var writer = new Mp4Writer(path, 320, 240))
                {
                    writer.WriteFrame(new byte[320 * 240 * 4], 0, TimeSpan.TicksPerSecond);
                    File.WriteAllText(path, "keep");
                    Throws<IOException>(() => writer.Complete());
                }
                Equal("keep", File.ReadAllText(path));
                Check(Directory.EnumerateFiles(folder, ".clip-recording-*.partial.mp4").Any(p => new FileInfo(p).Length > 1000), "완성된 복구 MP4 보존");
            });
            Run("4K 인코더 크기·프레임 입력", () =>
            {
                string path = Path.Combine(work, "encoder-4k.mp4");
                using var writer = new Mp4Writer(path, 3840, 2160);
                var pixels = new byte[3840 * 2160 * 4];
                for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 1] = 180; pixels[i + 3] = 255; }
                for (int frame = 0; frame < 3; frame++) writer.WriteFrame(pixels, frame * 1_000_000L, 1_000_000L);
                writer.Complete(); Check(new FileInfo(path).Length > 1000, "4K MP4 생성");
            });
            Run("MP4 디코더 메타데이터·축소·방향·구간 탐색", () =>
            {
                using var reader = new VideoReader(Path.Combine(work, "encoder-pattern.mp4"));
                Equal(320, reader.Info.Width); Equal(240, reader.Info.Height); Check(Math.Abs(reader.Info.Duration.TotalSeconds - 4) < .01, "영상 길이");
                reader.ConfigureOutput(160, 120); reader.Seek(TimeSpan.FromSeconds(2));
                VideoFrame? frame; do { frame = reader.Read(); } while (frame is not null && frame.Timestamp < TimeSpan.FromSeconds(2).Ticks);
                Check(frame is not null, "탐색 이후 프레임"); Equal(160, frame!.Image.PixelWidth);
                byte[] pixels = Bytes(frame.Image);
                Check(pixels[(10 * 160 + 140) * 4 + 2] > 220 && pixels[(110 * 160 + 140) * 4] > 220, "위 빨강·아래 파랑");
            });
            Run("GIF 15fps 시간 양자화·구간/크기 검증", () =>
            {
                var info = new VideoInfo(1920, 1080, TimeSpan.FromMinutes(1));
                foreach (int fps in new[] { 5, 10, 15, 20, 25, 30 }) foreach (double duration in new[] { .1, .15, .333, 1.01, 2.5, 30 })
                {
                    var plan = new GifOptions(TimeSpan.Zero, TimeSpan.FromSeconds(duration), 640, fps).Validate(info);
                    Equal(360, plan.Height); Equal(plan.Centiseconds, Enumerable.Range(0, plan.Frames).Sum(plan.Delay));
                    Check(Enumerable.Range(0, plan.Frames).All(i => plan.Delay(i) >= 2), "GIF 최소 지연");
                }
                Throws<ArgumentException>(() => new GifOptions(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), 640, 10).Validate(info));
                Throws<ArgumentException>(() => new GifOptions(TimeSpan.Zero, TimeSpan.FromSeconds(31), 640, 10).Validate(info));
                Throws<ArgumentException>(() => new GifOptions(TimeSpan.Zero, TimeSpan.FromSeconds(1), 1920, 10).Validate(info));
            });
            Run("MP4→GIF 구간·크기·다중 프레임·시간·원본 보존", () =>
            {
                string source = Path.Combine(work, "encoder-pattern.mp4"); byte[] before = File.ReadAllBytes(source);
                var result = GifExporter.ExportAsync(source, Path.Combine(work, "gif"), new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), 160, 15), null, default).GetAwaiter().GetResult();
                Equal(30, result.Frames); Equal(TimeSpan.FromSeconds(2), result.Duration);
                using var stream = File.OpenRead(result.Path); var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Equal(30, decoder.Frames.Count); Equal(160, decoder.Frames[0].PixelWidth); Equal(120, decoder.Frames[0].PixelHeight);
                Equal(200, decoder.Frames.Sum(f => Convert.ToInt32(((BitmapMetadata)f.Metadata).GetQuery("/grctlext/Delay"))));
                Check(!Bytes(decoder.Frames[0]).SequenceEqual(Bytes(decoder.Frames[^1])), "시간에 따른 프레임 변화");
                Check(before.SequenceEqual(File.ReadAllBytes(source)), "MP4 원본 불변");
                File.WriteAllText(Path.Combine(work, "gif-result-path.txt"), result.Path);
            });
            Run("GIF 변환 중 취소·임시 파일 정리·기존 파일 유지", () =>
            {
                string folder = Path.Combine(work, "gif-cancel"); Directory.CreateDirectory(folder); string keep = Path.Combine(folder, "keep.gif"); File.WriteAllText(keep, "keep");
                using var cancel = new CancellationTokenSource();
                Throws<OperationCanceledException>(() => GifExporter.Export(Path.Combine(work, "encoder-pattern.mp4"), folder, new(TimeSpan.Zero, TimeSpan.FromSeconds(4), 160, 30), new InlineProgress(i => { if (i >= 10) cancel.Cancel(); }), cancel.Token));
                Equal("keep", File.ReadAllText(keep)); Equal(1, Directory.EnumerateFiles(folder).Count());
                string blocked = Path.Combine(folder, "blocked"); File.WriteAllText(blocked, "keep");
                Throws<IOException>(() => GifExporter.Export(Path.Combine(work, "encoder-pattern.mp4"), blocked, new(TimeSpan.Zero, TimeSpan.FromSeconds(1), 160, 10)));
                Equal("keep", File.ReadAllText(blocked));
            });
            Run("GIF 홀수 크기·짧은 구간·손상 원본 실패", () =>
            {
                var result = GifExporter.Export(Path.Combine(work, "encoder-pattern.mp4"), Path.Combine(work, "gif-odd"), new(TimeSpan.FromSeconds(3.7), TimeSpan.FromSeconds(3.85), 161, 10));
                Equal(161, result.Width); Equal(121, result.Height); Equal(TimeSpan.FromMilliseconds(150), result.Duration);
                string broken = Path.Combine(work, "broken.mp4"); File.WriteAllText(broken, "invalid");
                Throws<Exception>(() => GifExporter.Export(broken, Path.Combine(work, "gif-broken"), new(TimeSpan.Zero, TimeSpan.FromSeconds(1), 160, 10)));
                Check(!Directory.Exists(Path.Combine(work, "gif-broken")), "손상 원본에 결과 없음");
            });
            Run("GIF 프레임 색인·개별 디코딩·썸네일·손상 파일 거부", () =>
            {
                string path = File.ReadAllText(Path.Combine(work, "gif-result-path.txt"));
                using var animation = new GifAnimation(path); Equal(30, animation.Count); Equal(160, animation.Width); Equal(120, animation.Height);
                using var file = File.OpenRead(path); var decoder = new GifBitmapDecoder(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                foreach (int i in new[] { 0, 10, 29 }) Check(Bytes(animation.ReadFrame(i)).SequenceEqual(Bytes(decoder.Frames[i])), "개별 프레임 픽셀 일치");
                Equal(80, animation.ReadFrame(0, 80).PixelWidth); Equal(200, Enumerable.Range(0, animation.Count).Sum(animation.Delay));
                byte[] data = File.ReadAllBytes(path); string truncated = Path.Combine(work, "truncated.gif"); File.WriteAllBytes(truncated, data[..(data.Length / 2)]);
                Throws<EndOfStreamException>(() => new GifAnimation(truncated));
                string partial = Path.Combine(work, "partial-frame.gif"); data[Array.IndexOf(data, (byte)0x2c, 13) + 1] = 1; File.WriteAllBytes(partial, data);
                Throws<InvalidDataException>(() => new GifAnimation(partial));
            });
            Console.WriteLine($"PASS {passed}개 시나리오 · 증거 폴더: {work}"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private sealed class InlineProgress(Action<int> report) : IProgress<int> { public void Report(int value) => report(value); }
    private static int GifStress(string[] args)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess(); var clock = System.Diagnostics.Stopwatch.StartNew();
            long peak = 0;
            var task = GifExporter.ExportAsync(args[1], args[2], new(TimeSpan.Zero, TimeSpan.FromSeconds(30), 1280, 30), null, default);
            while (!task.IsCompleted) { process.Refresh(); peak = Math.Max(peak, process.WorkingSet64); Thread.Sleep(100); }
            var result = task.GetAwaiter().GetResult();
            var report = new { result, elapsed_ms = clock.ElapsedMilliseconds, peak_working_set_bytes = peak };
            string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2], "stress-results.json"), json); Console.WriteLine(json);
            return result.Frames == 900 && peak < 768L * 1024 * 1024 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flag);
    private static int CaptureStress(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        var region = new MonitorArea(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]));
        var process = System.Diagnostics.Process.GetCurrentProcess(); Native.Capture(region); GC.Collect(); GC.WaitForPendingFinalizers();
        int before = GetGuiResources(process.Handle, 0); var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) { var image = Native.Capture(region); if (image.PixelWidth != region.Width || image.PixelHeight != region.Height) return 1; }
        GC.Collect(); GC.WaitForPendingFinalizers(); int after = GetGuiResources(process.Handle, 0);
        Console.WriteLine($"캡처 100회: {region.Width}x{region.Height}, {timer.ElapsedMilliseconds}ms, GDI {before} -> {after}");
        return after <= before + 4 ? 0 : 1;
    }
    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"기대 {expected}, 실제 {actual}"); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception(typeof(T).Name + " 예외 없음"); }
    private static byte[] Bytes(BitmapSource source) { var image = ImageDocument.Normalize(source); byte[] bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes; }
    private static BitmapSource Pattern(int w, int h)
    {
        byte[] pixels = new byte[w * h * 4]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y * w + x) * 4; pixels[i] = (byte)(x * 3 % 256); pixels[i + 1] = (byte)(y * 5 % 256); pixels[i + 2] = (byte)((x + y) * 7 % 256); pixels[i + 3] = 255; }
        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); result.Freeze(); return result;
    }
}
