using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrdersApi.IntegrationTests;

public class OrdersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.Equal("healthy", body!.Status);
    }

    [Fact]
    public async Task ListOrders_ReturnsCatalog()
    {
        var response = await _client.GetAsync("/api/orders");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OrdersDto>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Orders.Count);
    }

    [Fact]
    public async Task GetOrder_WithKnownId_ReturnsOrder()
    {
        var response = await _client.GetAsync("/api/orders/1001");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Widget", body!.Item);
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/orders/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record HealthDto(string Status, string Version, string Environment, DateTimeOffset Timestamp);

    private sealed record OrderDto(string Id, string Item, int Quantity, string Status);

    private sealed record OrdersDto(List<OrderDto> Orders);
}
