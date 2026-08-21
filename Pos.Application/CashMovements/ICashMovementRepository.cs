using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.CashMovements;

public interface ICashMovementRepository
{
    Task AddAsync(CashMovement movement, CancellationToken cancellationToken);

    Task<IReadOnlyList<CashMovement>> GetByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    // Sumas usadas por CashMovementService (límite de CashOut) y RegisterSessionService
    // (ExpectedCash del cierre). Mismo motivo que ISaleRepository.
    // GetCompletedCashTotalByRegisterSessionAsync: SQLite no soporta SUM sobre columnas decimal en
    // el servidor, así que la implementación EF suma en memoria.
    Task<decimal> GetCashInTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    Task<decimal> GetCashOutTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);
}
