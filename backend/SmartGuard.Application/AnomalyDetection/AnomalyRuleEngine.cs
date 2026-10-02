using SmartGuard.Domain.Models;

namespace SmartGuard.Application.AnomalyDetection;

public class AnomalyRuleEngine
{
    public Alert Evaluate(PropertyStatus propertyStatus, SecurityEvent securityEvent, IEnumerable<SecurityEvent> recentEvents)
    {
        var recentList = recentEvents.ToList();

        var isAwayDoorOpen = propertyStatus == PropertyStatus.Away && securityEvent.EventType == SecurityEventType.DoorOpened;
        var repeatedMotion = propertyStatus == PropertyStatus.Away
            && securityEvent.EventType == SecurityEventType.MotionDetected
            && recentList.Count(e => e.EventType == SecurityEventType.MotionDetected && e.Timestamp >= DateTimeOffset.UtcNow.AddMinutes(-15)) >= 3;

        if (isAwayDoorOpen)
        {
            return new Alert
            {
                Priority = AlertPriority.High,
                Message = "Potentially unusual door activity detected while the property is marked away.",
                Status = AlertStatus.Unread,
                SecurityEventId = securityEvent.Id,
                PropertyId = securityEvent.PropertyId,
            };
        }

        if (repeatedMotion)
        {
            return new Alert
            {
                Priority = AlertPriority.Medium,
                Message = "Repeated motion activity detected during an away status window.",
                Status = AlertStatus.Unread,
                SecurityEventId = securityEvent.Id,
                PropertyId = securityEvent.PropertyId,
            };
        }

        return new Alert
        {
            Priority = AlertPriority.Low,
            Message = "Routine security event recorded.",
            Status = AlertStatus.Unread,
            SecurityEventId = securityEvent.Id,
            PropertyId = securityEvent.PropertyId,
        };
    }
}
