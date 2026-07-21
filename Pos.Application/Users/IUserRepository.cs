using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;

namespace Pos.Application.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(OrganizationId organizationId, string username, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetByOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}
