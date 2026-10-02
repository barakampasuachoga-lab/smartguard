using SmartGuard.Application.AnomalyDetection;
using SmartGuard.Domain.Models;

namespace SmartGuard.Tests;

public class AnomalyRuleEngineTests
{
    [Fact]
    public void Evaluate_WhenPropertyAwayAndDoorOpened_ReturnsHighPriorityAlert()
    {
        var engine = new AnomalyRuleEngine();
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            PropertyId = "property-1",
            DeviceId = "device-1",
            EventType = SecurityEventType.DoorOpened,
            Timestamp = DateTimeOffset.UtcNow,
            Location = "Main Entrance"
        };

        var alert = engine.Evaluate(PropertyStatus.Away, securityEvent, new[] { securityEvent });

        Assert.Equal(AlertPriority.High, alert.Priority);
        Assert.Equal("property-1", alert.PropertyId);
        Assert.Equal(securityEvent.Id, alert.SecurityEventId);
    }

    [Fact]
    public void Evaluate_WhenHomeAndNormalMotion_ReturnsLowPriorityAlert()
    {
        var engine = new AnomalyRuleEngine();
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            PropertyId = "property-2",
            DeviceId = "device-2",
            EventType = SecurityEventType.MotionDetected,
            Timestamp = DateTimeOffset.UtcNow,
            Location = "Living room"
        };

        var alert = engine.Evaluate(PropertyStatus.Home, securityEvent, Array.Empty<SecurityEvent>());

        Assert.Equal(AlertPriority.Low, alert.Priority);
        Assert.Equal("property-2", alert.PropertyId);
    }
}
