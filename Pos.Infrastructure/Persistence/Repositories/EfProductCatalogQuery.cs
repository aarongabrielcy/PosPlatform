using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.Products.ManageProduct;
using Pos.Domain.Common.Identifiers;

namespace Pos.Infrastructure.Persistence.Repositories;

// Implementa IProductCatalogQuery combinando ProductRecord+InventoryItemRecord (LEFT JOIN
// filtrado a la Branch actual) en una sola consulta traducida a SQL: evita el N+1 de resolver la
// existencia producto por producto que produciría iterar SearchAsync + GetByBranchAndProductAsync
// para una página de hasta 50 productos (ver TAREA 24C, sección 10).
public sealed class EfProductCatalogQuery : IProductCatalogQuery
{
    private readonly PosDbContext _context;

    public EfProductCatalogQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ProductCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        ProductCatalogStatusFilter filter,
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
            where product.OrganizationId == organizationId.Value
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
            ProductCatalogStatusFilter.Active => query.Where(x => x.product.IsActive),
            ProductCatalogStatusFilter.Inactive => query.Where(x => !x.product.IsActive),
            ProductCatalogStatusFilter.OutOfStock => query.Where(x =>
                x.product.TracksInventory && (x.inventoryItem == null || x.inventoryItem.Quantity <= 0m)),
            ProductCatalogStatusFilter.LowStock => query.Where(x =>
                x.product.TracksInventory && x.inventoryItem != null &&
                x.inventoryItem.Quantity > 0m && x.inventoryItem.Quantity <= x.inventoryItem.ReorderPoint),
            _ => query,
        };

        var rows = await query
            .OrderBy(x => x.product.Name)
            .ThenBy(x => x.product.Id)
            .Skip(skip)
            .Take(take + 1)
            .Select(x => new
            {
                x.product.Id,
                x.product.Sku,
                x.product.Barcode,
                x.product.Name,
                x.product.SalePriceAmount,
                x.product.SalePriceCurrency,
                x.product.TracksInventory,
                x.product.IsActive,
                Quantity = x.inventoryItem != null ? x.inventoryItem.Quantity : 0m,
                ReorderPoint = x.inventoryItem != null ? x.inventoryItem.ReorderPoint : 0m,
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = rows.Count > take;

        var items = rows
            .Take(take)
            .Select(r => new ProductCatalogItem(
                new ProductId(r.Id),
                r.Sku,
                r.Barcode,
                r.Name,
                r.SalePriceAmount,
                r.SalePriceCurrency,
                r.TracksInventory,
                r.Quantity,
                r.ReorderPoint,
                r.IsActive))
            .ToList();

        return new ProductCatalogPageResult(items, hasNextPage);
    }

    // 3 consultas COUNT de costo fijo (no escalan con el tamaño del catálogo): no es el único
    // round-trip teóricamente posible (una sola consulta agregada con SUM condicionales lo
    // lograría), pero es la forma más simple de expresar correctamente el mismo criterio de
    // Activo/Stock bajo/Sin existencia que SearchPageAsync sin duplicar lógica sutil de LINQ
    // agregado. Sigue sin ser N+1: el costo no depende de cuántos productos existan.
    public async Task<ProductCatalogSummary> GetSummaryAsync(
        OrganizationId organizationId, BranchId branchId, CancellationToken cancellationToken)
    {
        var branchIdValue = branchId.Value;

        var query =
            from product in _context.Products.AsNoTracking()
            where product.OrganizationId == organizationId.Value
            join inventoryItem in _context.InventoryItems.AsNoTracking()
                    .Where(i => i.BranchId == branchIdValue)
                on product.Id equals inventoryItem.ProductId into inventoryItems
            from inventoryItem in inventoryItems.DefaultIfEmpty()
            select new { product, inventoryItem };

        var totalProducts = await query.CountAsync(cancellationToken);

        var lowStockCount = await query.CountAsync(x =>
            x.product.TracksInventory && x.inventoryItem != null &&
            x.inventoryItem.Quantity > 0m && x.inventoryItem.Quantity <= x.inventoryItem.ReorderPoint,
            cancellationToken);

        var outOfStockCount = await query.CountAsync(x =>
            x.product.TracksInventory && (x.inventoryItem == null || x.inventoryItem.Quantity <= 0m),
            cancellationToken);

        return new ProductCatalogSummary(totalProducts, lowStockCount, outOfStockCount);
    }
}
