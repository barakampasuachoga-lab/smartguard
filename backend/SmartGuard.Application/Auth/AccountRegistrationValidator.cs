namespace SmartGuard.Application.Auth;

public sealed record AccountRegistrationValidationResult(
    bool IsValid,
    string FullName,
    string Email,
    string Role,
    string? ErrorMessage = null
);

public static class AccountRegistrationValidator
{
    public static AccountRegistrationValidationResult Validate(string fullName, string email, string password)
    {
        var trimmedName = fullName?.Trim() ?? string.Empty;
        var trimmedEmail = email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return new AccountRegistrationValidationResult(false, string.Empty, string.Empty, "Resident User", "Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(trimmedEmail) || !trimmedEmail.Contains('@'))
        {
            return new AccountRegistrationValidationResult(false, trimmedName, string.Empty, "Resident User", "Please enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return new AccountRegistrationValidationResult(false, trimmedName, trimmedEmail, "Resident User", "Password must be at least 8 characters long.");
        }

        return new AccountRegistrationValidationResult(true, trimmedName, trimmedEmail, "Resident User");
    }
}
