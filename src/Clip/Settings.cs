using System;
using System.IO;
using System.Text.Json;

namespace Clip;

public sealed record ClipSettings(string CaptureDirectory, uint Modifiers = 6, uint KeyCode = 50, bool OpenAfterCapture = false);

public sealed class SettingsStore
{
    public string DataDirectory { get; }
    public string PathName => Path.Combine(DataDirectory, "settings.json");
    public string? Warning { get; private set; }
    public SettingsStore(string directory) => DataDirectory = Path.GetFullPath(directory);
    public ClipSettings Load()
    {
        Directory.CreateDirectory(DataDirectory);
        var defaults = new ClipSettings(Path.Combine(DataDirectory, "captures"));
        if (!File.Exists(PathName)) return defaults;
        try
        {
            var value = JsonSerializer.Deserialize<ClipSettings>(File.ReadAllText(PathName));
            if (value is null || string.IsNullOrWhiteSpace(value.CaptureDirectory) ||
                !Path.IsPathFullyQualified(value.CaptureDirectory) || value.Modifiers is 0 or > 15 ||
                value.KeyCode is < 1 or > 254) throw new InvalidDataException("설정 값이 올바르지 않습니다.");
            return value;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Warning = "설정을 읽지 못해 기본값으로 열었습니다. 기존 설정 파일은 유지했습니다.";
            return defaults;
        }
    }
    public void Save(ClipSettings settings) => AtomicFile.Write(PathName,
        stream => JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true }), true);

    public static string ResolveDataDirectory(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--data-dir") return Path.GetFullPath(args[i + 1]);
        // 설치판의 current 폴더는 업데이트 때 교체된다. 사용자 데이터는 그 밖에 둔다.
        if (Velopack.Locators.VelopackLocator.Current is { CurrentlyInstalledVersion: not null, IsPortable: false })
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clip", "data");
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            if (File.Exists(Path.Combine(folder.FullName, "AGENTS.md")) &&
                File.Exists(Path.Combine(folder.FullName, "src", "Clip", "Clip.csproj")))
                return Path.Combine(folder.FullName, "data");
            folder = folder.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "data");
    }
}

public static class AtomicFile
{
    public static void Write(string destination, Action<Stream> write, bool overwrite = false)
    {
        string full = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, ".clip-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(true);
            }
            File.Move(temp, full, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
