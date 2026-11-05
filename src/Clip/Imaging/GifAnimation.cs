using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace Clip.Imaging;

// 전체 프레임·불투명 GIF의 위치만 먼저 색인한다. WIC에는 한 프레임짜리 GIF만 전달한다.
// 큰 GIF의 모든 프레임을 WPF 디코더에 생성시키는 지연과 프레임별 렌더 캐시를 피한다.
public sealed class GifAnimation : IDisposable
{
    private readonly FileStream stream;
    private readonly byte[] header;
    private readonly List<(long Offset, int Length, int Delay)> frames = new();
    public int Width { get; }
    public int Height { get; }
    public int Count => frames.Count;
    public int Delay(int index) => frames[index].Delay;
    public GifAnimation(string path, bool firstFrameOnly = false)
    {
        stream = File.OpenRead(path);
        try
        {
            using var input = new BinaryReader(stream, Encoding.ASCII, true);
            byte[] start = Read(input, 13);
            string signature = Encoding.ASCII.GetString(start, 0, 6);
            if (signature is not ("GIF87a" or "GIF89a")) throw new InvalidDataException("GIF 파일이 아닙니다.");
            Width = start[6] | start[7] << 8; Height = start[8] | start[9] << 8;
            if (Width is < 1 or > 1280 || Height is < 1 or > 1280) throw new InvalidDataException("GIF 미리보기는 가로·세로 최대 1280px를 지원합니다.");
            int paletteLength = (start[10] & 0x80) != 0 ? 3 * (1 << ((start[10] & 7) + 1)) : 0;
            header = new byte[13 + paletteLength]; start.CopyTo(header, 0); Read(input, paletteLength).CopyTo(header, 13);
            int delay = 10; bool transparent = false;
            while (true)
            {
                byte block = input.ReadByte();
                if (block == 0x3b) break;
                if (block == 0x21)
                {
                    byte label = input.ReadByte();
                    if (label == 0xf9)
                    {
                        if (input.ReadByte() != 4) throw new InvalidDataException("GIF 제어 블록이 올바르지 않습니다.");
                        byte[] control = Read(input, 4); delay = Math.Max(2, control[1] | control[2] << 8); transparent = (control[0] & 1) != 0;
                        if (input.ReadByte() != 0) throw new InvalidDataException("GIF 제어 블록 종료 오류입니다.");
                    }
                    else SkipBlocks(input);
                    continue;
                }
                if (block != 0x2c) throw new InvalidDataException("GIF 프레임 블록 오류입니다.");
                long offset = stream.Position - 1; byte[] descriptor = Read(input, 9);
                if (transparent || descriptor[0] != 0 || descriptor[1] != 0 || descriptor[2] != 0 || descriptor[3] != 0 ||
                    (descriptor[4] | descriptor[5] << 8) != Width || (descriptor[6] | descriptor[7] << 8) != Height)
                    throw new InvalidDataException("부분 프레임·투명 GIF의 미리보기는 지원하지 않습니다.");
                if ((descriptor[8] & 0x80) != 0) Skip(3 * (1 << ((descriptor[8] & 7) + 1)));
                input.ReadByte(); SkipBlocks(input);
                long length = stream.Position - offset;
                if (length > 8 * 1024 * 1024 || frames.Count >= 900) throw new InvalidDataException("GIF 미리보기 범위를 넘었습니다.");
                frames.Add((offset, (int)length, delay)); delay = 10; transparent = false;
                if (firstFrameOnly) break;
            }
            if (Count == 0) throw new InvalidDataException("GIF 프레임이 없습니다.");
        }
        catch { stream.Dispose(); throw; }
    }
    public BitmapSource ReadFrame(int index, int thumbnailWidth = 0)
    {
        var frame = frames[index]; stream.Position = frame.Offset;
        using var encoded = new MemoryStream(header.Length + frame.Length + 1);
        encoded.Write(header); byte[] data = new byte[frame.Length]; stream.ReadExactly(data); encoded.Write(data); encoded.WriteByte(0x3b); encoded.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (thumbnailWidth > 0) image.DecodePixelWidth = Math.Min(thumbnailWidth, Width);
        image.StreamSource = encoded; image.EndInit(); image.Freeze(); return image;
    }
    private void SkipBlocks(BinaryReader input) { while (true) { byte length = input.ReadByte(); if (length == 0) return; Skip(length); } }
    private void Skip(int count) { if (stream.Position + count > stream.Length) throw new EndOfStreamException("GIF 파일이 잘렸습니다."); stream.Position += count; }
    private static byte[] Read(BinaryReader input, int count) { byte[] data = input.ReadBytes(count); if (data.Length != count) throw new EndOfStreamException(); return data; }
    public void Dispose() => stream.Dispose();
}
