namespace SmartGuard.Domain.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentTransactionId { get; set; }
    public int UserId { get; set; }
    public long SubscriptionId { get; set; }
    public int AmountKes { get; set; }
    public string MpesaReceiptNumber { get; set; } = string.Empty;
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
}
