using System.Globalization;
using SmartGuard.Domain.Models;

namespace SmartGuard.Application.AnomalyDetection;

public class AnomalyRuleEngine
{
    private static readonly TimeSpan RoutineTolerance = TimeSpan.FromMinutes(45);

    public Alert? Evaluate(Property property, SecurityEvent securityEvent, IEnumerable<SecurityEvent> recentEvents, SecuritySchedule schedule, IEnumerable<TrustedContact>? trustedContacts = null, IEnumerable<SecurityCheckIn>? recentCheckIns = null)
    {
        var reasons = new List<string>();
        var isEntryEvent = securityEvent.EventType is SecurityEventType.DoorOpened or SecurityEventType.WindowOpened;
        var anomalyRuleMatched = false;

        var awayMode = property.Status is PropertyStatus.Away or PropertyStatus.Vacation;
        var localTime = securityEvent.Timestamp.LocalDateTime;
        var accessWindows = (trustedContacts ?? Array.Empty<TrustedContact>())
            .Where(contact => contact.IsActive && IsScheduledDay(localTime.DayOfWeek, contact.Days)
                && IsWithinAccessWindow(localTime.TimeOfDay, contact.StartTime, contact.EndTime))
            .ToList();
        var lastCheckIn = (recentCheckIns ?? Array.Empty<SecurityCheckIn>())
            .Where(checkIn => checkIn.PropertyId == property.Id && checkIn.CreatedAt <= securityEvent.Timestamp
                && checkIn.CreatedAt >= securityEvent.Timestamp.AddMinutes(-60))
            .OrderByDescending(checkIn => checkIn.CreatedAt)
            .FirstOrDefault();
        if (awayMode && isEntryEvent)
        {
            anomalyRuleMatched = true;
            reasons.Add($"Property is marked {property.Status.ToString().ToUpperInvariant()}.");
            reasons.Add($"{securityEvent.EventType} was recorded at {DisplayLocation(securityEvent)}.");
            if (accessWindows.Count > 0)
                reasons.Add($"Time overlaps an authorized access window for {string.Join(", ", accessWindows.Select(contact => contact.Name))}; this sensor does not identify who entered.");
            if (lastCheckIn is not null)
                reasons.Add($"Signed-in user {lastCheckIn.UserName} checked in {Math.Max(0, (int)(securityEvent.Timestamp - lastCheckIn.CreatedAt).TotalMinutes)} minutes earlier; this sensor does not identify who entered.");
        }
        else if ((property.Status is PropertyStatus.Sleep or PropertyStatus.Night) && isEntryEvent)
        {
            anomalyRuleMatched = true;
            reasons.Add($"Property is in {property.Status.ToString().ToUpperInvariant()} mode.");
            reasons.Add($"{securityEvent.EventType} was recorded at {DisplayLocation(securityEvent)}.");
        }

        var period = schedule.Periods.FirstOrDefault(item => IsWithinPeriod(localTime.TimeOfDay, item));
        if (period is not null)
        {
            reasons.Add($"Event occurred during the configured {period.Name} period ({period.Start}–{period.End}).");
        }

        var routinesForEvent = schedule.Routines
            .Where(routine => string.Equals(routine.EventType, securityEvent.EventType.ToString(), StringComparison.OrdinalIgnoreCase)
                && IsScheduledDay(localTime.DayOfWeek, routine.Days))
            .Select(routine => (Routine: routine, Time: TimeOnly.TryParse(routine.Time, CultureInfo.InvariantCulture, out var time) ? time : (TimeOnly?)null))
            .Where(item => item.Time.HasValue)
            .ToList();

        if (routinesForEvent.Count > 0)
        {
            var actualTime = TimeOnly.FromDateTime(localTime);
            var expected = routinesForEvent
                .Select(item => (item.Routine, item.Time!.Value, Delta: CircularTimeDistance(actualTime, item.Time!.Value)))
                .OrderBy(item => item.Delta)
                .First();
            if (expected.Delta > RoutineTolerance)
            {
                anomalyRuleMatched = true;
                var expectedLabel = string.IsNullOrWhiteSpace(expected.Routine.Label) ? securityEvent.EventType.ToString() : expected.Routine.Label;
                reasons.Add($"This differs from the usual {expectedLabel} routine at {expected.Value:HH:mm} ({expected.Routine.Days}).");
            }
        }

        var recentMotionCount = recentEvents.Count(item => item.PropertyId == property.Id
            && item.EventType == SecurityEventType.MotionDetected
            && item.Timestamp >= securityEvent.Timestamp.AddMinutes(-15)
            && item.Timestamp <= securityEvent.Timestamp);
        if ((property.Status is PropertyStatus.Away or PropertyStatus.Vacation or PropertyStatus.Sleep or PropertyStatus.Night)
            && securityEvent.EventType == SecurityEventType.MotionDetected && recentMotionCount >= 2)
        {
            anomalyRuleMatched = true;
            reasons.Add($"{recentMotionCount + 1} motion events were recorded in a 15-minute window while the property was {property.Status.ToString().ToLowerInvariant()}.");
        }

        if (!anomalyRuleMatched) return null;

        var priority = awayMode && isEntryEvent && accessWindows.Count == 0 && lastCheckIn is null
            ? AlertPriority.High
            : AlertPriority.Medium;
        var alertType = awayMode && isEntryEvent
            ? accessWindows.Count > 0 ? "Access during trusted hours" : lastCheckIn is not null ? "Access after user check-in" : "Unexpected access"
            : "Routine deviation";

        return new Alert
        {
            SecurityEventId = securityEvent.Id,
            PropertyId = property.Id,
            Priority = priority,
            AlertType = alertType,
            Message = "Potentially unusual activity. " + string.Join(" ", reasons),
            Status = AlertStatus.Unread,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string DisplayLocation(SecurityEvent securityEvent) => string.IsNullOrWhiteSpace(securityEvent.Location)
        ? securityEvent.SensorType
        : securityEvent.Location;

    private static bool IsWithinPeriod(TimeSpan time, SecuritySchedulePeriod period)
    {
        if (!TimeSpan.TryParse(period.Start, CultureInfo.InvariantCulture, out var start)
            || !TimeSpan.TryParse(period.End, CultureInfo.InvariantCulture, out var end)) return false;
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }

    private static bool IsWithinAccessWindow(TimeSpan time, string startText, string endText)
    {
        if (!TimeSpan.TryParse(startText, CultureInfo.InvariantCulture, out var start)
            || !TimeSpan.TryParse(endText, CultureInfo.InvariantCulture, out var end)) return false;
        return start <= end ? time >= start && time <= end : time >= start || time <= end;
    }

    private static bool IsScheduledDay(DayOfWeek day, string days)
    {
        var normalized = days.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (normalized is "everyday" or "daily") return true;
        if (normalized is "weekdays" or "monday-friday") return (int)day >= (int)DayOfWeek.Monday && (int)day <= (int)DayOfWeek.Friday;
        if (normalized is "weekends" or "saturday-sunday") return day is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return normalized.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(name => Enum.TryParse<DayOfWeek>(name, true, out var scheduledDay) && scheduledDay == day);
    }

    private static TimeSpan CircularTimeDistance(TimeOnly left, TimeOnly right)
    {
        var difference = (left.ToTimeSpan() - right.ToTimeSpan()).Duration();
        return difference > TimeSpan.FromHours(12) ? TimeSpan.FromDays(1) - difference : difference;
    }
}
