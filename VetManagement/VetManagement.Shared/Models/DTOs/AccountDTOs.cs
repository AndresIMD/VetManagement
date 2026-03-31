namespace VetManagement.Shared.Models.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}

public class UserInfo
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
