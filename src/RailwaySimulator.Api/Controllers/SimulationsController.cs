using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RailwaySimulator.Application;
using RailwaySimulator.Application.Configuration;

namespace RailwaySimulator.Api.Controllers;

[ApiController]
[Route("api/simulations")]
public sealed class SimulationsController(
    RouteConfigurationLoader loader,
    RouteSimulationService simulationService) : ControllerBase
{
    [HttpPost("analyze")]
    [Consumes("application/json")]
    [ProducesResponseType<SimulationAnalysis>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [RequestSizeLimit(ApiLimits.MaximumRequestBytes)]
    [EnableRateLimiting(ApiLimits.SimulationRateLimitPolicy)]
    public ActionResult<SimulationAnalysis> Analyze([FromBody] RouteConfiguration configuration)
    {
        if (configuration.Sections?.Count > ApiLimits.MaximumSections)
        {
            throw new RouteConfigurationException(
                $"A route cannot contain more than {ApiLimits.MaximumSections} sections.");
        }

        if (configuration.Train?.Precision is double precision && precision < ApiLimits.MinimumPrecision)
        {
            throw new RouteConfigurationException(
                FormattableString.Invariant(
                    $"'train.precision' must be at least {ApiLimits.MinimumPrecision}."));
        }

        SimulationScenario scenario = loader.Map(configuration);
        return Ok(simulationService.Analyze(scenario));
    }
}
