using McfEditor.Models;

namespace McfEditor.IO;

public sealed class McfExtractionService
{
    public Task<ExtractionManifest> ExtractAsync(
        string sourceFile,
        string outputFolder,
        bool useImageIdMap,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return McfArchiveReader.Extract(sourceFile, outputFolder, useImageIdMap, progress);
        }, cancellationToken);
    }
}
