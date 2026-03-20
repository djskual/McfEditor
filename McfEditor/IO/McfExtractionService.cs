using McfEditor.Models;

namespace McfEditor.IO;

public sealed class McfExtractionService
{
    public Task<ExtractionManifest> ExtractAsync(
        string sourceFile,
        string outputFolder,
        bool useImageIdMap,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = McfArchiveReader.Extract(sourceFile, outputFolder, useImageIdMap);
        return Task.FromResult(manifest);
    }
}
