namespace RailwaySimulator.Application.Configuration;

public sealed class RouteConfiguration
{
    public TrainConfiguration? Train { get; init; }

    public double? EndSpeedLimit { get; init; }

    public IReadOnlyList<SectionConfiguration?>? Sections { get; init; }

    public sealed class TrainConfiguration
    {
        public double? Mass { get; init; }

        public double? MaximumForce { get; init; }

        public double? Precision { get; init; }

        public double? InitialSpeed { get; init; }
    }

    public sealed class SectionConfiguration
    {
        public string? Type { get; init; }

        public double? Distance { get; init; }

        public double? Force { get; init; }

        public double? AlightingTime { get; init; }

        public double? BoardingTime { get; init; }

        public double? SpeedLimit { get; init; }
    }
}
