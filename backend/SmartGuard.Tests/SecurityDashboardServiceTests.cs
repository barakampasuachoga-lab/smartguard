using Microsoft.EntityFrameworkCore;
using SmartGuard.Application.Services;
using SmartGuard.Domain.Models;
using SmartGuard.Infrastructure.Data;

namespace SmartGuard.Tests;

public class SecurityDashboardServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_ReturnsMetricsForSeededData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        context.Properties.Add(new Property
        {
            Id = "prop-1",
            Name = "Downtown Villa",
            Status = PropertyStatus.Away,
            Address = "1 Main St",
            Owner = "Alicia"
        });

        context.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            PropertyId = "prop-1",
            DeviceId = "cam-1",
            EventType = SecurityEventType.MotionDetected,
            Location = "Front Door",
            Description = "Movement detected",
            Status = "New",
            Timestamp = DateTimeOffset.UtcNow,
            Priority = AlertPriority.High
        });

        context.Alerts.Add(new Alert
        {
            Id = Guid.NewGuid(),
            PropertyId = "prop-1",
            SecurityEventId = Guid.NewGuid(),
            Priority = AlertPriority.High,
            Message = "Door opened while away",
            Status = AlertStatus.Unread
        });

        await context.SaveChangesAsync();

        var service = new SecurityDashboardService(new AppDbContextSecurityDashboardDataSource(context));
        var overview = await service.GetOverviewAsync();

        Assert.Equal(1, overview.TotalProperties);
        Assert.Equal(1, overview.TotalEvents);
        Assert.Equal(1, overview.OpenAlerts);
        Assert.Equal(1, overview.TotalDevices);
        Assert.True(overview.RiskScore >= 0);
    }
}
