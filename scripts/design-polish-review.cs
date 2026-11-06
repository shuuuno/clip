// 실제 제품 컨트롤을 렌더한다. 데스크톱 캡처나 마우스/키보드 입력 검증은 아니다.
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clip;
using Clip.Views;

internal static class Review
{
    private static string output = "";
    private static readonly List<string> checks = new();
    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        string data = Path.Combine(output, "data");
        if (Directory.Exists(data)) throw new IOException("새 검증 폴더를 지정하세요.");
        var application = new ClipApplication(new[] { "--data-dir", data });
        Theme.Apply(application);
        var main = new MainWindow(application);
        application.MainWindow = main;
        try
        {
            main.Show(); Pump();
            Check("빈 편집기의 복사·저장 비활성", !Button(main, "복사 · Ctrl+C").IsEnabled && !Button(main, "PNG 저장 · Ctrl+S").IsEnabled);
            Shot(main, "01-empty");
            var settings = new SettingsDialog(main, application.Settings, false, (_, _) => null);
            settings.Show(); Pump(); Shot(settings, "02-settings");
            Children<TextBox>(settings).Single().Text = "relative-folder";
            Click(Button(settings, "저장")); Pump();
            Check("잘못된 저장 경로 오류 안내", Children<TextBlock>(settings).Any(x => x.Text.Contains("전체 폴더 경로")));
            Shot(settings, "03-settings-error"); settings.Close();

            string folder = application.Settings.CaptureDirectory;
            Directory.CreateDirectory(folder);
            string imagePath = Path.Combine(folder, "화면 공유 체크리스트.png");
            Fixture(imagePath);
            main.RefreshLibrary(); Pump();
            Children<ListBox>(main).Single().SelectedIndex = 0; Pump();
            Check("라이브러리 선택으로 이미지 열기", main.Document?.Width == 1000 && main.Document.Height == 660);
            Click(Button(main, "박스  R")); Pump();
            Check("박스 도구 속성 상태", Combo(main, "색").IsEnabled && Combo(main, "굵기").IsEnabled && !Combo(main, "글자").IsEnabled);
            Shot(main, "04-editor");
            foreach (string name in new[] { "선택  V", "번호  N", "박스  R", "텍스트  T", "모자이크  M", "크롭  C", "가로 띠 제거  H", "세로 띠 제거  J" })
            {
                Click(Button(main, name)); Pump();
                Check(name + " 선택 상태", AutomationProperties.GetItemStatus(Button(main, name)) == "선택됨");
            }
            main.Width = 960; main.Height = 640; Pump();
            Inside(main, "설정", "이미지 열기", "영역 녹화", "새 캡처", "가로 띠 제거  H", "세로 띠 제거  J", "PNG 저장 · Ctrl+S", "복사 · Ctrl+C");
            Shot(main, "05-minimum");
            main.Width = 1220; main.Height = 800; Pump();
            var text = new TextDialog(main); text.Show(); Pump(); Shot(text, "06-text"); text.Close();
            main.Document!.Add(new(Clip.Imaging.Tool.Rectangle, new Rect(80, 180, 820, 65), Colors.Red, 3));
            Check("편집 후 저장 가능", main.SaveDocument()); Pump();
            Check("저장한 PNG 보존", Directory.GetFiles(folder, "*.png").Length == 2 && File.Exists(imagePath));
            Shot(main, "07-saved");
            bool focused = Button(main, "복사 · Ctrl+C").Focus(); Pump();
            bool keyboardFocused = Button(main, "복사 · Ctrl+C").IsKeyboardFocused;
            Shot(main, "08-focus");
            Check("실제 WPF 키보드 포커스 획득", focused && keyboardFocused);
            if (!args.Contains("--baseline"))
            {
                Brush Resource(string key) => (Brush)application.FindResource("Clip" + key);
                Contrast("입력 경계/Canvas", Resource("InputBorder"), Theme.Canvas, 3);
                Contrast("입력 경계/Elevated", Resource("InputBorder"), Theme.Elevated, 3);
                Contrast("포커스/Panel", Theme.AccentText, Theme.Panel, 3);
                Contrast("포커스/Background", Theme.AccentText, Theme.Background, 3);
                Contrast("주요 버튼 글자/Accent", Resource("AccentInk"), Theme.Accent, 4.5);
                Contrast("오류 글자/Panel", Resource("Error"), Theme.Panel, 4.5);
            }
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { method = "실제 WPF 컨트롤의 RenderTargetBitmap, 입력 주입 없음", dpi = VisualTreeHelper.GetDpi(main).PixelsPerInchX, checks }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS {checks.Count}개 WPF 렌더·상태 검사: {output}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { main.FinishClose(); application.Shutdown(); }
    }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }
    private static Button Button(Window window, string name) => Children<Button>(window).Single(x => AutomationProperties.GetName(x) == name || x.Content as string == name);
    private static ComboBox Combo(Window window, string name) => Children<ComboBox>(window).Single(x => AutomationProperties.GetName(x) == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
    private static void Contrast(string name, Brush foreground, Brush background, double minimum)
    {
        static double Luminance(Brush brush)
        {
            var c = ((SolidColorBrush)brush).Color;
            static double Linear(byte value) { double x = value / 255.0; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        double a = Luminance(foreground), b = Luminance(background), ratio = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        Check($"{name} 대비 {ratio:F2}:1 (기준 {minimum}:1)", ratio >= minimum);
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Inside(Window window, params string[] names)
    {
        var root = (FrameworkElement)VisualTreeHelper.GetParent((FrameworkElement)window.Content);
        var rectangles = names.Select(name => { var b = Button(window, name); return b.TransformToAncestor(root).TransformBounds(new Rect(b.RenderSize)); }).ToArray();
        Check("최소 창 버튼 경계", rectangles.All(x => x.Left >= 0 && x.Top >= 0 && x.Right <= root.ActualWidth && x.Bottom <= root.ActualHeight));
        for (int i = 0; i < rectangles.Length; i++)
            for (int j = i + 1; j < rectangles.Length; j++)
                Check($"최소 창 버튼 겹침 {i}/{j}", !rectangles[i].IntersectsWith(rectangles[j]));
    }
    private static void Shot(Window window, string name)
    {
        window.UpdateLayout(); Pump();
        var root = (FrameworkElement)VisualTreeHelper.GetParent((FrameworkElement)window.Content);
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var contentBitmap = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        contentBitmap.Render(root);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, new Rect(root.RenderSize));
            drawing.DrawImage(contentBitmap, new Rect(root.RenderSize));
        }
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name + ".png"))) png.Save(stream);
        var controls = Children<Control>(window).Where(x => x.IsVisible).Select(x => new { type = x.GetType().Name, name = AutomationProperties.GetName(x), content = (x as ContentControl)?.Content as string, enabled = x.IsEnabled, width = x.ActualWidth, height = x.ActualHeight });
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(controls, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Fixture(string path)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 252)), null, new Rect(0, 0, 1000, 660));
            void Text(string value, double size, double x, double y) => dc.DrawText(new FormattedText(value, System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface(Theme.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Brushes.DarkSlateGray, 1), new Point(x, y));
            Text("화면 공유 체크리스트", 32, 48, 38); Text("UI 검증용 합성 이미지", 18, 48, 96);
            int index = 0;
            foreach (string line in new[] { "필요한 부분에 번호와 박스 표시", "공유 전에 민감한 정보 가리기", "편집 결과를 복사하거나 PNG로 저장" })
            {
                double y = 190 + index++ * 130; Text(line, 25, 74, y); dc.DrawLine(new Pen(Brushes.LightSlateGray, 1), new Point(48, y + 74), new Point(952, y + 74));
            }
        }
        var bitmap = new RenderTargetBitmap(1000, 660, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); png.Save(stream);
    }
}
