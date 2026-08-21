using Pos.Application.Reports;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Reports;

internal sealed class FakeOperationalReportsQuery : IOperationalReportsQuery
{
    private readonly SalesSummaryReport _salesSummary;
    private readonly IReadOnlyList<RegisterClosureReportItem> _closures;
    private readonly RegisterClosureReportItem? _closureDetail;
    private readonly CashMovementsReportResult _cashMovements;
    private readonly IReadOnlyList<ProductSalesReportItem> _productSales;
    private readonly IReadOnlyList<OperatorActivityReportItem> _operatorActivity;

    public FakeOperationalReportsQuery(
        SalesSummaryReport? salesSummary = null,
        IReadOnlyList<RegisterClosureReportItem>? closures = null,
        RegisterClosureReportItem? closureDetail = null,
        CashMovementsReportResult? cashMovements = null,
        IReadOnlyList<ProductSalesReportItem>? productSales = null,
        IReadOnlyList<OperatorActivityReportItem>? operatorActivity = null)
    {
        _salesSummary = salesSummary ?? SalesSummaryReport.Empty;
        _closures = closures ?? Array.Empty<RegisterClosureReportItem>();
        _closureDetail = closureDetail;
        _cashMovements = cashMovements ?? CashMovementsReportResult.Empty;
        _productSales = productSales ?? Array.Empty<ProductSalesReportItem>();
        _operatorActivity = operatorActivity ?? Array.Empty<OperatorActivityReportItem>();
    }

    public int GetSalesSummaryCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public DateTimeOffset? LastFromUtc { get; private set; }

    public DateTimeOffset? LastToUtcExclusive { get; private set; }

    public int GetRegisterClosuresCallCount { get; private set; }

    public int GetRegisterClosureDetailCallCount { get; private set; }

    public RegisterSessionId? LastRegisterSessionId { get; private set; }

    public int GetCashMovementsCallCount { get; private set; }

    public int GetProductSalesCallCount { get; private set; }

    public int GetOperatorActivityCallCount { get; private set; }

    public Task<SalesSummaryReport> GetSalesSummaryAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        GetSalesSummaryCallCount++;
        LastOrganizationId = organizationId;
        LastFromUtc = fromUtc;
        LastToUtcExclusive = toUtcExclusive;

        return Task.FromResult(_salesSummary);
    }

    public Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        GetRegisterClosuresCallCount++;
        LastOrganizationId = organizationId;
        LastFromUtc = fromUtc;
        LastToUtcExclusive = toUtcExclusive;

        return Task.FromResult(_closures);
    }

    public Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        OrganizationId organizationId, RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        GetRegisterClosureDetailCallCount++;
        LastOrganizationId = organizationId;
        LastRegisterSessionId = registerSessionId;

        return Task.FromResult(_closureDetail);
    }

    public Task<CashMovementsReportResult> GetCashMovementsAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        GetCashMovementsCallCount++;
        LastOrganizationId = organizationId;
        LastFromUtc = fromUtc;
        LastToUtcExclusive = toUtcExclusive;

        return Task.FromResult(_cashMovements);
    }

    public Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        GetProductSalesCallCount++;
        LastOrganizationId = organizationId;
        LastFromUtc = fromUtc;
        LastToUtcExclusive = toUtcExclusive;

        return Task.FromResult(_productSales);
    }

    public Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        GetOperatorActivityCallCount++;
        LastOrganizationId = organizationId;
        LastFromUtc = fromUtc;
        LastToUtcExclusive = toUtcExclusive;

        return Task.FromResult(_operatorActivity);
    }
}
