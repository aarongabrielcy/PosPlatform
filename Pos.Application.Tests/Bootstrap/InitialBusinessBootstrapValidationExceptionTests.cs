using Pos.Application.Bootstrap;

namespace Pos.Application.Tests.Bootstrap;

public class InitialBusinessBootstrapValidationExceptionTests
{
    [Fact]
    public void ConstructorSetsMessageAndPreservesInnerException()
    {
        var inner = new InvalidOperationException("Causa original.");

        var exception = new InitialBusinessBootstrapValidationException("Nombre inválido.", inner);

        Assert.Equal("Nombre inválido.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void EmptyOrWhitespaceMessageIsRejected()
    {
        var inner = new InvalidOperationException("Causa original.");

        Assert.Throws<ArgumentException>(() => new InitialBusinessBootstrapValidationException("", inner));
        Assert.Throws<ArgumentException>(() => new InitialBusinessBootstrapValidationException("   ", inner));
    }
}
