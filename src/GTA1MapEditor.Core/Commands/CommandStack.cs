namespace GTA1MapEditor.Core.Commands;

/// <summary>
/// Unbounded undo/redo stack. Executing a new command after an undo discards
/// the redo history (standard editor behavior).
/// </summary>
public sealed class CommandStack
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? UndoDescription => _undo.TryPeek(out var c) ? c.Description : null;
    public string? RedoDescription => _redo.TryPeek(out var c) ? c.Description : null;

    public event Action? Changed;

    public void Execute(IEditCommand cmd, MapEditor editor)
    {
        cmd.Do(editor);
        _undo.Push(cmd);
        _redo.Clear();
        Changed?.Invoke();
    }

    public void Undo(MapEditor editor)
    {
        if (!CanUndo) return;
        var cmd = _undo.Pop();
        cmd.Undo(editor);
        _redo.Push(cmd);
        Changed?.Invoke();
    }

    public void Redo(MapEditor editor)
    {
        if (!CanRedo) return;
        var cmd = _redo.Pop();
        cmd.Do(editor);
        _undo.Push(cmd);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}
