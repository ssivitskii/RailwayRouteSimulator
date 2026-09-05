using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Results;

public abstract record RouteRunResult
{
    private RouteRunResult() { }

    public sealed record Success(Time TotalTime) : RouteRunResult;

    public sealed record SectionFailed(int SectionIndex, SectionPassResult Reason, Time ElapsedBeforeFailure)
        : RouteRunResult;

    public sealed record EndSpeedViolation(Speed Limit, Speed Actual, Time ElapsedBeforeFinish)
        : RouteRunResult;
}
