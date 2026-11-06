using System;
using System.IO;
using CommunityToolkit.WinUI.Notifications;

namespace Clip.Platform;

public sealed class CaptureNotifications : IDisposable
{
    private readonly Action<string> open;
    public CaptureNotifications(Action<string> open)
    {
        this.open = open;
        ToastNotificationManagerCompat.OnActivated += Activated;
    }
    private void Activated(ToastNotificationActivatedEventArgsCompat args)
    {
        try
        {
            var values = ToastArguments.Parse(args.Argument);
            if (values.TryGetValue("capture", out string path) && Path.IsPathFullyQualified(path) &&
                Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)) open(path);
        }
        catch (Exception) { /* 잘못된 알림 인수는 열지 않는다. */ }
    }
    public void Show(string path, bool copied)
    {
        new ToastContentBuilder().AddArgument("capture", path)
            .AddText(copied ? "복사됐어요" : "저장됐어요 · 복사를 다시 시도해 주세요")
            .AddText("누르면 Clip에서 캡처를 엽니다.")
            .Show(toast => { toast.Tag = Guid.NewGuid().ToString("N")[..16]; toast.Group = "captures"; });
    }
    public void Dispose() => ToastNotificationManagerCompat.OnActivated -= Activated;
}
