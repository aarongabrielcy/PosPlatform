using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa IInventoryCatalogQuery combinando ProductRecord+InventoryItemRecord (LEFT JOIN
// filtrado a la Branch actual) en una sola consulta traducida a SQL, igual patrón que
// EfProductCatalogQuery (TAREA 24G, sección 6/9): evita el N+1 de resolver la existencia producto
// por producto. A diferencia de ProductCatalog, solo incluye productos con TracksInventory == true
// y nunca filtra por IsActive (sección 4/30: el historial/existencia de un producto inactivo no
// debe desaparecer). La consulta base se repite en SearchPageAsync/GetSummaryAsync en vez de
// extraerse a un método compartido: un tipo con nombre en la proyección impide que EF traduzca las
// cláusulas OrderBy/Where/CountAsync posteriores a SQL (falla en tiempo de ejecución con
// "could not be translated"); el tipo anónimo, en cambio, sí se traduce correctamente.
public sealed class EfInventoryCatalogQuery : IInventoryCatalogQuery
{
    private readonly PosDbContext _context;

    public EfInventoryCatalogQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<InventoryCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
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
            from product in _context.Products.AsNoTracking()
            where product.OrganizationId == organizationId.Value && product.TracksInventory
            join inventoryItem in _context.InventoryItems.AsNoTracking()
                    .Where(i => i.BranchId == branchIdValue)
                on product.Id equals inventoryItem.ProductId into inventoryItems
            from inventoryItem in inventoryItems.DefaultIfEmpty()
            select new { product, inventoryItem };

        var trimmedTerm = searchTerm?.Trim();

        if (!string.IsNullOrEmpty(trimmedTerm))
        {
            var upperTerm = trimmedTerm.ToUpperInvariant();

            query = query.Where(x =>
                x.product.Sku.Contains(upperTerm) ||
                (x.product.Barcode != null && x.product.Barcode.Contains(trimmedTerm)) ||
                EF.Functions.Like(x.product.Name, $"%{trimmedTerm}%"));
        }

        query = filter switch
        {
            InventoryCatalogStatusFilter.OutOfStock => query.Where(x =>
                x.inventoryItem == null || x.inventoryItem.Quantity <= 0m),
            InventoryCatalogStatusFilter.LowStock => query.Where(x =>
                x.inventoryItem != null && x.inventoryItem.Quantity > 0m &&
                x.inventoryItem.Quantity <= x.inventoryItem.ReorderPoint),
            InventoryCatalogStatusFilter.InStock => query.Where(x =>
                x.inventoryItem != null && x.inventoryItem.Quantity > x.inventoryItem.ReorderPoint),
            _ => query,
        };

        // Orden profesional (TAREA 24G, sección 9): Sin existencia primero, luego Stock bajo, luego
        // Con stock, y dentro de cada grupo por nombre/Id estable. El operador condicional se
        // traduce a CASE WHEN en SQL, sin traer filas de más a memoria para ordenarlas.
        var rows = await query
            .OrderBy(x => x.inventoryItem == null || x.inventoryItem.Quantity <= 0m
                ? 0
                : x.inventoryItem.Quantity <= x.inventoryItem.ReorderPoint ? 1 : 2)
            .ThenBy(x => x.product.Name)
            .ThenBy(x => x.product.Id)
            .Skip(skip)
            .Take(take + 1)
            .Select(x => new
            {
                x.product.Id,
                x.product.Sku,
                x.product.Barcode,
                x.product.Name,
                x.product.IsActive,
                Quantity = x.inventoryItem != null ? x.inventoryItem.Quantity : 0m,
                ReorderPoint = x.inventoryItem != null ? x.inventoryItem.ReorderPoint : 0m,
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = rows.Count > take;

        var items = rows
            .Take(take)
            .Select(r => new InventoryCatalogItem(
                new ProductId(r.Id),
                r.Sku,
                r.Barcode,
                r.Name,
                r.IsActive,
                r.Quantity,
                r.ReorderPoint,
                ComputeStockStatus(r.Quantity, r.ReorderPoint)))
            .ToList();

        return new InventoryCatalogPageResult(items, hasNextPage);
    }

    // 4 categorías mutuamente excluyentes y exhaustivas: InStockCount se deriva por resta en vez de
    // una cuarta consulta COUNT, sin perder precisión (TAREA 24G, sección 13).
    public async Task<InventorySummary> GetSummaryAsync(
        OrganizationId organizationId, BranchId branchId, CancellationToken cancellationToken)
    {
        var branchIdValue = branchId.Value;

        var query =
            from product in _context.Products.AsNoTracking()
            where product.OrganizationId == organizationId.Value && product.TracksInventory
            join inventoryItem in _context.InventoryItems.AsNoTracking()
                    .Where(i => i.BranchId == branchIdValue)
                on product.Id equals inventoryItem.ProductId into inventoryItems
            from inventoryItem in inventoryItems.DefaultIfEmpty()
            select new { product, inventoryItem };

        var trackedProductsCount = await query.CountAsync(cancellationToken);

        var outOfStockCount = await query.CountAsync(x =>
            x.inventoryItem == null || x.inventoryItem.Quantity <= 0m,
            cancellationToken);

        var lowStockCount = await query.CountAsync(x =>
            x.inventoryItem != null && x.inventoryItem.Quantity > 0m &&
            x.inventoryItem.Quantity <= x.inventoryItem.ReorderPoint,
            cancellationToken);

        var inStockCount = trackedProductsCount - outOfStockCount - lowStockCount;

        return new InventorySummary(trackedProductsCount, inStockCount, lowStockCount, outOfStockCount);
    }

    private static InventoryStockStatus ComputeStockStatus(decimal quantity, decimal reorderPoint)
    {
        if (quantity <= 0m)
        {
            return InventoryStockStatus.OutOfStock;
        }

        return quantity <= reorderPoint ? InventoryStockStatus.LowStock : InventoryStockStatus.InStock;
    }
}
