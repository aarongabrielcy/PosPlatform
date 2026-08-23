using Pos.Application.Organizations;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeOrganizationRepository : IOrganizationRepository
{
    private readonly Organization? _organization;

    public FakeOrganizationRepository(Organization? organization = null)
    {
        _organization = organization;
    }

    public Task<Organization?> GetByIdAsync(OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(_organization);

    public Task<Organization?> GetFirstAsync(CancellationToken cancellationToken) => Task.FromResult(_organization);

    public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Organization>>(_organization is null ? [] : [_organization]);

    public Task AddAsync(Organization organization, CancellationToken cancellationToken) => Task.CompletedTask;
}
