using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace PocApi.SmokeTests;

/// <summary>
/// End-to-end smoke tests executed against a live, deployed environment (ephemeral PR env,
/// dev, or prod). The target is provided via the API_BASE_URL environment variable by the
/// CI pipeline after a successful CDK deploy.
///
/// When API_BASE_URL is not set (e.g. a local `dotnet test` across the whole solution),
/// each test no-ops so it never fails outside of a deployment context.
/// </summary>
public class SmokeTests
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ITestOutputHelper _output;

    public SmokeTests(ITestOutputHelper output) => _output = output;

    private bool TargetUnset()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            _output.WriteLine("API_BASE_URL not set; skipping live smoke test.");
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
    public async Task Greeting_ReturnsPersonalizedMessage()
    {
        if (TargetUnset())
        {
            return;
        }

        var response = await Client.GetAsync($"{BaseUrl!.TrimEnd('/')}/api/greeting?name=ProServe");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello, ProServe!", body);
    }
}
