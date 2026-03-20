using McfEditor.Models;
using System.IO;
using System.IO.Compression;

namespace McfEditor.IO;

public static class McfArchiveWriter
{
    public static CompressionReport Rebuild(
    string originalFile,
    string outputFile,
    string imagesDirectory,
    IProgress<ProgressInfo>? progress = null)
    {
        var data = File.ReadAllBytes(originalFile);

        int sizeOfToc = BitConverter.ToInt32(data, 32);
        int numFiles = BitConverter.ToInt32(data, 48);
        int offsetDataStart = sizeOfToc + 56;
        int offsetOriginal = offsetDataStart;
        int offsetNew = offsetDataStart;

        byte[] originalHeader = data[..48];

        using var tocStream = new MemoryStream();
        using var dataStream = new MemoryStream();

        for (int imageId = 0; imageId < numFiles; imageId++)
        {
            int originalZsize = BitConverter.ToInt32(data, offsetOriginal + 12);
            short originalWidth = BitConverter.ToInt16(data, offsetOriginal + 28);
            short originalHeight = BitConverter.ToInt16(data, offsetOriginal + 30);
            short originalImageMode = BitConverter.ToInt16(data, offsetOriginal + 32);

            string expectedMode = originalImageMode switch
            {
                4096 => "L",
                4356 => "RGBA",
                _ => throw new InvalidDataException($"Unsupported original image mode {originalImageMode} for image {imageId}.")
            };

            string pngPath = Path.Combine(imagesDirectory, $"img_{imageId}.png");
            if (!File.Exists(pngPath))
                throw new FileNotFoundException("Missing PNG file.", pngPath);

            var (raw, width, height, mode) = PngCodec.ReadPngAsRaw(pngPath, expectedMode);

            using var compressedStream = new MemoryStream();
            using (var z = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
                z.Write(raw, 0, raw.Length);

            byte[] imageZlib = compressedStream.ToArray();
            int zsize = imageZlib.Length;

            int mod = zsize % 4;
            if (mod == 3) imageZlib = imageZlib.Concat(new byte[1]).ToArray();
            else if (mod == 2) imageZlib = imageZlib.Concat(new byte[2]).ToArray();
            else if (mod == 1) imageZlib = imageZlib.Concat(new byte[3]).ToArray();

            zsize = imageZlib.Length;

            int fileId = imageId + 1;
            int always8 = 8;
            int always1 = 1;
            short always1Short = 1;
            short imageMode = mode == "L" ? (short)4096 : (short)4356;
            int maxPixelCount = mode == "L" ? width * height : width * height * 4;

            byte[] headerPart1 = new byte[24];
            WriteHeaderPart1(headerPart1, fileId, always8, zsize, maxPixelCount, always1);
            uint hash1 = Crc32(headerPart1);

            byte[] tail = new byte[8 + imageZlib.Length];
            BitConverter.GetBytes((short)width).CopyTo(tail, 0);
            BitConverter.GetBytes((short)height).CopyTo(tail, 2);
            BitConverter.GetBytes(imageMode).CopyTo(tail, 4);
            BitConverter.GetBytes(always1Short).CopyTo(tail, 6);
            Buffer.BlockCopy(imageZlib, 0, tail, 8, imageZlib.Length);
            uint hash2 = Crc32(tail);

            using (var bw = new BinaryWriter(dataStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write(new byte[] { (byte)'I', (byte)'M', (byte)'G', (byte)' ' });
                bw.Write(fileId);
                bw.Write(always8);
                bw.Write(zsize);
                bw.Write(maxPixelCount);
                bw.Write(always1);
                bw.Write(hash1);
                bw.Write((short)width);
                bw.Write((short)height);
                bw.Write(imageMode);
                bw.Write(always1Short);
                bw.Write(imageZlib);
                bw.Write(hash2);
            }

            using (var bw = new BinaryWriter(tocStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write(new byte[] { (byte)'I', (byte)'M', (byte)'G', (byte)' ' });
                bw.Write(fileId);
                bw.Write(offsetNew);
                bw.Write(zsize + 40);
            }

            double percent = numFiles <= 0 ? 0 : ((imageId + 1) * 100.0 / numFiles);
            progress?.Report(new ProgressInfo(
                $"Rebuilding image {imageId + 1}/{numFiles}...",
                percent));

            offsetOriginal += originalZsize + 40;
            offsetNew += zsize + 40;
        }

        byte[] structToc = tocStream.ToArray();
        byte[] structData = dataStream.ToArray();

        using var final = new MemoryStream();
        using (var bw = new BinaryWriter(final, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(originalHeader);
            bw.Write(numFiles);
            bw.Write(structToc);

            byte[] tocForCrc = new byte[4 + structToc.Length];
            BitConverter.GetBytes(numFiles).CopyTo(tocForCrc, 0);
            Buffer.BlockCopy(structToc, 0, tocForCrc, 4, structToc.Length);
            bw.Write(Crc32(tocForCrc));

            bw.Write(structData);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        File.WriteAllBytes(outputFile, final.ToArray());

        return new CompressionReport
        {
            Success = true,
            OriginalFile = Path.GetFullPath(originalFile),
            OutputFile = Path.GetFullPath(outputFile),
            ImagesDirectory = Path.GetFullPath(imagesDirectory),
            ImageCount = numFiles
        };
    }

    private static void WriteHeaderPart1(byte[] buffer, int fileId, int always8, int zsize, int maxPixelCount, int always1)
    {
        buffer[0] = (byte)'I';
        buffer[1] = (byte)'M';
        buffer[2] = (byte)'G';
        buffer[3] = (byte)' ';
        BitConverter.GetBytes(fileId).CopyTo(buffer, 4);
        BitConverter.GetBytes(always8).CopyTo(buffer, 8);
        BitConverter.GetBytes(zsize).CopyTo(buffer, 12);
        BitConverter.GetBytes(maxPixelCount).CopyTo(buffer, 16);
        BitConverter.GetBytes(always1).CopyTo(buffer, 20);
    }

    private static readonly uint[] CrcTable = CreateCrc32Table();

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte b in bytes)
        {
            uint index = (crc ^ b) & 0xFF;
            crc = (crc >> 8) ^ CrcTable[index];
        }

        return ~crc;
    }

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) == 1)
                    crc = 0xEDB88320 ^ (crc >> 1);
                else
                    crc >>= 1;
            }

            table[i] = crc;
        }

        return table;
    }
}
