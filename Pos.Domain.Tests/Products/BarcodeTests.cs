using Pos.Domain.Common.Exceptions;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class BarcodeTests
{
    [Fact]
    public void PreservesLeadingZeros()
    {
        var barcode = new Barcode("0001234");

        Assert.Equal("0001234", barcode.Value);
    }

    [Fact]
    public void TrimsOuterSpaces()
    {
        var barcode = new Barcode("  1234  ");

        Assert.Equal("1234", barcode.Value);
    }

    [Fact]
    public void UsesValueEquality()
    {
        var first = new Barcode("1234567890");
        var second = new Barcode("1234567890");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345678901234567890123456789012")]
    public void AcceptsLengthsBetweenFourAndThirtyTwo(string value)
    {
        var barcode = new Barcode(value);

        Assert.Equal(value, barcode.Value);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode(null!));
    }

    [Fact]
    public void RejectsEmpty()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode(string.Empty));
    }

    [Fact]
    public void RejectsLessThanFourCharacters()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode("123"));
    }

    [Fact]
    public void RejectsMoreThanThirtyTwoCharacters()
    {
        var tooLong = new string('1', 33);

        Assert.Throws<DomainValidationException>(() => new Barcode(tooLong));
    }

    [Fact]
    public void RejectsLetters()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode("12A4"));
    }

    [Fact]
    public void RejectsInternalSpaces()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode("12 34"));
    }

    [Fact]
    public void RejectsSymbols()
    {
        Assert.Throws<DomainValidationException>(() => new Barcode("12-34"));
    }

    [Fact]
    public void ToStringReturnsValue()
    {
        var barcode = new Barcode("1234567890");

        Assert.Equal("1234567890", barcode.ToString());
    }
}
