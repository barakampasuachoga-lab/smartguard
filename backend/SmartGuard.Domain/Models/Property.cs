namespace SmartGuard.Domain.Models;

public class Property
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Owner { get; set; } = string.Empty;
    public int? OwnerUserId { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Home;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SecurityEvent> SecurityEvents { get; set; } = new List<SecurityEvent>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
