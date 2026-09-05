using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.Sections;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Entities;

public sealed class Route
{
    private readonly IRouteSection[] _sections;
    private readonly Speed _endSpeedLimit;

    public Route(IEnumerable<IRouteSection> sections, Speed endSpeedLimit)
    {
        ArgumentNullException.ThrowIfNull(sections);
        _sections = sections.ToArray();
        if (Array.Exists(_sections, static section => section is null))
            throw new ArgumentException("Route sections must not contain null elements.", nameof(sections));

        _endSpeedLimit = endSpeedLimit;
    }

    public RouteRunResult Run(Train train)
    {
        return Execute(train).Result;
    }

    public RouteExecution Execute(Train train)
    {
        ArgumentNullException.ThrowIfNull(train);
        var total = new Time(0);
        var trace = new List<RouteSectionTrace>(_sections.Length);

        for (int index = 0; index < _sections.Length; index++)
        {
            IRouteSection section = _sections[index];
            Speed entrySpeed = train.CurrentSpeed;
            SectionPassResult result = section.Pass(train);
            trace.Add(CreateTrace(index, section, train, entrySpeed, result));
            if (result is not SectionPassResult.Success success)
            {
                return new RouteExecution(
                    new RouteRunResult.SectionFailed(index, result, total),
                    _endSpeedLimit,
                    trace);
            }

            total = new Time(total.Value + success.Elapsed.Value);
        }

        RouteRunResult routeResult = train.CurrentSpeed.Value <= _endSpeedLimit.Value + PhysicsConstants.KinematicsEpsilon
            ? new RouteRunResult.Success(total)
            : new RouteRunResult.EndSpeedViolation(_endSpeedLimit, train.CurrentSpeed, total);
        return new RouteExecution(routeResult, _endSpeedLimit, trace);
    }

    private static RouteSectionTrace CreateTrace(
        int index,
        IRouteSection section,
        Train train,
        Speed entrySpeed,
        SectionPassResult result)
    {
        Distance? plannedDistance = section switch
        {
            NormalTrack normal => normal.Length,
            PoweredTrack powered => powered.Length,
            _ => null,
        };
        Time stationWait = section is Station station
            ? new Time(station.AlightingTime.Value + station.BoardingTime.Value)
            : new Time(0);
        Speed? speedLimit = section is Station limitedStation ? limitedStation.EntrySpeedLimit : null;
        Time? elapsed = result is SectionPassResult.Success success ? success.Elapsed : null;
        Speed? exitSpeed = result is SectionPassResult.Success ? train.CurrentSpeed : null;
        Time? executedStationWait = section is not Station
            ? new Time(0)
            : result is SectionPassResult.Success ? stationWait : null;
        var modeledAcceleration = GetModeledPeakAcceleration(section, train, entrySpeed, result);
        return new RouteSectionTrace(
            index,
            GetKind(section),
            plannedDistance,
            stationWait,
            executedStationWait,
            speedLimit,
            entrySpeed,
            exitSpeed,
            modeledAcceleration,
            elapsed,
            result);
    }

    private static RouteSectionKind GetKind(IRouteSection section)
    {
        return section switch
        {
            NormalTrack => RouteSectionKind.NormalTrack,
            PoweredTrack => RouteSectionKind.PoweredTrack,
            Station => RouteSectionKind.Station,
            _ => throw new InvalidOperationException($"Unknown route section type '{section.GetType().Name}'."),
        };
    }

    private static Acceleration? GetModeledPeakAcceleration(
        IRouteSection section,
        Train train,
        Speed entrySpeed,
        SectionPassResult result)
    {
        double? magnitude = section switch
        {
            PoweredTrack powered when result is SectionPassResult.Success =>
                Math.Abs(powered.Force.Value) / train.Mass.Value,
            Station when result is SectionPassResult.Success && entrySpeed.Value > PhysicsConstants.KinematicsEpsilon =>
                train.MaximumForce.Value / train.Mass.Value,
            NormalTrack when result is SectionPassResult.Success => 0,
            Station when result is SectionPassResult.Success => 0,
            _ => null,
        };
        return magnitude is double value ? new Acceleration(value) : null;
    }
}
