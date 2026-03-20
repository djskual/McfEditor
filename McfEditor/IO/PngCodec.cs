using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace McfEditor.IO;

public static class PngCodec
{
    public static void SaveRawToPng(string outputPath, int width, int height, string mode, byte[] raw)
    {
        BitmapSource bitmap;

        if (mode == "L")
        {
            int stride = width;
            bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Gray8,
                null,
                raw,
                stride);
        }
        else if (mode == "RGBA")
        {
            var bgra = ConvertRgbaToBgra(raw);
            int stride = width * 4;
            bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                bgra,
                stride);
        }
        else
        {
            throw new NotSupportedException($"Unsupported image mode: {mode}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var fs = File.Create(outputPath);
        encoder.Save(fs);
    }

    public static (byte[] Raw, int Width, int Height, string Mode) ReadPngAsRaw(string pngPath, string expectedMode)
    {
        using var fs = File.OpenRead(pngPath);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        int width = frame.PixelWidth;
        int height = frame.PixelHeight;

        if (expectedMode == "L")
        {
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Gray8, null, 0);
            int stride = width;
            var raw = new byte[height * stride];
            converted.CopyPixels(raw, stride, 0);
            return (raw, width, height, "L");
        }

        if (expectedMode == "RGBA")
        {
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int stride = width * 4;
            var bgra = new byte[height * stride];
            converted.CopyPixels(bgra, stride, 0);
            return (ConvertBgraToRgba(bgra), width, height, "RGBA");
        }

        throw new NotSupportedException($"Unsupported image mode: {expectedMode}");
    }

    private static byte[] ConvertRgbaToBgra(byte[] rgba)
    {
        var bgra = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i + 0] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i + 0];
            bgra[i + 3] = rgba[i + 3];
        }
        return bgra;
    }

    private static byte[] ConvertBgraToRgba(byte[] bgra)
    {
        var rgba = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            rgba[i + 0] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i + 0];
            rgba[i + 3] = bgra[i + 3];
        }
        return rgba;
    }
}
