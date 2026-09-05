using RailwaySimulator.Application;
using RailwaySimulator.Application.Configuration;
using RailwaySimulator.Cli;

namespace RailwaySimulator.Tests;

public sealed class ConfigurationAndCliTests
{
    [Fact]
    public void MapWithValidConfigurationCreatesRunnableScenario()
    {
        var configuration = new RouteConfiguration
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
            ],
        };

        SimulationScenario scenario = new RouteConfigurationLoader().Map(configuration);
        SimulationReport report = new RouteSimulationService().Simulate(scenario);

        Assert.True(report.Succeeded);
        Assert.Equal(1, report.SectionCount);
    }

    [Fact]
    public void MapWithUnknownSectionTypeThrowsConfigurationError()
    {
        var configuration = new RouteConfiguration
        {
            Train = ValidTrain(),
            EndSpeedLimit = 6,
            Sections = [new RouteConfiguration.SectionConfiguration { Type = "tunnel" }],
        };

        RouteConfigurationException exception = Assert.Throws<RouteConfigurationException>(
            () => new RouteConfigurationLoader().Map(configuration));

        Assert.Contains("unknown type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapWithMissingRequiredScalarThrowsConfigurationError()
    {
        var configuration = new RouteConfiguration
        {
            Train = new RouteConfiguration.TrainConfiguration
            {
                MaximumForce = 2000,
                Precision = 0.1,
                InitialSpeed = 0,
            },
            EndSpeedLimit = 6,
            Sections = [new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 10 }],
        };

        RouteConfigurationException exception = Assert.Throws<RouteConfigurationException>(
            () => new RouteConfigurationLoader().Map(configuration));

        Assert.Contains("train.mass", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapWithNullSectionThrowsConfigurationErrorWithIndex()
    {
        var configuration = new RouteConfiguration
        {
            Train = ValidTrain(),
            EndSpeedLimit = 6,
            Sections = [null],
        };

        RouteConfigurationException exception = Assert.Throws<RouteConfigurationException>(
            () => new RouteConfigurationLoader().Map(configuration));

        Assert.Contains("Section 0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CliWithFailedSimulationReturnsOneAndPrintsReason()
    {
        string path = Path.GetTempFileName();
        try
        {
            const string json = """
                {
                  "train": { "mass": 1000, "maximumForce": 2000, "precision": 0.1, "initialSpeed": 0 },
                  "endSpeedLimit": 3,
                  "sections": [ { "type": "powered", "distance": 50, "force": 500 } ]
                }
                """;
            await File.WriteAllTextAsync(path, json);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await new CliApplication(output, error).RunAsync(
                ["simulate", path],
                CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Contains("SIMULATION FAILED", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CliWithInvalidJsonReturnsConfigurationErrorCode()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "not-json");
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await new CliApplication(output, error).RunAsync(
                ["simulate", path],
                CancellationToken.None);

            Assert.Equal(2, exitCode);
            Assert.Contains("Configuration error", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CliWithValidConfigurationCanWriteJsonReport()
    {
        string path = Path.GetTempFileName();
        try
        {
            const string json = """
                {
                  "train": { "mass": 1000, "maximumForce": 2000, "precision": 0.1, "initialSpeed": 0 },
                  "endSpeedLimit": 6,
                  "sections": [ { "type": "powered", "distance": 20, "force": 500 } ]
                }
                """;
            await File.WriteAllTextAsync(path, json);
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await new CliApplication(output, error).RunAsync(
                ["simulate", path, "--json"],
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains("\"Succeeded\": true", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AnalyzeJsonIsDeterministicAndContainsTrace()
    {
        string path = await WriteValidConfigurationAsync();
        try
        {
            using var firstOutput = new StringWriter();
            using var secondOutput = new StringWriter();
            using var firstError = new StringWriter();
            using var secondError = new StringWriter();

            int firstExit = await new CliApplication(firstOutput, firstError).RunAsync(
                ["analyze", path, "--format", "json"],
                CancellationToken.None);
            int secondExit = await new CliApplication(secondOutput, secondError).RunAsync(
                ["analyze", path, "--format", "json"],
                CancellationToken.None);

            Assert.Equal(0, firstExit);
            Assert.Equal(0, secondExit);
            Assert.Equal(firstOutput.ToString(), secondOutput.ToString());
            Assert.Contains("\"Trace\"", firstOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, firstError.ToString());
            Assert.Equal(string.Empty, secondError.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CompareCsvHasStableHeaderAndOneRowPerInput()
    {
        string path = await WriteValidConfigurationAsync();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await new CliApplication(output, error).RunAsync(
                ["compare", path, path, "--format", "csv"],
                CancellationToken.None);
            string[] lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(0, exitCode);
            Assert.Equal(3, lines.Length);
            Assert.Equal("rank,route,succeeded,elapsedSeconds,safetyMargin,result,recommended", lines[0]);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OptimizeRejectsIterationCountAboveCap()
    {
        string path = await WriteValidConfigurationAsync();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await new CliApplication(output, error).RunAsync(
                ["optimize", path, "--min", "0", "--max", "5", "--iterations", "1002"],
                CancellationToken.None);

            Assert.Equal(2, exitCode);
            Assert.Contains("Iterations must be between", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> WriteValidConfigurationAsync()
    {
        string path = Path.GetTempFileName();
        const string json = """
            {
              "train": { "mass": 1000, "maximumForce": 2000, "precision": 0.1, "initialSpeed": 0 },
              "endSpeedLimit": 6,
              "sections": [ { "type": "powered", "distance": 20, "force": 500 } ]
            }
            """;
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static RouteConfiguration.TrainConfiguration ValidTrain()
    {
        return new RouteConfiguration.TrainConfiguration
        {
            Mass = 1000,
            MaximumForce = 2000,
            Precision = 0.1,
            InitialSpeed = 0,
        };
    }
}
