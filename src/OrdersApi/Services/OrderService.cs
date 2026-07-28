using OrdersApi.Contracts;

namespace OrdersApi.Services;

/// <summary>
/// In-memory order catalog. A real service would back this with a datastore; for the POC it
/// demonstrates a second, independently deployable microservice with its own domain.
/// </summary>
public sealed class OrderService : IOrderService
{
    private static readonly IReadOnlyList<Order> Catalog = new List<Order>
    {
        new("1001", "Widget", 3, "shipped"),
        new("1002", "Gadget", 1, "processing"),
        new("1003", "Gizmo", 7, "delivered")
    };

    public IReadOnlyList<Order> GetAll() => Catalog;

    public Order? GetById(string id) =>
        Catalog.FirstOrDefault(order => string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase));
}
