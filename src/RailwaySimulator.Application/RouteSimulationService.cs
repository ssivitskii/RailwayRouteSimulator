using RailwaySimulator.Domain.Results;

namespace RailwaySimulator.Application;

public sealed class RouteSimulationService
{
    public SimulationReport Simulate(SimulationScenario scenario)
    {
        return Analyze(scenario).Report;
    }

    public SimulationAnalysis Analyze(SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        RouteExecution execution = scenario.Route.Execute(scenario.Train);
        SimulationReport report = CreateReport(scenario, execution.Result);
        SectionAnalysis[] trace = execution.Trace.Select(MapTrace).ToArray();
        SimulationMetrics metrics = CreateMetrics(scenario, execution, report, trace);
        return new SimulationAnalysis(report, metrics, trace);
    }

    private static SimulationReport CreateReport(SimulationScenario scenario, RouteRunResult result)
    {
        return result switch
        {
            RouteRunResult.Success success => new SimulationReport(
                true,
                scenario.SectionCount,
                scenario.Train.CurrentSpeed.Value,
                success.TotalTime.Value,
                success.TotalTime.Value,
                "Route completed successfully.",
                null),
            RouteRunResult.EndSpeedViolation violation => new SimulationReport(
                false,
                scenario.SectionCount,
                violation.Actual.Value,
                violation.ElapsedBeforeFinish.Value,
                violation.ElapsedBeforeFinish.Value,
                FormattableString.Invariant(
                    $"Final speed {violation.Actual.Value:F3} exceeds limit {violation.Limit.Value:F3}."),
                null),
            RouteRunResult.SectionFailed failure => new SimulationReport(
                false,
                scenario.SectionCount,
                null,
                null,
                failure.ElapsedBeforeFailure.Value,
                Describe(failure.Reason),
                failure.SectionIndex),
            _ => throw new InvalidOperationException("Unknown simulation result."),
        };
    }

    private static SimulationMetrics CreateMetrics(
        SimulationScenario scenario,
        RouteExecution execution,
        SimulationReport report,
        SectionAnalysis[] trace)
    {
        double configuredWait = scenario.Plan.Sections.Sum(section => section.ConfiguredStationWait);
        double? executedWait = trace.Any(section =>
            string.Equals(section.Type, RouteSectionKind.Station.ToString(), StringComparison.Ordinal)
            && section.ExecutedStationWait is null)
            ? null
            : trace.Sum(section => section.ExecutedStationWait ?? 0);
        double plannedDistance = scenario.Plan.Sections.Sum(section => section.PlannedDistance ?? 0);
        double movingTime = trace
            .Where(section => section.Succeeded && section.ElapsedTime is not null)
            .Sum(section => Math.Max(0, section.ElapsedTime!.Value - (section.ExecutedStationWait ?? 0)));
        double[] sampledSpeeds =
        [
            scenario.Plan.InitialSpeed,
            .. trace.Where(section => section.ExitSpeed is not null).Select(section => section.ExitSpeed!.Value),
        ];
        double[] modeledAccelerations = trace
            .Where(section => section.ModeledPeakAcceleration is not null)
            .Select(section => section.ModeledPeakAcceleration!.Value)
            .ToArray();
        var margins = new List<(double Margin, string Label)>();
        margins.AddRange(trace
            .Where(section => section.SpeedLimitMargin is not null)
            .Select(section => (
                section.SpeedLimitMargin!.Value,
                $"Station section {section.Index} speed limit")));
        if (execution.Result is RouteRunResult.Success or RouteRunResult.EndSpeedViolation)
        {
            margins.Add((
                execution.FinalSpeedLimit.Value - report.FinalSpeed!.Value,
                "Final speed limit"));
        }

        (double Margin, string Label)? limitingConstraint = margins.Count == 0
            ? null
            : margins.MinBy(candidate => candidate.Margin);
        string tightestConstraint = limitingConstraint?.Label ?? FindFallbackConstraint(trace, report);
        return new SimulationMetrics(
            report.ElapsedTime,
            movingTime,
            configuredWait,
            executedWait,
            plannedDistance,
            report.Succeeded ? plannedDistance : null,
            sampledSpeeds.Average(),
            sampledSpeeds.Max(),
            sampledSpeeds.Min(),
            modeledAccelerations.Length == 0 ? null : modeledAccelerations.Max(),
            limitingConstraint?.Margin,
            scenario.SectionCount,
            scenario.Plan.Sections.Count(section => section.Kind == RouteSectionKind.Station),
            tightestConstraint);
    }

    private static string FindFallbackConstraint(
        SectionAnalysis[] trace,
        SimulationReport report)
    {
        if (report.FailedSection is int failedSection)
            return $"Section {failedSection} failure";
        SectionAnalysis? slowest = trace
            .Where(section => section.Succeeded && section.ElapsedTime is not null)
            .MaxBy(section => section.ElapsedTime);
        return slowest is null ? "No evaluated constraint" : $"Section {slowest.Index} elapsed time heuristic";
    }

    private static SectionAnalysis MapTrace(RouteSectionTrace trace)
    {
        string result = trace.Result switch
        {
            SectionPassResult.Success => "Success",
            SectionPassResult.CannotMove failure => failure.Reason,
            SectionPassResult.SpeedLimitExceeded failure =>
                FormattableString.Invariant(
                    $"Speed {failure.Actual.Value:F3} exceeds limit {failure.Limit.Value:F3}."),
            _ => throw new InvalidOperationException("Unknown section result."),
        };
        return new SectionAnalysis(
            trace.Index,
            trace.Kind.ToString(),
            trace.Result is SectionPassResult.Success,
            trace.EntrySpeed.Value,
            trace.ExitSpeed?.Value,
            trace.Elapsed?.Value,
            trace.PlannedDistance?.Value,
            trace.ConfiguredStationWait.Value,
            trace.ExecutedStationWait?.Value,
            trace.ModeledPeakAcceleration?.Value,
            trace.SpeedLimit?.Value,
            trace.SpeedLimit?.Value - trace.EntrySpeed.Value,
            result);
    }

    private static string Describe(SectionPassResult result)
    {
        return result switch
        {
            SectionPassResult.CannotMove failure => failure.Reason,
            SectionPassResult.SpeedLimitExceeded failure =>
                FormattableString.Invariant(
                    $"Speed {failure.Actual.Value:F3} exceeds section limit {failure.Limit.Value:F3}."),
            _ => "The route section failed.",
        };
    }
}
