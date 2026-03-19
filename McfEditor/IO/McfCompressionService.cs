using McfEditor.Models;
using System.Text.Json;
using System.IO;

namespace McfEditor.IO;

public sealed class McfCompressionService
{
    private readonly PythonProcessRunner _runner = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CompressionReport> RebuildAsync(
        string pythonExecutable,
        string scriptPath,
        string originalFile,
        string outputFile,
        string imagesDirectory,
        CancellationToken cancellationToken = default)
    {
        var reportPath = Path.Combine(Path.GetDirectoryName(outputFile) ?? imagesDirectory, "rebuild-report.json");

        var result = await _runner.RunAsync(
            pythonExecutable,
            scriptPath,
            new[]
            {
                originalFile,
                outputFile,
                imagesDirectory,
                "--json-report", reportPath
            },
            workingDirectory: Path.GetDirectoryName(scriptPath),
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError);
        }

        if (!File.Exists(reportPath))
            throw new FileNotFoundException("Compression report was not created.", reportPath);

        var json = await File.ReadAllTextAsync(reportPath, cancellationToken);
        return JsonSerializer.Deserialize<CompressionReport>(json, _jsonOptions)
               ?? throw new InvalidOperationException("Unable to deserialize compression report.");
    }
}
