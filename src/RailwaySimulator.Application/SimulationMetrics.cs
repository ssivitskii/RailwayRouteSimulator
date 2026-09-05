namespace RailwaySimulator.Application;

public sealed record SimulationMetrics(
    double? TotalElapsedTime,
    double MovingTime,
    double ConfiguredStationWait,
    double? ExecutedStationWait,
    double PlannedTrackDistance,
    double? ActualTrackDistance,
    double AverageSampledSpeed,
    double MaximumSampledSpeed,
    double MinimumSampledSpeed,
    double? MaximumModeledAcceleration,
    double? SmallestSpeedLimitMargin,
    int SectionCount,
    int StationCount,
    string TightestConstraint);
