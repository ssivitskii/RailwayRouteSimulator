using RailwaySimulator.Application;
using System.Globalization;
using System.Text.Json;

namespace RailwaySimulator.Cli;

public static class ReportFormatter
{
    private const string AnalysisCsvHeader = "route,succeeded,result,elapsedSeconds,movingSeconds,configuredStationWaitSeconds,executedStationWaitSeconds,plannedTrackDistanceMeters,actualTrackDistanceMeters,averageSampledSpeed,maxSampledSpeed,minSampledSpeed,maxModeledAcceleration,smallestSpeedLimitMargin,sections,stations,tightestConstraint";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static async Task WriteSimulationAsync(
        TextWriter output,
        string route,
        SimulationAnalysis analysis,
        bool detailed,
        OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            if (detailed)
                await WriteJsonAsync(output, analysis).ConfigureAwait(false);
            else
                await WriteJsonAsync(output, analysis.Report).ConfigureAwait(false);
            return;
        }

        if (format == OutputFormat.Csv)
        {
            await WriteAnalysisCsvAsync(output, route, analysis).ConfigureAwait(false);
            return;
        }

        await WriteSimulationTextAsync(output, analysis.Report).ConfigureAwait(false);
        if (detailed)
            await WriteAnalysisDetailsAsync(output, analysis).ConfigureAwait(false);
    }

    public static async Task WriteComparisonAsync(
        TextWriter output,
        RouteComparisonReport report,
        OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(output, report).ConfigureAwait(false);
            return;
        }

        if (format == OutputFormat.Csv)
        {
            await output.WriteLineAsync("rank,route,succeeded,elapsedSeconds,safetyMargin,result,recommended")
                .ConfigureAwait(false);
            foreach (RankedRoute route in report.Rankings)
            {
                await output.WriteLineAsync(string.Join(
                    ",",
                    route.Rank.ToString(CultureInfo.InvariantCulture),
                    Escape(route.Route),
                    route.Succeeded.ToString(CultureInfo.InvariantCulture),
                    Format(route.ElapsedTime),
                    Format(route.SafetyMargin),
                    Escape(route.Result),
                    (route.Rank == 1).ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            }

            return;
        }

        await output.WriteLineAsync($"Recommended route: {report.Recommendation}").ConfigureAwait(false);
        await output.WriteLineAsync(report.Rationale).ConfigureAwait(false);
        foreach (RankedRoute route in report.Rankings)
        {
            await output.WriteLineAsync(
                $"{route.Rank}. {route.Route}: {(route.Succeeded ? "success" : "failure")}; " +
                $"time {FormatFixed(route.ElapsedTime)} s; margin {Format(route.SafetyMargin)}; {route.Result}")
                .ConfigureAwait(false);
        }
    }

    public static async Task WriteOptimizationAsync(
        TextWriter output,
        OptimizationReport report,
        OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(output, report).ConfigureAwait(false);
            return;
        }

        if (format == OutputFormat.Csv)
        {
            await output.WriteLineAsync("initialSpeed,succeeded,elapsedSeconds,safetyMargin,result,recommended")
                .ConfigureAwait(false);
            foreach (OptimizationCandidate candidate in report.Candidates)
            {
                await output.WriteLineAsync(string.Join(
                    ",",
                    Format(candidate.InitialSpeed),
                    candidate.Succeeded.ToString(CultureInfo.InvariantCulture),
                    Format(candidate.ElapsedTime),
                    Format(candidate.SafetyMargin),
                    Escape(candidate.Result),
                    candidate.Recommended.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            }

            return;
        }

        await output.WriteLineAsync($"Optimized parameter: {report.Parameter}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Search: [{FormatFixed(report.Minimum)}, {FormatFixed(report.Maximum)}], iterations: {report.Iterations}")
            .ConfigureAwait(false);
        if (report.RecommendedAnalysis is null || report.RecommendedValue is null)
        {
            await output.WriteLineAsync("No feasible initial speed was found on the evaluated grid.")
                .ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(
            $"Recommended initial speed: {FormatFixed(report.RecommendedValue.Value)} m/s")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Result: {report.RecommendedAnalysis.Report.Summary}; " +
            $"time {FormatFixed(report.RecommendedAnalysis.Report.ElapsedTime)} s; " +
            $"margin {Format(report.RecommendedAnalysis.Metrics.SmallestSpeedLimitMargin)}")
            .ConfigureAwait(false);
    }

    private static string Escape(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Format(double value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Format(double? value)
    {
        return value?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatFixed(double value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string FormatFixed(double? value)
    {
        return value is double number ? FormatFixed(number) : "unknown";
    }

    private static async Task WriteAnalysisCsvAsync(
        TextWriter output,
        string route,
        SimulationAnalysis analysis)
    {
        SimulationMetrics metrics = analysis.Metrics;
        await output.WriteLineAsync(AnalysisCsvHeader).ConfigureAwait(false);
        await output.WriteLineAsync(string.Join(
            ",",
            Escape(route),
            analysis.Report.Succeeded.ToString(CultureInfo.InvariantCulture),
            Escape(analysis.Report.Summary),
            Format(metrics.TotalElapsedTime),
            Format(metrics.MovingTime),
            Format(metrics.ConfiguredStationWait),
            Format(metrics.ExecutedStationWait),
            Format(metrics.PlannedTrackDistance),
            Format(metrics.ActualTrackDistance),
            Format(metrics.AverageSampledSpeed),
            Format(metrics.MaximumSampledSpeed),
            Format(metrics.MinimumSampledSpeed),
            Format(metrics.MaximumModeledAcceleration),
            Format(metrics.SmallestSpeedLimitMargin),
            metrics.SectionCount.ToString(CultureInfo.InvariantCulture),
            metrics.StationCount.ToString(CultureInfo.InvariantCulture),
            Escape(metrics.TightestConstraint))).ConfigureAwait(false);
    }

    private static async Task WriteAnalysisDetailsAsync(TextWriter output, SimulationAnalysis analysis)
    {
        SimulationMetrics metrics = analysis.Metrics;
        await output.WriteLineAsync($"Moving time: {FormatFixed(metrics.MovingTime)} s").ConfigureAwait(false);
        await output.WriteLineAsync($"Configured station wait: {FormatFixed(metrics.ConfiguredStationWait)} s")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Executed station wait: {FormatFixed(metrics.ExecutedStationWait)} s")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Planned track distance: {FormatFixed(metrics.PlannedTrackDistance)} m")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Actual track distance: {(metrics.ActualTrackDistance is double distance ? $"{FormatFixed(distance)} m" : "not reported for a failed route")}")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Sampled speed min/avg/max: {FormatFixed(metrics.MinimumSampledSpeed)} / " +
            $"{FormatFixed(metrics.AverageSampledSpeed)} / {FormatFixed(metrics.MaximumSampledSpeed)} m/s")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Maximum modeled acceleration: {FormatFixed(metrics.MaximumModeledAcceleration)} m/s²")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Smallest evaluated speed-limit margin: {Format(metrics.SmallestSpeedLimitMargin)}")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Stations: {metrics.StationCount}; tightest constraint/heuristic: {metrics.TightestConstraint}")
            .ConfigureAwait(false);
        await output.WriteLineAsync("Execution trace:").ConfigureAwait(false);
        foreach (SectionAnalysis section in analysis.Trace)
        {
            await output.WriteLineAsync(
                $"  [{section.Index}] {section.Type}: {FormatFixed(section.EntrySpeed)}->" +
                $"{FormatFixed(section.ExitSpeed)} m/s; time {FormatFixed(section.ElapsedTime)} s; " +
                $"acceleration {FormatFixed(section.ModeledPeakAcceleration)} m/s²; " +
                section.Result)
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonAsync<T>(TextWriter output, T value)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(value, SerializerOptions)).ConfigureAwait(false);
    }

    private static async Task WriteSimulationTextAsync(TextWriter output, SimulationReport report)
    {
        await output.WriteLineAsync(report.Succeeded ? "SIMULATION SUCCEEDED" : "SIMULATION FAILED")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Sections: {report.SectionCount}").ConfigureAwait(false);
        if (report.ElapsedTime is double elapsed)
        {
            await output.WriteLineAsync($"Total elapsed time: {FormatFixed(elapsed)} s").ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync(
                $"Total elapsed time: unknown; completed sections: {FormatFixed(report.CompletedSectionsElapsedTime)} s")
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync(report.FinalSpeed is double finalSpeed
            ? $"Final speed: {FormatFixed(finalSpeed)} m/s"
            : "Final speed: unknown because a section did not complete").ConfigureAwait(false);
        if (report.FailedSection is int index)
            await output.WriteLineAsync($"Failed section: {index}").ConfigureAwait(false);
        await output.WriteLineAsync($"Result: {report.Summary}").ConfigureAwait(false);
    }
}
