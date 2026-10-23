using Microsoft.AspNetCore.Identity;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Authorization;

/// <summary>Identity's messages that staff see (passwords and user names), in the clinic's language.</summary>
public sealed class LocalizedIdentityErrors : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), Texts.T("The current password is incorrect."));
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), Texts.T("Passwords must be at least {0} characters.", length));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), Texts.T("Passwords must have at least one digit ('0'-'9')."));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), Texts.T("Passwords must have at least one lowercase letter ('a'-'z')."));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), Texts.T("Passwords must have at least one uppercase letter ('A'-'Z')."));
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), Texts.T("Passwords must have at least one symbol, such as # or !."));
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), Texts.T("Passwords must use at least {0} different characters.", uniqueChars));
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), Texts.T("The user name '{0}' is already taken.", userName));
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), Texts.T("The email '{0}' is already in use.", email));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), Texts.T("The user name '{0}' is invalid: use only letters, digits and - . _ @ +", userName));
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), Texts.T("The email '{0}' is invalid.", email));

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
