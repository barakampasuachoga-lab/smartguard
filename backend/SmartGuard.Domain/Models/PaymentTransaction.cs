namespace SmartGuard.Domain.Models;

public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public long SubscriptionId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public int AmountKes { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Status { get; set; } = PaymentStatuses.Pending;
    public string? MerchantRequestId { get; set; }
    public string? CheckoutRequestId { get; set; }
    public string? ResponseDescription { get; set; }
    public int? ResultCode { get; set; }
    public string? MpesaReceiptNumber { get; set; }
    public DateTimeOffset InitiatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public static class PaymentStatuses
{
    public const string Pending = "PENDING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}
