using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Clip.Recording;

// Windows SDK의 mfobjects.h / mfreadwrite.h ABI를 사용하는 작은 상호 운용 경계.
// COM 포인터는 생성한 스레드에서만 사용하고, 호출마다 HRESULT를 확인한다.
public sealed unsafe class Mp4Writer : IDisposable
{
    private nint writer;
    private uint stream;
    private bool comStarted, mfStarted, completed;
    private readonly int ownerThread = Environment.CurrentManagedThreadId;
    private readonly int bytes;
    private readonly string destination, temporary;
    private long endTime;
    public long FramesWritten { get; private set; }
    public Mp4Writer(string destination, int width, int height, int fps = 30)
    {
        if (width < 2 || height < 2 || width > 3840 || height > 3840 || (long)width * height > 3840L * 2160 ||
            width % 2 != 0 || height % 2 != 0 || fps is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(width), "녹화 크기는 짝수 픽셀, 최대 3840×2160 상당 영역입니다.");
        this.destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(this.destination)!);
        if (File.Exists(this.destination)) throw new IOException("기존 녹화 파일을 덮어쓸 수 없습니다.");
        temporary = Path.Combine(Path.GetDirectoryName(this.destination)!, ".clip-recording-" + Guid.NewGuid().ToString("N") + ".partial.mp4");
        bytes = checked(width * height * 4);
        try
        {
            int hr = CoInitializeEx(0, 0);
            if (hr >= 0) comStarted = true;
            else if (hr != unchecked((int)0x80010106)) Check(hr); // 이미 STA인 시험 스레드도 허용.
            Check(MFStartup(0x20070, 0)); mfStarted = true;
            Check(MFCreateSinkWriterFromURL(temporary, 0, 0, out writer));
            nint output = 0, input = 0;
            try
            {
                Check(MFCreateMediaType(out output)); Configure(output, new Guid("34363248-0000-0010-8000-00aa00389b71"), width, height, fps);
                SetUInt32(output, new("20332624-fb0d-4d9e-bd0d-cbf6786c102e"), (uint)Math.Clamp((long)width * height * 6, 2_000_000, 40_000_000));
                SetUInt32(output, new("ad76a80b-2d5c-4e0b-b375-64e520137036"), 77); // H.264 Main
                uint index; Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Slot(writer, 3))(writer, output, &index)); stream = index;
                Check(MFCreateMediaType(out input)); Configure(input, new Guid("00000016-0000-0010-8000-00aa00389b71"), width, height, fps);
                SetUInt32(input, new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"), (uint)(width * 4)); // 위에서 아래로 저장한 RGB32
                Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Slot(writer, 4))(writer, stream, input, 0));
                Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 5))(writer));
            }
            finally { Release(ref input); Release(ref output); }
        }
        catch { Dispose(); throw; }
    }
    public void WriteFrame(byte[] bgra, long timestamp, long duration)
    {
        EnsureThread();
        if (writer == 0 || completed) throw new ObjectDisposedException(nameof(Mp4Writer));
        if (bgra.Length != bytes || timestamp < endTime || duration <= 0) throw new ArgumentException("프레임 크기 또는 시간이 올바르지 않습니다.");
        long nextEnd = checked(timestamp + duration);
        nint buffer = 0, sample = 0;
        try
        {
            Check(MFCreateMemoryBuffer((uint)bytes, out buffer));
            nint data; Check(((delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, null, null));
            try { Marshal.Copy(bgra, 0, data, bytes); }
            finally { Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(buffer, 4))(buffer)); }
            Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(buffer, 6))(buffer, (uint)bytes));
            Check(MFCreateSample(out sample));
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(sample, 42))(sample, buffer));
            Check(((delegate* unmanaged[Stdcall]<nint, long, int>)Slot(sample, 36))(sample, timestamp));
            Check(((delegate* unmanaged[Stdcall]<nint, long, int>)Slot(sample, 38))(sample, duration));
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(writer, 6))(writer, stream, sample));
            FramesWritten++; endTime = nextEnd;
        }
        finally { Release(ref sample); Release(ref buffer); }
    }
    public string Complete()
    {
        EnsureThread();
        if (writer == 0 || completed) throw new ObjectDisposedException(nameof(Mp4Writer));
        if (FramesWritten == 0) throw new InvalidOperationException("저장할 녹화 프레임이 없습니다.");
        Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 11))(writer));
        Release(ref writer);
        // 인코딩을 마친 파일은 최종 이동 실패 시에도 복구할 수 있게 보존한다.
        completed = true;
        try { File.Move(temporary, destination, false); }
        catch (Exception e) { throw new IOException("녹화 파일 이동 실패. 완성된 임시 파일: " + temporary, e); }
        return destination;
    }
    private static void Configure(nint type, Guid subtype, int width, int height, int fps)
    {
        SetGuid(type, new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), new("73646976-0000-0010-8000-00aa00389b71"));
        SetGuid(type, new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"), subtype);
        SetUInt32(type, new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd"), 2);
        SetUInt64(type, new("1652c33d-d6b2-4012-b834-72030849a37d"), ((ulong)width << 32) | (uint)height);
        SetUInt64(type, new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0"), ((ulong)fps << 32) | 1);
        SetUInt64(type, new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6"), (1UL << 32) | 1);
    }
    private void EnsureThread() { if (ownerThread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("인코더는 생성한 스레드에서만 사용할 수 있습니다."); }
    private static nint Slot(nint instance, int index) => (*(nint**)instance)[index];
    private static void SetGuid(nint p, Guid key, Guid value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(p, 24))(p, &key, &value));
    private static void SetUInt32(nint p, Guid key, uint value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Slot(p, 21))(p, &key, value));
    private static void SetUInt64(nint p, Guid key, ulong value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)Slot(p, 22))(p, &key, value));
    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    private static void Release(ref nint p) { if (p != 0) { ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(p, 2))(p); p = 0; } }
    public void Dispose()
    {
        EnsureThread(); Release(ref writer);
        if (mfStarted) { MFShutdown(); mfStarted = false; }
        if (comStarted) { CoUninitialize(); comStarted = false; }
        if (!completed && File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll")] private static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out nint type);
    [DllImport("mfplat.dll")] private static extern int MFCreateMemoryBuffer(uint length, out nint buffer);
    [DllImport("mfplat.dll")] private static extern int MFCreateSample(out nint sample);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MFCreateSinkWriterFromURL(string url, nint stream, nint attributes, out nint writer);
}
