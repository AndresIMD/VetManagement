namespace VetManagement.Api.Authorization;

/// <summary>
/// System-level claims for internal control.
/// </summary>
public static class SystemClaims
{
 public const string CLAIM_TYPE = "sys";
 public const string MUST_CHANGE_PASSWORD = "must-change-password";

 /// <summary>
 /// Claim type of the account's security stamp in a token. Identity gives the account a new stamp when its password
 /// changes or is reset, so tokens issued before stop working (see UserAccess).
 /// </summary>
 public const string SECURITY_STAMP = "sst";

 /// <summary>What a session with a temporary password may reach: its account (to change the password) and the realtime hubs.</summary>
 public static readonly string[] TEMPORARY_PASSWORD_PATHS = ["/api/account", "/hubs", "/healthz"];
}
