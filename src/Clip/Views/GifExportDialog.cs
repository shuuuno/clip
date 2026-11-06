using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clip.Recording;

namespace Clip.Views;

public sealed class GifExportDialog : Window
{
    private readonly string source, directory;
    private readonly Action saved;
    private readonly TextBox start = Field("GIF 시작 시간"), end = Field("GIF 끝 시간"), width = Field("GIF 가로 크기");
    private readonly ComboBox fps = new() { ItemsSource = new[] { 5, 10, 15, 20, 25, 30 }, SelectedIndex = 2, Width = 245, Height = 32 };
    private readonly TextBlock info = Theme.Label("원본 영상 확인 중…", 12, Theme.Muted), estimate = Theme.Label("", 12, Theme.Muted), status = Theme.Label("", 12);
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 8, Margin = new Thickness(0, 14, 0, 14) };
    private readonly StackPanel fields = new();
    private readonly Button export, cancel, preview;
    private VideoInfo? video;
    private CancellationTokenSource? cancellation;
    private bool closeAfterFinish, closed;
    private string? lastSaved;
    public GifExportDialog(Window owner, string source, string directory, Action saved)
    {
        this.source = source; this.directory = directory; this.saved = saved;
        Owner = owner; Title = "Clip · GIF 내보내기"; Width = 530; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text;
        var root = new StackPanel { Margin = new Thickness(24) }; Content = root;
        var title = Theme.Label("GIF 내보내기", 24); title.FontWeight = FontWeights.SemiBold; root.Children.Add(title); info.Margin = new Thickness(0, 8, 0, 24); root.Children.Add(info);
        AddField("시작 시간 (초)", start); AddField("끝 시간 (초)", end); AddField("가로 크기 (px)", width); AddField("초당 프레임", fps);
        AutomationProperties.SetName(fps, "GIF 초당 프레임"); root.Children.Add(fields);
        estimate.Margin = new Thickness(0, 12, 0, 8); estimate.TextWrapping = TextWrapping.Wrap; root.Children.Add(estimate);
        var hint = Theme.Label("최대 30초 · 가로·세로 1280px 이내 · 비율 유지\n원본 MP4는 유지하고 별도 GIF로 저장합니다.", 12, Theme.Muted); root.Children.Add(hint);
        root.Children.Add(progress); status.TextWrapping = TextWrapping.Wrap; status.MaxWidth = 475; root.Children.Add(status);
        var buttons = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) }; root.Children.Add(buttons);
        export = Theme.Button("GIF 저장", () => _ = ExportAsync(), true); export.IsEnabled = false;
        cancel = Theme.Button("닫기", () => { if (cancellation is not null) Cancel(); else Close(); });
        preview = Theme.Button("GIF 보기", () => Dispatcher.BeginInvoke(() =>
        {
            if (lastSaved is null || closed) return;
            try { new GifWindow(this, lastSaved).ShowDialog(); } catch (Exception e) { status.Text = "GIF를 열지 못했습니다. " + e.Message; }
        })); preview.IsEnabled = false;
        buttons.Children.Add(export); buttons.Children.Add(cancel); buttons.Children.Add(preview);
        start.TextChanged += (_, _) => UpdateEstimate(); end.TextChanged += (_, _) => UpdateEstimate(); width.TextChanged += (_, _) => UpdateEstimate(); fps.SelectionChanged += (_, _) => UpdateEstimate();
        Loaded += async (_, _) =>
        {
            try
            {
                video = await GifExporter.ProbeAsync(source); if (closed) return;
                info.Text = $"{video.Width} × {video.Height}px · {video.Duration.TotalSeconds:0.###}초";
                start.Text = "0"; end.Text = Math.Min(10, Math.Floor(video.Duration.TotalMilliseconds) / 1000).ToString("0.###", CultureInfo.InvariantCulture);
                width.Text = Math.Max(1, Math.Min(640, Math.Min(video.Width, (int)Math.Floor(1280.0 * video.Width / video.Height)))).ToString(CultureInfo.InvariantCulture);
                UpdateEstimate();
            }
            catch (Exception e) { if (!closed) { info.Text = "원본 영상을 읽지 못했습니다."; status.Text = e.Message; } }
        };
        Closing += (_, e) => { if (cancellation is not null) { e.Cancel = true; closeAfterFinish = true; Cancel(); } };
        Closed += (_, _) => closed = true;
    }
    private static TextBox Field(string name)
    {
        var field = new TextBox { Width = 245, Height = 32, Padding = new Thickness(7, 4, 7, 4) }; AutomationProperties.SetName(field, name); return field;
    }
    private void AddField(string label, FrameworkElement control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var text = Theme.Label(label); text.Width = 175; row.Children.Add(text); row.Children.Add(control); fields.Children.Add(row);
    }
    private GifOptions Options()
    {
        if (!double.TryParse(start.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double first) ||
            !double.TryParse(end.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double last) ||
            !double.IsFinite(first) || !double.IsFinite(last) || first < 0 || last < 0 || first > 86400 * 365 || last > 86400 * 365 ||
            !int.TryParse(width.Text, out int pixels)) throw new ArgumentException("시간과 가로 크기에 올바른 숫자를 입력해 주세요. 소수점은 .을 사용합니다.");
        return new(TimeSpan.FromSeconds(first), TimeSpan.FromSeconds(last), pixels, (int)fps.SelectedItem);
    }
    private void UpdateEstimate()
    {
        if (video is null || closed) return;
        try
        {
            var plan = Options().Validate(video);
            estimate.Text = $"출력 {plan.Width} × {plan.Height}px · {plan.Centiseconds / 100.0:0.##}초 · {plan.Frames}프레임";
            export.IsEnabled = cancellation is null;
        }
        catch (Exception e) { estimate.Text = e.Message; export.IsEnabled = false; }
    }
    private async Task ExportAsync()
    {
        if (video is null || cancellation is not null) return;
        GifOptions options;
        try { options = Options(); options.Validate(video); } catch (Exception e) { status.Text = e.Message; return; }
        cancellation = new CancellationTokenSource(); var tokenSource = cancellation;
        fields.IsEnabled = false; export.IsEnabled = preview.IsEnabled = false; cancel.Content = "변환 취소"; progress.Value = 0; status.Text = "GIF로 변환 중…";
        try
        {
            var result = await GifExporter.ExportAsync(source, directory, options, new Progress<int>(value => { if (ReferenceEquals(cancellation, tokenSource)) progress.Value = value; }), tokenSource.Token);
            lastSaved = result.Path; progress.Value = 100; saved();
            status.Text = $"GIF 저장 완료 · {result.Bytes / 1024.0:0.#} KB\n{result.Path}";
        }
        catch (OperationCanceledException) { progress.Value = 0; status.Text = "변환을 취소했습니다. 원본과 기존 파일은 유지했습니다."; }
        catch (Exception e) { progress.Value = 0; status.Text = "GIF 저장 실패 · " + e.Message; }
        finally
        {
            cancellation = null; tokenSource.Dispose(); fields.IsEnabled = true; cancel.Content = "닫기"; cancel.IsEnabled = true;
            preview.IsEnabled = lastSaved is not null; UpdateEstimate();
            if (closeAfterFinish) Close();
        }
    }
    private void Cancel() { if (cancellation is null) return; cancellation.Cancel(); cancel.IsEnabled = false; status.Text = "변환 취소 중…"; }
    public void RequestClose()
    {
        // 변환 취소 버튼은 대화상자를 유지하고, 창 닫기·앱 종료는 정리 후 닫는다.
        if (cancellation is not null) { closeAfterFinish = true; Cancel(); } else Close();
    }
}
