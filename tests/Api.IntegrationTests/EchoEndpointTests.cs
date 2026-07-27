using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
namespace PocApi.IntegrationTests;
public class EchoEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public EchoEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();
    [Fact]
    public async Task Echo_ReturnsProvidedMessage()
    {
        var response = await _client.GetAsync("/api/echo?message=hello-proserve");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EchoDto>();
        Assert.Equal("hello-proserve", body!.Message);
    }
    private sealed record EchoDto(string Message);
}
