namespace VetManagement.Staff.UI.Commands;

public interface ICommand
{
    Task ExecuteAsync();

    Task UndoAsync();

    string Description { get; }

    DateTime Timestamp { get; }

    string? PerformedBy { get; }
}
