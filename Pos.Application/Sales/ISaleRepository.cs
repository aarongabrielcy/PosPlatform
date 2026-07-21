using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken);

    Task UpdateAsync(Sale sale, CancellationToken cancellationToken);
}
