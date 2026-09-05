using RailwaySimulator.Application.Configuration;

namespace RailwaySimulator.Application;

public sealed class InitialSpeedOptimizer
{
    public const int MaximumIterations = 1001;

    private readonly RouteConfigurationLoader _loader = new();
    private readonly RouteSimulationService _simulation = new();
    private readonly RouteComparisonService _comparison = new();

    public OptimizationReport Optimize(
        RouteConfiguration configuration,
        double minimum,
        double maximum,
        int iterations)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!double.IsFinite(minimum) || minimum < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum initial speed must be finite and non-negative.");
        if (!double.IsFinite(maximum) || maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum initial speed must be finite and no smaller than the minimum.");
        if (iterations is < 2 or > MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterations),
                $"Iterations must be between 2 and {MaximumIterations}.");
        }

        var entries = new List<RouteAnalysisEntry>(iterations);
        var speeds = new double[iterations];
        for (int index = 0; index < iterations; index++)
        {
            double fraction = index / (double)(iterations - 1);
            double speed = minimum + ((maximum - minimum) * fraction);
            speeds[index] = speed;
            SimulationScenario scenario = _loader.Map(configuration, speed);
            entries.Add(new RouteAnalysisEntry($"candidate-{index}", index, _simulation.Analyze(scenario)));
        }

        RouteAnalysisEntry[] feasible = entries.Where(entry => entry.Analysis.Report.Succeeded).ToArray();
        int? winnerIndex = feasible.Length switch
        {
            0 => null,
            1 => feasible[0].InputOrder,
            _ => FindWinnerIndex(entries, _comparison.Compare(feasible)),
        };
        OptimizationCandidate[] candidates = entries.Select((entry, index) => new OptimizationCandidate(
            speeds[index],
            entry.Analysis.Report.Succeeded,
            entry.Analysis.Report.ElapsedTime,
            entry.Analysis.Metrics.SmallestSpeedLimitMargin,
            entry.Analysis.Report.Summary,
            index == winnerIndex)).ToArray();
        return new OptimizationReport(
            "initialSpeed",
            minimum,
            maximum,
            iterations,
            winnerIndex is int selected ? speeds[selected] : null,
            winnerIndex is int analysisIndex ? entries[analysisIndex].Analysis : null,
            candidates);
    }

    private static int FindWinnerIndex(
        IEnumerable<RouteAnalysisEntry> entries,
        RouteComparisonReport comparison)
    {
        RankedRoute winner = comparison.Rankings[0];
        return entries.Single(entry => string.Equals(entry.Route, winner.Route, StringComparison.Ordinal)).InputOrder;
    }
}
