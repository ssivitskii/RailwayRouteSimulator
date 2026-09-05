using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RailwaySimulator.Api;
using RailwaySimulator.Application.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RailwaySimulator.ApiTests;

public sealed class SimulationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public SimulationApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidStationRouteReturnsAnalysis()
    {
        RouteConfiguration request = CreateValidConfiguration();

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/simulations/analyze", request);
        JsonDocument analysis = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(analysis.RootElement.GetProperty("report").GetProperty("succeeded").GetBoolean());
        Assert.True(analysis.RootElement.TryGetProperty("metrics", out _));
        Assert.Equal(3, analysis.RootElement.GetProperty("trace").GetArrayLength());
    }

    [Fact]
    public async Task MalformedJsonReturnsBadRequest()
    {
        using var content = new StringContent("{ not-json", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/api/simulations/analyze", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DomainInvalidConfigurationReturnsProblemDetails()
    {
        var invalidTrain = new RouteConfiguration.TrainConfiguration
        {
            Mass = 0,
            MaximumForce = 2_000,
            Precision = 0.1,
            InitialSpeed = 0,
        };
        RouteConfiguration request = CreateValidConfiguration(train: invalidTrain);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/simulations/analyze", request);
        JsonDocument problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Mass", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrecisionBelowHttpSafetyLimitReturnsBadRequest()
    {
        var train = new RouteConfiguration.TrainConfiguration
        {
            Mass = 1_000,
            MaximumForce = 2_000,
            Precision = 0.0001,
            InitialSpeed = 0,
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/simulations/analyze",
            CreateValidConfiguration(train: train));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullBodyReturnsBadRequest()
    {
        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/api/simulations/analyze", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FiniteInputsThatOverflowIntermediateArithmeticReturnDeterministicFailure()
    {
        var train = new RouteConfiguration.TrainConfiguration
        {
            Mass = 1,
            MaximumForce = 1e308,
            Precision = 1e308,
            InitialSpeed = 0,
        };
        RouteConfiguration.SectionConfiguration[] sections =
        [
            new RouteConfiguration.SectionConfiguration
            {
                Type = "powered",
                Distance = 1,
                Force = 1e308,
            },
        ];
        RouteConfiguration request = CreateValidConfiguration(train, sections);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/simulations/analyze", request);
        JsonDocument analysis = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(analysis.RootElement.GetProperty("report").GetProperty("succeeded").GetBoolean());
        Assert.Contains(
            "numeric range",
            analysis.RootElement.GetProperty("report").GetProperty("summary").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task StationWaitAggregateOverflowReturnsProblemDetails()
    {
        RouteConfiguration.SectionConfiguration[] sections =
        [
            new RouteConfiguration.SectionConfiguration
            {
                Type = "station",
                AlightingTime = 1e308,
                BoardingTime = 1e308,
                SpeedLimit = 6,
            },
        ];

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/simulations/analyze",
            CreateValidConfiguration(sections: sections));
        JsonDocument problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("station wait", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouteElapsedTimeOverflowReturnsFailedAnalysisInsteadOfServerError()
    {
        var train = new RouteConfiguration.TrainConfiguration
        {
            Mass = 1,
            MaximumForce = 1,
            Precision = 1e307,
            InitialSpeed = 1e-308,
        };
        RouteConfiguration.SectionConfiguration[] sections =
        [
            new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 1 },
            new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 1 },
        ];

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/simulations/analyze",
            CreateValidConfiguration(train, sections));
        JsonDocument analysis = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(analysis.RootElement.GetProperty("report").GetProperty("succeeded").GetBoolean());
        Assert.Equal(1, analysis.RootElement.GetProperty("report").GetProperty("failedSection").GetInt32());
        JsonElement trace = analysis.RootElement.GetProperty("trace");
        Assert.True(trace[0].GetProperty("succeeded").GetBoolean());
        Assert.False(trace[1].GetProperty("succeeded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, trace[1].GetProperty("elapsedTime").ValueKind);
    }

    [Fact]
    public async Task StationManeuverElapsedOverflowReturnsFailedAnalysis()
    {
        var train = new RouteConfiguration.TrainConfiguration
        {
            Mass = 1,
            MaximumForce = 1e-308,
            Precision = 1e308,
            InitialSpeed = 1,
        };
        RouteConfiguration.SectionConfiguration[] sections =
        [
            new RouteConfiguration.SectionConfiguration
            {
                Type = "station",
                AlightingTime = 0,
                BoardingTime = 0,
                SpeedLimit = 1,
            },
        ];

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/simulations/analyze",
            CreateValidConfiguration(train, sections, endSpeedLimit: 1));
        JsonDocument analysis = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(analysis.RootElement.GetProperty("report").GetProperty("succeeded").GetBoolean());
        Assert.Equal(0, analysis.RootElement.GetProperty("report").GetProperty("failedSection").GetInt32());
        Assert.Contains(
            "numeric range",
            analysis.RootElement.GetProperty("report").GetProperty("summary").GetString(),
            StringComparison.Ordinal);
        Assert.False(analysis.RootElement.GetProperty("trace")[0].GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task LargeFiniteSpeedAverageRemainsFinite()
    {
        var train = new RouteConfiguration.TrainConfiguration
        {
            Mass = 1,
            MaximumForce = 0,
            Precision = 1,
            InitialSpeed = 1e308,
        };
        RouteConfiguration.SectionConfiguration[] sections =
        [
            new RouteConfiguration.SectionConfiguration { Type = "normal", Distance = 1 },
        ];

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/simulations/analyze",
            CreateValidConfiguration(train, sections, endSpeedLimit: 1e308));
        JsonDocument analysis = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(analysis.RootElement.GetProperty("report").GetProperty("succeeded").GetBoolean());
        Assert.Equal(
            1e308,
            analysis.RootElement.GetProperty("metrics").GetProperty("averageSampledSpeed").GetDouble());
    }

    [Fact]
    public void AnalyzeEndpointAloneUsesTheConfiguredConcurrencyPolicy()
    {
        EndpointDataSource dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        Endpoint analyze = Assert.Single(
            dataSource.Endpoints,
            endpoint =>
                endpoint.DisplayName?.Contains("SimulationsController.Analyze", StringComparison.Ordinal) is true);
        EnableRateLimitingAttribute? rateLimit = analyze.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        RateLimiterOptions options = _factory.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.NotNull(rateLimit);
        Assert.Equal(ApiLimits.SimulationRateLimitPolicy, rateLimit.PolicyName);
        Assert.DoesNotContain(
            dataSource.Endpoints.Where(endpoint => endpoint != analyze),
            endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null);
        Assert.Equal(4, ApiLimits.MaximumConcurrentSimulations);
        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        Assert.NotNull(options.OnRejected);
    }

    [Fact]
    public async Task KestrelPayloadTooLargeExceptionReturnsSafeProblemDetails()
    {
        IExceptionHandler handler = Assert.Single(
            _factory.Services.GetServices<IExceptionHandler>(),
            candidate => candidate is ApiExceptionHandler);
        var context = new DefaultHttpContext
        {
            RequestServices = _factory.Services,
        };
        context.Response.Body = new MemoryStream();
        var exception = new BadHttpRequestException(
            "Sensitive transport detail must not be returned.",
            StatusCodes.Status413PayloadTooLarge);

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        context.Response.Body.Position = 0;
        JsonDocument problem = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Request body too large", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            $"The request body must not exceed {ApiLimits.MaximumRequestBytes} bytes.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(
            "Sensitive transport detail",
            problem.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouteAboveSectionLimitReturnsBadRequest()
    {
        RouteConfiguration.SectionConfiguration[] sections = Enumerable
            .Range(0, ApiLimits.MaximumSections + 1)
            .Select(_ => new RouteConfiguration.SectionConfiguration
                {
                    Type = "normal",
                    Distance = 1,
                })
            .ToArray();
        RouteConfiguration request = CreateValidConfiguration(sections: sections);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/simulations/analyze", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static RouteConfiguration CreateValidConfiguration(
        RouteConfiguration.TrainConfiguration? train = null,
        IReadOnlyList<RouteConfiguration.SectionConfiguration?>? sections = null,
        double endSpeedLimit = 6)
    {
        return new RouteConfiguration
        {
            Train = train ?? new RouteConfiguration.TrainConfiguration
            {
                Mass = 1_000,
                MaximumForce = 2_000,
                Precision = 0.1,
                InitialSpeed = 0,
            },
            EndSpeedLimit = endSpeedLimit,
            Sections = sections ??
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
                    AlightingTime = 5,
                    BoardingTime = 4,
                    SpeedLimit = 6,
                },
                new RouteConfiguration.SectionConfiguration
                {
                    Type = "normal",
                    Distance = 10,
                },
            ],
        };
    }
}
