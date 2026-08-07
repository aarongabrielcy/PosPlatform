using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa IInventoryMovementQuery: join InventoryMovementRecord+ProductRecord (por
// Organization+Branch) para resolver Sku/ProductName sin N+1 (TAREA 24G, sección 21). Read-only:
// InventoryMovement es historial inmutable, nunca se actualiza ni se borra desde aquí.
public sealed class EfInventoryMovementQuery : IInventoryMovementQuery
{
    private readonly PosDbContext _context;

    public EfInventoryMovementQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<InventoryMovementPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        InventoryMovementFilter filter,
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

        var branchIdValue = branchId.Value;

        var query =
            from movement in _context.InventoryMovements.AsNoTracking()
            join product in _context.Products.AsNoTracking()
                on movement.ProductId equals product.Id
            where product.OrganizationId == organizationId.Value && movement.BranchId == branchIdValue
            select new { movement, product };

        if (filter.ProductId is { } productId)
        {
            query = query.Where(x => x.movement.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var trimmedTerm = filter.SearchTerm.Trim();
            var upperTerm = trimmedTerm.ToUpperInvariant();

            query = query.Where(x =>
                x.product.Sku.Contains(upperTerm) || EF.Functions.Like(x.product.Name, $"%{trimmedTerm}%"));
        }

        if (filter.Type is { } type)
        {
            query = query.Where(x => x.movement.Type == type);
        }

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(x => x.movement.OccurredAtUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(x => x.movement.OccurredAtUtc <= toUtc);
        }

        var rows = await query
            .OrderByDescending(x => x.movement.OccurredAtUtc)
            .ThenByDescending(x => x.movement.Id)
            .Skip(skip)
            .Take(take + 1)
            .Select(x => new
            {
                x.movement.Id,
                x.movement.ProductId,
                x.product.Sku,
                x.product.Name,
                x.movement.Type,
                x.movement.Quantity,
                x.movement.QuantityBefore,
                x.movement.QuantityAfter,
                x.movement.SaleId,
                x.movement.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = rows.Count > take;

        var items = rows
            .Take(take)
            .Select(r => new InventoryMovementItem(
                new InventoryMovementId(r.Id),
                new ProductId(r.ProductId),
                r.Sku,
                r.Name,
                r.Type,
                r.Quantity,
                r.QuantityBefore,
                r.QuantityAfter,
                r.SaleId is { } saleId ? new SaleId(saleId) : null,
                r.OccurredAtUtc))
            .ToList();

        return new InventoryMovementPageResult(items, hasNextPage);
    }
}
