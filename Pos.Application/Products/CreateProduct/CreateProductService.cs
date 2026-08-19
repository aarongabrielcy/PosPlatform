using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Enforcement;
using Pos.Application.Inventory;
using Pos.Application.ProductAudit;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;

namespace Pos.Application.Products.CreateProduct;

public sealed class CreateProductService : ICreateProductService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IProductAuditRepository _productAuditRepository;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateProductService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository,
        IProductAuditRepository productAuditRepository,
        IInstallationEnforcementStateService enforcementStateService,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
        _productAuditRepository = productAuditRepository ?? throw new ArgumentNullException(nameof(productAuditRepository));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<CreateProductResult> CreateAsync(
        CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_enforcementStateService.Current != InstallationEnforcementState.Allowed)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InstallationRestricted);
        }

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ManageProducts))
        {
            return CreateProductResult.Failure(CreateProductResultStatus.NotAuthorized);
        }

        var registerSession = _currentRegisterSession.Current;

        if (registerSession is null)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.RegisterSessionRequired);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidName);
        }

        Sku sku;

        try
        {
            sku = new Sku(request.Sku);
        }
        catch (DomainValidationException)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidSku);
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
                return CreateProductResult.Failure(CreateProductResultStatus.InvalidBarcode);
            }
        }

        if (request.SalePrice < 0m)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidSalePrice);
        }

        Money salePrice;

        try
        {
            salePrice = new Money(request.SalePrice, registerSession.Currency);
        }
        catch (DomainValidationException)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidSalePrice);
        }

        Money? cost = null;

        if (request.Cost is { } costAmount)
        {
            if (costAmount < 0m)
            {
                return CreateProductResult.Failure(CreateProductResultStatus.InvalidCost);
            }

            try
            {
                cost = new Money(costAmount, registerSession.Currency);
            }
            catch (DomainValidationException)
            {
                return CreateProductResult.Failure(CreateProductResultStatus.InvalidCost);
            }
        }

        if (request.InitialQuantity < 0m)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidInitialQuantity);
        }

        if (request.ReorderPoint < 0m)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidReorderPoint);
        }

        var existingBySku = await _productRepository.GetBySkuAsync(user.OrganizationId, sku, cancellationToken);

        if (existingBySku is not null)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.DuplicateSku);
        }

        if (barcode is { } barcodeValue)
        {
            var existingByBarcode = await _productRepository.GetByBarcodeAsync(
                user.OrganizationId, barcodeValue, cancellationToken);

            if (existingByBarcode is not null)
            {
                return CreateProductResult.Failure(CreateProductResultStatus.DuplicateBarcode);
            }
        }

        var now = _clock.UtcNow;

        // Producto e InventoryItem (si aplica) se construyen por completo en memoria antes de
        // tocar el repositorio: así ninguna excepción de validación posterior puede dejar el
        // Product agregado al ChangeTracker sin su InventoryItem correspondiente, sin necesidad
        // de una segunda transacción manual.
        Product product;

        try
        {
            product = new Product(
                ProductId.New(),
                user.OrganizationId,
                sku,
                barcode,
                request.Name,
                request.Description,
                salePrice,
                cost,
                request.TracksInventory,
                now);
        }
        catch (DomainValidationException)
        {
            return CreateProductResult.Failure(CreateProductResultStatus.InvalidName);
        }

        InventoryItem? inventoryItem = null;

        if (request.TracksInventory)
        {
            try
            {
                inventoryItem = new InventoryItem(
                    InventoryItemId.New(),
                    registerSession.BranchId,
                    product.Id,
                    request.InitialQuantity,
                    request.ReorderPoint,
                    now);
            }
            catch (DomainValidationException)
            {
                return CreateProductResult.Failure(CreateProductResultStatus.InvalidInitialQuantity);
            }
        }

        var auditEvent = BuildCreatedAuditEvent(user, product, inventoryItem, now);

        await _productRepository.AddAsync(product, cancellationToken);

        if (inventoryItem is not null)
        {
            await _inventoryItemRepository.AddAsync(inventoryItem, cancellationToken);
        }

        await _productAuditRepository.AddAsync(auditEvent, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return CreateProductResult.SuccessResult(product.Id, product.Sku.Value);
    }

    // Un solo ProductAuditEvent.Created por creación, con un ProductAuditChange por campo
    // inicial presente (TAREA 24D, sección 10). Barcode/Description/Cost solo se agregan si el
    // producto los trae; ReorderPoint/InventoryQuantity solo si se creó InventoryItem.
    private static ProductAuditEvent BuildCreatedAuditEvent(
        AuthenticatedUser user, Product product, InventoryItem? inventoryItem, DateTimeOffset now)
    {
        var changes = new List<(ProductAuditField FieldName, string? OldValue, string? NewValue)>
        {
            (ProductAuditField.Sku, null, product.Sku.Value),
        };

        if (product.Barcode is { } barcode)
        {
            changes.Add((ProductAuditField.Barcode, null, barcode.Value));
        }

        changes.Add((ProductAuditField.Name, null, product.Name));

        if (product.Description is not null)
        {
            changes.Add((ProductAuditField.Description, null, product.Description));
        }

        changes.Add((
            ProductAuditField.SalePrice,
            null,
            ProductAuditValueFormatter.FormatMoney(product.SalePrice.Amount, product.SalePrice.Currency)));

        if (product.Cost is { } cost)
        {
            changes.Add((ProductAuditField.Cost, null, ProductAuditValueFormatter.FormatMoney(cost.Amount, cost.Currency)));
        }

        changes.Add((ProductAuditField.TracksInventory, null, ProductAuditValueFormatter.FormatBool(product.TracksInventory)));

        if (inventoryItem is not null)
        {
            changes.Add((
                ProductAuditField.ReorderPoint, null, ProductAuditValueFormatter.FormatDecimal(inventoryItem.ReorderPoint)));
            changes.Add((
                ProductAuditField.InventoryQuantity, null, ProductAuditValueFormatter.FormatDecimal(inventoryItem.Quantity)));
        }

        return ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(),
            user.OrganizationId,
            product.Id,
            user.UserId,
            user.Username,
            user.DisplayName,
            product.Sku.Value,
            product.Name,
            now,
            changes);
    }
}
