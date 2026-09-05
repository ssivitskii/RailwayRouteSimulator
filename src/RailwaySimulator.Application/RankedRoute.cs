namespace RailwaySimulator.Application;

public sealed record RankedRoute(
    int Rank,
    string Route,
    bool Succeeded,
    double? ElapsedTime,
    double? SafetyMargin,
    string Result);
