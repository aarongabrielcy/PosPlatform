using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;

namespace Pos.Application.RegisterSessions;

public interface IRegisterSessionRepository
{
    Task<RegisterSession?> GetByIdAsync(RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    Task<RegisterSession?> GetOpenByRegisterAsync(RegisterId registerId, CancellationToken cancellationToken);

    Task AddAsync(RegisterSession registerSession, CancellationToken cancellationToken);

    Task UpdateAsync(RegisterSession registerSession, CancellationToken cancellationToken);
}
