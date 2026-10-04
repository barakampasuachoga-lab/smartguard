using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Services;

public sealed class SubscriptionExpiryService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var subscriptions = await db.Subscriptions
                    .Where(x => x.Status == SubscriptionStatuses.Active || x.Status == SubscriptionStatuses.Trial)
                    .ToListAsync(stoppingToken);
                var now = DateTimeOffset.UtcNow;
                var expired = subscriptions.Where(x =>
                    (x.Status == SubscriptionStatuses.Trial && x.TrialEnd <= now) ||
                    (x.Status == SubscriptionStatuses.Active && x.CurrentPeriodEnd <= now)).ToList();
                foreach (var subscription in expired)
                {
                    subscription.Status = SubscriptionStatuses.Expired;
                    subscription.UpdatedAt = now;
                }

                if (expired.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Expired {SubscriptionCount} SmartGuard subscriptions.", expired.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Subscription expiry sweep failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
