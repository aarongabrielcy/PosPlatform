using Pos.Application.Authentication;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Authentication;

public class AuthenticatedUserTests
{
    [Fact]
    public void PermissionsCollectionHasNoDuplicatesEvenWhenSourceHasRepeatedValues()
    {
        var authenticatedUser = CreateAuthenticatedUser(
            [Permission.ProcessSale, Permission.ProcessSale, Permission.ViewReports]);

        Assert.Equal(2, authenticatedUser.Permissions.Count);
        Assert.Contains(Permission.ProcessSale, authenticatedUser.Permissions);
        Assert.Contains(Permission.ViewReports, authenticatedUser.Permissions);
    }

    [Fact]
    public void MutatingTheSourceCollectionAfterConstructionDoesNotAffectAuthenticatedUser()
    {
        var source = new List<Permission> { Permission.ProcessSale };
        var authenticatedUser = CreateAuthenticatedUser(source);

        source.Add(Permission.ManageUsers);

        Assert.Single(authenticatedUser.Permissions);
        Assert.DoesNotContain(Permission.ManageUsers, authenticatedUser.Permissions);
    }

    [Fact]
    public void HasPermissionReturnsTrueOnlyForGrantedPermissions()
    {
        var authenticatedUser = CreateAuthenticatedUser([Permission.ProcessSale]);

        Assert.True(authenticatedUser.HasPermission(Permission.ProcessSale));
        Assert.False(authenticatedUser.HasPermission(Permission.ManageUsers));
    }

    private static AuthenticatedUser CreateAuthenticatedUser(IEnumerable<Permission> permissions) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "ADMIN",
            "Administrator",
            "Administrator",
            permissions);
}
