using McfEditor.Models;

namespace McfEditor.UndoRedo;

public sealed class ImageReplacementAction : IUndoableAction
{
    private readonly McfImageEntry _entry;
    private readonly string? _oldReplacementPath;
    private readonly bool _oldIsModified;
    private readonly string? _newReplacementPath;
    private readonly bool _newIsModified;

    public string Description => $"Replace image {_entry.Index}";

    public ImageReplacementAction(
        McfImageEntry entry,
        string? oldReplacementPath,
        bool oldIsModified,
        string? newReplacementPath,
        bool newIsModified)
    {
        _entry = entry;
        _oldReplacementPath = oldReplacementPath;
        _oldIsModified = oldIsModified;
        _newReplacementPath = newReplacementPath;
        _newIsModified = newIsModified;
    }

    public void Undo()
    {
        _entry.ReplacementPath = _oldReplacementPath;
        _entry.IsModified = _oldIsModified;
    }

    public void Redo()
    {
        _entry.ReplacementPath = _newReplacementPath;
        _entry.IsModified = _newIsModified;
    }
}
