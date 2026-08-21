using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa IOperationalReportsQuery (BASIC-RPT-01): agregados de solo lectura sobre Sale/
// SaleLine/Payment/RegisterSession/CashMovement/Register/User, tenant-scoped por OrganizationId.
// Mismo criterio que EfSalesHistoryQuery: SQLite/EF no traduce SUM sobre columnas decimal en el
// servidor, así que cada método trae las filas ya acotadas por el filtro (período/Organization) y
// agrega en memoria, nunca cargando toda la tabla ni resolviendo N+1 (un lookup por lote de
// Users/Registers, no uno por fila).
public sealed class EfOperationalReportsQuery : IOperationalReportsQuery
{
    private readonly PosDbContext _context;

    public EfOperationalReportsQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SalesSummaryReport> GetSalesSummaryAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        var salesInPeriod = await _context.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId.Value && s.Status == SaleStatus.Completed
                && s.CompletedAtUtc >= fromUtc && s.CompletedAtUtc < toUtcExclusive)
            .Select(s => new { s.Id, s.Currency })
            .ToListAsync(cancellationToken);

        if (salesInPeriod.Count == 0)
        {
            return SalesSummaryReport.Empty;
        }

        var saleIds = salesInPeriod.Select(s => s.Id).ToList();
        var currency = salesInPeriod[0].Currency;

        // GrossSales = SUM(SaleLine.Quantity * UnitPrice), nunca CashSales + CardSales (mismo
        // criterio de RegisterClosingSummary/EfSaleRepository: puede haber otros métodos de pago).
        var lineAmounts = await _context.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new { l.Quantity, l.UnitPriceAmount })
            .ToListAsync(cancellationToken);

        var grossSales = lineAmounts.Sum(l => l.Quantity * l.UnitPriceAmount);

        var payments = await _context.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        var cashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var cardSales = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);

        return new SalesSummaryReport(salesInPeriod.Count, grossSales, cashSales, cardSales, currency);
    }

    public async Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        var sessionQuery =
            from rs in _context.RegisterSessions.AsNoTracking()
            join reg in _context.Registers.AsNoTracking() on rs.RegisterId equals reg.Id
            join br in _context.Branches.AsNoTracking() on reg.BranchId equals br.Id
            where br.OrganizationId == organizationId.Value
                && rs.Status == RegisterSessionStatus.Closed
                && rs.ClosedAtUtc >= fromUtc && rs.ClosedAtUtc < toUtcExclusive
            orderby rs.ClosedAtUtc descending
            select new SessionRow(rs, reg.Name);

        var sessions = await sessionQuery.ToListAsync(cancellationToken);

        return await BuildClosureItemsAsync(sessions, cancellationToken);
    }

    public async Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        OrganizationId organizationId, RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        var sessionIdValue = registerSessionId.Value;

        var sessionQuery =
            from rs in _context.RegisterSessions.AsNoTracking()
            join reg in _context.Registers.AsNoTracking() on rs.RegisterId equals reg.Id
            join br in _context.Branches.AsNoTracking() on reg.BranchId equals br.Id
            where br.OrganizationId == organizationId.Value
                && rs.Id == sessionIdValue
                && rs.Status == RegisterSessionStatus.Closed
            select new SessionRow(rs, reg.Name);

        var sessions = await sessionQuery.ToListAsync(cancellationToken);
        var items = await BuildClosureItemsAsync(sessions, cancellationToken);

        return items.SingleOrDefault();
    }

    public async Task<CashMovementsReportResult> GetCashMovementsAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        var movementQuery =
            from m in _context.CashMovements.AsNoTracking()
            join rs in _context.RegisterSessions.AsNoTracking() on m.RegisterSessionId equals rs.Id
            join reg in _context.Registers.AsNoTracking() on rs.RegisterId equals reg.Id
            join br in _context.Branches.AsNoTracking() on reg.BranchId equals br.Id
            where br.OrganizationId == organizationId.Value
                && m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtcExclusive
            orderby m.CreatedAtUtc descending
            select new { Movement = m, RegisterName = reg.Name };

        var rows = await movementQuery.ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return CashMovementsReportResult.Empty;
        }

        var actorIds = rows.Select(r => r.Movement.ActorUserId).Distinct().ToList();
        var displayNamesById = await _context.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var currency = rows[0].Movement.Currency;

        var entries = rows.Select(r => new CashMovementReportEntry(
                r.Movement.Id,
                new RegisterSessionId(r.Movement.RegisterSessionId),
                r.RegisterName,
                r.Movement.Type,
                r.Movement.Amount,
                r.Movement.Currency,
                r.Movement.Reason,
                new UserId(r.Movement.ActorUserId),
                displayNamesById.TryGetValue(r.Movement.ActorUserId, out var actorName) ? actorName : "(desconocido)",
                r.Movement.CreatedAtUtc))
            .ToList();

        var cashInTotal = rows.Where(r => r.Movement.Type == CashMovementType.CashIn).Sum(r => r.Movement.Amount);
        var cashOutTotal = rows.Where(r => r.Movement.Type == CashMovementType.CashOut).Sum(r => r.Movement.Amount);

        return new CashMovementsReportResult(entries, cashInTotal, cashOutTotal, currency);
    }

    public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        var rows = await (
            from line in _context.SaleLines.AsNoTracking()
            join sale in _context.Sales.AsNoTracking() on line.SaleId equals sale.Id
            where sale.OrganizationId == organizationId.Value
                && sale.Status == SaleStatus.Completed
                && sale.CompletedAtUtc >= fromUtc && sale.CompletedAtUtc < toUtcExclusive
            select new
            {
                line.ProductId,
                line.ProductSku,
                line.ProductName,
                line.Quantity,
                line.UnitPriceAmount,
                line.Currency,
                sale.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        // Agrupado por ProductId (identidad estable, sección 15 de la tarea): el Sku/Name mostrado
        // es el snapshot de la venta más reciente del grupo, nunca la fila Product actual.
        var items = rows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.CompletedAtUtc).First();

                return new ProductSalesReportItem(
                    new ProductId(g.Key),
                    latest.ProductSku,
                    latest.ProductName,
                    g.Sum(r => r.Quantity),
                    g.Sum(r => r.Quantity * r.UnitPriceAmount),
                    latest.Currency);
            })
            .OrderByDescending(i => i.SalesAmount)
            .ToList();

        return items;
    }

    public async Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken)
    {
        var salesInPeriod = await _context.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId.Value && s.Status == SaleStatus.Completed
                && s.CompletedAtUtc >= fromUtc && s.CompletedAtUtc < toUtcExclusive)
            .Select(s => new { s.Id, s.CreatedByUserId, s.Currency })
            .ToListAsync(cancellationToken);

        if (salesInPeriod.Count == 0)
        {
            return Array.Empty<OperatorActivityReportItem>();
        }

        var saleIds = salesInPeriod.Select(s => s.Id).ToList();
        var saleToUser = salesInPeriod.ToDictionary(s => s.Id, s => s.CreatedByUserId);
        var currency = salesInPeriod[0].Currency;

        var lineAmounts = await _context.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new { l.SaleId, Amount = l.Quantity * l.UnitPriceAmount })
            .ToListAsync(cancellationToken);

        var grossByUser = lineAmounts
            .GroupBy(l => saleToUser[l.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var payments = await _context.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.SaleId, p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        var cashByUser = payments.Where(p => p.Method == PaymentMethod.Cash)
            .GroupBy(p => saleToUser[p.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var cardByUser = payments.Where(p => p.Method == PaymentMethod.Card)
            .GroupBy(p => saleToUser[p.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var saleCountByUser = salesInPeriod
            .GroupBy(s => s.CreatedByUserId)
            .ToDictionary(g => g.Key, g => g.Count());

        var userIds = saleCountByUser.Keys.ToList();
        var displayNamesById = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = userIds
            .Select(userId => new OperatorActivityReportItem(
                new UserId(userId),
                displayNamesById.TryGetValue(userId, out var name) ? name : "(desconocido)",
                saleCountByUser[userId],
                grossByUser.TryGetValue(userId, out var gross) ? gross : 0m,
                cashByUser.TryGetValue(userId, out var cash) ? cash : 0m,
                cardByUser.TryGetValue(userId, out var card) ? card : 0m,
                currency))
            .OrderByDescending(i => i.GrossSales)
            .ToList();

        return items;
    }

    // Fila intermedia RegisterSession+RegisterName (sección 9/11-12 de la tarea): se comparte entre
    // GetRegisterClosuresAsync/GetRegisterClosureDetailAsync para nunca duplicar la agregación de
    // CashSales/CardSales/GrossSales/CashIn/CashOut (BuildClosureItemsAsync).
    private sealed record SessionRow(RegisterSessionRecord Session, string RegisterName);

    private async Task<List<RegisterClosureReportItem>> BuildClosureItemsAsync(
        List<SessionRow> sessions, CancellationToken cancellationToken)
    {
        if (sessions.Count == 0)
        {
            return [];
        }

        var sessionIds = sessions.Select(s => s.Session.Id).ToList();

        var salesForSessions = await _context.Sales.AsNoTracking()
            .Where(s => sessionIds.Contains(s.RegisterSessionId) && s.Status == SaleStatus.Completed)
            .Select(s => new { s.Id, s.RegisterSessionId })
            .ToListAsync(cancellationToken);
        var saleIds = salesForSessions.Select(s => s.Id).ToList();
        var saleToSession = salesForSessions.ToDictionary(s => s.Id, s => s.RegisterSessionId);

        var lineAmounts = await _context.SaleLines.AsNoTracking()
            .Where(l => saleIds.Contains(l.SaleId))
            .Select(l => new { l.SaleId, Amount = l.Quantity * l.UnitPriceAmount })
            .ToListAsync(cancellationToken);
        var grossBySession = lineAmounts
            .GroupBy(l => saleToSession[l.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var payments = await _context.Payments.AsNoTracking()
            .Where(p => saleIds.Contains(p.SaleId))
            .Select(p => new { p.SaleId, p.Method, p.Amount })
            .ToListAsync(cancellationToken);
        var cashBySession = payments.Where(p => p.Method == PaymentMethod.Cash)
            .GroupBy(p => saleToSession[p.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var cardBySession = payments.Where(p => p.Method == PaymentMethod.Card)
            .GroupBy(p => saleToSession[p.SaleId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var cashMovements = await _context.CashMovements.AsNoTracking()
            .Where(m => sessionIds.Contains(m.RegisterSessionId))
            .Select(m => new { m.RegisterSessionId, m.Type, m.Amount })
            .ToListAsync(cancellationToken);
        var cashInBySession = cashMovements.Where(m => m.Type == CashMovementType.CashIn)
            .GroupBy(m => m.RegisterSessionId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var cashOutBySession = cashMovements.Where(m => m.Type == CashMovementType.CashOut)
            .GroupBy(m => m.RegisterSessionId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var userIds = sessions.Select(s => s.Session.OpenedByUserId)
            .Concat(sessions.Where(s => s.Session.ClosedByUserId is not null).Select(s => s.Session.ClosedByUserId!.Value))
            .Distinct()
            .ToList();
        var displayNamesById = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = new List<RegisterClosureReportItem>(sessions.Count);

        foreach (var row in sessions)
        {
            var session = row.Session;
            var grossSales = grossBySession.TryGetValue(session.Id, out var gross) ? gross : 0m;
            var cashSales = cashBySession.TryGetValue(session.Id, out var cash) ? cash : 0m;
            var cardSales = cardBySession.TryGetValue(session.Id, out var card) ? card : 0m;
            var cashIn = cashInBySession.TryGetValue(session.Id, out var cashInAmount) ? cashInAmount : 0m;
            var cashOut = cashOutBySession.TryGetValue(session.Id, out var cashOutAmount) ? cashOutAmount : 0m;

            var openedByName = displayNamesById.TryGetValue(session.OpenedByUserId, out var openedName)
                ? openedName
                : "(desconocido)";
            var closedByUserId = session.ClosedByUserId!.Value;
            var closedByName = displayNamesById.TryGetValue(closedByUserId, out var closedName)
                ? closedName
                : "(desconocido)";

            items.Add(new RegisterClosureReportItem(
                new RegisterSessionId(session.Id),
                row.RegisterName,
                session.OpenedAtUtc,
                session.ClosedAtUtc!.Value,
                new UserId(session.OpenedByUserId),
                openedByName,
                new UserId(closedByUserId),
                closedByName,
                session.OpeningFloatAmount,
                cashSales,
                cardSales,
                grossSales,
                cashIn,
                cashOut,
                session.ExpectedCashAmount!.Value,
                session.CountedCashAmount!.Value,
                session.CashDifferenceAmount!.Value,
                session.OpeningFloatCurrency));
        }

        return items;
    }
}
