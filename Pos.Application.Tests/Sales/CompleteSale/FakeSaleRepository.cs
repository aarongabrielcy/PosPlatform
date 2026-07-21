using Pos.Application.Sales;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Tests.Sales.CompleteSale;

internal sealed class FakeSaleRepository : ISaleRepository
{
    private readonly Sale? _sale;
    private readonly List<string>? _operationLog;

    public FakeSaleRepository(Sale? sale, List<string>? operationLog = null)
    {
        _sale = sale;
        _operationLog = operationLog;
    }

    public int GetByIdCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public Sale? UpdatedSale { get; private set; }

    public Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken)
    {
        GetByIdCallCount++;

        return Task.FromResult(_sale is not null && _sale.Id == saleId ? _sale : null);
    }

    public Task UpdateAsync(Sale sale, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        UpdatedSale = sale;
        _operationLog?.Add("Sale.Update");

        return Task.CompletedTask;
    }
}
