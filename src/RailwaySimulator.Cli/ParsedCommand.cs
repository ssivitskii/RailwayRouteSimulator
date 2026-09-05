namespace RailwaySimulator.Cli;

internal sealed record ParsedCommand(
    string Name,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Options,
    OutputFormat Format);
