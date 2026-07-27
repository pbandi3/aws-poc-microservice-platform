using Amazon.Lambda.AspNetCoreServer.Hosting;
using PocApi.Contracts;
using PocApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Runs as a standard Kestrel web server locally and inside WebApplicationFactory,
// and transparently switches to the API Gateway REST proxy adapter when hosted on Lambda.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Version and environment are injected by the CDK stack as Lambda environment variables.
// They fall back to local defaults so the app runs unchanged on a developer machine.
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

app.Run();

// Exposed so the integration test project can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program { }
