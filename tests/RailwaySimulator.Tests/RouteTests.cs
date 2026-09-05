using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.Sections;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Tests;

public sealed class RouteTests
{
    [Fact]
    public void CompositeRouteWithinLimitsSucceeds()
    {
        IRouteSection[] sections =
        [
            new PoweredTrack(new Distance(20), new Force(500)),
            new NormalTrack(new Distance(10)),
            new Station(new Time(3), new Time(2), new Speed(6)),
            new NormalTrack(new Distance(5)),
        ];

        RouteRunResult result = new Route(sections, new Speed(6)).Run(CreateTrain());

        Assert.IsType<RouteRunResult.Success>(result);
    }

    [Fact]
    public void RouteWithExcessiveFinalSpeedReturnsViolation()
    {
        IRouteSection[] sections = [new PoweredTrack(new Distance(50), new Force(500))];

        RouteRunResult result = new Route(sections, new Speed(3)).Run(CreateTrain());

        RouteRunResult.EndSpeedViolation violation = Assert.IsType<RouteRunResult.EndSpeedViolation>(result);
        Assert.True(violation.Actual.Value > violation.Limit.Value);
    }

    [Fact]
    public void RouteWithExcessiveStationEntrySpeedReportsSectionIndex()
    {
        IRouteSection[] sections =
        [
            new PoweredTrack(new Distance(50), new Force(500)),
            new Station(new Time(1), new Time(1), new Speed(3)),
        ];

        RouteRunResult result = new Route(sections, new Speed(10)).Run(CreateTrain());

        RouteRunResult.SectionFailed failure = Assert.IsType<RouteRunResult.SectionFailed>(result);
        Assert.Equal(1, failure.SectionIndex);
        Assert.IsType<SectionPassResult.SpeedLimitExceeded>(failure.Reason);
    }

    [Fact]
    public void RouteWithStationaryTrainReturnsFailure()
    {
        IRouteSection[] sections = [new NormalTrack(new Distance(10))];

        RouteRunResult result = new Route(sections, new Speed(1)).Run(CreateTrain());

        RouteRunResult.SectionFailed failure = Assert.IsType<RouteRunResult.SectionFailed>(result);
        Assert.IsType<SectionPassResult.CannotMove>(failure.Reason);
    }

    [Fact]
    public void PoweredTrackWithExcessiveForceFailsBeforeTraversal()
    {
        IRouteSection[] sections = [new PoweredTrack(new Distance(10), new Force(2500))];

        RouteRunResult result = new Route(sections, new Speed(10)).Run(CreateTrain());

        RouteRunResult.SectionFailed failure = Assert.IsType<RouteRunResult.SectionFailed>(result);
        Assert.Contains(
            "exceeds",
            Assert.IsType<SectionPassResult.CannotMove>(failure.Reason).Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RouteRejectsNullSectionElements()
    {
        IRouteSection[] sections = [null!];

        Assert.Throws<ArgumentException>(() => new Route(sections, new Speed(1)));
    }

    private static Train CreateTrain()
    {
        return new Train(new Mass(1000), new Force(2000), new Precision(0.1));
    }
}
