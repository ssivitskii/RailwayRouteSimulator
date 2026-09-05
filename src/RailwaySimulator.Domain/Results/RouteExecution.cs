using RailwaySimulator.Domain.ValueObjects;
using System.Collections.ObjectModel;

namespace RailwaySimulator.Domain.Results;

public sealed class RouteExecution
{
    private readonly ReadOnlyCollection<RouteSectionTrace> _trace;

    public RouteExecution(RouteRunResult result, Speed finalSpeedLimit, IEnumerable<RouteSectionTrace> trace)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(trace);
        Result = result;
        FinalSpeedLimit = finalSpeedLimit;
        _trace = Array.AsReadOnly(trace.ToArray());
    }

    public RouteRunResult Result { get; }

    public Speed FinalSpeedLimit { get; }

    public IReadOnlyList<RouteSectionTrace> Trace => _trace;
}
