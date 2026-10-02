namespace SmartGuard.Domain.Models;

public enum SecurityEventType
{
    MotionDetected,
    DoorOpened,
    DoorClosed,
    WindowOpened,
    WindowClosed,
    DeviceOffline,
    PotentiallyUnusualActivity,
    Other
}
