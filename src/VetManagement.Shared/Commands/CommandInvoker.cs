namespace VetManagement.Shared.Commands;

/// <summary>
/// Invokes commands, managing a history for undo/redo operations.
/// </summary>
public sealed class CommandInvoker
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public async Task ExecuteCommandAsync(ICommand command)
    {
        await command.ExecuteAsync();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public async Task UndoLastCommandAsync()
    {
        if (_undoStack.Count > 0)
        {
            var command = _undoStack.Pop();
            await command.UndoAsync();
            _redoStack.Push(command);
        }
    }

    public async Task RedoLastCommandAsync()
    {
        if (_redoStack.Count > 0)
        {
            var command = _redoStack.Pop();
            await command.ExecuteAsync();
            _undoStack.Push(command);
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public IEnumerable<ICommand> GetUndoHistory() => _undoStack;
}
