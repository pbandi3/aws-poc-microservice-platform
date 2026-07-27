using PocApi.Services;
using Xunit;

namespace PocApi.UnitTests;

public class GreetingServiceTests
{
    private readonly GreetingService _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Greet_WithMissingName_ReturnsDefaultGreeting(string? name)
    {
        Assert.Equal("Hello, World!", _sut.Greet(name));
    }

    [Fact]
    public void Greet_WithName_ReturnsPersonalizedGreeting()
    {
        Assert.Equal("Hello, ProServe!", _sut.Greet("ProServe"));
    }

    [Fact]
    public void Greet_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Hello, ProServe!", _sut.Greet("   ProServe   "));
    }
}
