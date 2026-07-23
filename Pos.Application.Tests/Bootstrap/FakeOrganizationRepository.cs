using Pos.Application.Organizations;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeOrganizationRepository : IOrganizationRepository
{
    private readonly List<Organization> _organizations;

    public FakeOrganizationRepository(IEnumerable<Organization>? seed = null)
    {
        _organizations = seed?.ToList() ?? [];
    }

    public int GetAllCallCount { get; private set; }

    public int AddCallCount { get; private set; }

    // Punto de extensión para pruebas de concurrencia/cancelación: permite retener el
    // control dentro de la sección crítica protegida por el semáforo del servicio.
    public Func<Task>? BeforeGetAllAsync { get; set; }

    public Task<Organization?> GetByIdAsync(OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(_organizations.SingleOrDefault(o => o.Id == organizationId));

    public Task<Organization?> GetFirstAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_organizations.OrderBy(o => o.Name, StringComparer.Ordinal).ThenBy(o => o.Id.Value).FirstOrDefault());

    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken)
    {
        GetAllCallCount++;

        if (BeforeGetAllAsync is not null)
        {
            await BeforeGetAllAsync();
        }

        return _organizations.ToList();
    }

    public Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _organizations.Add(organization);

        return Task.CompletedTask;
    }
}
