using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Sections;

public sealed record PoweredTrack(Distance Length, Force Force) : IRouteSection
{
    public SectionPassResult Pass(Train train)
    {
        ArgumentNullException.ThrowIfNull(train);
        ApplyForceResult forceResult = train.ApplyForce(Force);
        if (forceResult is ApplyForceResult.ExceedsLimit limit)
        {
            return new SectionPassResult.CannotMove(
                FormattableString.Invariant(
                    $"Requested force {limit.Requested.Value} exceeds the train limit {limit.Allowed.Value}."));
        }

        SectionPassResult result = train.Traverse(Length);
        train.ResetAcceleration();
        return result;
    }
}
