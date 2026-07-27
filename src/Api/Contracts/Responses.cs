namespace PocApi.Contracts;

/// <summary>Payload returned by the health probe endpoint.</summary>
public sealed record HealthResponse(
    string Status,
    string Version,
    string Environment,
    DateTimeOffset Timestamp);

/// <summary>Payload returned by the version endpoint.</summary>
public sealed record VersionResponse(string Version, string Environment);

/// <summary>Payload returned by the greeting endpoint.</summary>
public sealed record GreetingResponse(string Message);

/// <summary>Root service descriptor.</summary>
public sealed record ServiceInfoResponse(string Service, string Version, string Environment);
