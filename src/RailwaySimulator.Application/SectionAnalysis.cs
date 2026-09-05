namespace RailwaySimulator.Application;

public sealed record SectionAnalysis(
    int Index,
    string Type,
    bool Succeeded,
    double EntrySpeed,
    double? ExitSpeed,
    double? ElapsedTime,
    double? PlannedDistance,
    double ConfiguredStationWait,
    double? ExecutedStationWait,
    double? ModeledPeakAcceleration,
    double? SpeedLimit,
    double? SpeedLimitMargin,
    string Result);
