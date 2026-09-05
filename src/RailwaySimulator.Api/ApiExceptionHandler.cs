using Microsoft.AspNetCore.Diagnostics;
using RailwaySimulator.Application.Configuration;

namespace RailwaySimulator.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        bool isConfigurationError = exception is RouteConfigurationException;
        bool isPayloadTooLarge = exception is BadHttpRequestException
        {
            StatusCode: StatusCodes.Status413PayloadTooLarge,
        };
        int status = isConfigurationError
            ? StatusCodes.Status400BadRequest
            : isPayloadTooLarge
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status500InternalServerError;

        if (!isConfigurationError && !isPayloadTooLarge)
            logger.LogError(exception, "Unhandled simulation API exception.");

        string title = isConfigurationError
            ? "Invalid route configuration"
            : isPayloadTooLarge ? "Request body too large" : "Simulation failed";
        string detail = isConfigurationError
            ? exception.Message
            : isPayloadTooLarge
                ? $"The request body must not exceed {ApiLimits.MaximumRequestBytes} bytes."
                : "The simulation could not be completed.";
        IResult problem = Results.Problem(
            statusCode: status,
            title: title,
            detail: detail);
        await problem.ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
