namespace OrdersApi.Contracts;

/// <summary>Payload returned by the health probe endpoint.</summary>
public sealed record HealthResponse(
    string Status,
    string Version,
    string Environment,
    DateTimeOffset Timestamp);

/// <summary>Payload returned by the version endpoint.</summary>
public sealed record VersionResponse(string Version, string Environment);

/// <summary>Root service descriptor.</summary>
public sealed record ServiceInfoResponse(string Service, string Version, string Environment);

/// <summary>A single order.</summary>
public sealed record Order(string Id, string Item, int Quantity, string Status);

/// <summary>Collection wrapper for the orders listing endpoint.</summary>
public sealed record OrdersResponse(IReadOnlyList<Order> Orders);
