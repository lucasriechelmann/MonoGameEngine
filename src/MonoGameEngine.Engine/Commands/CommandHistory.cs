namespace MonoGameEngine.Engine.Commands;

/// <summary>An undoable edit.</summary>
public interface ICommand
{
    /// <summary>Short description shown in the Edit menu.</summary>
    string Name { get; }

    /// <summary>Applies the edit (also used for redo).</summary>
    void Do();

    /// <summary>Reverts the edit.</summary>
    void Undo();
}

/// <summary>Undo and redo stacks for <see cref="ICommand"/>s.</summary>
public sealed class CommandHistory(int capacity = 200)
{
    private readonly List<ICommand> _undo = new();
    private readonly List<ICommand> _redo = new();

    /// <summary>Raised whenever the stacks change.</summary>
    public event Action? Changed;

    /// <summary>True if there is something to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>True if there is something to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Name of the command <see cref="Undo"/> would revert.</summary>
    public string? UndoName => CanUndo ? _undo[^1].Name : null;

    /// <summary>Name of the command <see cref="Redo"/> would reapply.</summary>
    public string? RedoName => CanRedo ? _redo[^1].Name : null;

    /// <summary>Applies a command and records it.</summary>
    public void Execute(ICommand command)
    {
        command.Do();
        Record(command);
    }

    /// <summary>
    /// Records a command whose effect has already been applied (a gizmo drag or text edit that changed
    /// the value live). Clears the redo stack.
    /// </summary>
    public void Record(ICommand command)
    {
        _undo.Add(command);
        if (_undo.Count > capacity) _undo.RemoveAt(0);
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Reverts the most recent command.</summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo();
        _redo.Add(command);
        Changed?.Invoke();
    }

    /// <summary>Reapplies the most recently undone command.</summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Do();
        _undo.Add(command);
        Changed?.Invoke();
    }

    /// <summary>Forgets all history, for example when a different scene is loaded.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}
