using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfProductRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<Guid> SeedOrganizationAsync(PosDbContext context, string name = "Acme")
    {
        var organizationId = Guid.NewGuid();
        context.Add(new OrganizationRecord { Id = organizationId, Name = name, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return organizationId;
    }

    private static Product CreateProduct(OrganizationId organizationId, string sku, string name = "Producto") =>
        new(
            ProductId.New(),
            organizationId,
            new Sku(sku),
            barcode: null,
            name,
            description: null,
            new Money(10m, "MXN"),
            cost: null,
            tracksInventory: true,
            CreatedAtUtc);

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfProductRepository(context);

        var result = await repository.GetByIdAsync(ProductId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsExistingProductPreservingIsActiveAndOptionalFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        var product = CreateProduct(new OrganizationId(organizationId), "SKU-001");
        product.Deactivate();

        await repository.AddAsync(product, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsActive);
        Assert.Null(reloaded.Barcode);
        Assert.Null(reloaded.Cost);
    }

    // ---------- GetBySkuAsync ----------

    [Fact]
    public async Task GetBySkuAsyncFiltersByOrganizationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfProductRepository(context);
        var productA = CreateProduct(new OrganizationId(organizationA), "SKU-001", "Producto A");
        await repository.AddAsync(productA, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetBySkuAsync(new OrganizationId(organizationA), new Sku("SKU-001"), CancellationToken.None);
        var foundForB = await repository.GetBySkuAsync(new OrganizationId(organizationB), new Sku("SKU-001"), CancellationToken.None);

        Assert.NotNull(foundForA);
        Assert.Equal("Producto A", foundForA!.Name);
        Assert.Null(foundForB);
    }

    [Fact]
    public async Task GetBySkuAsyncAllowsSameSkuInDifferentOrganizations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationA), "SKU-001", "Producto A"), CancellationToken.None);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationB), "SKU-001", "Producto B"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetBySkuAsync(new OrganizationId(organizationA), new Sku("SKU-001"), CancellationToken.None);
        var foundForB = await repository.GetBySkuAsync(new OrganizationId(organizationB), new Sku("SKU-001"), CancellationToken.None);

        Assert.Equal("Producto A", foundForA!.Name);
        Assert.Equal("Producto B", foundForB!.Name);
    }

    // ---------- GetByOrganizationAsync ----------

    [Fact]
    public async Task GetByOrganizationAsyncReturnsOnlyProductsOfThatOrganizationInDeterministicOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationA), "SKU-002", "Zeta"), CancellationToken.None);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationA), "SKU-001", "Alfa"), CancellationToken.None);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationB), "SKU-003", "Beta"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var products = await repository.GetByOrganizationAsync(new OrganizationId(organizationA), CancellationToken.None);

        Assert.Equal(2, products.Count);
        Assert.Equal(["Alfa", "Zeta"], products.Select(p => p.Name).ToArray());
    }

    // ---------- Barcode no es único ----------

    [Fact]
    public async Task DifferentProductsMayShareTheSameBarcode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);

        var productOne = new Product(
            ProductId.New(), new OrganizationId(organizationId), new Sku("SKU-001"), new Barcode("1234567890"),
            "Producto 1", null, new Money(10m, "MXN"), null, true, CreatedAtUtc);
        var productTwo = new Product(
            ProductId.New(), new OrganizationId(organizationId), new Sku("SKU-002"), new Barcode("1234567890"),
            "Producto 2", null, new Money(10m, "MXN"), null, true, CreatedAtUtc);

        await repository.AddAsync(productOne, CancellationToken.None);
        await repository.AddAsync(productTwo, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var products = await repository.GetByOrganizationAsync(new OrganizationId(organizationId), CancellationToken.None);

        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Equal("1234567890", p.Barcode!.Value.Value));
    }

    // ---------- GetByBarcodeAsync ----------

    [Fact]
    public async Task GetByBarcodeAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfProductRepository(context);

        var result = await repository.GetByBarcodeAsync(
            OrganizationId.New(), new Barcode("1234567890"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBarcodeAsyncFindsExistingProduct()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        var product = new Product(
            ProductId.New(), new OrganizationId(organizationId), new Sku("SKU-001"), new Barcode("1234567890"),
            "Producto con código", null, new Money(10m, "MXN"), null, true, CreatedAtUtc);
        await repository.AddAsync(product, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var result = await repository.GetByBarcodeAsync(
            new OrganizationId(organizationId), new Barcode("1234567890"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Producto con código", result!.Name);
    }

    [Fact]
    public async Task GetByBarcodeAsyncFiltersByOrganizationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfProductRepository(context);
        var productA = new Product(
            ProductId.New(), new OrganizationId(organizationA), new Sku("SKU-001"), new Barcode("1234567890"),
            "Producto A", null, new Money(10m, "MXN"), null, true, CreatedAtUtc);
        await repository.AddAsync(productA, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetByBarcodeAsync(
            new OrganizationId(organizationA), new Barcode("1234567890"), CancellationToken.None);
        var foundForB = await repository.GetByBarcodeAsync(
            new OrganizationId(organizationB), new Barcode("1234567890"), CancellationToken.None);

        Assert.NotNull(foundForA);
        Assert.Null(foundForB);
    }

    // ---------- SearchActiveAsync ----------

    [Fact]
    public async Task SearchActiveAsyncReturnsExactSkuMatchBeforePartialNameMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), "AGUA-001", "Refresco de cola"), CancellationToken.None);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), "COLA-1L", "Agua"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "cola-1l", 20, CancellationToken.None);

        Assert.Equal("COLA-1L", results[0].Sku.Value);
    }

    [Fact]
    public async Task SearchActiveAsyncOrdersNameStartsWithBeforeNameContains()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), "SKU-001", "Refresco de Cola"), CancellationToken.None);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), "SKU-002", "Cola de caballo"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "cola", 20, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("Cola de caballo", results[0].Name);
        Assert.Equal("Refresco de Cola", results[1].Name);
    }

    [Fact]
    public async Task SearchActiveAsyncIsCaseInsensitive()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), "SKU-001", "Manzana Roja"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "MANZANA", 20, CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchActiveAsyncExcludesInactiveProducts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        var inactive = CreateProduct(new OrganizationId(organizationId), "SKU-001", "Manzana Roja");
        inactive.Deactivate();
        await repository.AddAsync(inactive, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "manzana", 20, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchActiveAsyncFindsProductsByBarcode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);
        var product = new Product(
            ProductId.New(), new OrganizationId(organizationId), new Sku("SKU-001"), new Barcode("7501234567890"),
            "Producto con código", null, new Money(10m, "MXN"), null, true, CreatedAtUtc);
        await repository.AddAsync(product, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "7501234567890", 20, CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchActiveAsyncFiltersByOrganizationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfProductRepository(context);
        await repository.AddAsync(CreateProduct(new OrganizationId(organizationA), "SKU-001", "Manzana"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationB), "manzana", 20, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchActiveAsyncLimitsResultCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfProductRepository(context);

        for (var i = 0; i < 5; i++)
        {
            await repository.AddAsync(CreateProduct(new OrganizationId(organizationId), $"SKU-00{i}", $"Manzana {i}"), CancellationToken.None);
        }

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var results = await repository.SearchActiveAsync(new OrganizationId(organizationId), "manzana", 3, CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchActiveAsyncRejectsBlankSearchTerm()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfProductRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.SearchActiveAsync(OrganizationId.New(), "   ", 20, CancellationToken.None));
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var product = CreateProduct(new OrganizationId(organizationId), "SKU-001");
        var repository = new EfProductRepository(context);

        await repository.AddAsync(product, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<ProductRecord>().AnyAsync(r => r.Id == product.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncRejectsNullProduct()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfProductRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfProductRepository(null!));
    }
}
