using Amazon.Lambda.AspNetCoreServer.Hosting;
using OrdersApi.Contracts;
using OrdersApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

var appVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? "0.0.0-local";
var environmentName = Environment.GetEnvironmentVariable("ENVIRONMENT_NAME") ?? "local";

app.MapGet("/", () =>
    Results.Ok(new ServiceInfoResponse("aws-poc-orders", appVersion, environmentName)));

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("healthy", appVersion, environmentName, DateTimeOffset.UtcNow)));

app.MapGet("/api/version", () =>
    Results.Ok(new VersionResponse(appVersion, environmentName)));

app.MapGet("/api/orders", (IOrderService orders) =>
    Results.Ok(new OrdersResponse(orders.GetAll())));

app.MapGet("/api/orders/{id}", (string id, IOrderService orders) =>
{
    var order = orders.GetById(id);
    return order is null
        ? Results.NotFound(new { message = $"Order '{id}' not found" })
        : Results.Ok(order);
});

app.Run();

// Exposed so the integration test project can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program { }
