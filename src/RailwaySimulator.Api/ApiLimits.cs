namespace RailwaySimulator.Api;

public static class ApiLimits
{
    public const long MaximumRequestBytes = 128 * 1024;

    public const int MaximumSections = 64;

    public const int MaximumConcurrentSimulations = 4;

    public const double MinimumPrecision = 0.001;

    public const string SimulationRateLimitPolicy = "simulation";
}
