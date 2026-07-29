using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PocApi.IntegrationTests;

public class FarewellEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public FarewellEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Farewell_ReturnsGoodbyeMessage()
    {
        var response = await _client.GetAsync("/api/farewell?name=ProServe");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FarewellDto>();
        Assert.Equal("Goodbye, ProServe!", body!.Message);
    }

    private sealed record FarewellDto(string Message);
}
