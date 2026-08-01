using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Security;

namespace Pos.Application.Tests.SalesCart;

public class SalesCartServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    // ---------- SearchProductsAsync (sección 23) ----------

    [Fact]
    public async Task SearchReturnsEmptyWhenUserIsNotAuthenticated()
    {
        var fixture = new Fixture();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("agua");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchReturnsEmptyWhenRegisterIsNotOpen()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("agua");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchReturnsEmptyForABlankTerm()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("   ");

        Assert.Empty(results);
        Assert.Equal(0, fixture.ProductRepository.SearchActiveCallCount);
    }

    [Fact]
    public async Task SearchFindsAnExactSkuMatch()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("SKU-001");

        Assert.Single(results);
        Assert.Equal(product.Id, results[0].ProductId);
    }

    [Fact]
    public async Task SearchFindsAPartialNameMatch()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("natural");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchIsCaseInsensitive()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("AGUA");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchExcludesInactiveProducts()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        fixture.AddProduct("SKU-001", "Agua natural 1L", isActive: false);
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("agua");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchLimitsResultsTo20()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();

        for (var i = 0; i < 25; i++)
        {
            fixture.AddProduct($"SKU-{i:000}", $"Manzana {i}");
        }

        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("manzana");

        Assert.Equal(20, results.Count);
    }

    [Fact]
    public async Task SearchIncludesAvailableQuantityFromInventory()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 8m);
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("agua");

        Assert.Equal(8m, results[0].AvailableQuantity);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task SearchMarksAProductWithoutStockAsUnavailable()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var results = await service.SearchProductsAsync("agua");

        Assert.Equal(0m, results[0].AvailableQuantity);
        Assert.False(results[0].IsAvailable);
    }

    [Fact]
    public async Task SearchDoesNotWriteToAnyRepository()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        await service.SearchProductsAsync("agua");

        Assert.Equal(0, fixture.CurrentSalesCart.SetCallCount);
    }

    // ---------- AddProductAsync (sección 24) ----------

    [Fact]
    public async Task AddingAProductCreatesALineWithQuantityOneByDefault()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L", price: 10m);
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.True(result.Success);
        Assert.Single(result.Snapshot!.Lines);
        Assert.Equal(1m, result.Snapshot.Lines[0].Quantity);
    }

    [Fact]
    public async Task AddingTheSameProductTwiceIncreasesTheQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        await service.AddProductAsync(new AddProductToCartRequest(product.Id));
        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Single(result.Snapshot!.Lines);
        Assert.Equal(2m, result.Snapshot.Lines[0].Quantity);
    }

    [Fact]
    public async Task AddingAnUnknownProductReturnsProductNotFound()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(ProductId.New()));

        Assert.False(result.Success);
        Assert.Equal(SalesCartResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task AddingAnInactiveProductReturnsProductInactive()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L", isActive: false);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(SalesCartResultStatus.ProductInactive, result.Status);
    }

    [Fact]
    public async Task AddingAProductWithoutStockReturnsOutOfStock()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(SalesCartResultStatus.OutOfStock, result.Status);
    }

    [Fact]
    public async Task AddingMoreThanAvailableStockReturnsInsufficientStock()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 2m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id, 3m));

        Assert.Equal(SalesCartResultStatus.InsufficientStock, result.Status);
    }

    [Fact]
    public async Task AddingMoreThanAvailableStockExposesTheAvailableQuantityOnTheResult()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 2m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id, 3m));

        Assert.Equal(2m, result.AvailableQuantity);
    }

    [Fact]
    public async Task AddingAProductWithoutStockExposesZeroAsTheAvailableQuantityOnTheResult()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(0m, result.AvailableQuantity);
    }

    [Fact]
    public async Task AddingAProductThatDoesNotTrackInventoryIgnoresStock()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Servicio", tracksInventory: false);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id, 100m));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddingWithoutAnAuthenticatedUserReturnsNotAuthenticated()
    {
        var fixture = new Fixture();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(ProductId.New()));

        Assert.Equal(SalesCartResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task AddingWithoutAnOpenRegisterReturnsRegisterSessionRequired()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(ProductId.New()));

        Assert.Equal(SalesCartResultStatus.RegisterSessionRequired, result.Status);
    }

    [Fact]
    public async Task AddingAProductWithADifferentCurrencyThanTheRegisterReturnsCurrencyMismatch()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister("MXN");
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L", currency: "USD");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(SalesCartResultStatus.CurrencyMismatch, result.Status);
    }

    [Fact]
    public async Task AddedLinePriceComesFromTheRepositoryNotTheRequest()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L", price: 15.50m);
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(15.50m, result.Snapshot!.Lines[0].UnitPriceAmount);
    }

    [Fact]
    public async Task AddedLineTaxIsAlwaysZeroBecauseTheDomainDoesNotModelTax()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        Assert.Equal(0m, result.Snapshot!.Lines[0].LineTaxAmount);
        Assert.Equal(0m, result.Snapshot.TaxTotalAmount);
    }

    [Fact]
    public async Task AddingAProductDoesNotModifyInventory()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var inventoryItem = await fixture.InventoryItemRepository.GetByBranchAndProductAsync(
            fixture.BranchId, product.Id, CancellationToken.None);
        Assert.Equal(5m, inventoryItem!.Quantity);
    }

    [Fact]
    public async Task AddingAnInvalidQuantityReturnsInvalidQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();

        var result = await service.AddProductAsync(new AddProductToCartRequest(product.Id, 0m));

        Assert.Equal(SalesCartResultStatus.InvalidQuantity, result.Status);
    }

    // ---------- UpdateQuantityAsync / RemoveLine / Clear (sección 25) ----------

    [Fact]
    public async Task UpdateQuantityIncreasesTheLineQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(product.Id, 3m));

        Assert.True(result.Success);
        Assert.Equal(3m, result.Snapshot!.Lines[0].Quantity);
    }

    [Fact]
    public async Task UpdateQuantityDecreasesTheLineQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id, 3m));

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(product.Id, 1m));

        Assert.Equal(1m, result.Snapshot!.Lines[0].Quantity);
    }

    [Fact]
    public async Task UpdateQuantityRejectsAZeroOrNegativeQuantity()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(product.Id, 0m));

        Assert.Equal(SalesCartResultStatus.InvalidQuantity, result.Status);
    }

    [Fact]
    public async Task UpdateQuantityAboveAvailableStockReturnsInsufficientStock()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(product.Id, 10m));

        Assert.Equal(SalesCartResultStatus.InsufficientStock, result.Status);
    }

    [Fact]
    public async Task UpdateQuantityAboveAvailableStockExposesTheAvailableQuantityOnTheResult()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(product.Id, 10m));

        Assert.Equal(5m, result.AvailableQuantity);
    }

    [Fact]
    public async Task UpdateQuantityOfALineThatDoesNotExistReturnsLineNotFound()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var result = await service.UpdateQuantityAsync(new UpdateCartLineQuantityRequest(ProductId.New(), 2m));

        Assert.Equal(SalesCartResultStatus.LineNotFound, result.Status);
    }

    [Fact]
    public async Task RemoveLineDeletesTheLineAndRecalculatesTotals()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = service.RemoveLine(product.Id);

        Assert.True(result.Success);
        Assert.Empty(result.Snapshot!.Lines);
        Assert.Equal(0m, result.Snapshot.TotalAmount);
    }

    [Fact]
    public void RemovingALineThatDoesNotExistIsIdempotent()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        var result = service.RemoveLine(ProductId.New());

        Assert.True(result.Success);
        Assert.Empty(result.Snapshot!.Lines);
    }

    [Fact]
    public async Task ClearEmptiesTheCart()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var product = fixture.AddProduct("SKU-001", "Agua natural 1L");
        fixture.AddInventory(product, 5m);
        var service = fixture.BuildService();
        await service.AddProductAsync(new AddProductToCartRequest(product.Id));

        var result = service.Clear();

        Assert.True(result.Success);
        Assert.Empty(result.Snapshot!.Lines);
        Assert.Equal(1, fixture.CurrentSalesCart.ClearCallCount);
    }

    [Fact]
    public void ClearingAnAlreadyEmptyCartIsIdempotent()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var service = fixture.BuildService();

        service.Clear();
        var result = service.Clear();

        Assert.True(result.Success);
        Assert.Empty(result.Snapshot!.Lines);
    }

    [Fact]
    public async Task TotalsAreRecalculatedAfterEachLineChange()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        fixture.OpenRegister();
        var productA = fixture.AddProduct("SKU-001", "Agua 1L", price: 10m);
        var productB = fixture.AddProduct("SKU-002", "Refresco 600ml", price: 15m);
        fixture.AddInventory(productA, 5m);
        fixture.AddInventory(productB, 5m);
        var service = fixture.BuildService();

        await service.AddProductAsync(new AddProductToCartRequest(productA.Id, 2m));
        var result = await service.AddProductAsync(new AddProductToCartRequest(productB.Id, 1m));

        Assert.Equal(35m, result.Snapshot!.SubtotalAmount);
        Assert.Equal(35m, result.Snapshot.TotalAmount);
    }

    [Fact]
    public void RemoveLineWithoutAnOpenRegisterReturnsRegisterSessionRequired()
    {
        var fixture = new Fixture();
        fixture.AuthenticateUser();
        var service = fixture.BuildService();

        var result = service.RemoveLine(ProductId.New());

        Assert.Equal(SalesCartResultStatus.RegisterSessionRequired, result.Status);
    }

    [Fact]
    public void ClearWithoutAnAuthenticatedUserReturnsNotAuthenticated()
    {
        var fixture = new Fixture();
        var service = fixture.BuildService();

        var result = service.Clear();

        Assert.Equal(SalesCartResultStatus.NotAuthenticated, result.Status);
    }

    private sealed class Fixture
    {
        public FakeCurrentUserSession CurrentUserSession { get; } = new();

        public FakeCurrentRegisterSession CurrentRegisterSession { get; } = new();

        public FakeCurrentSalesCart CurrentSalesCart { get; } = new();

        public FakeProductRepository ProductRepository { get; } = new();

        public FakeInventoryItemRepository InventoryItemRepository { get; } = new();

        public OrganizationId OrganizationId { get; } = OrganizationId.New();

        public BranchId BranchId { get; } = BranchId.New();

        public global::Pos.Application.SalesCart.SalesCartService BuildService() => new(
            CurrentUserSession, CurrentRegisterSession, CurrentSalesCart, ProductRepository, InventoryItemRepository);

        public void AuthenticateUser()
        {
            CurrentUserSession.CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId, RoleId.New(), "cajero", "Cajero Uno", "Cajero", new HashSet<Permission>());
        }

        public void OpenRegister(string currency = "MXN")
        {
            CurrentRegisterSession.Current = new ActiveRegisterSession(
                RegisterSessionId.New(), OrganizationId, BranchId, RegisterId.New(), "Caja 1",
                UserId.New(), "Cajero Uno", FixedNow, 100m, currency);
        }

        public Product AddProduct(
            string sku,
            string name,
            decimal price = 10m,
            string currency = "MXN",
            bool tracksInventory = true,
            bool isActive = true)
        {
            var product = new Product(
                ProductId.New(), OrganizationId, new Sku(sku), null, name, null,
                new Money(price, currency), null, tracksInventory, FixedNow);

            if (!isActive)
            {
                product.Deactivate();
            }

            ProductRepository.Add(product);

            return product;
        }

        public void AddInventory(Product product, decimal quantity) =>
            InventoryItemRepository.Add(
                new InventoryItem(InventoryItemId.New(), BranchId, product.Id, quantity, 0m, FixedNow));
    }
}
