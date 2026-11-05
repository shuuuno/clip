using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clip.Recording;

public sealed record VideoInfo(int Width, int Height, TimeSpan Duration);
public sealed record VideoFrame(long Timestamp, BitmapSource Image);

// Windows SDK IMFSourceReader / IMF2DBuffer ABI. 모든 COM 호출·해제는 생성 스레드에서 수행한다.
public sealed unsafe class VideoReader : IDisposable
{
    private const uint VideoStream = 0xfffffffc;
    private nint reader;
    private bool comStarted, mfStarted, configured;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private int width, height, stride;
    private byte[] pixels = Array.Empty<byte>();
    public VideoInfo Info { get; }
    public VideoReader(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("원본 MP4를 찾을 수 없습니다.", path);
        try
        {
            int hr = CoInitializeEx(0, 0); if (hr >= 0) comStarted = true; else if (hr != unchecked((int)0x80010106)) Check(hr);
            Check(MFStartup(0x20070, 0)); mfStarted = true;
            nint attributes = 0, type = 0;
            try
            {
                Check(MFCreateAttributes(out attributes, 1));
                SetUInt32(attributes, new("0f81da2c-b537-4672-a8b2-a681b17307a3"), 1);
                Check(MFCreateSourceReaderFromURL(path, attributes, out reader));
                Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)Slot(reader, 4))(reader, 0xfffffffe, 0));
                Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)Slot(reader, 4))(reader, VideoStream, 1));
                Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(reader, 6))(reader, VideoStream, &type));
                var size = Size(type);
                if (size.Width < 1 || size.Height < 1 || size.Width > 16384 || size.Height > 16384) throw new InvalidDataException("지원하지 않는 영상 크기입니다.");
                Guid key = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a"); Variant duration = default;
                try
                {
                    Check(((delegate* unmanaged[Stdcall]<nint, uint, Guid*, Variant*, int>)Slot(reader, 12))(reader, 0xffffffff, &key, &duration));
                    if (duration.Type is not (20 or 21) || duration.Value <= 0) throw new InvalidDataException("영상 재생 시간을 읽지 못했습니다.");
                    Info = new(size.Width, size.Height, TimeSpan.FromTicks(duration.Value));
                }
                finally { PropVariantClear(ref duration); }
            }
            finally { Release(ref type); Release(ref attributes); }
        }
        catch { Dispose(); throw; }
    }
    public void ConfigureOutput(int width, int height)
    {
        EnsureThread();
        if (width < 1 || height < 1 || width > 3840 || height > 3840) throw new ArgumentOutOfRangeException(nameof(width));
        nint type = 0;
        try
        {
            Check(MFCreateMediaType(out type));
            SetGuid(type, new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), new("73646976-0000-0010-8000-00aa00389b71"));
            SetGuid(type, new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"), new("00000016-0000-0010-8000-00aa00389b71"));
            SetUInt64(type, new("1652c33d-d6b2-4012-b834-72030849a37d"), ((ulong)width << 32) | (uint)height);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Slot(reader, 7))(reader, VideoStream, 0, type));
        }
        finally { Release(ref type); }
        this.width = width; this.height = height; UpdateFormat();
        pixels = new byte[checked(width * height * 4)]; configured = true;
    }
    private void UpdateFormat()
    {
        nint type = 0;
        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(reader, 6))(reader, VideoStream, &type));
            if (Size(type) != (width, height)) throw new InvalidDataException("디코더가 요청한 출력 크기를 제공하지 않습니다.");
            Guid key = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"); uint value;
            int hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, uint*, int>)Slot(type, 7))(type, &key, &value);
            stride = hr >= 0 ? unchecked((int)value) : -width * 4;
        }
        finally { Release(ref type); }
    }
    public void Seek(TimeSpan position)
    {
        EnsureThread(); if (position < TimeSpan.Zero || position >= Info.Duration) throw new ArgumentOutOfRangeException(nameof(position));
        Guid format = Guid.Empty; var value = new Variant { Type = 20, Value = position.Ticks };
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Variant*, int>)Slot(reader, 8))(reader, &format, &value));
    }
    public VideoFrame? Read(CancellationToken cancellation = default)
    {
        EnsureThread(); if (!configured) throw new InvalidOperationException("출력 크기를 먼저 지정해야 합니다.");
        while (true)
        {
            cancellation.ThrowIfCancellationRequested(); nint sample = 0, buffer = 0, twoD = 0;
            try
            {
                uint stream, flags; long time;
                Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, uint*, uint*, long*, nint*, int>)Slot(reader, 9))(reader, VideoStream, 0, &stream, &flags, &time, &sample));
                if ((flags & 1) != 0) throw new InvalidDataException("영상 디코딩 오류입니다.");
                if ((flags & 0x20) != 0) UpdateFormat();
                if (sample == 0) { if ((flags & 2) != 0) return null; continue; }
                Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(sample, 41))(sample, &buffer));
                Guid iid = new("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb");
                int qi = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(buffer, 0))(buffer, &iid, &twoD);
                if (qi >= 0)
                {
                    nint first; int pitch;
                    Check(((delegate* unmanaged[Stdcall]<nint, nint*, int*, int>)Slot(twoD, 3))(twoD, &first, &pitch));
                    try { CopyRows(first, pitch); }
                    finally { Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(twoD, 4))(twoD)); }
                }
                else
                {
                    nint data; uint length;
                    Check(((delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, null, &length));
                    try
                    {
                        if ((long)Math.Abs(stride) * height > length) throw new InvalidDataException("영상 버퍼가 필요한 크기보다 작습니다.");
                        CopyRows(stride < 0 ? data + (height - 1) * -stride : data, stride);
                    }
                    finally { Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(buffer, 4))(buffer)); }
                }
                var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4); bitmap.Freeze();
                return new(time, bitmap);
            }
            finally { Release(ref twoD); Release(ref buffer); Release(ref sample); }
        }
    }
    private void CopyRows(nint first, int pitch)
    {
        if (Math.Abs((long)pitch) < width * 4L || Math.Abs((long)pitch) > 1024 * 1024) throw new InvalidDataException("지원하지 않는 영상 행 간격입니다.");
        for (int y = 0; y < height; y++) Marshal.Copy(first + y * pitch, pixels, y * width * 4, width * 4);
    }
    private static (int Width, int Height) Size(nint type)
    {
        Guid key = new("1652c33d-d6b2-4012-b834-72030849a37d"); ulong value;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong*, int>)Slot(type, 8))(type, &key, &value));
        return ((int)(value >> 32), (int)(value & uint.MaxValue));
    }
    private void EnsureThread() { if (thread != Environment.CurrentManagedThreadId || reader == 0) throw new InvalidOperationException("디코더의 생성 스레드·수명이 올바르지 않습니다."); }
    private static nint Slot(nint p, int index) => (*(nint**)p)[index];
    private static void SetGuid(nint p, Guid key, Guid value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(p, 24))(p, &key, &value));
    private static void SetUInt32(nint p, Guid key, uint value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Slot(p, 21))(p, &key, value));
    private static void SetUInt64(nint p, Guid key, ulong value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)Slot(p, 22))(p, &key, value));
    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    private static void Release(ref nint p) { if (p != 0) { ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(p, 2))(p); p = 0; } }
    public void Dispose()
    {
        if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("디코더는 생성 스레드에서 해제해야 합니다.");
        Release(ref reader); if (mfStarted) { MFShutdown(); mfStarted = false; } if (comStarted) { CoUninitialize(); comStarted = false; }
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public long Value; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref Variant value);
    [DllImport("mfplat.dll")] private static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out nint attributes, uint size);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out nint type);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int MFCreateSourceReaderFromURL(string url, nint attributes, out nint reader);
}
