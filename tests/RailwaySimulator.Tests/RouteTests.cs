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

    [Fact]
    public void TrainRejectsDefaultValueObjectsAtConstructionBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Train(default, new Force(1), new Precision(0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Train(new Mass(1), new Force(1), default));
    }

    [Fact]
    public void TrainAcceptsZeroForceAndSpeedAfterExplicitInvariantChecks()
    {
        var train = new Train(new Mass(1), default, new Precision(0.1), default);

        Assert.Equal(0, train.MaximumForce.Value);
        Assert.Equal(0, train.CurrentSpeed.Value);
    }

    [Fact]
    public void ExtremelySmallPrecisionStopsAtIntegrationStepBudget()
    {
        var train = new Train(new Mass(1), new Force(1), new Precision(1e-12), new Speed(1));

        SectionPassResult result = train.Traverse(new Distance(1));

        SectionPassResult.CannotMove failure = Assert.IsType<SectionPassResult.CannotMove>(result);
        Assert.Contains("safety limit", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtremelySmallPrecisionStopsSpeedChangeAtIntegrationStepBudget()
    {
        var train = new Train(new Mass(1), new Force(1), new Precision(1e-12));

        SectionPassResult result = train.ChangeSpeedTo(new Speed(1));

        SectionPassResult.CannotMove failure = Assert.IsType<SectionPassResult.CannotMove>(result);
        Assert.Contains("safety limit", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsecutiveManeuversShareOneIntegrationStepBudget()
    {
        var train = new Train(new Mass(1), new Force(1), new Precision(0.001), new Speed(999));

        SectionPassResult braking = train.ChangeSpeedTo(new Speed(0));
        SectionPassResult acceleration = train.ChangeSpeedTo(new Speed(999));

        Assert.IsType<SectionPassResult.Success>(braking);
        SectionPassResult.CannotMove failure = Assert.IsType<SectionPassResult.CannotMove>(acceleration);
        Assert.Contains("shared", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteElapsedTimeOverflowMarksOffendingSectionAsFailed()
    {
        IRouteSection[] sections =
        [
            new NormalTrack(new Distance(1)),
            new NormalTrack(new Distance(1)),
        ];
        var train = new Train(new Mass(1), new Force(1), new Precision(1e307), new Speed(1e-308));

        RouteExecution execution = new Route(sections, new Speed(1)).Execute(train);

        RouteRunResult.SectionFailed failure = Assert.IsType<RouteRunResult.SectionFailed>(execution.Result);
        Assert.Equal(1, failure.SectionIndex);
        Assert.Contains(
            "numeric range",
            Assert.IsType<SectionPassResult.CannotMove>(failure.Reason).Reason,
            StringComparison.Ordinal);
        Assert.IsType<SectionPassResult.Success>(execution.Trace[0].Result);
        Assert.IsType<SectionPassResult.CannotMove>(execution.Trace[1].Result);
        Assert.Null(execution.Trace[1].Elapsed);
        Assert.Null(execution.Trace[1].ExitSpeed);
    }

    [Fact]
    public void StationManeuverElapsedOverflowReturnsDeterministicFailure()
    {
        IRouteSection[] sections = [new Station(new Time(0), new Time(0), new Speed(1))];
        var train = new Train(new Mass(1), new Force(1e-308), new Precision(1e308), new Speed(1));

        RouteExecution execution = new Route(sections, new Speed(1)).Execute(train);

        RouteRunResult.SectionFailed failure = Assert.IsType<RouteRunResult.SectionFailed>(execution.Result);
        Assert.Equal(0, failure.SectionIndex);
        Assert.Contains(
            "numeric range",
            Assert.IsType<SectionPassResult.CannotMove>(failure.Reason).Reason,
            StringComparison.Ordinal);
        Assert.IsType<SectionPassResult.CannotMove>(Assert.Single(execution.Trace).Result);
    }

    private static Train CreateTrain()
    {
        return new Train(new Mass(1000), new Force(2000), new Precision(0.1));
    }
}
