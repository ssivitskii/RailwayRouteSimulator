using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.Sections;
using RailwaySimulator.Domain.ValueObjects;
using System.Text.Json;

namespace RailwaySimulator.Application.Configuration;

public sealed class RouteConfigurationLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<SimulationScenario> LoadAsync(string path, CancellationToken cancellationToken)
    {
        RouteConfiguration configuration = await LoadConfigurationAsync(path, cancellationToken).ConfigureAwait(false);
        return Map(configuration);
    }

    public async Task<RouteConfiguration> LoadConfigurationAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new RouteConfigurationException("A configuration file path is required.");

        try
        {
            await using FileStream stream = File.OpenRead(path);
            RouteConfiguration? configuration = await JsonSerializer.DeserializeAsync<RouteConfiguration>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            return configuration ?? throw new RouteConfigurationException("The configuration file is empty.");
        }
        catch (JsonException exception)
        {
            throw new RouteConfigurationException("The route configuration is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new RouteConfigurationException(exception.Message, exception);
        }
    }

    public SimulationScenario Map(RouteConfiguration configuration, double? initialSpeedOverride = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        RouteConfiguration.TrainConfiguration trainConfiguration = configuration.Train
            ?? throw new RouteConfigurationException("The 'train' object is required.");
        IReadOnlyList<RouteConfiguration.SectionConfiguration?> sections = configuration.Sections
            ?? throw new RouteConfigurationException("The 'sections' array is required.");
        if (sections.Count == 0)
            throw new RouteConfigurationException("At least one route section is required.");

        try
        {
            double configuredInitialSpeed = Require(trainConfiguration.InitialSpeed, "train.initialSpeed");
            double initialSpeed = initialSpeedOverride ?? configuredInitialSpeed;
            double endSpeedLimit = Require(configuration.EndSpeedLimit, "endSpeedLimit");
            var train = new Train(
                new Mass(Require(trainConfiguration.Mass, "train.mass")),
                new Force(Require(trainConfiguration.MaximumForce, "train.maximumForce")),
                new Precision(Require(trainConfiguration.Precision, "train.precision")),
                new Speed(initialSpeed));
            IRouteSection[] mappedSections = sections.Select(MapSection).ToArray();
            var route = new Route(mappedSections, new Speed(endSpeedLimit));
            RouteSectionPlan[] sectionPlans = mappedSections.Select(MapPlan).ToArray();
            return new SimulationScenario(train, route, new RoutePlan(initialSpeed, endSpeedLimit, sectionPlans));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new RouteConfigurationException(exception.Message, exception);
        }
    }

    private static IRouteSection MapSection(RouteConfiguration.SectionConfiguration? section, int index)
    {
        if (section is null)
            throw new RouteConfigurationException($"Section {index}: section must not be null.");

        string type = section.Type?.Trim().ToLowerInvariant()
            ?? throw new RouteConfigurationException($"Section {index}: 'type' is required.");

        try
        {
            return type switch
            {
                "normal" => new NormalTrack(new Distance(Require(section.Distance, "distance", index))),
                "powered" => new PoweredTrack(
                    new Distance(Require(section.Distance, "distance", index)),
                    new Force(Require(section.Force, "force", index))),
                "station" => new Station(
                    new Time(Require(section.AlightingTime, "alightingTime", index)),
                    new Time(Require(section.BoardingTime, "boardingTime", index)),
                    new Speed(Require(section.SpeedLimit, "speedLimit", index))),
                _ => throw new RouteConfigurationException($"Section {index}: unknown type '{section.Type}'."),
            };
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new RouteConfigurationException($"Section {index}: {exception.Message}", exception);
        }
    }

    private static RouteSectionPlan MapPlan(IRouteSection section, int index)
    {
        return section switch
        {
            NormalTrack normal => new RouteSectionPlan(
                index,
                RouteSectionKind.NormalTrack,
                normal.Length.Value,
                0,
                null),
            PoweredTrack powered => new RouteSectionPlan(
                index,
                RouteSectionKind.PoweredTrack,
                powered.Length.Value,
                0,
                null),
            Station station => new RouteSectionPlan(
                index,
                RouteSectionKind.Station,
                null,
                station.AlightingTime.Value + station.BoardingTime.Value,
                station.EntrySpeedLimit.Value),
            _ => throw new InvalidOperationException($"Unknown route section type '{section.GetType().Name}'."),
        };
    }

    private static double Require(double? value, string propertyName, int index)
    {
        return value ?? throw new RouteConfigurationException($"Section {index}: '{propertyName}' is required.");
    }

    private static double Require(double? value, string propertyPath)
    {
        return value ?? throw new RouteConfigurationException($"'{propertyPath}' is required.");
    }
}
