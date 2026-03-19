using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace McfEditor.Models;

public sealed class McfImageEntry : INotifyPropertyChanged
{
    private string? _replacementPath;
    private bool _isModified;
    private string _displayName = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("imageMode")]
    public string ImageMode { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("zsize")]
    public int CompressedSize { get; set; }

    [JsonPropertyName("extractedPath")]
    public string ExtractedPath { get; set; } = string.Empty;

    [JsonPropertyName("mappedPath")]
    public string? MappedPath { get; set; }

    [JsonIgnore]
    public string? ReplacementPath
    {
        get => _replacementPath;
        set
        {
            if (_replacementPath == value)
                return;

            _replacementPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewPath));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    [JsonIgnore]
    public bool IsModified
    {
        get => _isModified;
        set
        {
            if (_isModified == value)
                return;

            _isModified = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    [JsonIgnore]
    public string DisplayName
    {
        get => string.IsNullOrWhiteSpace(_displayName) ? $"img_{Index}.png" : _displayName;
        set
        {
            if (_displayName == value)
                return;

            _displayName = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string PreviewPath => ReplacementPath ?? ExtractedPath;

    [JsonIgnore]
    public string StatusText => IsModified ? "Modified" : "Original";

    [JsonIgnore]
    public string ModeSummary => $"{ImageMode} - {Width}x{Height}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
