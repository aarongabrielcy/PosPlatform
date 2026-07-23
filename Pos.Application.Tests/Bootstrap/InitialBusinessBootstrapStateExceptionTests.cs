using Pos.Application.Bootstrap;

namespace Pos.Application.Tests.Bootstrap;

public class InitialBusinessBootstrapStateExceptionTests
{
    [Fact]
    public void MessageOnlyConstructorSetsMessageAndNoInnerException()
    {
        var exception = new InitialBusinessBootstrapStateException("Estado inconsistente.");

        Assert.Equal("Estado inconsistente.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ConstructorWithInnerExceptionPreservesTheCause()
    {
        var inner = new InvalidOperationException("Causa original.");

        var exception = new InitialBusinessBootstrapStateException("Estado inconsistente.", inner);

        Assert.Equal("Estado inconsistente.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void EmptyOrWhitespaceMessageIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new InitialBusinessBootstrapStateException(""));
        Assert.Throws<ArgumentException>(() => new InitialBusinessBootstrapStateException("   "));
    }
}
