using Pos.Application.Security;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCallCount { get; private set; }

    public int VerifyCallCount { get; private set; }

    public string Hash(string password)
    {
        HashCallCount++;

        return $"hashed:{password}";
    }

    public bool Verify(string password, string passwordHash)
    {
        VerifyCallCount++;

        return passwordHash == $"hashed:{password}";
    }
}
