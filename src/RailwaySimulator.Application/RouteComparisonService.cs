namespace RailwaySimulator.Application;

public sealed class RouteComparisonService
{
    public const double NearEqualElapsedTolerance = 0.01;

    public RouteComparisonReport Compare(IEnumerable<RouteAnalysisEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        RouteAnalysisEntry[] input = entries.ToArray();
        if (input.Length < 2)
            throw new ArgumentException("At least two routes are required for comparison.", nameof(entries));

        RouteAnalysisEntry[] ordered =
        [
            .. OrderStatusGroup(input.Where(entry => entry.Analysis.Report.Succeeded)),
            .. OrderStatusGroup(input.Where(entry => !entry.Analysis.Report.Succeeded)),
        ];
        RankedRoute[] rankings = ordered.Select((entry, index) => new RankedRoute(
            index + 1,
            entry.Route,
            entry.Analysis.Report.Succeeded,
            entry.Analysis.Report.ElapsedTime,
            entry.Analysis.Metrics.SmallestSpeedLimitMargin,
            entry.Analysis.Report.Summary)).ToArray();
        return new RouteComparisonReport(
            rankings[0].Route,
            "Successful routes rank first. Within each result group, routes no more than 1% slower than that group's fastest route form one safety cohort ordered by larger evaluated speed-limit margin; remaining routes use elapsed time, then input order.",
            rankings);
    }

    private static IEnumerable<RouteAnalysisEntry> OrderStatusGroup(IEnumerable<RouteAnalysisEntry> entries)
    {
        RouteAnalysisEntry[] snapshot = entries.ToArray();
        if (snapshot.Length == 0)
            return [];

        double fastest = snapshot.Min(entry => entry.Analysis.Report.ElapsedTime ?? double.PositiveInfinity);
        double cohortLimit = fastest * (1 + NearEqualElapsedTolerance);
        RouteAnalysisEntry[] safetyCohort = snapshot
            .Where(entry => (entry.Analysis.Report.ElapsedTime ?? double.PositiveInfinity) <= cohortLimit)
            .OrderByDescending(entry => entry.Analysis.Metrics.SmallestSpeedLimitMargin ?? double.NegativeInfinity)
            .ThenBy(entry => entry.InputOrder)
            .ToArray();
        RouteAnalysisEntry[] remaining = snapshot
            .Where(entry => (entry.Analysis.Report.ElapsedTime ?? double.PositiveInfinity) > cohortLimit)
            .OrderBy(entry => entry.Analysis.Report.ElapsedTime ?? double.PositiveInfinity)
            .ThenBy(entry => entry.InputOrder)
            .ToArray();
        return [.. safetyCohort, .. remaining];
    }
}
