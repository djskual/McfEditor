using System.IO;
using System.Text;

namespace McfEditor.IO;

public sealed record ImageIdMapData(
    IReadOnlyList<string> Paths,
    IReadOnlyList<int> ImageIds);

public static class ImageIdMapReader
{
    public static ImageIdMapData Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.Unicode);

        fs.Seek(12, SeekOrigin.Begin);
        var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (magic != "Skr0")
            throw new InvalidDataException("Invalid imageidmap.res header.");

        fs.Seek(24, SeekOrigin.Begin);
        var numPaths = br.ReadInt32();

        fs.Seek(32, SeekOrigin.Begin);

        var paths = new List<string>(numPaths);
        for (int i = 0; i < numPaths; i++)
        {
            int pathLenChars = br.ReadInt32();
            var pathBytes = br.ReadBytes(pathLenChars * 2);
            var parsedPath = Encoding.Unicode.GetString(pathBytes).Replace('/', Path.DirectorySeparatorChar);

            paths.Add(parsedPath);

            fs.Seek(4, SeekOrigin.Current);
        }

        int numIds = br.ReadInt32();
        var imageIds = new List<int>(numIds);

        for (int i = 0; i < numIds; i++)
        {
            int mifId = br.ReadInt32();
            imageIds.Add(mifId - 1);
        }

        return new ImageIdMapData(paths, imageIds);
    }
}
