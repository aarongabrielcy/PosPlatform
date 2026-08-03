using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Inventory;
using Pos.Application.ProductAudit;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;

namespace Pos.Application.Products.ManageProduct;

public sealed class ProductManagementService : IProductManagementService
{
    private const int MaxSearchResults = 20;

    // "Reciente" V1 (TAREA 24D, sección 29): últimas 24 horas.
    private static readonly TimeSpan RecentActivityWindow = TimeSpan.FromHours(24);

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly IProductCatalogQuery _productCatalogQuery;
    private readonly IProductAuditRepository _productAuditRepository;
    private readonly IProductAuditQuery _productAuditQuery;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ProductManagementService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IProductCatalogQuery productCatalogQuery,
        IProductAuditRepository productAuditRepository,
        IProductAuditQuery productAuditQuery,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
        _inventoryMovementRepository = inventoryMovementRepository ?? throw new ArgumentNullException(nameof(inventoryMovementRepository));
        _productCatalogQuery = productCatalogQuery ?? throw new ArgumentNullException(nameof(productCatalogQuery));
        _productAuditRepository = productAuditRepository ?? throw new ArgumentNullException(nameof(productAuditRepository));
        _productAuditQuery = productAuditQuery ?? throw new ArgumentNullException(nameof(productAuditQuery));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // Requiere ManageProducts porque es la búsqueda que alimenta la pantalla administrativa (con
    // opción de incluir inactivos), a diferencia de SalesCartService.SearchProductsAsync, que
    // cualquier usuario autenticado con caja abierta puede usar para vender.
    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        string searchTerm, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ManageProducts) || registerSession is null)
        {
            return Array.Empty<ProductSearchResult>();
        }

        var trimmed = (searchTerm ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Array.Empty<ProductSearchResult>();
        }

        var products = await _productRepository.SearchAsync(
            user.OrganizationId, trimmed, includeInactive, MaxSearchResults, cancellationToken);

        var results = new List<ProductSearchResult>(products.Count);

        foreach (var product in products)
        {
            var availableQuantity = await ResolveQuantityAsync(registerSession.BranchId, product, cancellationToken);

            results.Add(new ProductSearchResult(
                product.Id,
                product.Sku.Value,
                product.Name,
                product.SalePrice.Amount,
                product.SalePrice.Currency,
                availableQuantity,
                product.TracksInventory,
                product.IsActive));
        }

        return results;
    }

    public async Task<ProductDetails?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ManageProducts))
        {
            return null;
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

        if (product is null || product.OrganizationId != user.OrganizationId)
        {
            return null;
        }

        return await BuildProductDetailsAsync(product, _currentRegisterSession.Current, cancellationToken);
    }

    public async Task<ProductCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm,
        ProductCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ManageProducts) || registerSession is null)
        {
            return ProductCatalogPageResult.Empty;
        }

        var page = await _productCatalogQuery.SearchPageAsync(
            user.OrganizationId, registerSession.BranchId, searchTerm, filter, skip, take, cancellationToken);

        // El indicador de actividad reciente se oculta por completo sin ViewProductAudit (TAREA
        // 24D, sección 34): ni siquiera se consulta la auditoría en ese caso.
        if (!user.HasPermission(Permission.ViewProductAudit) || page.Items.Count == 0)
        {
            return page;
        }

        var productIds = page.Items.Select(item => item.ProductId).ToList();
        var sinceUtc = _clock.UtcNow - RecentActivityWindow;

        var recentActivity = await _productAuditQuery.GetRecentActivityAsync(
            user.OrganizationId, productIds, sinceUtc, cancellationToken);

        if (recentActivity.Count == 0)
        {
            return page;
        }

        var itemsWithActivity = page.Items
            .Select(item => recentActivity.TryGetValue(item.ProductId, out var activity)
                ? new ProductCatalogItem(
                    item.ProductId, item.Sku, item.Barcode, item.Name, item.SalePriceAmount, item.Currency,
                    item.TracksInventory, item.Quantity, item.ReorderPoint, item.IsActive, activity)
                : item)
            .ToList();

        return new ProductCatalogPageResult(itemsWithActivity, page.HasNextPage);
    }

    public async Task<ProductCatalogSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ManageProducts) || registerSession is null)
        {
            return ProductCatalogSummary.Empty;
        }

        return await _productCatalogQuery.GetSummaryAsync(user.OrganizationId, registerSession.BranchId, cancellationToken);
    }

    public async Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ManageProducts))
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.NotAuthorized);
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.OrganizationId != user.OrganizationId)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidName);
        }

        Sku sku;

        try
        {
            sku = new Sku(request.Sku);
        }
        catch (DomainValidationException)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidSku);
        }

        // Duplicado de Sku: solo si el valor normalizado realmente cambia respecto al actual, y
        // excluyendo el propio producto (igual criterio que el duplicado de Barcode más abajo).
        if (sku.Value != product.Sku.Value)
        {
            var existingBySku = await _productRepository.GetBySkuAsync(user.OrganizationId, sku, cancellationToken);

            if (existingBySku is not null && existingBySku.Id != product.Id)
            {
                return UpdateProductResult.Failure(UpdateProductResultStatus.DuplicateSku);
            }
        }

        Barcode? barcode = null;

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            try
            {
                barcode = new Barcode(request.Barcode);
            }
            catch (DomainValidationException)
            {
                return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidBarcode);
            }
        }

        if (request.SalePrice < 0m)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidSalePrice);
        }

        // La moneda siempre es la ya asignada al Product existente: nunca se acepta la moneda
        // enviada por Desktop.
        Money salePrice;

        try
        {
            salePrice = new Money(request.SalePrice, product.SalePrice.Currency);
        }
        catch (DomainValidationException)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidSalePrice);
        }

        Money? cost = null;

        if (request.Cost is { } costAmount)
        {
            if (costAmount < 0m)
            {
                return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidCost);
            }

            try
            {
                cost = new Money(costAmount, product.SalePrice.Currency);
            }
            catch (DomainValidationException)
            {
                return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidCost);
            }
        }

        if (request.ReorderPoint is { } requestedReorderPoint && requestedReorderPoint < 0m)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidReorderPoint);
        }

        // Duplicado de Barcode: solo si el valor realmente cambia respecto al actual, y excluyendo
        // el propio producto.
        if (barcode?.Value != product.Barcode?.Value && barcode is { } barcodeToCheck)
        {
            var existingByBarcode = await _productRepository.GetByBarcodeAsync(
                user.OrganizationId, barcodeToCheck, cancellationToken);

            if (existingByBarcode is not null && existingByBarcode.Id != product.Id)
            {
                return UpdateProductResult.Failure(UpdateProductResultStatus.DuplicateBarcode);
            }
        }

        // Estado "antes" capturado justo antes de mutar Domain (TAREA 24D, sección 11): permite
        // diferenciar campo por campo después de aplicar los mutadores.
        var beforeSku = product.Sku.Value;
        var beforeBarcode = product.Barcode?.Value;
        var beforeName = product.Name;
        var beforeDescription = product.Description;
        var beforeSalePrice = product.SalePrice;
        var beforeCost = product.Cost;

        try
        {
            product.ChangeSku(sku);
            product.Rename(request.Name);
            product.ChangeDescription(request.Description);
            product.ChangeBarcode(barcode);
            product.ChangeSalePrice(salePrice);
            product.ChangeCost(cost);
        }
        catch (DomainValidationException)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InvalidName);
        }

        var changes = new List<(ProductAuditField FieldName, string? OldValue, string? NewValue)>();

        AddIfChanged(changes, ProductAuditField.Sku, beforeSku, product.Sku.Value);
        AddIfChanged(changes, ProductAuditField.Barcode, beforeBarcode, product.Barcode?.Value);
        AddIfChanged(changes, ProductAuditField.Name, beforeName, product.Name);
        AddIfChanged(changes, ProductAuditField.Description, beforeDescription, product.Description);
        AddIfChanged(
            changes, ProductAuditField.SalePrice,
            ProductAuditValueFormatter.FormatMoney(beforeSalePrice.Amount, beforeSalePrice.Currency),
            ProductAuditValueFormatter.FormatMoney(product.SalePrice.Amount, product.SalePrice.Currency));
        AddIfChanged(
            changes, ProductAuditField.Cost,
            beforeCost is null ? null : ProductAuditValueFormatter.FormatMoney(beforeCost.Amount, beforeCost.Currency),
            product.Cost is null ? null : ProductAuditValueFormatter.FormatMoney(product.Cost.Amount, product.Cost.Currency));

        await _productRepository.UpdateAsync(product, cancellationToken);

        InventoryItem? updatedInventoryItem = null;

        // Solo se toca InventoryItem si el producto controla inventario, la request trae un
        // ReorderPoint y hay una caja actual desde la que resolver el Branch: nunca se crea un
        // InventoryItem aquí, y Quantity nunca se modifica por esta vía.
        if (product.TracksInventory && request.ReorderPoint is { } reorderPoint &&
            _currentRegisterSession.Current is { } registerSession)
        {
            var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
                registerSession.BranchId, product.Id, cancellationToken);

            if (inventoryItem is not null)
            {
                var beforeReorderPoint = inventoryItem.ReorderPoint;
                inventoryItem.ChangeReorderPoint(reorderPoint, _clock.UtcNow);
                await _inventoryItemRepository.UpdateAsync(inventoryItem, cancellationToken);
                updatedInventoryItem = inventoryItem;

                AddIfChanged(
                    changes, ProductAuditField.ReorderPoint,
                    ProductAuditValueFormatter.FormatDecimal(beforeReorderPoint),
                    ProductAuditValueFormatter.FormatDecimal(inventoryItem.ReorderPoint));
            }
        }

        // Solo se crea auditoría si hubo al menos un cambio real: evita un evento Updated vacío
        // (TAREA 24D, sección 11).
        if (changes.Count > 0)
        {
            var auditEvent = ProductAuditEvent.CreateUpdated(
                ProductAuditEventId.New(),
                user.OrganizationId,
                product.Id,
                user.UserId,
                user.Username,
                user.DisplayName,
                product.Sku.Value,
                product.Name,
                _clock.UtcNow,
                changes);

            await _productAuditRepository.AddAsync(auditEvent, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        var details = await BuildProductDetailsAsync(
            product, _currentRegisterSession.Current, cancellationToken, updatedInventoryItem);

        return UpdateProductResult.SuccessResult(details);
    }

    private static void AddIfChanged(
        List<(ProductAuditField FieldName, string? OldValue, string? NewValue)> changes,
        ProductAuditField fieldName,
        string? oldValue,
        string? newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add((fieldName, oldValue, newValue));
        }
    }

    public async Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ManageProducts))
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.NotAuthorized);
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

        if (product is null || product.OrganizationId != user.OrganizationId)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound);
        }

        // Sin transición real no se crea auditoría falsa (TAREA 24D, sección 12): activar un
        // producto ya activo (o desactivar uno ya inactivo) sigue siendo un no-op auditable-mente.
        var wasActive = product.IsActive;

        if (isActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        await _productRepository.UpdateAsync(product, cancellationToken);

        if (wasActive != isActive)
        {
            var now = _clock.UtcNow;

            var auditEvent = isActive
                ? ProductAuditEvent.CreateActivated(
                    ProductAuditEventId.New(), user.OrganizationId, product.Id, user.UserId,
                    user.Username, user.DisplayName, product.Sku.Value, product.Name, now)
                : ProductAuditEvent.CreateDeactivated(
                    ProductAuditEventId.New(), user.OrganizationId, product.Id, user.UserId,
                    user.Username, user.DisplayName, product.Sku.Value, product.Name, now);

            await _productAuditRepository.AddAsync(auditEvent, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        var details = await BuildProductDetailsAsync(product, _currentRegisterSession.Current, cancellationToken);

        return UpdateProductResult.SuccessResult(details);
    }

    public async Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.AdjustInventory))
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.NotAuthorized);
        }

        var registerSession = _currentRegisterSession.Current;

        if (registerSession is null)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.RegisterSessionRequired);
        }

        if (request.Quantity <= 0m)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.InvalidQuantity);
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.OrganizationId != user.OrganizationId)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ProductNotFound);
        }

        if (!product.TracksInventory)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ProductDoesNotTrackInventory);
        }

        var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
            registerSession.BranchId, product.Id, cancellationToken);

        if (inventoryItem is null)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.InventoryItemNotFound);
        }

        if (request.AdjustmentType == InventoryAdjustmentType.Decrease && request.Quantity > inventoryItem.Quantity)
        {
            return AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ResultingQuantityNegative);
        }

        var now = _clock.UtcNow;
        var quantityBefore = inventoryItem.Quantity;

        // InventoryMovement calcula y valida QuantityAfter internamente; ApplyMovement exige que
        // QuantityBefore coincida exactamente con la Quantity actual del InventoryItem, lo que
        // impide aplicar un movimiento calculado sobre un estado obsoleto.
        var movement = request.AdjustmentType == InventoryAdjustmentType.Increase
            ? InventoryMovement.CreateManualIncrease(
                InventoryMovementId.New(),
                inventoryItem.Id,
                registerSession.BranchId,
                product.Id,
                user.UserId,
                request.Quantity,
                quantityBefore,
                now)
            : InventoryMovement.CreateManualDecrease(
                InventoryMovementId.New(),
                inventoryItem.Id,
                registerSession.BranchId,
                product.Id,
                user.UserId,
                request.Quantity,
                quantityBefore,
                now);

        inventoryItem.ApplyMovement(movement);

        // ProductAudit (quién/contexto administrativo) y InventoryMovement (qué movimiento sufrió
        // el inventario) son responsabilidades distintas, pero deben quedar en el mismo commit
        // (TAREA 24D, sección 13).
        var auditEvent = ProductAuditEvent.CreateInventoryAdjusted(
            ProductAuditEventId.New(),
            user.OrganizationId,
            product.Id,
            user.UserId,
            user.Username,
            user.DisplayName,
            product.Sku.Value,
            product.Name,
            now,
            ProductAuditValueFormatter.FormatDecimal(movement.QuantityBefore),
            ProductAuditValueFormatter.FormatDecimal(movement.QuantityAfter));

        await _inventoryMovementRepository.AddAsync(movement, cancellationToken);
        await _inventoryItemRepository.UpdateAsync(inventoryItem, cancellationToken);
        await _productAuditRepository.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return AdjustProductInventoryResult.SuccessResult(product.Id, inventoryItem.Quantity);
    }

    private async Task<ProductDetails> BuildProductDetailsAsync(
        Product product,
        ActiveRegisterSession? registerSession,
        CancellationToken cancellationToken,
        InventoryItem? knownInventoryItem = null)
    {
        var quantity = 0m;
        var reorderPoint = 0m;

        if (product.TracksInventory && registerSession is not null)
        {
            var inventoryItem = knownInventoryItem ?? await _inventoryItemRepository.GetByBranchAndProductAsync(
                registerSession.BranchId, product.Id, cancellationToken);

            if (inventoryItem is not null)
            {
                quantity = inventoryItem.Quantity;
                reorderPoint = inventoryItem.ReorderPoint;
            }
        }

        return new ProductDetails(
            product.Id,
            product.Sku.Value,
            product.Barcode?.Value,
            product.Name,
            product.Description,
            product.SalePrice.Amount,
            product.SalePrice.Currency,
            product.Cost?.Amount,
            product.TracksInventory,
            product.IsActive,
            quantity,
            reorderPoint);
    }

    private async Task<decimal> ResolveQuantityAsync(BranchId branchId, Product product, CancellationToken cancellationToken)
    {
        if (!product.TracksInventory)
        {
            return 0m;
        }

        var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(branchId, product.Id, cancellationToken);

        return inventoryItem?.Quantity ?? 0m;
    }
}
