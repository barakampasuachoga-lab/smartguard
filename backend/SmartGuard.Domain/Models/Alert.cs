namespace SmartGuard.Domain.Models;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SecurityEventId { get; set; }
    public string PropertyId { get; set; } = string.Empty;
    public AlertPriority Priority { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AlertType { get; set; } = "Security anomaly";
    public AlertStatus Status { get; set; } = AlertStatus.Unread;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgementComment { get; set; }
    public int? ResolvedByUserId { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public string? EscalationDetails { get; set; }

    public SecurityEvent? SecurityEvent { get; set; }
}
