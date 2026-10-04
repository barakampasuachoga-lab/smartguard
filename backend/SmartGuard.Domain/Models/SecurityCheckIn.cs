namespace SmartGuard.Domain.Models;

public class SecurityCheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PropertyId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
