using System.Text.RegularExpressions;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Helpers;

/// <summary>
/// The API's password rules (Program.cs, Identity options), checked while typing so staff see what's missing before
/// saving. The API still has the last word.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 10;

    public static string Hint => Strings.T("At least {0} characters, with uppercase, lowercase, a number and a symbol.", MinLength);

    public static IEnumerable<string> Check(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            yield return Strings.T("Password is required");
            yield break;
        }
        if (password.Length < MinLength)
            yield return Strings.T("Password must be at least {0} characters long.", MinLength);
        if (!Regex.IsMatch(password, "[A-Z]"))
            yield return Strings.T("Password must contain at least one uppercase letter.");
        if (!Regex.IsMatch(password, "[a-z]"))
            yield return Strings.T("Password must contain at least one lowercase letter.");
        if (!Regex.IsMatch(password, "[0-9]"))
            yield return Strings.T("Password must contain at least one number.");
        if (!Regex.IsMatch(password, "[^a-zA-Z0-9]"))
            yield return Strings.T("Password must contain at least one symbol, such as # or !.");
    }
}
