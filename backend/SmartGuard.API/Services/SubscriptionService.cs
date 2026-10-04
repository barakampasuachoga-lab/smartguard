using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Services;

public sealed class SubscriptionService(AppDbContext db)
{
    public static readonly SubscriptionPlan[] Plans =
    [
        new() { Code = "BASIC", Name = "Basic", PriceKes = 499, MaxProperties = 1, MaxDevices = 3 },
        new() { Code = "STANDARD", Name = "Standard", PriceKes = 999, MaxProperties = 3, MaxDevices = 10, AnomalyDetection = true, Analytics = true, IncidentManagement = true },
        new() { Code = "PROFESSIONAL", Name = "Professional", PriceKes = 1999, MaxProperties = 10, MaxDevices = 30, AnomalyDetection = true, Analytics = true, IncidentManagement = true, SecurityIntelligence = true, MultipleStaffAccounts = true, AdvancedReports = true, PrioritySupport = true }
    ];

    public async Task<(UserSubscription Subscription, SubscriptionPlan Plan)> EnsureSubscriptionAsync(int userId, CancellationToken cancellationToken = default)
    {
        var userSubscriptions = await db.Subscriptions.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var subscription = userSubscriptions.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (subscription is null)
        {
            var trialStartedAt = DateTimeOffset.UtcNow;
            subscription = new UserSubscription
            {
                UserId = userId,
                PlanCode = "STANDARD",
                Status = SubscriptionStatuses.Trial,
                TrialStart = trialStartedAt,
                TrialEnd = trialStartedAt.AddDays(14),
                CreatedAt = trialStartedAt,
                UpdatedAt = trialStartedAt
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        if ((subscription.Status == SubscriptionStatuses.Trial && subscription.TrialEnd <= now) ||
            (subscription.Status == SubscriptionStatuses.Active && subscription.CurrentPeriodEnd <= now))
        {
            subscription.Status = SubscriptionStatuses.Expired;
            subscription.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Code == subscription.PlanCode, cancellationToken)
            ?? Plans.First(x => x.Code == "STANDARD");
        return (subscription, plan);
    }

    public static bool HasAccess(UserSubscription subscription, DateTimeOffset now) =>
        (subscription.Status == SubscriptionStatuses.Trial && subscription.TrialEnd > now) ||
        (subscription.Status == SubscriptionStatuses.Active && subscription.CurrentPeriodEnd > now);
}
