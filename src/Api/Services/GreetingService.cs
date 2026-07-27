namespace PocApi.Services;

/// <inheritdoc />
public sealed class GreetingService : IGreetingService
{
    private const string DefaultTarget = "World";

    public string Greet(string? name)
    {
        var target = string.IsNullOrWhiteSpace(name) ? DefaultTarget : name.Trim();
        return $"Hello, {target}!";
    }
}
