using McfEditor.Models;
using System.IO;
using System.IO.Compression;

namespace McfEditor.IO;

public static class McfArchiveReader
{
    public static ExtractionManifest Extract(
    string sourceFile,
    string outputFolder,
    bool useImageIdMap,
    IProgress<ProgressInfo>? progress = null)
    {
        Directory.CreateDirectory(outputFolder);

        var unsortedDir = Path.Combine(outputFolder, "Unsorted");
        var mappedRoot = Path.Combine(outputFolder, "Images");
        Directory.CreateDirectory(unsortedDir);

        var data = File.ReadAllBytes(sourceFile);

        if (data.Length < 64 || data[1] != (byte)'M' || data[2] != (byte)'C' || data[3] != (byte)'F')
            throw new InvalidDataException("This is not a correct MCF file.");

        int sizeOfToc = BitConverter.ToInt32(data, 32);
        int dataStart = sizeOfToc + 56;
        int numFiles = BitConverter.ToInt32(data, 48);

        ImageIdMapData? idMap = null;
        var warnings = new List<string>();

        var idMapPath = Path.Combine(Path.GetDirectoryName(sourceFile) ?? string.Empty, "imageidmap.res");
        if (useImageIdMap && File.Exists(idMapPath))
            idMap = ImageIdMapReader.Read(idMapPath);

        var entries = new List<McfImageEntry>();
        int offset = dataStart;

        for (int imageId = 0; imageId < numFiles; imageId++)
        {
            int zsize = BitConverter.ToInt32(data, offset + 12);
            short width = BitConverter.ToInt16(data, offset + 28);
            short height = BitConverter.ToInt16(data, offset + 30);
            short imageMode = BitConverter.ToInt16(data, offset + 32);

            int zlibDataOffset = offset + 36;
            var compressed = new byte[zsize];
            Buffer.BlockCopy(data, zlibDataOffset, compressed, 0, zsize);

            byte[] raw;
            using (var input = new MemoryStream(compressed))
            using (var z = new ZLibStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                z.CopyTo(output);
                raw = output.ToArray();
            }

            string modeName = imageMode switch
            {
                4096 => "L",
                4356 => "RGBA",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(modeName))
            {
                warnings.Add($"Unsupported image mode {imageMode} for image {imageId}; skipped.");
                offset += zsize + 40;
                continue;
            }

            string fileName = $"img_{imageId}.png";
            string extractedPath = Path.Combine(unsortedDir, fileName);
            PngCodec.SaveRawToPng(extractedPath, width, height, modeName, raw);

            string? mappedPath = null;
            if (idMap is not null)
            {
                for (int i = 0; i < idMap.ImageIds.Count; i++)
                {
                    if (idMap.ImageIds[i] != imageId)
                        continue;

                    mappedPath = idMap.Paths[i];
                    string mappedOutput = Path.Combine(mappedRoot, mappedPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(mappedOutput)!);
                    File.Copy(extractedPath, mappedOutput, overwrite: true);
                    break;
                }
            }

            string relativePath = mappedPath is null
                ? Path.Combine("Unsorted", fileName)
                : Path.Combine("Images", mappedPath);

            entries.Add(new McfImageEntry
            {
                Index = imageId,
                FileName = fileName,
                Width = width,
                Height = height,
                ImageMode = modeName,
                Offset = zlibDataOffset,
                CompressedSize = zsize,
                ExtractedPath = Path.GetFullPath(extractedPath),
                MappedPath = mappedPath,
                RelativePath = relativePath,
                DisplayName = string.IsNullOrWhiteSpace(mappedPath) ? fileName : mappedPath
            });

            double percent = numFiles <= 0 ? 0 : ((imageId + 1) * 100.0 / numFiles);
            progress?.Report(new ProgressInfo(
                $"Extracting image {imageId + 1}/{numFiles}...",
                percent));

            offset += zsize + 40;
        }

        return new ExtractionManifest
        {
            SourceFile = Path.GetFullPath(sourceFile),
            WorkingDirectory = Path.GetFullPath(outputFolder),
            ImageCount = entries.Count,
            ParseIdMapUsed = idMap is not null,
            Entries = entries,
            Warnings = warnings
        };
    }
}
