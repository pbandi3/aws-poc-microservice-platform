using OrdersApi.Services;
using Xunit;

namespace OrdersApi.UnitTests;

public class OrderServiceTests
{
    private readonly OrderService _sut = new();

    [Fact]
    public void GetAll_ReturnsSeededCatalog()
    {
        var orders = _sut.GetAll();

        Assert.Equal(3, orders.Count);
    }

    [Theory]
    [InlineData("1001", "Widget")]
    [InlineData("1002", "Gadget")]
    [InlineData("1003", "Gizmo")]
    public void GetById_WithKnownId_ReturnsOrder(string id, string expectedItem)
    {
        var order = _sut.GetById(id);

        Assert.NotNull(order);
        Assert.Equal(expectedItem, order!.Item);
    }

    [Fact]
    public void GetById_IsCaseInsensitive()
    {
        // Ids are numeric here, but the lookup guards against casing for alphanumeric ids.
        Assert.NotNull(_sut.GetById("1001"));
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        Assert.Null(_sut.GetById("9999"));
    }
}
