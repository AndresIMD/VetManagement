namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// The session's token in memory, for the whole app. The storage lives in JavaScript, and in the desktop app the HTTP
/// handlers run outside the web view's scope, where JavaScript can't: without this their calls would go out unsigned.
/// </summary>
public sealed class SessionToken
{
    public string? Value { get; set; }
}
