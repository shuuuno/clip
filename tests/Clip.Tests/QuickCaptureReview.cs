using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clip;
using Clip.Imaging;
using Clip.Platform;
using CommunityToolkit.WinUI.Notifications;

internal static class QuickCaptureReview
{
    // 제품에 QA 옵션을 넣지 않고 실제 앱의 캡처 완료·알림 수신 경로를 검증한다.
    public static int Run(string dataDirectory, string evidenceDirectory)
    {
        Directory.CreateDirectory(evidenceDirectory);
        IntPtr desktop = OpenInputDesktop(0, false, 0x0100);
        if (desktop == IntPtr.Zero)
        {
            File.WriteAllText(Path.Combine(evidenceDirectory, "integration.txt"), "확인 불가 · 입력 데스크톱에 접근할 수 없습니다. Windows 잠금을 해제한 뒤 다시 실행하세요.\n");
            return 2;
        }
        CloseDesktop(desktop);
        var application = new ClipApplication(new[] { "--startup", "--data-dir", dataDirectory });
        int result = 1;
        application.Startup += (_, _) => application.Dispatcher.BeginInvoke(async () =>
        {
            Window? fixture = null;
            try
            {
                var window = (Clip.Views.MainWindow)application.MainWindow;
                var complete = typeof(ClipApplication).GetMethod("CompleteCapture", BindingFlags.Instance | BindingFlags.NonPublic)!;
                fixture = new Window
                {
                    Title = "Clip 빠른 캡처 검증 화면", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                    WindowState = WindowState.Maximized, Background = new SolidColorBrush(Color.FromRgb(233, 238, 245)),
                    Content = new TextBlock { Text = "Clip 빠른 캡처 검증\n\n사용자 데이터 없는 합성 화면", FontSize = 40, Foreground = Brushes.DarkSlateBlue, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                fixture.Show();
                var bitmap = Pattern();
                complete.Invoke(application, new object[] { bitmap });
                await Task.Delay(1200);
                Check(!window.IsVisible, "첫 캡처 뒤 창 숨김");
                var files = Directory.GetFiles(application.Settings.CaptureDirectory, "*.png");
                Check(files.Length == 1, "첫 캡처 자동 저장");
                Check(Bytes(ImageDocument.Load(files[0])).SequenceEqual(Bytes(bitmap)), "저장 픽셀 일치");
                CaptureScreen(Path.Combine(evidenceDirectory, "01-notification.png"));
                BitmapSource? copied = null;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try { copied = Clipboard.GetImage(); break; }
                    catch (System.Runtime.InteropServices.COMException) { await Task.Delay(100); }
                }
                Check(copied is not null, "자동 클립보드 이미지");
                Check(Bytes(copied!).SequenceEqual(Bytes(bitmap)), "클립보드 픽셀 일치");
                // 실제 COM 수신 핸들러에 같은 인수를 전달한다. 물리 클릭 검증과는 구분한다.
                var notifications = (CaptureNotifications)typeof(ClipApplication).GetField("notifications", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(application)!;
                var args = (ToastNotificationActivatedEventArgsCompat)Activator.CreateInstance(typeof(ToastNotificationActivatedEventArgsCompat), true)!;
                typeof(ToastNotificationActivatedEventArgsCompat).GetProperty("Argument")!.SetValue(args, new ToastArguments { { "capture", files[0] } }.ToString());
                typeof(CaptureNotifications).GetMethod("Activated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(notifications, new object[] { args });
                await Task.Delay(300);
                Check(window.IsVisible && window.Document is { IsDirty: false }, "알림 수신 경로에서 저장 캡처 열기");
                var library = (ListBox)typeof(Clip.Views.MainWindow).GetField("library", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                Check(library.SelectedItem is Clip.Views.LibraryEntry entry && entry.Path == files[0], "알림 대상 선택");
                SaveWindow(window, Path.Combine(evidenceDirectory, "02-opened-capture.png"));
                window.Document!.Add(new(Tool.Number, new(40, 40, 0, 0), Colors.Red));
                var edited = window.Document; window.Hide(); fixture.Activate();
                complete.Invoke(application, new object[] { bitmap });
                complete.Invoke(application, new object[] { bitmap });
                await Task.Delay(900);
                Check(!window.IsVisible, "반복 캡처 뒤 창 숨김");
                Check(Directory.GetFiles(application.Settings.CaptureDirectory, "*.png").Length == 3, "반복 캡처 누적");
                Check(ReferenceEquals(edited, window.Document) && edited.IsDirty && edited.Marks.Count == 1, "기존 미저장 편집 보존");
                edited.MarkSaved();
                var error = application.ApplySettings(application.Settings with { OpenAfterCapture = true }, application.StartAtLogin);
                Check(error is null, "창 열기 설정 저장");
                complete.Invoke(application, new object[] { bitmap });
                await Task.Delay(300);
                Check(window.IsVisible, "창 열기 옵션 적용");
                Check(Directory.GetFiles(application.Settings.CaptureDirectory, "*.png").Length == 4, "창 열기 모드도 자동 저장");
                SaveWindow(window, Path.Combine(evidenceDirectory, "03-repeat-library.png"));
                var settings = new Clip.Views.SettingsDialog(window, application.Settings, application.StartAtLogin, application.ApplySettings);
                settings.Show(); await Task.Delay(200);
                SaveWindow(settings, Path.Combine(evidenceDirectory, "04-settings.png")); settings.Close();
                File.WriteAllText(Path.Combine(evidenceDirectory, "integration.txt"), "PASS 자동 저장·클립보드 픽셀·창 숨김·알림 수신 대상 선택·반복 누적·미저장 편집 보존·창 열기 옵션\n물리 단축키·실제 알림 클릭·콜드 스타트는 확인 불가\n");
                result = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(evidenceDirectory, "integration.txt"), error.ToString()); }
            finally
            {
                ToastNotificationManagerCompat.Uninstall();
                fixture?.Close();
                if (application.MainWindow is Clip.Views.MainWindow main) main.Document?.MarkSaved();
                application.Quit();
            }
        });
        application.Run(); return result;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    private static BitmapSource Pattern()
    {
        var pixels = new byte[480 * 320 * 4];
        for (int y = 0; y < 320; y++) for (int x = 0; x < 480; x++)
        { int i = (y * 480 + x) * 4; pixels[i] = (byte)(x / 2); pixels[i + 1] = (byte)(y / 2); pixels[i + 2] = 100; pixels[i + 3] = 255; }
        var bitmap = BitmapSource.Create(480, 320, 96, 96, PixelFormats.Bgra32, null, pixels, 480 * 4); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Bytes(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes;
    }
    private static void SaveWindow(Window window, string path)
    {
        var scale = VisualTreeHelper.GetDpi(window);
        var image = new RenderTargetBitmap((int)(window.ActualWidth * scale.DpiScaleX), (int)(window.ActualHeight * scale.DpiScaleY), scale.PixelsPerInchX, scale.PixelsPerInchY, PixelFormats.Pbgra32);
        image.Render(window); ImageDocument.SavePng(image, path);
    }
    private static void CaptureScreen(string path)
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        using var image = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
        using var graphics = System.Drawing.Graphics.FromImage(image);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
