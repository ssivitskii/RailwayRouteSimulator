namespace RailwaySimulator.Application.Configuration;

public sealed class RouteConfigurationException : Exception
{
    public RouteConfigurationException(string message)
        : base(message)
    {
    }

    public RouteConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
