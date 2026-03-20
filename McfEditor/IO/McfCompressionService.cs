using McfEditor.Models;

namespace McfEditor.IO;

public sealed class McfCompressionService
{
    public Task<CompressionReport> RebuildAsync(
        string originalFile,
        string outputFile,
        string imagesDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var report = McfArchiveWriter.Rebuild(originalFile, outputFile, imagesDirectory);
        return Task.FromResult(report);
    }
}
