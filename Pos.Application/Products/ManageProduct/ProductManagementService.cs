using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Inventory;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Security;

namespace Pos.Application.Products.ManageProduct;

public sealed class ProductManagementService : IProductManagementService
{
    private const int MaxSearchResults = 20;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ProductManagementService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
        _inventoryMovementRepository = inventoryMovementRepository ?? throw new ArgumentNullException(nameof(inventoryMovementRepository));
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

        try
        {
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
                inventoryItem.ChangeReorderPoint(reorderPoint, _clock.UtcNow);
                await _inventoryItemRepository.UpdateAsync(inventoryItem, cancellationToken);
                updatedInventoryItem = inventoryItem;
            }
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        var details = await BuildProductDetailsAsync(
            product, _currentRegisterSession.Current, cancellationToken, updatedInventoryItem);

        return UpdateProductResult.SuccessResult(details);
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

        if (isActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
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

        await _inventoryMovementRepository.AddAsync(movement, cancellationToken);
        await _inventoryItemRepository.UpdateAsync(inventoryItem, cancellationToken);
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
