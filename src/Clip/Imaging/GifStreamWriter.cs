using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clip.Imaging;

// 각 프레임의 팔레트·LZW 압축은 Windows WIC에 맡기고 GIF89a 애니메이션 블록만 순서대로 쓴다.
// 전체 프레임을 GifBitmapEncoder.Frames에 쌓지 않아 긴 변환에서도 현재 프레임만 보유한다.
public sealed class GifStreamWriter : IDisposable
{
    private readonly BinaryWriter output;
    private readonly int width, height;
    private int frames;
    private bool completed;
    public GifStreamWriter(Stream stream, int width, int height)
    {
        if (width is < 1 or > 1280 || height is < 1 or > 1280) throw new ArgumentOutOfRangeException(nameof(width));
        this.width = width; this.height = height; output = new BinaryWriter(stream, Encoding.ASCII, true);
        output.Write("GIF89a"u8); output.Write((ushort)width); output.Write((ushort)height);
        output.Write(new byte[] { 0x70, 0, 0 }); // 프레임별 색상표를 사용한다.
        output.Write(new byte[] { 0x21, 0xff, 11 }); output.Write("NETSCAPE2.0"u8);
        output.Write(new byte[] { 3, 1, 0, 0, 0 }); // 무한 반복
    }
    public void WriteFrame(BitmapSource image, int delayCentiseconds)
    {
        if (completed) throw new InvalidOperationException("이미 완료한 GIF입니다.");
        if (image.PixelWidth != width || image.PixelHeight != height || delayCentiseconds is < 2 or > 65535) throw new ArgumentException("GIF 프레임 크기·시간이 올바르지 않습니다.");
        var palette = new BitmapPalette(image, 256);
        var indexed = new FormatConvertedBitmap(image, PixelFormats.Indexed8, palette, 0);
        var encoder = new GifBitmapEncoder { Palette = palette }; encoder.Frames.Add(BitmapFrame.Create(indexed));
        using var encoded = new MemoryStream(); encoder.Save(encoded); encoded.Position = 0;
        using var input = new BinaryReader(encoded);
        string signature = Encoding.ASCII.GetString(Read(input, 6));
        if (signature is not ("GIF87a" or "GIF89a")) throw new InvalidDataException("Windows GIF 인코더 출력이 올바르지 않습니다.");
        byte[] logical = Read(input, 7);
        byte[] global = (logical[4] & 0x80) != 0 ? Read(input, 3 * (1 << ((logical[4] & 7) + 1))) : Array.Empty<byte>();
        while (true)
        {
            byte block = input.ReadByte();
            if (block == 0x21) { input.ReadByte(); SkipBlocks(input); continue; }
            if (block != 0x2c) throw new InvalidDataException("GIF 프레임 블록을 찾지 못했습니다.");
            byte[] descriptor = Read(input, 9);
            if ((descriptor[4] | descriptor[5] << 8) != width || (descriptor[6] | descriptor[7] << 8) != height)
                throw new InvalidDataException("GIF 프레임 크기가 바뀌었습니다.");
            bool local = (descriptor[8] & 0x80) != 0;
            int sizeCode = (local ? descriptor[8] : logical[4]) & 7;
            byte[] colors = local ? Read(input, 3 * (1 << (sizeCode + 1))) : global;
            if (colors.Length == 0) throw new InvalidDataException("GIF 색상표가 없습니다.");
            output.Write(new byte[] { 0x21, 0xf9, 4, 4 }); // 이전 프레임 유지, 투명색 없음
            output.Write((ushort)delayCentiseconds); output.Write((byte)0); output.Write((byte)0);
            output.Write((byte)0x2c); output.Write((ushort)0); output.Write((ushort)0);
            output.Write((ushort)width); output.Write((ushort)height); output.Write((byte)(0x80 | (descriptor[8] & 0x40) | sizeCode));
            output.Write(colors); output.Write(input.ReadByte()); // LZW 최소 코드 크기
            while (true) { byte length = input.ReadByte(); output.Write(length); if (length == 0) break; output.Write(Read(input, length)); }
            frames++; return;
        }
    }
    public void Complete()
    {
        if (completed || frames == 0) throw new InvalidOperationException("GIF를 완료할 수 없습니다.");
        output.Write((byte)0x3b); output.Flush(); completed = true;
    }
    private static byte[] Read(BinaryReader input, int count) { var data = input.ReadBytes(count); if (data.Length != count) throw new EndOfStreamException(); return data; }
    private static void SkipBlocks(BinaryReader input) { while (true) { byte length = input.ReadByte(); if (length == 0) return; Read(input, length); } }
    public void Dispose() => output.Dispose();
}
