using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Enforcement;
using Pos.Application.Inventory;
using Pos.Application.ProductAudit;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;

namespace Pos.Application.Products.ManageProduct;

// Ver IProductPhotoService para la justificación de por qué esta funcionalidad vive fuera de
// ProductManagementService/AddPosInfrastructure. El resto de la lógica (permisos, enforcement,
// auditoría, notificación administrativa, orden seguro de reemplazo/borrado) replica exactamente
// el mismo patrón que ProductManagementService.UpdateAsync/SetActiveAsync.
public sealed class ProductPhotoService : IProductPhotoService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IProductAuditRepository _productAuditRepository;
    private readonly IAdministrativeNotificationWriter _administrativeNotificationWriter;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IProductImageStore _productImageStore;

    public ProductPhotoService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository,
        IProductAuditRepository productAuditRepository,
        IAdministrativeNotificationWriter administrativeNotificationWriter,
        IInstallationEnforcementStateService enforcementStateService,
        IUnitOfWork unitOfWork,
        IClock clock,
        IProductImageStore productImageStore)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
        _productAuditRepository = productAuditRepository ?? throw new ArgumentNullException(nameof(productAuditRepository));
        _administrativeNotificationWriter = administrativeNotificationWriter
            ?? throw new ArgumentNullException(nameof(administrativeNotificationWriter));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _productImageStore = productImageStore ?? throw new ArgumentNullException(nameof(productImageStore));
    }

    // Agrega o reemplaza la foto del producto (BASIC-UX-01, sección 6/13). El nuevo archivo
    // administrado se guarda ANTES de tocar Product/DB: si la imagen no es válida o excede el
    // tamaño permitido, Product queda exactamente como estaba. El archivo administrado anterior
    // (si existía) solo se borra DESPUÉS de confirmar el commit, nunca antes: un fallo a media
    // operación nunca deja a Product apuntando a un archivo inexistente.
    public async Task<UpdateProductResult> SetProductImageAsync(
        ProductId productId, byte[] imageContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageContent);

        if (_enforcementStateService.Current != InstallationEnforcementState.Allowed)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InstallationRestricted);
        }

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

        var storeResult = await _productImageStore.SaveAsync(imageContent, cancellationToken);

        if (!storeResult.Success)
        {
            return UpdateProductResult.Failure(storeResult.Status == ProductImageStoreStatus.TooLarge
                ? UpdateProductResultStatus.ImageTooLarge
                : UpdateProductResultStatus.InvalidImage);
        }

        var previousImageFileName = product.ImageFileName;
        product.ChangeImage(storeResult.ImageFileName!);

        await _productRepository.UpdateAsync(product, cancellationToken);

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
            [(ProductAuditField.Image, previousImageFileName, product.ImageFileName)]);

        await _productAuditRepository.AddAsync(auditEvent, cancellationToken);
        await _administrativeNotificationWriter.TryAddForProductAuditAsync(auditEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Limpieza best-effort del archivo anterior (sección 15): un fallo aquí nunca deshace el
        // cambio ya persistido y confirmado arriba.
        if (previousImageFileName is not null)
        {
            await _productImageStore.DeleteAsync(previousImageFileName, cancellationToken);
        }

        var details = await BuildProductDetailsAsync(product, cancellationToken);

        return UpdateProductResult.SuccessResult(details);
    }

    // Quita la foto del producto sin afectar el Product/inventario/historial (sección 14). Sin foto
    // previa es un no-op auditable-mente, mismo criterio que ProductManagementService.SetActiveAsync
    // sin transición real.
    public async Task<UpdateProductResult> RemoveProductImageAsync(
        ProductId productId, CancellationToken cancellationToken = default)
    {
        if (_enforcementStateService.Current != InstallationEnforcementState.Allowed)
        {
            return UpdateProductResult.Failure(UpdateProductResultStatus.InstallationRestricted);
        }

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

        var previousImageFileName = product.ImageFileName;

        if (previousImageFileName is null)
        {
            var unchangedDetails = await BuildProductDetailsAsync(product, cancellationToken);
            return UpdateProductResult.SuccessResult(unchangedDetails);
        }

        product.RemoveImage();

        await _productRepository.UpdateAsync(product, cancellationToken);

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
            [(ProductAuditField.Image, previousImageFileName, (string?)null)]);

        await _productAuditRepository.AddAsync(auditEvent, cancellationToken);
        await _administrativeNotificationWriter.TryAddForProductAuditAsync(auditEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        await _productImageStore.DeleteAsync(previousImageFileName, cancellationToken);

        var details = await BuildProductDetailsAsync(product, cancellationToken);

        return UpdateProductResult.SuccessResult(details);
    }

    private async Task<ProductDetails> BuildProductDetailsAsync(
        Domain.Products.Product product, CancellationToken cancellationToken)
    {
        var quantity = 0m;
        var reorderPoint = 0m;
        var registerSession = _currentRegisterSession.Current;

        if (product.TracksInventory && registerSession is not null)
        {
            var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
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
            reorderPoint,
            product.ImageFileName);
    }
}
