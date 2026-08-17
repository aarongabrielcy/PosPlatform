using System.Globalization;
using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Security;

namespace Pos.Application.Sales.Checkout;

// Primer checkout real (TAREA 25A): revalida el carrito contra Product/InventoryItem, crea Sale +
// SaleLines + Payment (Cash), descuenta inventario, crea InventoryMovement y completa la venta, todo
// en una única unidad de trabajo (IUnitOfWork.CommitAsync exactamente una vez). No atrapa fallos de
// CommitAsync: si la persistencia falla, la excepción se propaga sin tocar CurrentSalesCart (igual
// que RegisterSessionService/CompleteSaleHandler); el llamador (CheckoutViewModel) la captura y
// muestra un error genérico, permitiendo reintentar.
public sealed class CheckoutService : ICheckoutService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CheckoutService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        ICurrentSalesCart currentSalesCart,
        IProductRepository productRepository,
        IInventoryItemRepository inventoryItemRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
        _inventoryMovementRepository = inventoryMovementRepository ?? throw new ArgumentNullException(nameof(inventoryMovementRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return CheckoutResult.Failure(CheckoutResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ProcessSale))
        {
            return CheckoutResult.Failure(CheckoutResultStatus.NotAuthorized);
        }

        var registerSession = _currentRegisterSession.Current;

        if (registerSession is null)
        {
            return CheckoutResult.Failure(CheckoutResultStatus.RegisterSessionRequired);
        }

        var cartSnapshot = _currentSalesCart.Snapshot;

        if (!cartSnapshot.HasItems)
        {
            return CheckoutResult.Failure(CheckoutResultStatus.EmptyCart);
        }

        if (!string.Equals(cartSnapshot.Currency, registerSession.Currency, StringComparison.Ordinal))
        {
            return CheckoutResult.Failure(CheckoutResultStatus.CurrencyMismatch);
        }

        // ---------- Revalidación del carrito contra Product/InventoryItem (sección 7-9) ----------
        // No se confía en el snapshot: cada ProductId se vuelve a consultar. Si algo relevante
        // (Sku/Name/SalePrice) cambió desde que se agregó al carrito, se rechaza el cobro sin
        // completarlo silenciosamente con datos desactualizados.

        var preparedLines = new List<PreparedLine>(cartSnapshot.Lines.Count);

        foreach (var cartLine in cartSnapshot.Lines)
        {
            var product = await _productRepository.GetByIdAsync(cartLine.ProductId, cancellationToken);

            if (product is null || product.OrganizationId != user.OrganizationId)
            {
                return CheckoutResult.Failure(CheckoutResultStatus.ProductNotFound);
            }

            if (!product.IsActive)
            {
                return CheckoutResult.Failure(CheckoutResultStatus.ProductInactive);
            }

            if (!string.Equals(product.Sku.Value, cartLine.Sku, StringComparison.Ordinal))
            {
                return CheckoutResult.ProductChangedResult(
                    product.Id.Value, CheckoutProductChangeReason.Sku, product.Sku.Value);
            }

            if (!string.Equals(product.Name, cartLine.ProductName, StringComparison.Ordinal))
            {
                return CheckoutResult.ProductChangedResult(
                    product.Id.Value, CheckoutProductChangeReason.Name, product.Name);
            }

            if (product.SalePrice.Amount != cartLine.UnitPriceAmount
                || !string.Equals(product.SalePrice.Currency, cartLine.Currency, StringComparison.Ordinal))
            {
                return CheckoutResult.ProductChangedResult(
                    product.Id.Value,
                    CheckoutProductChangeReason.Price,
                    product.SalePrice.Amount.ToString("F2", CultureInfo.InvariantCulture));
            }

            InventoryItem? inventoryItem = null;

            // Productos sin TracksInventory nunca tienen InventoryItem asociado ni se revalidan
            // contra existencia (sección 9): igual criterio que SalesCartService.
            if (product.TracksInventory)
            {
                inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
                    registerSession.BranchId, product.Id, cancellationToken);

                var availableQuantity = inventoryItem?.Quantity ?? 0m;

                if (cartLine.Quantity > availableQuantity)
                {
                    return CheckoutResult.InsufficientStockResult(availableQuantity);
                }
            }

            preparedLines.Add(new PreparedLine(SaleLineId.New(), product, cartLine.Quantity, inventoryItem));
        }

        // ---------- Construcción de Sale + SaleLines (sección 11-12) ----------

        var now = _clock.UtcNow;
        var sale = new Sale(
            SaleId.New(),
            user.OrganizationId,
            registerSession.BranchId,
            registerSession.RegisterSessionId,
            user.UserId,
            registerSession.Currency,
            now);

        foreach (var prepared in preparedLines)
        {
            sale.AddLine(
                prepared.SaleLineId,
                prepared.Product.Id,
                prepared.Product.Sku,
                prepared.Product.Name,
                prepared.Quantity,
                prepared.Product.SalePrice);
        }

        // El total persistido proviene de Sale/Domain; el total del carrito es solo una
        // comparación de consistencia, nunca la fuente de verdad (sección 12).
        if (sale.Total.Amount != cartSnapshot.TotalAmount)
        {
            return CheckoutResult.Failure(CheckoutResultStatus.InternalValidationError);
        }

        // ---------- Pago (sección 13-14, TAREA 25C) ----------
        // Payment.Amount = Sale.Total (lo que efectivamente paga la venta) en ambos métodos. Para
        // Cash, el efectivo entregado y el cambio son solo resultado transitorio del checkout, no
        // hay CashTendered/Change en el esquema de Payment. Para Card (Manual Card: terminal externa
        // ajena a PosPlatform), el monto es exactamente el total y se exige una referencia/
        // autorización no vacía; nunca se solicitan datos sensibles de tarjeta.

        decimal roundedCashTendered;
        decimal changeAmount;
        string? cardReference;

        if (request.PaymentMethod == CheckoutPaymentMethod.Cash)
        {
            roundedCashTendered = Math.Round(request.CashTendered, 2, MidpointRounding.AwayFromZero);

            if (roundedCashTendered <= 0m)
            {
                return CheckoutResult.Failure(CheckoutResultStatus.InvalidPayment);
            }

            if (roundedCashTendered < sale.Total.Amount)
            {
                return CheckoutResult.Failure(CheckoutResultStatus.InsufficientCash);
            }

            changeAmount = roundedCashTendered - sale.Total.Amount;
            cardReference = null;

            sale.AddPayment(PaymentId.New(), PaymentMethod.Cash, sale.Total, now);
        }
        else
        {
            var trimmedReference = request.CardReference?.Trim();

            if (string.IsNullOrWhiteSpace(trimmedReference))
            {
                return CheckoutResult.Failure(CheckoutResultStatus.InvalidCardReference);
            }

            roundedCashTendered = 0m;
            changeAmount = 0m;
            cardReference = trimmedReference;

            sale.AddPayment(PaymentId.New(), PaymentMethod.Card, sale.Total, now, cardReference);
        }

        // ---------- Inventario: movimientos + aplicación (sección 9, 18) ----------
        // Toda la mutación en memoria ocurre antes de cualquier escritura, igual que
        // CompleteSaleHandler: ninguna escritura ocurre antes de este punto.

        var preparedMovements = new List<PreparedMovement>(preparedLines.Count);

        foreach (var prepared in preparedLines)
        {
            if (prepared.InventoryItem is null)
            {
                continue;
            }

            var movement = InventoryMovement.CreateSaleDecrease(
                InventoryMovementId.New(),
                prepared.InventoryItem.Id,
                registerSession.BranchId,
                prepared.Product.Id,
                user.UserId,
                sale.Id,
                prepared.SaleLineId,
                prepared.Quantity,
                prepared.InventoryItem.Quantity,
                now);

            preparedMovements.Add(new PreparedMovement(prepared.InventoryItem, movement));
        }

        foreach (var prepared in preparedMovements)
        {
            prepared.InventoryItem.ApplyMovement(prepared.Movement);
        }

        sale.Complete(now);

        // ---------- Persistencia: un único CommitAsync (sección 18) ----------

        await _saleRepository.AddAsync(sale, cancellationToken);

        foreach (var prepared in preparedMovements)
        {
            await _inventoryItemRepository.UpdateAsync(prepared.InventoryItem, cancellationToken);
        }

        foreach (var prepared in preparedMovements)
        {
            await _inventoryMovementRepository.AddAsync(prepared.Movement, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        // El carrito solo se limpia después de un Commit exitoso (sección 20).
        _currentSalesCart.Clear();

        var summary = new CheckoutSummary(
            sale.Id.Value,
            sale.CompletedAtUtc!.Value,
            sale.Total.Amount,
            sale.Total.Currency,
            request.PaymentMethod,
            roundedCashTendered,
            changeAmount,
            cardReference);

        return CheckoutResult.SuccessResult(summary);
    }

    private sealed record PreparedLine(SaleLineId SaleLineId, Product Product, decimal Quantity, InventoryItem? InventoryItem);

    private sealed record PreparedMovement(InventoryItem InventoryItem, InventoryMovement Movement);
}
