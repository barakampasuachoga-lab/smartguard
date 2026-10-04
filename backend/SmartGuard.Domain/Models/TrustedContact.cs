namespace SmartGuard.Domain.Models;

public class TrustedContact
{
    public int Id { get; set; }
    public string PropertyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Days { get; set; } = "Everyday";
    public string StartTime { get; set; } = "06:00";
    public string EndTime { get; set; } = "22:00";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
