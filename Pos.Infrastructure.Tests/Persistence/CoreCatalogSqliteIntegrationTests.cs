using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence;

public class CoreCatalogSqliteIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        return connection;
    }

    [Fact]
    public async Task OrganizationShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        context.Add(new OrganizationRecord
        {
            Id = id,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == id);

        Assert.Equal(id, reloaded.Id);
        Assert.Equal("Acme", reloaded.Name);
        Assert.True(reloaded.IsActive);
        Assert.Equal(createdAtUtc, reloaded.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, reloaded.CreatedAtUtc.Offset);
    }

    [Fact]
    public async Task BranchShouldRoundTripAndReferenceOrganization()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<BranchRecord>().SingleAsync(r => r.Id == branchId);

        Assert.Equal(organizationId, reloaded.OrganizationId);
        Assert.Equal("Sucursal Centro", reloaded.Name);
        Assert.Equal("SUC-1", reloaded.Code);
    }

    [Fact]
    public async Task DeletingOrganizationWithBranchShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var organizationToDelete = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organizationId);
        context.Remove(organizationToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RegisterShouldRoundTripAndReferenceBranch()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId,
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RegisterRecord>().SingleAsync(r => r.Id == registerId);

        Assert.Equal(branchId, reloaded.BranchId);
        Assert.Equal("Caja 1", reloaded.Name);
        Assert.Equal("CAJA-1", reloaded.Code);
    }

    [Fact]
    public async Task DeletingBranchWithRegisterShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RegisterRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var branchToDelete = await context.Set<BranchRecord>().SingleAsync(r => r.Id == branchId);
        context.Remove(branchToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProductShouldRoundTripAndReferenceOrganization()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = productId,
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Barcode = "1234567890",
            Name = "Producto de prueba",
            Description = "Descripción",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            CostAmount = 50m,
            CostCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<ProductRecord>().SingleAsync(r => r.Id == productId);

        Assert.Equal(organizationId, reloaded.OrganizationId);
        Assert.Equal("PROD-001", reloaded.Sku);
        Assert.Equal("1234567890", reloaded.Barcode);
        Assert.Equal(100m, reloaded.SalePriceAmount);
        Assert.Equal("MXN", reloaded.SalePriceCurrency);
        Assert.Equal(50m, reloaded.CostAmount);
        Assert.Equal("MXN", reloaded.CostCurrency);
    }

    [Fact]
    public async Task DeletingOrganizationWithProductShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Name = "Producto de prueba",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var organizationToDelete = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organizationId);
        context.Remove(organizationToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavingProductWithDuplicateSkuInSameOrganizationShouldFailDueToUniqueIndex()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Name = "Producto A",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Name = "Producto B",
            SalePriceAmount = 200m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavingProductsWithSameBarcodeInSameOrganizationShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Barcode = "1234567890",
            Name = "Producto A",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-002",
            Barcode = "1234567890",
            Name = "Producto B",
            SalePriceAmount = 200m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);

        var products = await context.Set<ProductRecord>()
            .Where(r => r.OrganizationId == organizationId && r.Barcode == "1234567890")
            .ToListAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task SavingProductsWithSameBarcodeInDifferentOrganizationsShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationAId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new OrganizationRecord { Id = organizationBId, Name = "Beta", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationAId,
            Sku = "PROD-001",
            Barcode = "1234567890",
            Name = "Producto A",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationBId,
            Sku = "PROD-001",
            Barcode = "1234567890",
            Name = "Producto B",
            SalePriceAmount = 200m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SavingSameSkuInDifferentOrganizationsShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationAId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new OrganizationRecord { Id = organizationBId, Name = "Beta", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationAId,
            Sku = "PROD-001",
            Name = "Producto A",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationBId,
            Sku = "PROD-001",
            Name = "Producto B",
            SalePriceAmount = 200m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SavingMultipleProductsWithNullBarcodeShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Barcode = null,
            Name = "Producto A",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new ProductRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Sku = "PROD-002",
            Barcode = null,
            Name = "Producto B",
            SalePriceAmount = 200m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SavingBranchesWithSameNameInSameOrganizationShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        context.Add(new BranchRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-2",
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);

        var branches = await context.Set<BranchRecord>()
            .Where(r => r.OrganizationId == organizationId && r.Name == "Sucursal Centro")
            .ToListAsync();

        Assert.Equal(2, branches.Count);
    }

    [Fact]
    public async Task SavingRegistersWithSameNameInSameBranchShouldSucceed()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = now });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RegisterRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        context.Add(new RegisterRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-2",
            IsActive = true,
            CreatedAtUtc = now,
        });

        var exception = await Record.ExceptionAsync(() => context.CommitAsync(CancellationToken.None));

        Assert.Null(exception);

        var registers = await context.Set<RegisterRecord>()
            .Where(r => r.BranchId == branchId && r.Name == "Caja 1")
            .ToListAsync();

        Assert.Equal(2, registers.Count);
    }

    [Fact]
    public async Task IntegratedStructureShouldRoundTripThroughSingleCommit()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId,
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });
        context.Add(new ProductRecord
        {
            Id = productId,
            OrganizationId = organizationId,
            Sku = "PROD-001",
            Barcode = "1234567890",
            Name = "Producto de prueba",
            Description = "Descripción",
            SalePriceAmount = 100m,
            SalePriceCurrency = "MXN",
            CostAmount = 60m,
            CostCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var organization = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organizationId);
        var branch = await context.Set<BranchRecord>().SingleAsync(r => r.Id == branchId);
        var register = await context.Set<RegisterRecord>().SingleAsync(r => r.Id == registerId);
        var product = await context.Set<ProductRecord>().SingleAsync(r => r.Id == productId);

        Assert.Equal(organizationId, organization.Id);
        Assert.Equal("Acme", organization.Name);
        Assert.True(organization.IsActive);
        Assert.Equal(createdAtUtc, organization.CreatedAtUtc);

        Assert.Equal(branchId, branch.Id);
        Assert.Equal(organizationId, branch.OrganizationId);
        Assert.Equal("Sucursal Centro", branch.Name);
        Assert.Equal("SUC-1", branch.Code);
        Assert.True(branch.IsActive);
        Assert.Equal(createdAtUtc, branch.CreatedAtUtc);

        Assert.Equal(registerId, register.Id);
        Assert.Equal(branchId, register.BranchId);
        Assert.Equal("Caja 1", register.Name);
        Assert.Equal("CAJA-1", register.Code);
        Assert.True(register.IsActive);
        Assert.Equal(createdAtUtc, register.CreatedAtUtc);

        Assert.Equal(productId, product.Id);
        Assert.Equal(organizationId, product.OrganizationId);
        Assert.Equal("PROD-001", product.Sku);
        Assert.Equal("1234567890", product.Barcode);
        Assert.Equal("Producto de prueba", product.Name);
        Assert.Equal(100m, product.SalePriceAmount);
        Assert.Equal("MXN", product.SalePriceCurrency);
        Assert.Equal(60m, product.CostAmount);
        Assert.Equal("MXN", product.CostCurrency);
        Assert.True(product.IsActive);
        Assert.Equal(createdAtUtc, product.CreatedAtUtc);
    }
}
