using System.Text.Json.Serialization;

namespace McfEditor.Models;

public sealed class CompressionReport
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("originalFile")]
    public string OriginalFile { get; set; } = string.Empty;

    [JsonPropertyName("outputFile")]
    public string OutputFile { get; set; } = string.Empty;

    [JsonPropertyName("imagesDirectory")]
    public string ImagesDirectory { get; set; } = string.Empty;

    [JsonPropertyName("imageCount")]
    public int ImageCount { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
