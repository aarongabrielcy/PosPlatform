using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static readonly PasswordHash ValidPasswordHash =
        new("v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=");

    private static readonly PasswordHash OtherPasswordHash =
        new("v1$pbkdf2-sha256$210000$b3RoZXItc2FsdA==$b3RoZXItaGFzaA==");

    private static User CreateUser() =>
        new(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, UtcNow);

    [Fact]
    public void IsCreatedActive()
    {
        var user = CreateUser();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void NormalizesUsernameToUppercase()
    {
        var user = new User(
            UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, UtcNow);

        Assert.Equal("JPEREZ", user.Username);
    }

    [Fact]
    public void TrimsUsernameAndDisplayName()
    {
        var user = new User(
            UserId.New(), OrganizationId.New(), RoleId.New(), "  jperez  ", "  Juan Pérez  ", ValidPasswordHash, UtcNow);

        Assert.Equal("JPEREZ", user.Username);
        Assert.Equal("Juan Pérez", user.DisplayName);
    }

    [Fact]
    public void CreationPreservesPasswordHash()
    {
        var user = CreateUser();

        Assert.Equal(ValidPasswordHash, user.PasswordHash);
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(default, OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, UtcNow));
    }

    [Fact]
    public void RejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), default, RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, UtcNow));
    }

    [Fact]
    public void RejectsDefaultRoleId()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), default, "jperez", "Juan Pérez", ValidPasswordHash, UtcNow));
    }

    [Fact]
    public void RejectsDefaultPasswordHash()
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", default, UtcNow));
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
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), username!, "Juan Pérez", ValidPasswordHash, UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("A")]
    public void RejectsInvalidDisplayName(string? displayName)
    {
        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", displayName!, ValidPasswordHash, UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new User(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, nonUtc));
    }

    [Fact]
    public void ChangeUsernameValidatesNormalizesAndUpdates()
    {
        var user = CreateUser();

        user.ChangeUsername("  mlopez  ");

        Assert.Equal("MLOPEZ", user.Username);
        Assert.Throws<DomainValidationException>(() => user.ChangeUsername("a"));
    }

    [Fact]
    public void ChangeUsernameDoesNotRegenerateUserIdOrOtherFields()
    {
        var user = CreateUser();
        var id = user.Id;
        var displayName = user.DisplayName;
        var roleId = user.RoleId;
        var createdAtUtc = user.CreatedAtUtc;

        user.ChangeUsername("mlopez");

        Assert.Equal(id, user.Id);
        Assert.Equal(displayName, user.DisplayName);
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal(createdAtUtc, user.CreatedAtUtc);
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
    public void ChangePasswordHashUpdatesOnlyTheHash()
    {
        var user = CreateUser();
        var username = user.Username;
        var displayName = user.DisplayName;
        var roleId = user.RoleId;
        var isActive = user.IsActive;
        var createdAtUtc = user.CreatedAtUtc;

        user.ChangePasswordHash(OtherPasswordHash);

        Assert.Equal(OtherPasswordHash, user.PasswordHash);
        Assert.Equal(username, user.Username);
        Assert.Equal(displayName, user.DisplayName);
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal(isActive, user.IsActive);
        Assert.Equal(createdAtUtc, user.CreatedAtUtc);
    }

    [Fact]
    public void ChangePasswordHashRejectsDefault()
    {
        var user = CreateUser();

        Assert.Throws<DomainValidationException>(() => user.ChangePasswordHash(default));
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

        var user = User.Rehydrate(id, organizationId, roleId, "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow);

        Assert.Equal(id, user.Id);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal("JPEREZ", user.Username);
        Assert.Equal("Juan Pérez", user.DisplayName);
        Assert.Equal(UtcNow, user.CreatedAtUtc);
    }

    [Fact]
    public void RehydrateRestoresPasswordHash()
    {
        var user = User.Rehydrate(
            UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow);

        Assert.Equal(ValidPasswordHash, user.PasswordHash);
    }

    [Fact]
    public void RehydrateRestoresActiveState()
    {
        var user = User.Rehydrate(
            UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow);

        Assert.True(user.IsActive);
    }

    [Fact]
    public void RehydrateRestoresInactiveState()
    {
        var user = User.Rehydrate(
            UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, false, UtcNow);

        Assert.False(user.IsActive);
    }

    [Fact]
    public void RehydrateRejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(default, OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), default, RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultRoleId()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), default, "jperez", "Juan Pérez", ValidPasswordHash, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsDefaultPasswordHash()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", default, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsInvalidUsername()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "a", "Juan Pérez", ValidPasswordHash, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsInvalidDisplayName()
    {
        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "A", ValidPasswordHash, true, UtcNow));
    }

    [Fact]
    public void RehydrateRejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => User.Rehydrate(UserId.New(), OrganizationId.New(), RoleId.New(), "jperez", "Juan Pérez", ValidPasswordHash, true, nonUtc));
    }
}
