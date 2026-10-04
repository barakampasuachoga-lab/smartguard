namespace SmartGuard.Domain.Models;

public class UserSubscription
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = SubscriptionStatuses.Trial;
    public DateTimeOffset? TrialStart { get; set; }
    public DateTimeOffset? TrialEnd { get; set; }
    public DateTimeOffset? CurrentPeriodStart { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SubscriptionStatuses
{
    public const string Trial = "TRIAL";
    public const string Active = "ACTIVE";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PastDue = "PAST_DUE";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}
