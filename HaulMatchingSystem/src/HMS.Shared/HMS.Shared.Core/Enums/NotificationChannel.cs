namespace HMS.Shared.Core.Enums
{
    [Flags]
    public enum NotificationChannel
    {
        None = 0,
        Push = 1,
        SMS = 2,
        Both = Push | SMS
    }
}
