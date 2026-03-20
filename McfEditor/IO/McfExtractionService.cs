using McfEditor.Models;
using System.Text.Json;
using System.IO;

namespace McfEditor.IO;

public sealed class McfExtractionService
{
    private readonly PythonProcessRunner _runner = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExtractionManifest> ExtractAsync(
        string pythonExecutable,
        string scriptPath,
        string sourceFile,
        string outputFolder,
        bool parseImageIdMapAutomatically,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolder);

        var manifestPath = Path.Combine(outputFolder, "manifest.json");

        var result = await _runner.RunAsync(
            pythonExecutable,
            scriptPath,
            new[]
            {
                sourceFile,
                outputFolder,
                "--parse-idmap", parseImageIdMapAutomatically ? "auto" : "no",
                "--print-number", "no",
                "--json-report", manifestPath
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

        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Extraction manifest was not created.", manifestPath);

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<ExtractionManifest>(json, _jsonOptions)
                       ?? throw new InvalidOperationException("Unable to deserialize extraction manifest.");

        foreach (var entry in manifest.Entries)
        {
            entry.DisplayName = string.IsNullOrWhiteSpace(entry.MappedPath)
                ? entry.FileName
                : entry.MappedPath!;

            if (string.IsNullOrWhiteSpace(entry.RelativePath))
                entry.RelativePath = string.IsNullOrWhiteSpace(entry.MappedPath)
                    ? Path.Combine("Unsorted", entry.FileName)
                    : Path.Combine("Images", entry.MappedPath!);
        }

        return manifest;
    }
}
