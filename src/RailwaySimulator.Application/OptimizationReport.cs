using System.Collections.ObjectModel;

namespace RailwaySimulator.Application;

public sealed class OptimizationReport
{
    private readonly ReadOnlyCollection<OptimizationCandidate> _candidates;

    public OptimizationReport(
        string parameter,
        double minimum,
        double maximum,
        int iterations,
        double? recommendedValue,
        SimulationAnalysis? recommendedAnalysis,
        IEnumerable<OptimizationCandidate> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentNullException.ThrowIfNull(candidates);
        Parameter = parameter;
        Minimum = minimum;
        Maximum = maximum;
        Iterations = iterations;
        RecommendedValue = recommendedValue;
        RecommendedAnalysis = recommendedAnalysis;
        _candidates = Array.AsReadOnly(candidates.ToArray());
    }

    public string Parameter { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public int Iterations { get; }

    public double? RecommendedValue { get; }

    public SimulationAnalysis? RecommendedAnalysis { get; }

    public IReadOnlyList<OptimizationCandidate> Candidates => _candidates;
}
