using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace PocApi.SmokeTests;

/// <summary>
/// End-to-end smoke tests for the orders microservice, run against a live deployment.
/// The target is provided via the ORDERS_BASE_URL environment variable by the CI pipeline.
/// When ORDERS_BASE_URL is unset (e.g. a greeting-only deploy, or local solution test runs),
/// each test no-ops so it never fails outside of an orders-deployment context.
/// </summary>
public class OrdersSmokeTests
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("ORDERS_BASE_URL");
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ITestOutputHelper _output;

    public OrdersSmokeTests(ITestOutputHelper output) => _output = output;

    private bool TargetUnset()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            _output.WriteLine("ORDERS_BASE_URL not set; skipping live orders smoke test.");
            return true;
        }

        return false;
    }

    [Fact]
    public async Task Health_IsReachableAndReportsHealthy()
    {
        if (TargetUnset())
        {
            return;
        }

        var response = await Client.GetAsync($"{BaseUrl!.TrimEnd('/')}/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListOrders_ReturnsCatalog()
    {
        if (TargetUnset())
        {
            return;
        }

        var response = await Client.GetAsync($"{BaseUrl!.TrimEnd('/')}/api/orders");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Widget", body);
    }
}
