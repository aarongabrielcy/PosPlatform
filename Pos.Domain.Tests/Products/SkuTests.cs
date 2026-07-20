using Pos.Domain.Common.Exceptions;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class SkuTests
{
    [Fact]
    public void NormalizesToUpperCase()
    {
        var sku = new Sku("pal-001");

        Assert.Equal("PAL-001", sku.Value);
    }

    [Fact]
    public void TrimsOuterSpaces()
    {
        var sku = new Sku("  PAL-001  ");

        Assert.Equal("PAL-001", sku.Value);
    }

    [Theory]
    [InlineData("PAL-001")]
    [InlineData("HELADO_VAINILLA")]
    [InlineData("PROD.2026")]
    [InlineData("A1")]
    public void PreservesValidValue(string value)
    {
        var sku = new Sku(value);

        Assert.Equal(value, sku.Value);
    }

    [Fact]
    public void UsesValueEquality()
    {
        var first = new Sku("PAL-001");
        var second = new Sku("pal-001");

        Assert.Equal(first, second);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<DomainValidationException>(() => new Sku(null!));
    }

    [Fact]
    public void RejectsEmpty()
    {
        Assert.Throws<DomainValidationException>(() => new Sku(string.Empty));
    }

    [Fact]
    public void RejectsLengthLessThanTwo()
    {
        Assert.Throws<DomainValidationException>(() => new Sku("A"));
    }

    [Fact]
    public void RejectsLengthGreaterThanForty()
    {
        var tooLong = new string('A', 41);

        Assert.Throws<DomainValidationException>(() => new Sku(tooLong));
    }

    [Theory]
    [InlineData("PAL 001")]
    [InlineData("PAL/001")]
    [InlineData("NIÑO-001")]
    [InlineData("PAL@001")]
    public void RejectsInvalidValues(string value)
    {
        Assert.Throws<DomainValidationException>(() => new Sku(value));
    }

    [Fact]
    public void ToStringReturnsValue()
    {
        var sku = new Sku("PAL-001");

        Assert.Equal("PAL-001", sku.ToString());
    }
}
