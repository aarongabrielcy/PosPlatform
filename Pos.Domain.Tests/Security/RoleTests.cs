using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Domain.Tests.Security;

public class RoleTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static Role CreateRole(IEnumerable<Permission>? permissions = null) =>
        new(RoleId.New(), OrganizationId.New(), "Cajero", UtcNow, permissions);

    [Fact]
    public void IsCreatedActive()
    {
        var role = CreateRole();

        Assert.True(role.IsActive);
    }

    [Fact]
    public void TrimsName()
    {
        var role = new Role(RoleId.New(), OrganizationId.New(), "  Cajero  ", UtcNow);

        Assert.Equal("Cajero", role.Name);
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Role(default, OrganizationId.New(), "Cajero", UtcNow));
    }

    [Fact]
    public void RejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => new Role(RoleId.New(), default, "Cajero", UtcNow));
    }

    [Fact]
    public void RejectsInvalidName()
    {
        Assert.Throws<DomainValidationException>(
            () => new Role(RoleId.New(), OrganizationId.New(), "A", UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new Role(RoleId.New(), OrganizationId.New(), "Cajero", nonUtc));
    }

    [Fact]
    public void AcceptsInitialPermissionsList()
    {
        var role = CreateRole([Permission.ProcessSale, Permission.ApplyDiscount]);

        Assert.Equal(2, role.Permissions.Count);
        Assert.True(role.HasPermission(Permission.ProcessSale));
        Assert.True(role.HasPermission(Permission.ApplyDiscount));
    }

    [Fact]
    public void RemovesDuplicatePermissionsFromInitialList()
    {
        var role = CreateRole([Permission.ProcessSale, Permission.ProcessSale]);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void GrantPermissionAddsPermission()
    {
        var role = CreateRole();

        role.GrantPermission(Permission.ManageProducts);

        Assert.True(role.HasPermission(Permission.ManageProducts));
    }

    [Fact]
    public void GrantPermissionIsIdempotent()
    {
        var role = CreateRole();

        role.GrantPermission(Permission.ManageProducts);
        role.GrantPermission(Permission.ManageProducts);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void RevokePermissionRemovesPermission()
    {
        var role = CreateRole([Permission.ManageProducts]);

        role.RevokePermission(Permission.ManageProducts);

        Assert.False(role.HasPermission(Permission.ManageProducts));
    }

    [Fact]
    public void RevokePermissionIsIdempotentWhenMissing()
    {
        var role = CreateRole();

        role.RevokePermission(Permission.ManageProducts);

        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void HasPermissionReturnsCorrectResult()
    {
        var role = CreateRole([Permission.ViewReports]);

        Assert.True(role.HasPermission(Permission.ViewReports));
        Assert.False(role.HasPermission(Permission.ManageUsers));
    }

    [Fact]
    public void RenameValidatesName()
    {
        var role = CreateRole();

        role.Rename("Gerente");

        Assert.Equal("Gerente", role.Name);
        Assert.Throws<DomainValidationException>(() => role.Rename("A"));
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var role = CreateRole();

        role.Deactivate();
        Assert.False(role.IsActive);

        role.Activate();
        Assert.True(role.IsActive);
    }

    [Fact]
    public void PermissionsCollectionCannotBeMutatedExternally()
    {
        var role = CreateRole([Permission.ProcessSale]);

        var permissions = role.Permissions;

        Assert.IsNotType<HashSet<Permission>>(permissions);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Permission>)permissions).Add(Permission.ManageUsers));
        Assert.True(role.HasPermission(Permission.ProcessSale));
        Assert.False(role.HasPermission(Permission.ManageUsers));
    }

    // ---------- Rehydrate ----------

    [Fact]
    public void RehydrateRestoresIdentifiersNameAndCreatedAtUtc()
    {
        var id = RoleId.New();
        var organizationId = OrganizationId.New();

        var role = Role.Rehydrate(id, organizationId, "Cajero", true, UtcNow);

        Assert.Equal(id, role.Id);
        Assert.Equal(organizationId, role.OrganizationId);
        Assert.Equal("Cajero", role.Name);
        Assert.Equal(UtcNow, role.CreatedAtUtc);
    }

    [Fact]
    public void RehydrateRestoresActiveState()
    {
        var role = Role.Rehydrate(RoleId.New(), OrganizationId.New(), "Cajero", true, UtcNow);

        Assert.True(role.IsActive);
    }

    [Fact]
    public void RehydrateRestoresInactiveState()
    {
        var role = Role.Rehydrate(RoleId.New(), OrganizationId.New(), "Cajero", false, UtcNow);

        Assert.False(role.IsActive);
    }

    [Fact]
    public void RehydrateRestoresPermissions()
    {
        var role = Role.Rehydrate(
            RoleId.New(),
            OrganizationId.New(),
            "Cajero",
            true,
            UtcNow,
            [Permission.ProcessSale, Permission.ApplyDiscount]);

        Assert.Equal(2, role.Permissions.Count);
        Assert.True(role.HasPermission(Permission.ProcessSale));
        Assert.True(role.HasPermission(Permission.ApplyDiscount));
    }

    [Fact]
    public void RehydrateRemovesDuplicatePermissions()
    {
        var role = Role.Rehydrate(
            RoleId.New(),
            OrganizationId.New(),
            "Cajero",
            true,
            UtcNow,
            [Permission.ProcessSale, Permission.ProcessSale]);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void RehydrateRejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => Role.Rehydrate(default, OrganizationId.New(), "Cajero", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => Role.Rehydrate(RoleId.New(), default, "Cajero", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsInvalidName()
    {
        Assert.Throws<DomainValidationException>(
            () => Role.Rehydrate(RoleId.New(), OrganizationId.New(), "A", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => Role.Rehydrate(RoleId.New(), OrganizationId.New(), "Cajero", true, nonUtc));
    }
}
