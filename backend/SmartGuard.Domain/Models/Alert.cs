namespace SmartGuard.Domain.Models;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SecurityEventId { get; set; }
    public string PropertyId { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertStatus Status { get; set; } = AlertStatus.Unread;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }

    public SecurityEvent? SecurityEvent { get; set; }
}
