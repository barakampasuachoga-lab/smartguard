using Microsoft.EntityFrameworkCore;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.API.Services;

public sealed class AlertEscalationService(
    IServiceScopeFactory scopeFactory,
    ILogger<AlertEscalationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await EscalateDueAlertsAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogError(exception, "Alert escalation cycle failed."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task EscalateDueAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<SmtpEmailSender>();
        var thresholdText = await db.SystemSettings.AsNoTracking()
            .Where(setting => setting.Name == "alertEscalationMinutes")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var thresholdMinutes = int.TryParse(thresholdText, out var configured) ? Math.Clamp(configured, 1, 240) : 10;
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-thresholdMinutes);
        var candidateAlerts = await db.Alerts
            .Where(alert => (alert.Status == AlertStatus.Unread || alert.Status == AlertStatus.Read)
                && alert.EscalatedAt == null)
            .ToListAsync(cancellationToken);
        var dueAlerts = candidateAlerts
            .Where(alert => (alert.Status == AlertStatus.Unread || alert.Status == AlertStatus.Read) && alert.CreatedAt <= threshold)
            .ToList();

        foreach (var alert in dueAlerts)
        {
            var assignedContacts = await db.TrustedContacts.AsNoTracking()
                .Where(contact => contact.PropertyId == alert.PropertyId && contact.IsActive
                    && contact.Email != null
                    && (contact.Relationship.ToLower().Contains("security") || contact.Relationship.ToLower().Contains("guard")))
                .ToListAsync(cancellationToken);
            var successful = new List<string>();
            var failures = new List<string>();
            if (emailSender.IsConfigured)
            {
                foreach (var contact in assignedContacts)
                {
                    try
                    {
                        await emailSender.SendAsync(contact.Email!, $"SmartGuard escalation: {alert.Priority} alert", alert.Message, cancellationToken);
                        successful.Add(contact.Name);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        failures.Add(contact.Name);
                        logger.LogWarning(exception, "Failed to escalate alert {AlertId} to assigned contact {ContactId}.", alert.Id, contact.Id);
                    }
                }
            }

            if (assignedContacts.Count > 0 && failures.Count == assignedContacts.Count && emailSender.IsConfigured)
                continue;

            alert.EscalatedAt = DateTimeOffset.UtcNow;
            alert.EscalationDetails = successful.Count > 0
                ? $"Escalated to {string.Join(", ", successful)} after {thresholdMinutes} minutes without acknowledgement."
                : "Escalation threshold reached; no assigned security contact with deliverable email is configured.";
        }

        if (dueAlerts.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
