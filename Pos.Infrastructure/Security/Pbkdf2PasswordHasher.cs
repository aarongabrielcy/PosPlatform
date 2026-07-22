using System.Security.Cryptography;
using Pos.Application.Security;

namespace Pos.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    internal const string FormatVersion = "v1";
    internal const string AlgorithmName = "pbkdf2-sha256";
    internal const int ProductionIterations = 210_000;

    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 256;
    private const int MinIterations = 1;
    private const int MaxIterations = 10_000_000;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher()
        : this(ProductionIterations)
    {
    }

    // Constructor reservado para pruebas: permite reducir las iteraciones para que las pruebas
    // no dependan de la latencia real de PBKDF2. DI siempre usa el constructor público.
    internal Pbkdf2PasswordHasher(int iterations)
    {
        if (iterations < MinIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations debe ser mayor que cero.");
        }

        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ValidatePassword(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return string.Join(
            '$',
            FormatVersion,
            AlgorithmName,
            _iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(derived));
    }

    public bool Verify(string password, string passwordHash)
    {
        ValidatePassword(password);

        if (string.IsNullOrEmpty(passwordHash))
        {
            throw new ArgumentException("El hash no puede ser nulo ni vacío.", nameof(passwordHash));
        }

        var segments = passwordHash.Split('$');

        if (segments.Length != 5)
        {
            return false;
        }

        var version = segments[0];
        var algorithm = segments[1];
        var iterationsText = segments[2];
        var saltText = segments[3];
        var hashText = segments[4];

        if (!string.Equals(version, FormatVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(algorithm, AlgorithmName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(iterationsText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var iterations)
            || iterations < MinIterations
            || iterations > MaxIterations)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(saltText);
            expectedHash = Convert.FromBase64String(hashText);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            throw new ArgumentException(
                $"Password debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.", nameof(password));
        }
    }
}
