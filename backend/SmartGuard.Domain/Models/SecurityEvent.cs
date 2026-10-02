namespace SmartGuard.Domain.Models;

public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public string PropertyId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public SecurityEventType EventType { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
    public string Status { get; set; } = "New";
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
