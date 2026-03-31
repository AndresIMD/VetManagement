namespace VetManagement.Api.Authorization;

/// <summary>
/// System-level claims for internal control.
/// </summary>
public static class SystemClaims
{
 public const string CLAIM_TYPE = "sys";
 public const string MUST_CHANGE_PASSWORD = "must-change-password";
}
