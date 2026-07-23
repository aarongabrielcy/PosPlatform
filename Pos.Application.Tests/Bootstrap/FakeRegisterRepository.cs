using Pos.Application.Registers;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeRegisterRepository : IRegisterRepository
{
    private readonly List<Register> _registers;

    public FakeRegisterRepository(IEnumerable<Register>? seed = null)
    {
        _registers = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public Task<Register?> GetByIdAsync(RegisterId registerId, CancellationToken cancellationToken) =>
        Task.FromResult(_registers.SingleOrDefault(r => r.Id == registerId));

    public Task<IReadOnlyList<Register>> GetByBranchAsync(BranchId branchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Register>>(
            _registers.Where(r => r.BranchId == branchId).ToList());

    public Task AddAsync(Register cashRegister, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _registers.Add(cashRegister);

        return Task.CompletedTask;
    }
}
