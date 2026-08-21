using Pos.Application.Inventory;
using Pos.Application.Reports;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Reports;

internal sealed class FakeOperationalReportsService : IOperationalReportsService
{
    private readonly SalesSummaryReport _salesSummary;
    private readonly IReadOnlyList<RegisterClosureReportItem> _closures;
    private readonly RegisterClosureReportItem? _closureDetail;
    private readonly CashMovementsReportResult _cashMovements;
    private readonly IReadOnlyList<ProductSalesReportItem> _productSales;
    private readonly IReadOnlyList<InventoryCatalogItem> _lowStockItems;
    private readonly IReadOnlyList<OperatorActivityReportItem> _operatorActivity;

    public FakeOperationalReportsService(
        SalesSummaryReport? salesSummary = null,
        IReadOnlyList<RegisterClosureReportItem>? closures = null,
        RegisterClosureReportItem? closureDetail = null,
        CashMovementsReportResult? cashMovements = null,
        IReadOnlyList<ProductSalesReportItem>? productSales = null,
        IReadOnlyList<InventoryCatalogItem>? lowStockItems = null,
        IReadOnlyList<OperatorActivityReportItem>? operatorActivity = null)
    {
        _salesSummary = salesSummary ?? SalesSummaryReport.Empty;
        _closures = closures ?? Array.Empty<RegisterClosureReportItem>();
        _closureDetail = closureDetail;
        _cashMovements = cashMovements ?? CashMovementsReportResult.Empty;
        _productSales = productSales ?? Array.Empty<ProductSalesReportItem>();
        _lowStockItems = lowStockItems ?? Array.Empty<InventoryCatalogItem>();
        _operatorActivity = operatorActivity ?? Array.Empty<OperatorActivityReportItem>();
    }

    public int GetSalesSummaryCallCount { get; private set; }

    public int GetRegisterClosuresCallCount { get; private set; }

    public int GetRegisterClosureDetailCallCount { get; private set; }

    public RegisterSessionId? LastRegisterSessionId { get; private set; }

    public int GetCashMovementsCallCount { get; private set; }

    public int GetProductSalesCallCount { get; private set; }

    public int GetLowStockCallCount { get; private set; }

    public int GetOperatorActivityCallCount { get; private set; }

    public Task<SalesSummaryReport> GetSalesSummaryAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        GetSalesSummaryCallCount++;
        return Task.FromResult(_salesSummary);
    }

    public Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        GetRegisterClosuresCallCount++;
        return Task.FromResult(_closures);
    }

    public Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken = default)
    {
        GetRegisterClosureDetailCallCount++;
        LastRegisterSessionId = registerSessionId;
        return Task.FromResult(_closureDetail);
    }

    public Task<CashMovementsReportResult> GetCashMovementsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        GetCashMovementsCallCount++;
        return Task.FromResult(_cashMovements);
    }

    public Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        GetProductSalesCallCount++;
        return Task.FromResult(_productSales);
    }

    public Task<IReadOnlyList<InventoryCatalogItem>> GetLowStockAsync(CancellationToken cancellationToken = default)
    {
        GetLowStockCallCount++;
        return Task.FromResult(_lowStockItems);
    }

    public Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        GetOperatorActivityCallCount++;
        return Task.FromResult(_operatorActivity);
    }
}
