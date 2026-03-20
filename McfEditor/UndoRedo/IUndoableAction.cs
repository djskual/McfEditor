namespace McfEditor.UndoRedo;

public interface IUndoableAction
{
    string Description { get; }

    void Undo();
    void Redo();
}
