using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;

namespace RailwaySimulator.Domain.Abstractions;

public interface IRouteSection
{
    SectionPassResult Pass(Train train);
}
