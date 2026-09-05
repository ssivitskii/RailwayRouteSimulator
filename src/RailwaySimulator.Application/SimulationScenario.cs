using RailwaySimulator.Domain.Entities;

namespace RailwaySimulator.Application;

public sealed record SimulationScenario(Train Train, Route Route, RoutePlan Plan)
{
    public int SectionCount => Plan.Sections.Count;
}
