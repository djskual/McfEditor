using System.Text.Json.Serialization;

namespace McfEditor.Models;

public sealed class ExtractionManifest
{
    [JsonPropertyName("sourceFile")]
    public string SourceFile { get; set; } = string.Empty;

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonPropertyName("imageCount")]
    public int ImageCount { get; set; }

    [JsonPropertyName("parseIdMapUsed")]
    public bool ParseIdMapUsed { get; set; }

    [JsonPropertyName("entries")]
    public List<McfImageEntry> Entries { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
