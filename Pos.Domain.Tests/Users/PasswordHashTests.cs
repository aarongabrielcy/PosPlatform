using Pos.Domain.Common.Exceptions;
using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class PasswordHashTests
{
    private const string SyntheticHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    [Fact]
    public void PreservesValueExactly()
    {
        var passwordHash = new PasswordHash(SyntheticHash);

        Assert.Equal(SyntheticHash, passwordHash.Value);
    }

    [Fact]
    public void UsesValueEquality()
    {
        var first = new PasswordHash(SyntheticHash);
        var second = new PasswordHash(SyntheticHash);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<DomainValidationException>(() => new PasswordHash(null!));
    }

    [Fact]
    public void RejectsEmpty()
    {
        Assert.Throws<DomainValidationException>(() => new PasswordHash(string.Empty));
    }

    [Fact]
    public void RejectsWhitespace()
    {
        Assert.Throws<DomainValidationException>(() => new PasswordHash("   "));
    }

    [Fact]
    public void AcceptsMaximumLength()
    {
        var value = new string('a', PasswordHash.MaxLength);

        var passwordHash = new PasswordHash(value);

        Assert.Equal(value, passwordHash.Value);
    }

    [Fact]
    public void RejectsMoreThanMaxLengthCharacters()
    {
        var tooLong = new string('a', PasswordHash.MaxLength + 1);

        Assert.Throws<DomainValidationException>(() => new PasswordHash(tooLong));
    }

    [Fact]
    public void ToStringDoesNotExposeTheHash()
    {
        var passwordHash = new PasswordHash(SyntheticHash);

        Assert.DoesNotContain(SyntheticHash, passwordHash.ToString());
    }
}
