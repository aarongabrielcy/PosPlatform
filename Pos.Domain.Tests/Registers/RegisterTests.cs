using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;

namespace Pos.Domain.Tests.Registers;

public class RegisterTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static Register CreateRegister() =>
        new(RegisterId.New(), BranchId.New(), "Caja 1", "CAJA-1", UtcNow);

    [Fact]
    public void IsCreatedActive()
    {
        var register = CreateRegister();

        Assert.True(register.IsActive);
    }

    [Fact]
    public void NormalizesCode()
    {
        var register = new Register(RegisterId.New(), BranchId.New(), "Caja 1", "caja-1", UtcNow);

        Assert.Equal("CAJA-1", register.Code);
    }

    [Fact]
    public void RejectsEmptyBranchId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Register(RegisterId.New(), default, "Caja 1", "CAJA-1", UtcNow));
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Register(default, BranchId.New(), "Caja 1", "CAJA-1", UtcNow));
    }

    [Fact]
    public void RejectsInvalidCode()
    {
        Assert.Throws<DomainValidationException>(
            () => new Register(RegisterId.New(), BranchId.New(), "Caja 1", "CAJA 1", UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new Register(RegisterId.New(), BranchId.New(), "Caja 1", "CAJA-1", nonUtc));
    }

    [Fact]
    public void RenameAndChangeCodeValidateCorrectly()
    {
        var register = CreateRegister();

        register.Rename("Caja Principal");
        register.ChangeCode("CAJA-2");

        Assert.Equal("Caja Principal", register.Name);
        Assert.Equal("CAJA-2", register.Code);

        Assert.Throws<DomainValidationException>(() => register.Rename("A"));
        Assert.Throws<DomainValidationException>(() => register.ChangeCode("*"));
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var register = CreateRegister();

        register.Deactivate();
        Assert.False(register.IsActive);

        register.Activate();
        Assert.True(register.IsActive);
    }
}
