using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa ISalesHistoryQuery: listado paginado, resumen agregado y detalle de Historial de
// ventas (TAREA 25B). Solo ventas Completed (sección 7). CashierDisplayName/RegisterName se
// resuelven por JOIN a Users/Registers/RegisterSessions actuales, no son snapshots (sección 26/27).
// SQLite/EF no traduce SUM sobre columnas decimal en el servidor (mismo límite documentado en
// EfSaleRepository.GetCompletedCashTotalByRegisterSessionAsync): los montos se traen acotados por
// SaleId de la página/filtro y se suman en memoria, nunca sumando fila por fila con N consultas.
public sealed class EfSalesHistoryQuery : ISalesHistoryQuery
{
    private readonly PosDbContext _context;

    public EfSalesHistoryQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SalesHistoryPageResult> SearchPageAsync(
        OrganizationId organizationId,
        SalesHistoryFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "skip no puede ser negativo.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "take debe ser mayor que cero.");
        }

        var headers = await BuildFilteredSalesQuery(organizationId, filter)
            .OrderByDescending(s => s.CompletedAtUtc)
            .ThenByDescending(s => s.Id)
            .Skip(skip)
            .Take(take + 1)
            .Select(s => new
            {
                s.Id,
                s.CompletedAtUtc,
                s.CreatedByUserId,
                s.RegisterSessionId,
                s.Currency,
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = headers.Count > take;
        var pageHeaders = headers.Take(take).ToList();

        if (pageHeaders.Count == 0)
        {
            return new SalesHistoryPageResult(Array.Empty<SalesHistoryItem>(), hasNextPage);
        }

        var saleIds = pageHeaders.Select(h => h.Id).ToList();

        // Agregados de línea (ItemCount = SUM(Quantity), Total = SUM(Quantity*UnitPrice)) acotados a
        // los SaleId de esta página únicamente: no se suman en el servidor (ver comentario de clase).
        var lineRows = await _context.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new { l.SaleId, l.Quantity, l.UnitPriceAmount })
            .ToListAsync(cancellationToken);

        var lineAggregatesBySale = lineRows
            .GroupBy(l => l.SaleId)
            .ToDictionary(
                g => g.Key,
                g => (ItemCount: g.Sum(l => l.Quantity), Total: g.Sum(l => l.Quantity * l.UnitPriceAmount)));

        var paymentRows = await _context.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.SaleId, p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        var paymentSummaryBySale = paymentRows
            .GroupBy(p => p.SaleId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SalesHistoryPaymentAmount>)g
                    .GroupBy(p => p.Method)
                    .Select(mg => new SalesHistoryPaymentAmount(mg.Key, mg.Sum(p => p.Amount)))
                    .ToList());

        var cashierIds = pageHeaders.Select(h => h.CreatedByUserId).Distinct().ToList();
        var cashierNamesById = await _context.Users.AsNoTracking()
            .Where(u => cashierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var sessionIds = pageHeaders.Select(h => h.RegisterSessionId).Distinct().ToList();
        var sessionsById = await _context.RegisterSessions.AsNoTracking()
            .Where(rs => sessionIds.Contains(rs.Id))
            .Select(rs => new { rs.Id, rs.RegisterId })
            .ToDictionaryAsync(rs => rs.Id, rs => rs.RegisterId, cancellationToken);

        var registerIds = sessionsById.Values.Distinct().ToList();
        var registerNamesById = await _context.Registers.AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name })
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var items = pageHeaders.Select(h =>
        {
            var lineAggregate = lineAggregatesBySale.TryGetValue(h.Id, out var agg) ? agg : (ItemCount: 0m, Total: 0m);
            var paymentSummary = paymentSummaryBySale.TryGetValue(h.Id, out var summary)
                ? summary
                : Array.Empty<SalesHistoryPaymentAmount>();

            var registerId = sessionsById.TryGetValue(h.RegisterSessionId, out var regId) ? regId : Guid.Empty;
            var registerName = registerNamesById.TryGetValue(registerId, out var regName) ? regName : "(desconocido)";
            var cashierName = cashierNamesById.TryGetValue(h.CreatedByUserId, out var cashierDisplayName)
                ? cashierDisplayName
                : "(desconocido)";

            return new SalesHistoryItem(
                new SaleId(h.Id),
                h.CompletedAtUtc!.Value,
                new UserId(h.CreatedByUserId),
                cashierName,
                registerId == Guid.Empty ? default : new RegisterId(registerId),
                registerName,
                new RegisterSessionId(h.RegisterSessionId),
                lineAggregate.ItemCount,
                lineAggregate.Total,
                h.Currency,
                paymentSummary);
        }).ToList();

        return new SalesHistoryPageResult(items, hasNextPage);
    }

    public async Task<SalesHistorySummary> GetSummaryAsync(
        OrganizationId organizationId, SalesHistoryFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var matchingHeaders = await BuildFilteredSalesQuery(organizationId, filter)
            .Select(s => new { s.Id, s.Currency })
            .ToListAsync(cancellationToken);

        if (matchingHeaders.Count == 0)
        {
            return SalesHistorySummary.Empty;
        }

        var saleIds = matchingHeaders.Select(h => h.Id).ToList();
        var currency = matchingHeaders[0].Currency;

        // Total vendido = SUM(SaleLine.Quantity * UnitPrice) del filtro completo, nunca de la
        // página cargada en Desktop (sección 16/17). No mezcla OpeningFloat.
        var lineAmounts = await _context.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new { l.Quantity, l.UnitPriceAmount })
            .ToListAsync(cancellationToken);

        var total = lineAmounts.Sum(l => l.Quantity * l.UnitPriceAmount);

        var paymentAmounts = await _context.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        var breakdown = paymentAmounts
            .GroupBy(p => p.Method)
            .Select(g => new SalesHistoryPaymentAmount(g.Key, g.Sum(p => p.Amount)))
            .ToList();

        return new SalesHistorySummary(matchingHeaders.Count, total, currency, breakdown);
    }

    public async Task<SaleHistoryDetail?> GetDetailAsync(
        OrganizationId organizationId, SaleId saleId, CancellationToken cancellationToken)
    {
        var record = await _context.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .SingleOrDefaultAsync(
                s => s.OrganizationId == organizationId.Value && s.Id == saleId.Value, cancellationToken);

        if (record is null)
        {
            return null;
        }

        // FK Restrict garantiza que User/RegisterSession/Register siguen existiendo mientras la
        // Sale exista: no se contempla el caso "sin cajero"/"sin caja" para una venta persistida.
        var cashierName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == record.CreatedByUserId)
            .Select(u => u.DisplayName)
            .SingleAsync(cancellationToken);

        var registerId = await _context.RegisterSessions.AsNoTracking()
            .Where(rs => rs.Id == record.RegisterSessionId)
            .Select(rs => rs.RegisterId)
            .SingleAsync(cancellationToken);

        var registerName = await _context.Registers.AsNoTracking()
            .Where(r => r.Id == registerId)
            .Select(r => r.Name)
            .SingleAsync(cancellationToken);

        var lines = record.Lines
            .Select(l => new SaleHistoryDetailLine(
                l.ProductSku, l.ProductName, l.Quantity, l.UnitPriceAmount, l.Quantity * l.UnitPriceAmount, l.Currency))
            .ToList();

        var payments = record.Payments
            .OrderBy(p => p.PaidAtUtc)
            .Select(p => new SaleHistoryDetailPayment(p.Method, p.Amount, p.Currency, p.PaidAtUtc))
            .ToList();

        var subtotal = lines.Sum(l => l.LineTotal);

        return new SaleHistoryDetail(
            new SaleId(record.Id),
            record.Status,
            record.CreatedAtUtc,
            record.CompletedAtUtc,
            new UserId(record.CreatedByUserId),
            cashierName,
            new RegisterId(registerId),
            registerName,
            new RegisterSessionId(record.RegisterSessionId),
            subtotal,
            subtotal,
            record.Currency,
            lines,
            payments);
    }

    public async Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var completedSales = _context.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId.Value && s.Status == SaleStatus.Completed);

        var cashierIds = await completedSales.Select(s => s.CreatedByUserId).Distinct().ToListAsync(cancellationToken);

        var cashiers = await _context.Users.AsNoTracking()
            .Where(u => cashierIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new SalesHistoryCashierOption(new UserId(u.Id), u.DisplayName))
            .ToListAsync(cancellationToken);

        var sessionIds = await completedSales.Select(s => s.RegisterSessionId).Distinct().ToListAsync(cancellationToken);

        var registerIds = await _context.RegisterSessions.AsNoTracking()
            .Where(rs => sessionIds.Contains(rs.Id))
            .Select(rs => rs.RegisterId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var registers = await _context.Registers.AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .Select(r => new SalesHistoryRegisterOption(new RegisterId(r.Id), r.Name))
            .ToListAsync(cancellationToken);

        return new SalesHistoryFilterOptions(cashiers, registers);
    }

    // Filtro compartido por SearchPageAsync/GetSummaryAsync (sección 17: el resumen debe respetar
    // exactamente los mismos filtros que el listado). Solo Completed (sección 7); ToUtc se trata
    // como límite EXCLUSIVO (sección 12): el llamador ya resuelve el rango [fromInclusive,
    // toExclusive) antes de construir el filtro.
    private IQueryable<SaleRecord> BuildFilteredSalesQuery(OrganizationId organizationId, SalesHistoryFilter filter)
    {
        var query = _context.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId.Value && s.Status == SaleStatus.Completed);

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(s => s.CompletedAtUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(s => s.CompletedAtUtc < toUtc);
        }

        if (filter.CashierUserId is { } cashierUserId)
        {
            query = query.Where(s => s.CreatedByUserId == cashierUserId.Value);
        }

        if (filter.RegisterId is { } registerId)
        {
            var registerIdValue = registerId.Value;

            query = query.Where(s =>
                _context.RegisterSessions.Any(rs => rs.Id == s.RegisterSessionId && rs.RegisterId == registerIdValue));
        }

        if (filter.PaymentMethod is { } paymentMethod)
        {
            // Una venta coincide si contiene AL MENOS UN Payment de ese método (sección 18): Sale
            // admite múltiples Payments, nunca se asume 1:1.
            query = query.Where(s => s.Payments.Any(p => p.Method == paymentMethod));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            var parsedSaleId = Guid.TryParse(term, out var parsed) ? parsed : (Guid?)null;

            query = query.Where(s =>
                (parsedSaleId != null && s.Id == parsedSaleId) ||
                s.Lines.Any(l => l.ProductSku.Contains(term) || EF.Functions.Like(l.ProductName, $"%{term}%")));
        }

        return query;
    }
}
