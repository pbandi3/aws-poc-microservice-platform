using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PocApi.IntegrationTests;

/// <summary>
/// Spins up the API in-memory via the ASP.NET test host. These run on every PR without
/// requiring any AWS infrastructure, giving fast feedback before the ephemeral deploy.
/// </summary>
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body!.Status);
    }

    [Fact]
    public async Task Greeting_WithName_ReturnsPersonalizedMessage()
    {
        var response = await _client.GetAsync("/api/greeting?name=ProServe");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GreetingDto>();
        Assert.NotNull(body);
        Assert.Equal("Hello, ProServe!", body!.Message);
    }

    [Fact]
    public async Task Greeting_WithoutName_ReturnsDefaultMessage()
    {
        var response = await _client.GetAsync("/api/greeting");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GreetingDto>();
        Assert.Equal("Hello, World!", body!.Message);
    }

    [Fact]
    public async Task Version_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record HealthDto(string Status, string Version, string Environment, DateTimeOffset Timestamp);

    private sealed record GreetingDto(string Message);
}
