using System;
using System.IO;
using Microsoft.Win32;

namespace Clip.Platform;

public sealed record StartupEntry(object Value, RegistryValueKind Kind);

/// <summary>현재 사용자 Run 항목 하나만 관리한다. 조회나 앱 시작만으로 등록하지 않는다.</summary>
public sealed class StartupRegistration
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "Clip.Capture";
    private readonly string keyPath;
    public string Command { get; }
    public StartupRegistration(string executable, string dataDirectory, string keyPath = RunKey)
    {
        if (!Path.IsPathFullyQualified(executable) || !Path.IsPathFullyQualified(dataDirectory))
            throw new ArgumentException("자동 실행에는 전체 경로가 필요합니다.");
        this.keyPath = keyPath;
        Command = Quote(Path.GetFullPath(executable)) + " --startup --data-dir " + Quote(Path.GetFullPath(dataDirectory));
    }
    private static string Quote(string value)
    {
        if (value.Contains('"') || value.Contains('\0')) throw new ArgumentException("경로에 사용할 수 없는 문자가 있습니다.");
        // Windows 명령줄에서 닫는 따옴표 앞의 역슬래시는 두 번 쓴다.
        int trailing = 0; for (int i = value.Length - 1; i >= 0 && value[i] == '\\'; i--) trailing++;
        return "\"" + value + new string('\\', trailing) + "\"";
    }
    public StartupEntry? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        object? value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is null ? null : new(value, key!.GetValueKind(ValueName));
    }
    public bool Matches(StartupEntry? entry) => entry?.Value is string command && string.Equals(command, Command, StringComparison.OrdinalIgnoreCase);
    public bool IsEnabled => Matches(Read());
    public void SetEnabled(bool enabled)
    {
        var previous = Read();
        if (enabled)
        {
            if (Command.Length > 260) throw new IOException("자동 실행 경로가 너무 깁니다. Clip을 더 짧은 폴더로 옮긴 뒤 다시 켜 주세요.");
            if (!Matches(previous)) Restore(new(Command, RegistryValueKind.String));
        }
        else if (Matches(previous)) Restore(null); // 다른 위치의 Clip 등록은 이 복사본에서 삭제하지 않는다.
    }
    public void Restore(StartupEntry? entry)
    {
        if (entry is null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true); key?.DeleteValue(ValueName, false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath); key.SetValue(ValueName, entry.Value, entry.Kind);
        }
    }
}
