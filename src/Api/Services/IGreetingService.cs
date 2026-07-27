namespace PocApi.Services;

/// <summary>
/// Encapsulates greeting logic so it can be unit-tested in isolation from the HTTP pipeline.
/// </summary>
public interface IGreetingService
{
    string Greet(string? name);
}
