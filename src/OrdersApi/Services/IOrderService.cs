using OrdersApi.Contracts;

namespace OrdersApi.Services;

/// <summary>
/// Read access to the order catalog. Kept behind an interface so business logic is unit-testable
/// independently of the HTTP pipeline and any future persistence layer.
/// </summary>
public interface IOrderService
{
    IReadOnlyList<Order> GetAll();

    Order? GetById(string id);
}
