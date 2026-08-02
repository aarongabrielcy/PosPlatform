using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Security;
using FakeInventoryItemRepository = Pos.Application.Tests.Sales.CompleteSale.FakeInventoryItemRepository;
using FakeInventoryMovementRepository = Pos.Application.Tests.Sales.CompleteSale.FakeInventoryMovementRepository;
using FakeSaleRepository = Pos.Application.Tests.Sales.CompleteSale.FakeSaleRepository;
using FakeUnitOfWork = Pos.Application.Tests.Sales.CompleteSale.FakeUnitOfWork;

namespace Pos.Application.Tests.Sales.Checkout;

public class CheckoutServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    // ---------- Precondiciones (sección 6) ----------

    [Fact]
    public async Task CheckoutWithoutAuthenticatedUserReturnsNotAuthenticated()
    {
        var fixture = new Fixture();
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.NotAuthenticated, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithoutProcessSalePermissionReturnsNotAuthorized()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.NotAuthorized, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithoutOpenRegisterSessionReturnsRegisterSessionRequired()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.RegisterSessionRequired, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithEmptyCartReturnsEmptyCart()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.EmptyCart, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithMismatchedCartCurrencyReturnsCurrencyMismatch()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        var line = new SalesCartLine(product.Id, product.Sku.Value, product.Name, 1m, 10m, 10m, "USD", 10m, true);
        fixture.CurrentSalesCart.SetSnapshot(new SalesCartSnapshot([line], "USD"));
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.CurrencyMismatch, result.Status);
        AssertNothingPersisted(fixture);
    }

    // ---------- Revalidación de producto (sección 7-8) ----------

    [Fact]
    public async Task CheckoutWithProductNoLongerInCatalogReturnsProductNotFound()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        fixture.ProductRepository.Remove(product.Id);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.ProductNotFound, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithInactiveProductReturnsProductInactive()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        product.Deactivate();
        fixture.ProductRepository.Add(product);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.ProductInactive, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithChangedSkuReturnsProductChangedWithSkuReason()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        product.ChangeSku(new Sku("SKU-NEW"));
        fixture.ProductRepository.Add(product);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.ProductChanged, result.Status);
        Assert.Equal(product.Id.Value, result.ChangedProductId);
        Assert.Equal(CheckoutProductChangeReason.Sku, result.ChangedProductReason);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithChangedNameReturnsProductChangedWithNameReason()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        product.Rename("Nombre actualizado");
        fixture.ProductRepository.Add(product);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.ProductChanged, result.Status);
        Assert.Equal(CheckoutProductChangeReason.Name, result.ChangedProductReason);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutWithChangedPriceReturnsProductChangedWithPriceReason()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        product.ChangeSalePrice(new Money(99m, "MXN"));
        fixture.ProductRepository.Add(product);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.ProductChanged, result.Status);
        Assert.Equal(CheckoutProductChangeReason.Price, result.ChangedProductReason);
        AssertNothingPersisted(fixture);
    }

    // ---------- Stock (sección 9) ----------

    [Fact]
    public async Task CheckoutWithInsufficientStockReturnsInsufficientStockWithAvailableQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct();
        fixture.SeedInventory(product, 1m);
        fixture.AddCartLine(product, 5m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(100m));

        Assert.Equal(CheckoutResultStatus.InsufficientStock, result.Status);
        Assert.Equal(1m, result.AvailableQuantity);
        AssertNothingPersisted(fixture);
    }

    [Fact]
    public async Task CheckoutForProductWithoutInventoryTrackingSucceedsWithoutTouchingInventory()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(tracksInventory: false, salePrice: 10m);
        fixture.AddCartLineForNonTrackedProduct(product, 3m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(30m));

        Assert.True(result.Success);
        Assert.Equal(0, fixture.InventoryItemRepository.GetByBranchAndProductCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Empty(fixture.InventoryMovementRepository.AddedMovements);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- Pago en efectivo (sección 14) ----------

    [Fact]
    public async Task CheckoutWithExactCashHasZeroChange()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(20m));

        Assert.True(result.Success);
        Assert.Equal(20m, result.Summary!.TotalAmount);
        Assert.Equal(20m, result.Summary.CashTendered);
        Assert.Equal(0m, result.Summary.ChangeAmount);
    }

    [Fact]
    public async Task CheckoutWithCashAboveTotalComputesCorrectChange()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 62.75m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(200m));

        Assert.True(result.Success);
        Assert.Equal(125.50m, result.Summary!.TotalAmount);
        Assert.Equal(200m, result.Summary.CashTendered);
        Assert.Equal(74.50m, result.Summary.ChangeAmount);
    }

    [Fact]
    public async Task CheckoutWithInsufficientCashReturnsInsufficientCash()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(10m));

        Assert.Equal(CheckoutResultStatus.InsufficientCash, result.Status);
        AssertNothingPersisted(fixture);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CheckoutWithNonPositiveCashReturnsInvalidPayment(decimal cashTendered)
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(cashTendered));

        Assert.Equal(CheckoutResultStatus.InvalidPayment, result.Status);
        AssertNothingPersisted(fixture);
    }

    // ---------- Éxito: Sale / SaleLines / Payment / Inventory / Movement / Commit ----------

    [Fact]
    public async Task SuccessfulCheckoutPersistsSaleLinesPaymentInventoryAndMovementInASingleCommit()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 2m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(20m));

        Assert.True(result.Success);

        var addedSale = fixture.SaleRepository.AddedSale;
        Assert.NotNull(addedSale);
        Assert.Equal(SaleStatus.Completed, addedSale!.Status);
        Assert.Equal(fixture.Organization.Value, addedSale.OrganizationId.Value);
        Assert.Equal(fixture.Branch.Value, addedSale.BranchId.Value);
        Assert.Equal(fixture.RegisterSessionId.Value, addedSale.RegisterSessionId.Value);
        Assert.Equal(fixture.User.Value, addedSale.CreatedByUserId.Value);

        var line = Assert.Single(addedSale.Lines);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(10m, line.UnitPrice.Amount);

        var payment = Assert.Single(addedSale.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(20m, payment.Amount.Amount);

        Assert.Equal(1, fixture.SaleRepository.AddCallCount);
        Assert.Equal(0, fixture.SaleRepository.UpdateCallCount);

        var updatedItem = Assert.Single(fixture.InventoryItemRepository.UpdatedItems);
        Assert.Equal(8m, updatedItem.Quantity);

        var movement = Assert.Single(fixture.InventoryMovementRepository.AddedMovements);
        Assert.Equal(InventoryMovementType.SaleDecrease, movement.Type);
        Assert.Equal(addedSale.Id, movement.SaleId);
        Assert.Equal(line.Id, movement.SaleLineId);
        Assert.Equal(2m, movement.Quantity);
        Assert.Equal(10m, movement.QuantityBefore);
        Assert.Equal(8m, movement.QuantityAfter);

        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task SuccessfulCheckoutWithMultipleLinesCreatesOneMovementPerLine()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product1 = fixture.AddProduct(sku: "SKU-001", salePrice: 10m);
        var product2 = fixture.AddProduct(sku: "SKU-002", salePrice: 5m);
        fixture.SeedInventory(product1, 10m);
        fixture.SeedInventory(product2, 20m);
        fixture.AddCartLine(product1, 2m);
        fixture.AddCartLine(product2, 3m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(35m));

        Assert.True(result.Success);
        Assert.Equal(35m, result.Summary!.TotalAmount);
        Assert.Equal(2, fixture.InventoryMovementRepository.AddedMovements.Count);
        Assert.Equal(2, fixture.InventoryItemRepository.UpdatedItems.Count);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- Carrito: limpieza solo en éxito (sección 20, 24) ----------

    [Fact]
    public async Task SuccessfulCheckoutClearsTheCart()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 10m);
        fixture.AddCartLine(product, 1m);
        var service = fixture.BuildService();

        await service.CheckoutAsync(new CheckoutRequest(10m));

        Assert.False(fixture.CurrentSalesCart.Snapshot.HasItems);
        Assert.Equal(1, fixture.CurrentSalesCart.ClearCallCount);
    }

    [Fact]
    public async Task FailedCheckoutNeverClearsTheCart()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs([Permission.ProcessSale]);
        fixture.OpenRegister();
        var product = fixture.AddProduct(salePrice: 10m);
        fixture.SeedInventory(product, 1m);
        fixture.AddCartLine(product, 5m);
        var service = fixture.BuildService();

        var result = await service.CheckoutAsync(new CheckoutRequest(50m));

        Assert.Equal(CheckoutResultStatus.InsufficientStock, result.Status);
        Assert.True(fixture.CurrentSalesCart.Snapshot.HasItems);
        Assert.Equal(0, fixture.CurrentSalesCart.ClearCallCount);
    }

    private static void AssertNothingPersisted(Fixture fixture)
    {
        Assert.Equal(0, fixture.SaleRepository.AddCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(0, fixture.InventoryMovementRepository.AddCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
        Assert.Equal(0, fixture.CurrentSalesCart.ClearCallCount);
    }

    // ---------- Fixture ----------

    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = OrganizationId.New();
            Branch = BranchId.New();
            Register = RegisterId.New();
            RegisterSessionId = RegisterSessionId.New();
            User = UserId.New();

            CurrentUserSession = new FakeCurrentUserSession();
            CurrentRegisterSession = new FakeCurrentRegisterSession();
            CurrentSalesCart = new FakeCurrentSalesCart();
            ProductRepository = new FakeProductRepository();
            InventoryItemRepository = new FakeInventoryItemRepository();
            InventoryMovementRepository = new FakeInventoryMovementRepository();
            SaleRepository = new FakeSaleRepository(null);
            UnitOfWork = new FakeUnitOfWork();
            Clock = new FakeClock(FixedNow);
        }

        public OrganizationId Organization { get; }

        public BranchId Branch { get; }

        public RegisterId Register { get; }

        public RegisterSessionId RegisterSessionId { get; }

        public UserId User { get; }

        public FakeCurrentUserSession CurrentUserSession { get; }

        public FakeCurrentRegisterSession CurrentRegisterSession { get; }

        public FakeCurrentSalesCart CurrentSalesCart { get; }

        public FakeProductRepository ProductRepository { get; }

        public FakeInventoryItemRepository InventoryItemRepository { get; }

        public FakeInventoryMovementRepository InventoryMovementRepository { get; }

        public FakeSaleRepository SaleRepository { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public FakeClock Clock { get; }

        public void AuthenticateAs(IEnumerable<Permission> permissions)
        {
            CurrentUserSession.CurrentUser = new AuthenticatedUser(
                User, Organization, RoleId.New(), "CAJERO", "Cajero Uno", "Cajero", permissions);
        }

        public void OpenRegister()
        {
            CurrentRegisterSession.Current = new ActiveRegisterSession(
                RegisterSessionId, Organization, Branch, Register, "Caja 1", User, "Cajero Uno", FixedNow, 500m, "MXN");
        }

        public Product AddProduct(string sku = "SKU-001", string name = "Producto de prueba", decimal salePrice = 10m, bool tracksInventory = true)
        {
            var product = new Product(
                ProductId.New(), Organization, new Sku(sku), null, name, null, new Money(salePrice, "MXN"), null, tracksInventory, FixedNow);
            ProductRepository.Add(product);

            return product;
        }

        public void SeedInventory(Product product, decimal quantity) =>
            InventoryItemRepository.Add(new InventoryItem(InventoryItemId.New(), Branch, product.Id, quantity, 0m, FixedNow));

        public void AddCartLine(Product product, decimal quantity)
        {
            var subtotal = product.SalePrice.Amount * quantity;
            var line = new SalesCartLine(
                product.Id, product.Sku.Value, product.Name, quantity, product.SalePrice.Amount, subtotal,
                product.SalePrice.Currency, 999m, true);

            AppendLine(line);
        }

        public void AddCartLineForNonTrackedProduct(Product product, decimal quantity)
        {
            var subtotal = product.SalePrice.Amount * quantity;
            var line = new SalesCartLine(
                product.Id, product.Sku.Value, product.Name, quantity, product.SalePrice.Amount, subtotal,
                product.SalePrice.Currency, 0m, false);

            AppendLine(line);
        }

        private void AppendLine(SalesCartLine line)
        {
            var existingLines = CurrentSalesCart.Snapshot.Lines;
            var newLines = existingLines.Append(line).ToList();
            CurrentSalesCart.SetSnapshot(new SalesCartSnapshot(newLines, "MXN"));
        }

        public CheckoutService BuildService() =>
            new(
                CurrentUserSession,
                CurrentRegisterSession,
                CurrentSalesCart,
                ProductRepository,
                InventoryItemRepository,
                InventoryMovementRepository,
                SaleRepository,
                UnitOfWork,
                Clock);
    }
}
