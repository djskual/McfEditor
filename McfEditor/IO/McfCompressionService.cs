using McfEditor.Models;

namespace McfEditor.IO;

public sealed class McfCompressionService
{
    public Task<CompressionReport> RebuildAsync(
        string originalFile,
        string outputFile,
        string imagesDirectory,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return McfArchiveWriter.Rebuild(originalFile, outputFile, imagesDirectory, progress);
        }, cancellationToken);
    }
}
