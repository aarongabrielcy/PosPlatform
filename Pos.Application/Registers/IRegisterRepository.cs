using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;

namespace Pos.Application.Registers;

public interface IRegisterRepository
{
    Task<Register?> GetByIdAsync(RegisterId registerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Register>> GetByBranchAsync(BranchId branchId, CancellationToken cancellationToken);

    Task AddAsync(Register cashRegister, CancellationToken cancellationToken);
}
