using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Results;

public abstract record SectionPassResult
{
    private SectionPassResult() { }

    public sealed record Success(Time Elapsed) : SectionPassResult;

    public sealed record CannotMove(string Reason) : SectionPassResult;

    public sealed record SpeedLimitExceeded(Speed Limit, Speed Actual) : SectionPassResult;
}
