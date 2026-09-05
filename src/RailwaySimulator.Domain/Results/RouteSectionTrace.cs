using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Results;

public sealed record RouteSectionTrace(
    int Index,
    RouteSectionKind Kind,
    Distance? PlannedDistance,
    Time ConfiguredStationWait,
    Time? ExecutedStationWait,
    Speed? SpeedLimit,
    Speed EntrySpeed,
    Speed? ExitSpeed,
    Acceleration? ModeledPeakAcceleration,
    Time? Elapsed,
    SectionPassResult Result);
