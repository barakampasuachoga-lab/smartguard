namespace SmartGuard.Domain.Models;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public int UserId { get; set; }
    public long SubscriptionId { get; set; }
    public int AmountKes { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
}
