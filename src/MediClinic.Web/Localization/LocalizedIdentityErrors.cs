using Microsoft.AspNetCore.Identity;
using MediClinic.Web.Controllers;

namespace MediClinic.Web.Localization;

public sealed class LocalizedIdentityErrors : IdentityErrorDescriber
{
    private static IdentityError Error(string code) => new() { Code = code, Description = TurkishIdentityErrors.Message(code) };
    public override IdentityError DefaultError() => Error(nameof(DefaultError));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed));
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName));
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail));
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName));
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail));
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName));
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName));
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword));
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled));
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole));
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole));
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort));
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars));
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));
}
