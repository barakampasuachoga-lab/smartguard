namespace SmartGuard.Domain.Models;

public class PasswordResetToken
{
    public string TokenHash { get; set; } = string.Empty;
    public int UserAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
