namespace SmartGuard.Domain.Models;

public class SecuritySchedule
{
    public List<SecuritySchedulePeriod> Periods { get; set; } =
    [
        new() { Name = "Morning", Start = "06:00", End = "09:00" },
        new() { Name = "Day", Start = "09:00", End = "17:00" },
        new() { Name = "Evening", Start = "17:00", End = "22:00" },
        new() { Name = "Night", Start = "22:00", End = "06:00" }
    ];

    public List<SecurityRoutine> Routines { get; set; } = new();
}

public class SecuritySchedulePeriod
{
    public string Name { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

public class SecurityRoutine
{
    public string Days { get; set; } = "Monday-Friday";
    public string Time { get; set; } = "08:00";
    public string EventType { get; set; } = "DoorOpened";
    public string Label { get; set; } = string.Empty;
}
