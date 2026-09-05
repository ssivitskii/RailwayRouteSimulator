using RailwaySimulator.Api;
using RailwaySimulator.Application;
using RailwaySimulator.Application.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = ApiLimits.MaximumRequestBytes);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSimulationRateLimiting();
builder.Services.AddSingleton<RouteConfigurationLoader>();
builder.Services.AddSingleton<RouteSimulationService>();

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health/live");
app.Run();

public partial class Program;
