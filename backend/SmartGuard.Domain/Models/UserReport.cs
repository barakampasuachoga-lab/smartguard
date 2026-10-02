namespace SmartGuard.Domain.Models;

public class UserReport
{
    public int Id { get; set; }
    public int RecipientUserId { get; set; }
    public int SenderUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}