using Microsoft.EntityFrameworkCore;
using Pos.Application.ProductAudit;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa IProductAuditQuery: consulta administrativa global paginada (SearchPageAsync) y
// consulta batch para el indicador de actividad reciente del catálogo (GetRecentActivityAsync),
// evitando el N+1 de resolver la actividad producto por producto (TAREA 24D, sección 30).
public sealed class EfProductAuditQuery : IProductAuditQuery
{
    private readonly PosDbContext _context;

    public EfProductAuditQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ProductAuditPageResult> SearchPageAsync(
        OrganizationId organizationId,
        ProductAuditFilter filter,
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

        var query = _context.ProductAuditEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId.Value);

        if (filter.ProductId is { } productId)
        {
            query = query.Where(e => e.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(e =>
                e.ProductSkuSnapshot.Contains(term) || EF.Functions.Like(e.ProductNameSnapshot, $"%{term}%"));
        }

        if (filter.ActorUserId is { } actorUserId)
        {
            query = query.Where(e => e.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorSearchTerm))
        {
            var term = filter.ActorSearchTerm.Trim();

            query = query.Where(e =>
                EF.Functions.Like(e.ActorUsernameSnapshot, $"%{term}%") ||
                EF.Functions.Like(e.ActorDisplayNameSnapshot, $"%{term}%"));
        }

        if (filter.Action is { } action)
        {
            query = query.Where(e => e.Action == action);
        }

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(e => e.OccurredAtUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(e => e.OccurredAtUtc <= toUtc);
        }

        var records = await query
            .Include(e => e.Changes)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var hasNextPage = records.Count > take;

        var items = records.Take(take).Select(ToEntry).ToList();

        return new ProductAuditPageResult(items, hasNextPage);
    }

    // Una sola consulta acotada por OrganizationId + ProductIds de la página actual (hasta 50) +
    // ventana de "reciente" (24h): el costo no depende de cuántos productos existan en el
    // catálogo completo, solo de cuántos eventos calzan esos filtros (TAREA 24D, sección 30). La
    // reducción a "el más reciente por producto" se hace en memoria sobre ese conjunto ya acotado,
    // evitando depender de traducción LINQ de GroupBy+First a SQL en el proveedor Sqlite.
    public async Task<IReadOnlyDictionary<ProductId, ProductRecentActivity>> GetRecentActivityAsync(
        OrganizationId organizationId,
        IReadOnlyList<ProductId> productIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return new Dictionary<ProductId, ProductRecentActivity>();
        }

        var productIdValues = productIds.Select(id => id.Value).ToList();

        var records = await _context.ProductAuditEvents.AsNoTracking()
            .Include(e => e.Changes)
            .Where(e =>
                e.OrganizationId == organizationId.Value &&
                productIdValues.Contains(e.ProductId) &&
                e.OccurredAtUtc >= sinceUtc &&
                e.Action != ProductAuditAction.Created)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<ProductId, ProductRecentActivity>();

        foreach (var record in records)
        {
            var productId = new ProductId(record.ProductId);

            if (result.ContainsKey(productId))
            {
                // Ya se registró el evento más reciente de este producto (la lista viene ordenada
                // DESC): se ignoran los eventos más antiguos del mismo producto.
                continue;
            }

            var topChanges = record.Changes
                .Take(2)
                .Select(change => new ProductAuditFieldChange(change.FieldName, change.OldValue, change.NewValue))
                .ToList();

            result[productId] = new ProductRecentActivity(
                productId,
                new ProductAuditEventId(record.Id),
                record.Action,
                record.OccurredAtUtc,
                record.ActorDisplayNameSnapshot,
                topChanges,
                record.Changes.Count);
        }

        return result;
    }

    private static ProductAuditEntry ToEntry(ProductAuditEventRecord record) =>
        new(
            new ProductAuditEventId(record.Id),
            new ProductId(record.ProductId),
            record.ProductSkuSnapshot,
            record.ProductNameSnapshot,
            new UserId(record.ActorUserId),
            record.ActorUsernameSnapshot,
            record.ActorDisplayNameSnapshot,
            record.Action,
            record.OccurredAtUtc,
            record.Changes
                .Select(change => new ProductAuditFieldChange(change.FieldName, change.OldValue, change.NewValue))
                .ToList());
}
