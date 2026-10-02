namespace SmartGuard.Application.Auth;

public sealed record PropertyRegistrationValidationResult(
    bool IsValid,
    string Name,
    string Address,
    string Owner,
    SmartGuard.Domain.Models.PropertyStatus Status,
    string? ErrorMessage = null
);

public static class PropertyRegistrationValidator
{
    public static PropertyRegistrationValidationResult Validate(string name, string address, string owner, SmartGuard.Domain.Models.PropertyStatus status)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedAddress = address?.Trim() ?? string.Empty;
        var trimmedOwner = owner?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return new PropertyRegistrationValidationResult(false, string.Empty, string.Empty, string.Empty, status, "Property name is required.");
        }

        if (string.IsNullOrWhiteSpace(trimmedAddress))
        {
            return new PropertyRegistrationValidationResult(false, trimmedName, string.Empty, trimmedOwner, status, "Property address is required.");
        }

        if (string.IsNullOrWhiteSpace(trimmedOwner))
        {
            return new PropertyRegistrationValidationResult(false, trimmedName, trimmedAddress, string.Empty, status, "Property owner is required.");
        }

        return new PropertyRegistrationValidationResult(true, trimmedName, trimmedAddress, trimmedOwner, status);
    }
}
