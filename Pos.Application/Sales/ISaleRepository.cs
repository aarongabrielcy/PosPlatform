using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken);

    Task AddAsync(Sale sale, CancellationToken cancellationToken);

    Task UpdateAsync(Sale sale, CancellationToken cancellationToken);

    // Suma de Payments con Method Cash de todas las Sales Completed de la sesión de caja
    // indicada. Usada por RegisterSessionService.CloseAsync para calcular ExpectedCash sin
    // cargar los agregados Sale completos.
    Task<decimal> GetCompletedCashTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);
}
