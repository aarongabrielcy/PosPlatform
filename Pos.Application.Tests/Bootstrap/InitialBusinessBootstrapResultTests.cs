using Pos.Application.Bootstrap;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Bootstrap;

public class InitialBusinessBootstrapResultTests
{
    [Fact]
    public void CreatedExposesTheFiveIdentifiers()
    {
        var organizationId = OrganizationId.New();
        var branchId = BranchId.New();
        var registerId = RegisterId.New();
        var roleId = RoleId.New();
        var userId = UserId.New();

        var result = InitialBusinessBootstrapResult.Created(organizationId, branchId, registerId, roleId, userId);

        Assert.Equal(InitialBusinessBootstrapStatus.Created, result.Status);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(branchId, result.BranchId);
        Assert.Equal(registerId, result.RegisterId);
        Assert.Equal(roleId, result.RoleId);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public void AlreadyInitializedDoesNotExposeAnyIdentifier()
    {
        var result = InitialBusinessBootstrapResult.AlreadyInitialized();

        Assert.Equal(InitialBusinessBootstrapStatus.AlreadyInitialized, result.Status);
        Assert.Null(result.OrganizationId);
        Assert.Null(result.BranchId);
        Assert.Null(result.RegisterId);
        Assert.Null(result.RoleId);
        Assert.Null(result.UserId);
    }

    [Fact]
    public void ToStringDoesNotExposeSensitiveData()
    {
        var result = InitialBusinessBootstrapResult.Created(
            OrganizationId.New(), BranchId.New(), RegisterId.New(), RoleId.New(), UserId.New());

        var text = result.ToString();

        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
    }
}
