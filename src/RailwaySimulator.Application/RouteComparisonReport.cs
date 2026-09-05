using System.Collections.ObjectModel;

namespace RailwaySimulator.Application;

public sealed class RouteComparisonReport
{
    private readonly ReadOnlyCollection<RankedRoute> _rankings;

    public RouteComparisonReport(string recommendation, string rationale, IEnumerable<RankedRoute> rankings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendation);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        ArgumentNullException.ThrowIfNull(rankings);
        Recommendation = recommendation;
        Rationale = rationale;
        _rankings = Array.AsReadOnly(rankings.ToArray());
    }

    public string Recommendation { get; }

    public string Rationale { get; }

    public IReadOnlyList<RankedRoute> Rankings => _rankings;
}
