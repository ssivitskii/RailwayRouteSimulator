using System.Collections.ObjectModel;

namespace RailwaySimulator.Application;

public sealed class SimulationAnalysis
{
    private readonly ReadOnlyCollection<SectionAnalysis> _trace;

    public SimulationAnalysis(SimulationReport report, SimulationMetrics metrics, IEnumerable<SectionAnalysis> trace)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        ArgumentNullException.ThrowIfNull(trace);
        _trace = Array.AsReadOnly(trace.ToArray());
    }

    public SimulationReport Report { get; }

    public SimulationMetrics Metrics { get; }

    public IReadOnlyList<SectionAnalysis> Trace => _trace;
}
