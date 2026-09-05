using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Sections;

public sealed record NormalTrack(Distance Length) : IRouteSection
{
    public SectionPassResult Pass(Train train)
    {
        ArgumentNullException.ThrowIfNull(train);
        return train.Traverse(Length);
    }
}
