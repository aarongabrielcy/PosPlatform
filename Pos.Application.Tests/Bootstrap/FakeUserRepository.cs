using Pos.Application.Users;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public FakeUserRepository(IEnumerable<User>? seed = null)
    {
        _users = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Id == userId));

    public Task<User?> GetByUsernameAsync(
        OrganizationId organizationId, string username, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(u => u.OrganizationId == organizationId && u.Username == username));

    public Task<IReadOnlyList<User>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(
            _users.Where(u => u.OrganizationId == organizationId).ToList());

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _users.Add(user);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        UpdateCallCount++;

        var index = _users.FindIndex(u => u.Id == user.Id);

        if (index >= 0)
        {
            _users[index] = user;
        }

        return Task.CompletedTask;
    }
}
