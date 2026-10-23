namespace VetManagement.Contracts.Accounts;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    /// <summary>Blocked by an administrator: can't sign in (e.g. staff who left the clinic).</summary>
    public bool IsBlocked { get; set; }
}

public class CreateUserRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class EditUserRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>A temporary password set by an administrator; the user must change it at the next sign-in.</summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
