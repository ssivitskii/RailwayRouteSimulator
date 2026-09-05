using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Results;

public abstract record ApplyForceResult
{
    private ApplyForceResult() { }

    public sealed record Success(Acceleration AppliedAcceleration) : ApplyForceResult;

    public sealed record ExceedsLimit(Force Requested, Force Allowed) : ApplyForceResult;
}
