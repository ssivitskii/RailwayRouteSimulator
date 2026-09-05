using RailwaySimulator.Application;
using RailwaySimulator.Application.Configuration;

namespace RailwaySimulator.Tests;

public sealed class AnalysisAndOptimizationTests
{
    private static readonly string[] ExpectedCohortOrder = ["safe-cohort", "fastest", "outside-cohort"];

    [Fact]
    public void AnalysisReportsOnlyModeledMetrics()
    {
        SimulationScenario scenario = new RouteConfigurationLoader().Map(CreateStationConfiguration());

        SimulationAnalysis analysis = new RouteSimulationService().Analyze(scenario);

        Assert.True(analysis.Report.Succeeded);
        Assert.Equal(3, analysis.Metrics.SectionCount);
        Assert.Equal(1, analysis.Metrics.StationCount);
        Assert.Equal(30, analysis.Metrics.PlannedTrackDistance);
        Assert.Equal(30, analysis.Metrics.ActualTrackDistance!.Value);
        Assert.Equal(5, analysis.Metrics.ConfiguredStationWait);
        Assert.Equal(5, analysis.Metrics.ExecutedStationWait);
        Assert.Equal(analysis.Report.ElapsedTime!.Value - 5, analysis.Metrics.MovingTime, 8);
        Assert.Equal(2, analysis.Metrics.MaximumModeledAcceleration!.Value);
        Assert.NotNull(analysis.Metrics.SmallestSpeedLimitMargin);
        Assert.Equal(3, analysis.Trace.Count);
    }

    [Fact]
    public void FailedRouteDoesNotClaimActualTrackDistance()
    {
        RouteConfiguration configuration = CreateStationConfiguration();
        configuration = new RouteConfiguration
        {
            Train = configuration.Train,
            EndSpeedLimit = 1,
            Sections = configuration.Sections,
        };

        SimulationAnalysis analysis = new RouteSimulationService().Analyze(
            new RouteConfigurationLoader().Map(configuration));

        Assert.False(analysis.Report.Succeeded);
        Assert.Null(analysis.Metrics.ActualTrackDistance);
        Assert.True(analysis.Metrics.SmallestSpeedLimitMargin!.Value < 0);
    }

    [Fact]
    public void ComparisonPrefersSuccessThenMarginWithinOnePercentAndKeepsStableOrder()
    {
        var service = new RouteComparisonService();
        RouteComparisonReport successOverFailure = service.Compare(
        [
            Entry("failed", 0, succeeded: false, elapsed: 1, margin: 100),
            Entry("successful", 1, succeeded: true, elapsed: 100, margin: 1),
        ]);
        RouteComparisonReport marginTieBreak = service.Compare(
        [
            Entry("fast", 0, succeeded: true, elapsed: 100, margin: 1),
            Entry("safe", 1, succeeded: true, elapsed: 100.5, margin: 5),
        ]);
        RouteComparisonReport stable = service.Compare(
        [
            Entry("first", 0, succeeded: true, elapsed: 10, margin: 2),
            Entry("second", 1, succeeded: true, elapsed: 10, margin: 2),
        ]);

        Assert.Equal("successful", successOverFailure.Recommendation);
        Assert.Equal("safe", marginTieBreak.Recommendation);
        Assert.Equal("first", stable.Recommendation);
    }

    [Fact]
    public void ComparisonUsesGlobalFastestCohortAndIsPermutationIndependent()
    {
        var service = new RouteComparisonService();
        RouteAnalysisEntry first = Entry("fastest", 0, succeeded: true, elapsed: 100, margin: 1);
        RouteAnalysisEntry second = Entry("safe-cohort", 1, succeeded: true, elapsed: 100.9, margin: 3);
        RouteAnalysisEntry third = Entry("outside-cohort", 2, succeeded: true, elapsed: 101.8, margin: 100);

        string[] forward = service.Compare([first, second, third]).Rankings.Select(route => route.Route).ToArray();
        string[] permuted = service.Compare([third, first, second]).Rankings.Select(route => route.Route).ToArray();

        Assert.Equal(ExpectedCohortOrder, forward);
        Assert.Equal(forward, permuted);
    }

    [Fact]
    public void FailedSectionTraceDoesNotFabricateMeasurements()
    {
        SimulationAnalysis analysis = new RouteSimulationService().Analyze(
            new RouteConfigurationLoader().Map(CreateStoppedConfiguration()));

        SectionAnalysis section = Assert.Single(analysis.Trace);
        Assert.False(section.Succeeded);
        Assert.Null(section.ExitSpeed);
        Assert.Null(section.ElapsedTime);
        Assert.Null(section.ModeledPeakAcceleration);
        Assert.Null(analysis.Metrics.MaximumModeledAcceleration);
    }

    [Fact]
    public void PartialPoweredFailureDoesNotExposeStaleAggregateMeasurements()
    {
        var configuration = new RouteConfiguration
        {
            Train = new RouteConfiguration.TrainConfiguration
            {
                Mass = 1000,
                MaximumForce = 2000,
                Precision = 0.1,
                InitialSpeed = 1,
            },
            EndSpeedLimit = 10,
            Sections =
            [
                new RouteConfiguration.SectionConfiguration
                {
                    Type = "powered",
                    Distance = 10,
                    Force = -1000,
                },
            ],
        };

        SimulationAnalysis analysis = new RouteSimulationService().Analyze(
            new RouteConfigurationLoader().Map(configuration));

        Assert.False(analysis.Report.Succeeded);
        Assert.Null(analysis.Report.FinalSpeed);
        Assert.Null(analysis.Report.ElapsedTime);
        Assert.Equal(0, analysis.Report.CompletedSectionsElapsedTime);
        Assert.Null(analysis.Metrics.TotalElapsedTime);
        Assert.Null(Assert.Single(analysis.Trace).ExitSpeed);
    }

    [Fact]
    public void FailureBeforeStationSeparatesConfiguredAndExecutedWait()
    {
        RouteConfiguration configuration = CreateStoppedConfiguration();
        configuration = new RouteConfiguration
        {
            Train = configuration.Train,
            EndSpeedLimit = configuration.EndSpeedLimit,
            Sections =
            [
                .. configuration.Sections!,
                new RouteConfiguration.SectionConfiguration
                {
                    Type = "station",
                    AlightingTime = 2,
                    BoardingTime = 3,
                    SpeedLimit = 10,
                },
            ],
        };

        SimulationAnalysis analysis = new RouteSimulationService().Analyze(
            new RouteConfigurationLoader().Map(configuration));

        Assert.Equal(5, analysis.Metrics.ConfiguredStationWait);
        Assert.Equal(0, analysis.Metrics.ExecutedStationWait);
    }

    [Fact]
    public void OptimizerUsesBoundedGridAndFreshScenarioForEveryCandidate()
    {
        RouteConfiguration configuration = CreateNormalTrackConfiguration();

        OptimizationReport report = new InitialSpeedOptimizer().Optimize(configuration, 1, 3, 3);

        Assert.Equal("initialSpeed", report.Parameter);
        Assert.Equal(3, report.Iterations);
        Assert.Equal(3, report.Candidates.Count);
        Assert.Equal(3, report.RecommendedValue!.Value);
        Assert.Single(report.Candidates, candidate => candidate.Recommended);
        Assert.All(report.Candidates, candidate => Assert.True(candidate.Succeeded));
    }

    [Fact]
    public void OptimizerRejectsIterationCountsOutsideCap()
    {
        RouteConfiguration configuration = CreateNormalTrackConfiguration();

        Assert.Throws<ArgumentOutOfRangeException>(() => new InitialSpeedOptimizer().Optimize(
            configuration,
            0,
            1,
            InitialSpeedOptimizer.MaximumIterations + 1));
    }

    [Fact]
    public void OptimizerDoesNotRecommendAnInfeasibleCandidate()
    {
        OptimizationReport report = new InitialSpeedOptimizer().Optimize(CreateStoppedConfiguration(), 0, 0, 2);

        Assert.Null(report.RecommendedValue);
        Assert.Null(report.RecommendedAnalysis);
        Assert.DoesNotContain(report.Candidates, candidate => candidate.Recommended);
    }

    [Fact]
    public void AnalysisDefensivelySnapshotsTrace()
    {
        var source = new List<SectionAnalysis>();
        var report = new SimulationReport(true, 0, 0, 0, 0, "Success", null);
        var metrics = new SimulationMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0, 0, "none");
        var analysis = new SimulationAnalysis(report, metrics, source);

        source.Add(new SectionAnalysis(0, "Normal", true, 0, 0, 0, 0, 0, 0, 0, null, null, "Success"));

        Assert.Empty(analysis.Trace);
        Assert.False(analysis.Trace is SectionAnalysis[]);
    }

    private static RouteAnalysisEntry Entry(
        string name,
        int order,
        bool succeeded,
        double elapsed,
        double? margin)
    {
        var report = new SimulationReport(succeeded, 1, 1, elapsed, elapsed, succeeded ? "Success" : "Failure", null);
        var metrics = new SimulationMetrics(
            elapsed,
            elapsed,
            0,
            0,
            1,
            succeeded ? 1 : null,
            1,
            1,
            1,
            0,
            margin,
            1,
            0,
            "test");
        return new RouteAnalysisEntry(name, order, new SimulationAnalysis(report, metrics, []));
    }

    private static RouteConfiguration CreateNormalTrackConfiguration()
    {
        return new RouteConfiguration
        {
            Train = new RouteConfiguration.TrainConfiguration
            {
                Mass = 1000,
                MaximumForce = 0,
                Precision = 0.1,
                InitialSpeed = 1,
            },
            EndSpeedLimit = 10,
            Sections = [new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 10 }],
        };
    }

    private static RouteConfiguration CreateStoppedConfiguration()
    {
        return new RouteConfiguration
        {
            Train = new RouteConfiguration.TrainConfiguration
            {
                Mass = 1000,
                MaximumForce = 0,
                Precision = 0.1,
                InitialSpeed = 0,
            },
            EndSpeedLimit = 10,
            Sections = [new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 10 }],
        };
    }

    private static RouteConfiguration CreateStationConfiguration()
    {
        return new RouteConfiguration
        {
            Train = new RouteConfiguration.TrainConfiguration
            {
                Mass = 1000,
                MaximumForce = 2000,
                Precision = 0.1,
                InitialSpeed = 0,
            },
            EndSpeedLimit = 6,
            Sections =
            [
                new RouteConfiguration.SectionConfiguration
                {
                    Type = "powered",
                    Distance = 20,
                    Force = 500,
                },
                new RouteConfiguration.SectionConfiguration
                {
                    Type = "station",
                    AlightingTime = 2,
                    BoardingTime = 3,
                    SpeedLimit = 6,
                },
                new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 10 },
            ],
        };
    }
}
