namespace PocApi.Services;
/// <inheritdoc />
public sealed class GreetingService : IGreetingService
{
    private const string DefaultTarget = "World";
    public string Greet(string? name)
    {
        var target = string.IsNullOrWhiteSpace(name) ? DefaultTarget : name.Trim();
        // hotfix: strip angle brackets to prevent reflected markup in the greeting response.
        target = target.Replace("<", string.Empty).Replace(">", string.Empty);
        return $"Hello, {target}!";
    }
}
