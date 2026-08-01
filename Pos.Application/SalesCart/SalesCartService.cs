using Pos.Application.Authentication;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Products;

namespace Pos.Application.SalesCart;

public sealed class SalesCartService : ISalesCartService
{
    private const int MaxSearchResults = 20;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public SalesCartService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        ICurrentSalesCart currentSalesCart,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
    }

    // Sin sesión o sin caja abierta se devuelve una lista vacía en vez de lanzar: mismo criterio
    // "estado seguro" que ya usa MainWindowViewModel para DisplayName/RoleName.
    public async Task<IReadOnlyList<ProductSearchResult>> SearchProductsAsync(
        string searchTerm, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || registerSession is null)
        {
            return Array.Empty<ProductSearchResult>();
        }

        var trimmed = (searchTerm ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Array.Empty<ProductSearchResult>();
        }

        var products = await _productRepository.SearchActiveAsync(
            user.OrganizationId, trimmed, MaxSearchResults, cancellationToken);

        var results = new List<ProductSearchResult>(products.Count);

        foreach (var product in products)
        {
            var availableQuantity = await ResolveAvailableQuantityAsync(
                registerSession.BranchId, product, cancellationToken);

            results.Add(new ProductSearchResult(
                product.Id,
                product.Sku.Value,
                product.Name,
                product.SalePrice.Amount,
                product.SalePrice.Currency,
                availableQuantity,
                product.TracksInventory));
        }

        return results;
    }

    public async Task<SalesCartResult> AddProductAsync(
        AddProductToCartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contextFailure = ValidateContext();

        if (contextFailure is not null)
        {
            return contextFailure;
        }

        if (request.Quantity <= 0m)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.InvalidQuantity);
        }

        var user = _currentUserSession.CurrentUser!;
        var registerSession = _currentRegisterSession.Current!;

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.OrganizationId != user.OrganizationId)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.ProductNotFound);
        }

        if (!product.IsActive)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.ProductInactive);
        }

        if (product.SalePrice.Currency != registerSession.Currency)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.CurrencyMismatch);
        }

        var availableQuantity = await ResolveAvailableQuantityAsync(registerSession.BranchId, product, cancellationToken);

        var snapshot = _currentSalesCart.Snapshot;
        var existingLine = snapshot.Lines.FirstOrDefault(l => l.ProductId == product.Id);
        var newQuantity = (existingLine?.Quantity ?? 0m) + request.Quantity;

        if (product.TracksInventory && newQuantity > availableQuantity)
        {
            return SalesCartResult.Failure(
                availableQuantity <= 0m ? SalesCartResultStatus.OutOfStock : SalesCartResultStatus.InsufficientStock,
                availableQuantity);
        }

        var lineSubtotal = product.SalePrice * newQuantity;
        var updatedLine = new SalesCartLine(
            product.Id,
            product.Sku.Value,
            product.Name,
            newQuantity,
            product.SalePrice.Amount,
            lineSubtotal.Amount,
            product.SalePrice.Currency,
            availableQuantity,
            product.TracksInventory);

        var newLines = existingLine is null
            ? snapshot.Lines.Append(updatedLine).ToList()
            : snapshot.Lines.Select(l => l.ProductId == product.Id ? updatedLine : l).ToList();

        var newSnapshot = new SalesCartSnapshot(newLines, registerSession.Currency);
        _currentSalesCart.SetSnapshot(newSnapshot);

        return SalesCartResult.SuccessResult(newSnapshot);
    }

    public async Task<SalesCartResult> UpdateQuantityAsync(
        UpdateCartLineQuantityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contextFailure = ValidateContext();

        if (contextFailure is not null)
        {
            return contextFailure;
        }

        if (request.NewQuantity <= 0m)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.InvalidQuantity);
        }

        var registerSession = _currentRegisterSession.Current!;
        var snapshot = _currentSalesCart.Snapshot;
        var existingLine = snapshot.Lines.FirstOrDefault(l => l.ProductId == request.ProductId);

        if (existingLine is null)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.LineNotFound);
        }

        // El precio y la existencia siempre se recargan desde Product/Inventory: nunca se
        // confía en lo que ya traía la línea en memoria.
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || !product.IsActive)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.ProductNotFound);
        }

        var availableQuantity = await ResolveAvailableQuantityAsync(registerSession.BranchId, product, cancellationToken);

        if (product.TracksInventory && request.NewQuantity > availableQuantity)
        {
            return SalesCartResult.Failure(
                availableQuantity <= 0m ? SalesCartResultStatus.OutOfStock : SalesCartResultStatus.InsufficientStock,
                availableQuantity);
        }

        var lineSubtotal = product.SalePrice * request.NewQuantity;
        var updatedLine = new SalesCartLine(
            product.Id,
            product.Sku.Value,
            product.Name,
            request.NewQuantity,
            product.SalePrice.Amount,
            lineSubtotal.Amount,
            product.SalePrice.Currency,
            availableQuantity,
            product.TracksInventory);

        var newLines = snapshot.Lines.Select(l => l.ProductId == product.Id ? updatedLine : l).ToList();
        var newSnapshot = new SalesCartSnapshot(newLines, registerSession.Currency);
        _currentSalesCart.SetSnapshot(newSnapshot);

        return SalesCartResult.SuccessResult(newSnapshot);
    }

    public SalesCartResult RemoveLine(ProductId productId)
    {
        var contextFailure = ValidateContext();

        if (contextFailure is not null)
        {
            return contextFailure;
        }

        var registerSession = _currentRegisterSession.Current!;
        var snapshot = _currentSalesCart.Snapshot;

        if (!snapshot.Lines.Any(l => l.ProductId == productId))
        {
            // Idempotente: eliminar una línea inexistente no es un error, el estado deseado ya
            // se cumple.
            return SalesCartResult.SuccessResult(snapshot);
        }

        var newLines = snapshot.Lines.Where(l => l.ProductId != productId).ToList();
        var newSnapshot = new SalesCartSnapshot(newLines, registerSession.Currency);
        _currentSalesCart.SetSnapshot(newSnapshot);

        return SalesCartResult.SuccessResult(newSnapshot);
    }

    public SalesCartResult Clear()
    {
        var contextFailure = ValidateContext();

        if (contextFailure is not null)
        {
            return contextFailure;
        }

        var registerSession = _currentRegisterSession.Current!;
        _currentSalesCart.Clear();

        return SalesCartResult.SuccessResult(SalesCartSnapshot.Empty(registerSession.Currency));
    }

    private SalesCartResult? ValidateContext()
    {
        if (_currentUserSession.CurrentUser is null)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.NotAuthenticated);
        }

        if (_currentRegisterSession.Current is null)
        {
            return SalesCartResult.Failure(SalesCartResultStatus.RegisterSessionRequired);
        }

        return null;
    }

    private async Task<decimal> ResolveAvailableQuantityAsync(
        BranchId branchId, Product product, CancellationToken cancellationToken)
    {
        if (!product.TracksInventory)
        {
            return 0m;
        }

        var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
            branchId, product.Id, cancellationToken);

        return inventoryItem?.Quantity ?? 0m;
    }
}
