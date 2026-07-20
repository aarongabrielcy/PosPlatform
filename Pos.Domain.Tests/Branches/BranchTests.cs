using Pos.Domain.Branches;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Branches;

public class BranchTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static Branch CreateBranch() =>
        new(BranchId.New(), OrganizationId.New(), "Sucursal Centro", "SUC-1", UtcNow);

    [Fact]
    public void IsCreatedActive()
    {
        var branch = CreateBranch();

        Assert.True(branch.IsActive);
    }

    [Fact]
    public void NormalizesCode()
    {
        var branch = new Branch(BranchId.New(), OrganizationId.New(), "Sucursal Centro", "suc-1", UtcNow);

        Assert.Equal("SUC-1", branch.Code);
    }

    [Fact]
    public void RejectsEmptyOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Branch(BranchId.New(), default, "Sucursal Centro", "SUC-1", UtcNow));
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Branch(default, OrganizationId.New(), "Sucursal Centro", "SUC-1", UtcNow));
    }

    [Fact]
    public void RejectsInvalidCode()
    {
        Assert.Throws<DomainValidationException>(
            () => new Branch(BranchId.New(), OrganizationId.New(), "Sucursal Centro", "SUC 1", UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new Branch(BranchId.New(), OrganizationId.New(), "Sucursal Centro", "SUC-1", nonUtc));
    }

    [Fact]
    public void RenameAndChangeCodeValidateCorrectly()
    {
        var branch = CreateBranch();

        branch.Rename("Sucursal Norte");
        branch.ChangeCode("SUC-2");

        Assert.Equal("Sucursal Norte", branch.Name);
        Assert.Equal("SUC-2", branch.Code);

        Assert.Throws<DomainValidationException>(() => branch.Rename("A"));
        Assert.Throws<DomainValidationException>(() => branch.ChangeCode("*"));
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var branch = CreateBranch();

        branch.Deactivate();
        Assert.False(branch.IsActive);

        branch.Activate();
        Assert.True(branch.IsActive);
    }
}
