using RailwaySimulator.Application;
using RailwaySimulator.Application.Configuration;
using System.Globalization;

namespace RailwaySimulator.Cli;

public sealed class CliApplication
{
    private const int DefaultOptimizationIterations = 21;
    private static readonly HashSet<string> NoCommandOptions = [];
    private static readonly HashSet<string> OptimizationOptions = new(StringComparer.Ordinal)
    {
        "--iterations",
        "--max",
        "--min",
    };

    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly RouteConfigurationLoader _loader = new();
    private readonly RouteSimulationService _simulation = new();
    private readonly RouteComparisonService _comparison = new();
    private readonly InitialSpeedOptimizer _optimizer = new();

    public CliApplication(TextWriter output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
        {
            await WriteHelpAsync().ConfigureAwait(false);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            ParsedCommand command = Parse(args);
            return command.Name switch
            {
                "simulate" => await RunSingleAsync(command, detailed: false, cancellationToken).ConfigureAwait(false),
                "analyze" => await RunSingleAsync(command, detailed: true, cancellationToken).ConfigureAwait(false),
                "compare" => await RunComparisonAsync(command, cancellationToken).ConfigureAwait(false),
                "optimize" => await RunOptimizationAsync(command, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown command '{command.Name}'."),
            };
        }
        catch (RouteConfigurationException exception)
        {
            await _error.WriteLineAsync($"Configuration error: {exception.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (ArgumentException exception)
        {
            await _error.WriteLineAsync($"Usage error: {exception.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (IOException exception)
        {
            await _error.WriteLineAsync($"I/O error: {exception.Message}").ConfigureAwait(false);
            return 3;
        }
        catch (UnauthorizedAccessException exception)
        {
            await _error.WriteLineAsync($"I/O error: {exception.Message}").ConfigureAwait(false);
            return 3;
        }
    }

    private static ParsedCommand Parse(string[] args)
    {
        string name = args[0].ToLowerInvariant();
        var arguments = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        OutputFormat format = OutputFormat.Text;
        bool formatSpecified = false;
        for (int index = 1; index < args.Length; index++)
        {
            string value = args[index];
            if (string.Equals(value, "--json", StringComparison.Ordinal))
            {
                if (formatSpecified)
                    throw new ArgumentException("Output format was specified more than once.");
                format = OutputFormat.Json;
                formatSpecified = true;
                continue;
            }

            if (!value.StartsWith("--", StringComparison.Ordinal))
            {
                arguments.Add(value);
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '{value}' requires a value.");
            string optionValue = args[++index];
            if (string.Equals(value, "--format", StringComparison.Ordinal))
            {
                if (formatSpecified)
                    throw new ArgumentException("Output format was specified more than once.");
                format = ParseFormat(optionValue);
                formatSpecified = true;
            }
            else if (!options.TryAdd(value, optionValue))
            {
                throw new ArgumentException($"Option '{value}' was specified more than once.");
            }
        }

        return new ParsedCommand(name, arguments, options, format);
    }

    private static OutputFormat ParseFormat(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "text" => OutputFormat.Text,
            "json" => OutputFormat.Json,
            "csv" => OutputFormat.Csv,
            _ => throw new ArgumentException("Format must be text, json, or csv."),
        };
    }

    private static void EnsureOptions(ParsedCommand command, HashSet<string> allowed)
    {
        string? unknown = command.Options.Keys.FirstOrDefault(option => !allowed.Contains(option));
        if (unknown is not null)
            throw new ArgumentException($"Unknown option '{unknown}' for command '{command.Name}'.");
    }

    private static double RequiredDouble(ParsedCommand command, string option)
    {
        if (!command.Options.TryGetValue(option, out string? value))
            throw new ArgumentException($"Option '{option}' is required.");
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            throw new ArgumentException($"Option '{option}' must be a number using invariant notation.");
        return result;
    }

    private static int OptionalIterations(ParsedCommand command)
    {
        if (!command.Options.TryGetValue("--iterations", out string? value))
            return DefaultOptimizationIterations;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int iterations))
            throw new ArgumentException("Option '--iterations' must be an integer.");
        return iterations;
    }

    private async Task<int> RunSingleAsync(
        ParsedCommand command,
        bool detailed,
        CancellationToken cancellationToken)
    {
        EnsureOptions(command, NoCommandOptions);
        if (command.Arguments.Count != 1)
            throw new ArgumentException($"Command '{command.Name}' requires exactly one route file.");
        string path = command.Arguments[0];
        SimulationScenario scenario = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        SimulationAnalysis analysis = _simulation.Analyze(scenario);
        await ReportFormatter.WriteSimulationAsync(_output, path, analysis, detailed, command.Format)
            .ConfigureAwait(false);
        return analysis.Report.Succeeded ? 0 : 1;
    }

    private async Task<int> RunComparisonAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        EnsureOptions(command, NoCommandOptions);
        if (command.Arguments.Count < 2)
            throw new ArgumentException("Command 'compare' requires at least two route files.");
        var entries = new List<RouteAnalysisEntry>(command.Arguments.Count);
        for (int index = 0; index < command.Arguments.Count; index++)
        {
            string path = command.Arguments[index];
            SimulationScenario scenario = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            entries.Add(new RouteAnalysisEntry(path, index, _simulation.Analyze(scenario)));
        }

        RouteComparisonReport report = _comparison.Compare(entries);
        await ReportFormatter.WriteComparisonAsync(_output, report, command.Format).ConfigureAwait(false);
        return report.Rankings[0].Succeeded ? 0 : 1;
    }

    private async Task<int> RunOptimizationAsync(ParsedCommand command, CancellationToken cancellationToken)
    {
        EnsureOptions(command, OptimizationOptions);
        if (command.Arguments.Count != 1)
            throw new ArgumentException("Command 'optimize' requires exactly one route file.");
        RouteConfiguration configuration = await _loader.LoadConfigurationAsync(
            command.Arguments[0],
            cancellationToken).ConfigureAwait(false);
        OptimizationReport report = _optimizer.Optimize(
            configuration,
            RequiredDouble(command, "--min"),
            RequiredDouble(command, "--max"),
            OptionalIterations(command));
        await ReportFormatter.WriteOptimizationAsync(_output, report, command.Format).ConfigureAwait(false);
        return report.RecommendedAnalysis?.Report.Succeeded == true ? 0 : 1;
    }

    private async Task WriteHelpAsync()
    {
        await _output.WriteLineAsync("Railway Route Simulator").ConfigureAwait(false);
        await _output.WriteLineAsync("simulate <route.json> [--format text|json|csv]").ConfigureAwait(false);
        await _output.WriteLineAsync("analyze <route.json> [--format text|json|csv]").ConfigureAwait(false);
        await _output.WriteLineAsync("compare <route1.json> <route2.json> [...] [--format text|json|csv]")
            .ConfigureAwait(false);
        await _output.WriteLineAsync(
            "optimize <route.json> --min <speed> --max <speed> [--iterations N] [--format text|json|csv]")
            .ConfigureAwait(false);
        await _output.WriteLineAsync("--json remains an alias for '--format json'.").ConfigureAwait(false);
        await _output.WriteLineAsync("Exit codes: 0 success, 1 best/result failed, 2 usage/configuration, 3 I/O.")
            .ConfigureAwait(false);
    }
}
