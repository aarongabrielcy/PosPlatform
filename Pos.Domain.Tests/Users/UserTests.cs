using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static User CreateUser() =>
        new(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", UtcNow);

    [Fact]
    public void IsCreatedActive()
    {
        var user = CreateUser();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void NormalizesUsernameToUppercase()
    {
        var user = new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", UtcNow);

        Assert.Equal("JPEREZ", user.Username);
    }

    [Fact]
    public void TrimsUsernameAndDisplayName()
    {
        var user = new User(UserId.New(), OrganizationId.New(), RoleId.New(), "  jperez  ", "  Juan Pérez  ", UtcNow);

        Assert.Equal("JPEREZ", user.Username);
        Assert.Equal("Juan Pérez", user.DisplayName);
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(default, OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", UtcNow));
    }

    [Fact]
    public void RejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), default, RoleId.New(), "jperez", "Juan Pérez", UtcNow));
    }

    [Fact]
    public void RejectsDefaultRoleId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), default, "jperez", "Juan Pérez", UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("this-username-is-definitely-way-too-long-1234")]
    [InlineData("jp erez")]
    [InlineData("jp@erez")]
    public void RejectsInvalidUsername(string? username)
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), username!, "Juan Pérez", UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("A")]
    public void RejectsInvalidDisplayName(string? displayName)
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", displayName!, UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", nonUtc));
    }

    [Fact]
    public void ChangeDisplayNameValidatesAndUpdates()
    {
        var user = CreateUser();

        user.ChangeDisplayName("Maria Lopez");

        Assert.Equal("Maria Lopez", user.DisplayName);
        Assert.Throws<DomainValidationException>(() => user.ChangeDisplayName("A"));
    }

    [Fact]
    public void ChangeRoleValidatesAndUpdates()
    {
        var user = CreateUser();
        var newRoleId = RoleId.New();

        user.ChangeRole(newRoleId);

        Assert.Equal(newRoleId, user.RoleId);
        Assert.Throws<DomainValidationException>(() => user.ChangeRole(default));
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var user = CreateUser();

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }

    // ---------- Rehydrate ----------

    [Fact]
    public void RehydrateRestoresIdentifiersAndFields()
    {
        var id = UserId.New();
        var organizationId = OrganizationId.New();
        var roleId = RoleId.New();

        var user = User.Rehydrate(id, organizationId, roleId, "jperez", "Juan Pérez", true, UtcNow);

        Assert.Equal(id, user.Id);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal("JPEREZ", user.Username);
        Assert.Equal("Juan Pérez", user.DisplayName);
        Assert.Equal(UtcNow, user.CreatedAtUtc);
    }

    [Fact]
    public void RehydrateRestoresActiveState()
    {
        var user = User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", true, UtcNow);

        Assert.True(user.IsActive);
    }

    [Fact]
    public void RehydrateRestoresInactiveState()
    {
        var user = User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", false, UtcNow);

        Assert.False(user.IsActive);
    }

    [Fact]
    public void RehydrateRejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(default, OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), default, RoleId.New(), "jperez", "Juan Pérez", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultRoleId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), default, "jperez", "Juan Pérez", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsInvalidUsername()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "a", "Juan Pérez", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsInvalidDisplayName()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "A", true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", true, nonUtc));
    }
}
