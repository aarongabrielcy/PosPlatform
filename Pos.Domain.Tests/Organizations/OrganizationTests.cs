using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;

namespace Pos.Domain.Tests.Organizations;

public class OrganizationTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    [Fact]
    public void IsCreatedActive()
    {
        var organization = new Organization(OrganizationId.New(), "Acme", UtcNow);

        Assert.True(organization.IsActive);
    }

    [Fact]
    public void TrimsName()
    {
        var organization = new Organization(OrganizationId.New(), "  Acme  ", UtcNow);

        Assert.Equal("Acme", organization.Name);
    }

    [Fact]
    public void RejectsInvalidName()
    {
        Assert.Throws<DomainValidationException>(() => new Organization(OrganizationId.New(), "A", UtcNow));
    }

    [Fact]
    public void RejectsDefaultId()
    {
        Assert.Throws<DomainValidationException>(() => new Organization(default, "Acme", UtcNow));
    }

    [Fact]
    public void RejectsNonUtcDate()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => new Organization(OrganizationId.New(), "Acme", nonUtc));
    }

    [Fact]
    public void RenameUpdatesValidName()
    {
        var organization = new Organization(OrganizationId.New(), "Acme", UtcNow);

        organization.Rename("Acme Corp");

        Assert.Equal("Acme Corp", organization.Name);
    }

    [Fact]
    public void RenameRejectsInvalidName()
    {
        var organization = new Organization(OrganizationId.New(), "Acme", UtcNow);

        Assert.Throws<DomainValidationException>(() => organization.Rename(" "));
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var organization = new Organization(OrganizationId.New(), "Acme", UtcNow);

        organization.Deactivate();
        Assert.False(organization.IsActive);

        organization.Activate();
        Assert.True(organization.IsActive);
    }
}
