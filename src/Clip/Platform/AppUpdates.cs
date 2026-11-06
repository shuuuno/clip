using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Clip.Platform;

public sealed class AppUpdates : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    public static void ApplyPendingAtStartup(string[] args)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource("https://github.com/shuuuno/clip", null, false));
            if (manager.IsInstalled && !manager.IsPortable && manager.UpdatePendingRestart is { } pending)
                manager.ApplyUpdatesAndRestart(pending, args);
        }
        catch (Exception) { /* 적용 실패 시 기존 버전으로 실행하고 다음 시작 때 재시도한다. */ }
    }
    public async Task CheckAsync(Action<string> status)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource("https://github.com/shuuuno/clip", null, false));
            if (!manager.IsInstalled || manager.IsPortable) return;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null || cancellation.IsCancellationRequested) return;
            await manager.DownloadUpdatesAsync(update, cancelToken: cancellation.Token);
            if (!cancellation.IsCancellationRequested) status("새 버전을 받았습니다. Clip을 완전히 종료한 뒤 다시 실행하면 자동 적용됩니다.");
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested) status("업데이트를 확인하지 못했습니다. 다음 실행 때 다시 확인합니다.");
        }
    }
    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
}
