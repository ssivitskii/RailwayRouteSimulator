using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace RailwaySimulator.Api;

internal static class SimulationRateLimitingExtensions
{
    public static IServiceCollection AddSimulationRateLimiting(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                IResult problem = Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Simulation capacity reached",
                    detail: "All simulation workers are busy. Try again shortly.");
                await problem.ExecuteAsync(context.HttpContext).ConfigureAwait(false);
            };
            options.AddConcurrencyLimiter(ApiLimits.SimulationRateLimitPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = ApiLimits.MaximumConcurrentSimulations;
                limiterOptions.QueueLimit = 0;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });
    }
}
