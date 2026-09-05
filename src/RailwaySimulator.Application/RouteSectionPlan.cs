using RailwaySimulator.Domain.Results;

namespace RailwaySimulator.Application;

public sealed record RouteSectionPlan(
    int Index,
    RouteSectionKind Kind,
    double? PlannedDistance,
    double ConfiguredStationWait,
    double? SpeedLimit);
