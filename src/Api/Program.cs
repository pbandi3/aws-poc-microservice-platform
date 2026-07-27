using Amazon.Lambda.AspNetCoreServer.Hosting;
using PocApi.Contracts;
using PocApi.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddEndpointsApiExplorer();
var app = builder.Build();
var appVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? "0.0.0-local";
var environmentName = Environment.GetEnvironmentVariable("ENVIRONMENT_NAME") ?? "local";
app.MapGet("/", () =>
    Results.Ok(new ServiceInfoResponse("aws-poc-microservice", appVersion, environmentName)));
app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("healthy", appVersion, environmentName, DateTimeOffset.UtcNow)));
app.MapGet("/api/version", () =>
    Results.Ok(new VersionResponse(appVersion, environmentName)));
app.MapGet("/api/greeting", (string? name, IGreetingService greetings) =>
    Results.Ok(new GreetingResponse(greetings.Greet(name))));
// feature/echo-endpoint: new capability, live in the PR environment before it reaches prod.
app.MapGet("/api/echo", (string? message) =>
    Results.Ok(new { message = message ?? string.Empty }));
app.Run();
public partial class Program { }
