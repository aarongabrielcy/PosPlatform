using Pos.Application.Security;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeRoleRepository : IRoleRepository
{
    private readonly List<Role> _roles;

    public FakeRoleRepository(IEnumerable<Role>? seed = null)
    {
        _roles = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken) =>
        Task.FromResult(_roles.SingleOrDefault(r => r.Id == roleId));

    public Task<Role?> GetByNameAsync(OrganizationId organizationId, string name, CancellationToken cancellationToken) =>
        Task.FromResult(_roles.SingleOrDefault(r => r.OrganizationId == organizationId && r.Name == name));

    public Task<IReadOnlyList<Role>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Role>>(
            _roles.Where(r => r.OrganizationId == organizationId).ToList());

    public Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _roles.Add(role);

        return Task.CompletedTask;
    }

    // El fake ya opera sobre la misma instancia de Role que el servicio mutó (GrantPermission/
    // RevokePermission): no hay un "record" separado que sincronizar, así que solo se registra la
    // llamada para que las pruebas puedan verificar cuántas reconciliaciones ocurrieron.
    public Task UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        UpdateCallCount++;

        return Task.CompletedTask;
    }
}
