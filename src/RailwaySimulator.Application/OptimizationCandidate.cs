namespace RailwaySimulator.Application;

public sealed record OptimizationCandidate(
    double InitialSpeed,
    bool Succeeded,
    double? ElapsedTime,
    double? SafetyMargin,
    string Result,
    bool Recommended);
