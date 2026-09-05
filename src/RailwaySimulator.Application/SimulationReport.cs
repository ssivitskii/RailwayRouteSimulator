namespace RailwaySimulator.Application;

public sealed record SimulationReport(
    bool Succeeded,
    int SectionCount,
    double? FinalSpeed,
    double? ElapsedTime,
    double CompletedSectionsElapsedTime,
    string Summary,
    int? FailedSection);
