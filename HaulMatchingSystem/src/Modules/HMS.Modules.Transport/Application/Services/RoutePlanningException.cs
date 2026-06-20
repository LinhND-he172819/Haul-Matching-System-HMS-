namespace HMS.Modules.Transport.Application.Services;

public sealed class RoutePlanningException : Exception
{
    public RoutePlanningException(string message)
        : base(message)
    {
    }

    public RoutePlanningException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
