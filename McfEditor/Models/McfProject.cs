using System.Collections.ObjectModel;

namespace McfEditor.Models;

public sealed class McfProject
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? OutputFilePath { get; set; }
    public bool IsDirty { get; set; }

    public ObservableCollection<McfImageEntry> Entries { get; } = new();
}
