namespace VetManagement.Staff.UI.Services.App;

/// <summary>
/// A singleton service to manage and broadcast global application status,
/// particularly for handling critical, app-wide errors (e.g., bootstrap failures).
/// </summary>
public class AppStatusService
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string? ErrorTitle { get; private set; }

    public string? ErrorMessage { get; private set; }

    public event Action? OnChange;

    /// <summary>
    /// Sets the application to an error state.
    /// </summary>
    /// <param name="title">The title for the error.</param>
    /// <param name="message">The detailed error message.</param>
    public void SetError(string title, string message)
    {
        ErrorTitle = title;
        ErrorMessage = message;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears the current error state, returning the application to normal.
    /// </summary>
    public void Clear()
    {
        ErrorTitle = null;
        ErrorMessage = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
