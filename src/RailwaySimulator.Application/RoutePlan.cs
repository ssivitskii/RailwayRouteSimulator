using System.Collections.ObjectModel;

namespace RailwaySimulator.Application;

public sealed class RoutePlan
{
    private readonly ReadOnlyCollection<RouteSectionPlan> _sections;

    public RoutePlan(double initialSpeed, double endSpeedLimit, IEnumerable<RouteSectionPlan> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        InitialSpeed = initialSpeed;
        EndSpeedLimit = endSpeedLimit;
        _sections = Array.AsReadOnly(sections.ToArray());
    }

    public double InitialSpeed { get; }

    public double EndSpeedLimit { get; }

    public IReadOnlyList<RouteSectionPlan> Sections => _sections;
}
