using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Velopack;
using Velopack.Locators;

internal static class UpdateReview
{
    public static int Run(string feedDirectory, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        try
        {
            var locator = new TestVelopackLocator("Clip", "0.6.0", Path.Combine(workDirectory, "packages"));
            var manager = new UpdateManager(feedDirectory, locator: locator);
            var update = manager.CheckForUpdatesAsync().GetAwaiter().GetResult() ?? throw new InvalidOperationException("새 버전을 찾지 못했습니다.");
            manager.DownloadUpdatesAsync(update).GetAwaiter().GetResult();
            var pending = manager.UpdatePendingRestart ?? throw new InvalidOperationException("재시작 대기 패키지가 없습니다.");
            Check(pending.Version == update.TargetFullRelease.Version, "다운로드 버전 일치");
            string downloaded = Path.Combine(locator.PackagesDir!, pending.FileName);
            Check(File.Exists(downloaded), "실제 nupkg 다운로드");
            using (var stream = File.OpenRead(downloaded))
                Check(Convert.ToHexString(SHA256.HashData(stream)).Equals(update.TargetFullRelease.SHA256, StringComparison.OrdinalIgnoreCase), "SHA256 일치");
            var current = new UpdateManager(feedDirectory, locator: new TestVelopackLocator("Clip", pending.Version.ToString(), Path.Combine(workDirectory, "current")));
            Check(current.CheckForUpdatesAsync().GetAwaiter().GetResult() is null, "최신 버전은 업데이트 없음");
            var brokenFeed = Path.Combine(workDirectory, "broken-feed"); Directory.CreateDirectory(brokenFeed);
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(feedDirectory, "releases.win.json")))!;
            foreach (var asset in json["Assets"]!.AsArray())
            {
                asset!["SHA256"] = new string('0', 64);
                asset["SHA1"] = new string('0', 40);
                File.Copy(Path.Combine(feedDirectory, asset["FileName"]!.GetValue<string>()), Path.Combine(brokenFeed, asset["FileName"]!.GetValue<string>()));
            }
            File.WriteAllText(Path.Combine(brokenFeed, "releases.win.json"), json.ToJsonString());
            var broken = new UpdateManager(brokenFeed, locator: new TestVelopackLocator("Clip", "0.6.0", Path.Combine(workDirectory, "rejected")));
            var badUpdate = broken.CheckForUpdatesAsync().GetAwaiter().GetResult()!;
            bool rejected = false;
            try { broken.DownloadUpdatesAsync(badUpdate).GetAwaiter().GetResult(); }
            catch (Velopack.Exceptions.ChecksumFailedException) { rejected = true; }
            Check(rejected && broken.UpdatePendingRestart is null, "손상 해시 패키지 적용 차단");
            File.WriteAllText(Path.Combine(workDirectory, "result.txt"), $"PASS 로컬 피드 0.6.0 → {pending.Version} 탐지·다운로드·SHA256·다음 시작 대기\nPASS 최신 버전은 업데이트 없음\nPASS 해시 불일치 패키지 거부\n실제 설치·재시작 교체·GitHub Release 전달은 별도 검증 필요\n");
            Console.WriteLine(File.ReadAllText(Path.Combine(workDirectory, "result.txt"))); return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(workDirectory, "result.txt"), error.ToString()); Console.Error.WriteLine(error); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
