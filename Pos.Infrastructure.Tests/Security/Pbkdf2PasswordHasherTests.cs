using Pos.Infrastructure.Security;

namespace Pos.Infrastructure.Tests.Security;

public class Pbkdf2PasswordHasherTests
{
    // Iteraciones reducidas exclusivamente para que las pruebas no dependan de la latencia real
    // de PBKDF2 con 210000 iteraciones. El constructor público de producción siempre usa 210000.
    private const int FastTestIterations = 10;

    private static Pbkdf2PasswordHasher CreateFastHasher() => new(FastTestIterations);

    // ---------- Formato ----------

    [Fact]
    public void HashProducesFiveSegments()
    {
        var hasher = CreateFastHasher();

        var hash = hasher.Hash("correct-horse-battery");

        Assert.Equal(5, hash.Split('$').Length);
    }

    [Fact]
    public void HashUsesVersionV1()
    {
        var hasher = CreateFastHasher();

        var segments = hasher.Hash("correct-horse-battery").Split('$');

        Assert.Equal("v1", segments[0]);
    }

    [Fact]
    public void HashUsesPbkdf2Sha256Algorithm()
    {
        var hasher = CreateFastHasher();

        var segments = hasher.Hash("correct-horse-battery").Split('$');

        Assert.Equal("pbkdf2-sha256", segments[1]);
    }

    [Fact]
    public void PublicConstructorUsesProductionIterations()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var segments = hasher.Hash("correct-horse-battery").Split('$');

        Assert.Equal("210000", segments[2]);
    }

    [Fact]
    public void HashSaltIsValidBase64WithAtLeastSixteenBytes()
    {
        var hasher = CreateFastHasher();

        var segments = hasher.Hash("correct-horse-battery").Split('$');
        var salt = Convert.FromBase64String(segments[3]);

        Assert.True(salt.Length >= 16);
    }

    [Fact]
    public void HashDerivedHashIsValidBase64WithAtLeastThirtyTwoBytes()
    {
        var hasher = CreateFastHasher();

        var segments = hasher.Hash("correct-horse-battery").Split('$');
        var derived = Convert.FromBase64String(segments[4]);

        Assert.True(derived.Length >= 32);
    }

    [Fact]
    public void HashDoesNotContainThePlainTextPassword()
    {
        var hasher = CreateFastHasher();
        const string password = "correct-horse-battery";

        var hash = hasher.Hash(password);

        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void SamePasswordProducesDifferentHashesEachTime()
    {
        var hasher = CreateFastHasher();
        const string password = "correct-horse-battery";

        var first = hasher.Hash(password);
        var second = hasher.Hash(password);

        Assert.NotEqual(first, second);
    }

    // ---------- Verify: casos válidos ----------

    [Fact]
    public void VerifyReturnsTrueForCorrectPassword()
    {
        var hasher = CreateFastHasher();
        const string password = "correct-horse-battery";

        var hash = hasher.Hash(password);

        Assert.True(hasher.Verify(password, hash));
    }

    [Fact]
    public void VerifyReturnsFalseForIncorrectPassword()
    {
        var hasher = CreateFastHasher();

        var hash = hasher.Hash("correct-horse-battery");

        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void VerifyReturnsFalseWhenHashHasBeenAltered()
    {
        var hasher = CreateFastHasher();
        const string password = "correct-horse-battery";

        var segments = hasher.Hash(password).Split('$');
        var tamperedHashBytes = Convert.FromBase64String(segments[4]);
        tamperedHashBytes[0] ^= 0xFF;
        segments[4] = Convert.ToBase64String(tamperedHashBytes);
        var tampered = string.Join('$', segments);

        Assert.False(hasher.Verify(password, tampered));
    }

    // ---------- Verify: formato corrupto o desconocido ----------

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("v1$pbkdf2-sha256$10$onlyfoursegments")]
    [InlineData("v1$pbkdf2-sha256$10$salt$hash$extra")]
    public void VerifyReturnsFalseForMalformedHash(string malformedHash)
    {
        var hasher = CreateFastHasher();

        Assert.False(hasher.Verify("correct-horse-battery", malformedHash));
    }

    [Fact]
    public void VerifyReturnsFalseForUnknownVersion()
    {
        var hasher = CreateFastHasher();
        var segments = hasher.Hash("correct-horse-battery").Split('$');
        segments[0] = "v2";

        Assert.False(hasher.Verify("correct-horse-battery", string.Join('$', segments)));
    }

    [Fact]
    public void VerifyReturnsFalseForUnknownAlgorithm()
    {
        var hasher = CreateFastHasher();
        var segments = hasher.Hash("correct-horse-battery").Split('$');
        segments[1] = "bcrypt";

        Assert.False(hasher.Verify("correct-horse-battery", string.Join('$', segments)));
    }

    [Fact]
    public void VerifyReturnsFalseForInvalidBase64Salt()
    {
        var hasher = CreateFastHasher();
        var segments = hasher.Hash("correct-horse-battery").Split('$');
        segments[3] = "not-valid-base64!!";

        Assert.False(hasher.Verify("correct-horse-battery", string.Join('$', segments)));
    }

    [Fact]
    public void VerifyReturnsFalseForInvalidBase64Hash()
    {
        var hasher = CreateFastHasher();
        var segments = hasher.Hash("correct-horse-battery").Split('$');
        segments[4] = "not-valid-base64!!";

        Assert.False(hasher.Verify("correct-horse-battery", string.Join('$', segments)));
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1")]
    [InlineData("0")]
    public void VerifyReturnsFalseForInvalidIterations(string iterations)
    {
        var hasher = CreateFastHasher();
        var segments = hasher.Hash("correct-horse-battery").Split('$');
        segments[2] = iterations;

        Assert.False(hasher.Verify("correct-horse-battery", string.Join('$', segments)));
    }

    [Fact]
    public void VerifyRejectsNullHash()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentException>(() => hasher.Verify("correct-horse-battery", null!));
    }

    [Fact]
    public void VerifyRejectsEmptyHash()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentException>(() => hasher.Verify("correct-horse-battery", string.Empty));
    }

    // ---------- Validación de password ----------

    [Fact]
    public void HashRejectsNullPassword()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentNullException>(() => hasher.Hash(null!));
    }

    [Fact]
    public void HashRejectsEmptyPassword()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash(string.Empty));
    }

    [Fact]
    public void HashRejectsWhitespaceOnlyPassword()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash("        "));
    }

    [Fact]
    public void HashRejectsPasswordShorterThanEightCharacters()
    {
        var hasher = CreateFastHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash("1234567"));
    }

    [Fact]
    public void HashRejectsPasswordLongerThanTwoHundredFiftySixCharacters()
    {
        var hasher = CreateFastHasher();
        var tooLong = new string('a', 257);

        Assert.Throws<ArgumentException>(() => hasher.Hash(tooLong));
    }

    [Fact]
    public void HashAcceptsPasswordAtMinimumAndMaximumLength()
    {
        var hasher = CreateFastHasher();

        var minLength = new string('a', 8);
        var maxLength = new string('a', 256);

        Assert.True(hasher.Verify(minLength, hasher.Hash(minLength)));
        Assert.True(hasher.Verify(maxLength, hasher.Hash(maxLength)));
    }

    [Fact]
    public void VerifyRejectsNullPassword()
    {
        var hasher = CreateFastHasher();
        var hash = hasher.Hash("correct-horse-battery");

        Assert.Throws<ArgumentNullException>(() => hasher.Verify(null!, hash));
    }

    [Fact]
    public void VerifyRejectsEmptyPassword()
    {
        var hasher = CreateFastHasher();
        var hash = hasher.Hash("correct-horse-battery");

        Assert.Throws<ArgumentException>(() => hasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void VerifyRejectsWhitespaceOnlyPassword()
    {
        var hasher = CreateFastHasher();
        var hash = hasher.Hash("correct-horse-battery");

        Assert.Throws<ArgumentException>(() => hasher.Verify("        ", hash));
    }

    // ---------- Constructor ----------

    [Fact]
    public void InternalConstructorRejectsNonPositiveIterations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pbkdf2PasswordHasher(0));
    }
}
