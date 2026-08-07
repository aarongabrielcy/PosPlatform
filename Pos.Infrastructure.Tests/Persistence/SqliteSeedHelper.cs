using Pos.Domain.Inventory;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence;

// Helper interno compartido para sembrar grafos de catálogo coherentes en pruebas SQLite reales
// (Foreign Keys=True). Ahora que Sale/InventoryItem/SaleLine tienen FK físicas hacia sus
// catálogos, ya no es válido insertar registros con Guid.NewGuid() sin sembrar el principal.
internal static class SqliteSeedHelper
{
    public static readonly DateTimeOffset DefaultTimestamp = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain en pruebas de persistencia;
    // no es un hash PBKDF2 real y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    public sealed record CatalogGraph(
        Guid OrganizationId,
        Guid BranchId,
        Guid RegisterId,
        Guid RoleId,
        Guid UserId,
        Guid RegisterSessionId,
        Guid ProductId);

    // Siembra Organization -> Branch -> Register -> Role -> User -> RegisterSession -> Product,
    // todos consistentes entre sí (mismo OrganizationId, Register de la misma Branch, etc.),
    // dejando el grafo mínimo necesario para insertar un InventoryItem o un Sale válidos.
    public static async Task<CatalogGraph> SeedFullCatalogGraphAsync(
        PosDbContext context,
        DateTimeOffset? timestamp = null,
        Guid? organizationId = null,
        Guid? branchId = null,
        Guid? registerId = null,
        Guid? roleId = null,
        Guid? userId = null,
        Guid? registerSessionId = null,
        Guid? productId = null,
        bool includeProduct = true)
    {
        var now = timestamp ?? DefaultTimestamp;
        var organization = organizationId ?? Guid.NewGuid();
        var branch = branchId ?? Guid.NewGuid();
        var register = registerId ?? Guid.NewGuid();
        var role = roleId ?? Guid.NewGuid();
        var user = userId ?? Guid.NewGuid();
        var registerSession = registerSessionId ?? Guid.NewGuid();
        var product = productId ?? Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organization,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new BranchRecord
        {
            Id = branch,
            OrganizationId = organization,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RegisterRecord
        {
            Id = register,
            BranchId = branch,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RoleRecord
        {
            Id = role,
            OrganizationId = organization,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new UserRecord
        {
            Id = user,
            OrganizationId = organization,
            RoleId = role,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = registerSession,
            RegisterId = register,
            OpenedByUserId = user,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "MXN",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = now,
        });
        if (includeProduct)
        {
            context.Add(new ProductRecord
            {
                Id = product,
                OrganizationId = organization,
                Sku = "SKU-001",
                Name = "Producto de prueba",
                SalePriceAmount = 10m,
                SalePriceCurrency = "MXN",
                TracksInventory = true,
                IsActive = true,
                CreatedAtUtc = now,
            });
        }

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return new CatalogGraph(organization, branch, register, role, user, registerSession, product);
    }

    public sealed record ProductCatalog(Guid OrganizationId, Guid BranchId, Guid ProductId);

    // Siembra Organization -> Branch -> Product, consistentes entre sí (mismo OrganizationId),
    // suficiente para insertar un InventoryItemRecord válido sin sembrar el grafo completo de Sale.
    public static async Task<ProductCatalog> SeedOrganizationBranchAndProductAsync(
        PosDbContext context,
        DateTimeOffset? timestamp = null,
        Guid? organizationId = null,
        Guid? branchId = null,
        Guid? productId = null,
        bool includeProduct = true)
    {
        var now = timestamp ?? DefaultTimestamp;
        var organization = organizationId ?? Guid.NewGuid();
        var branch = branchId ?? Guid.NewGuid();
        var product = productId ?? Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organization,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = now,
        });
        context.Add(new BranchRecord
        {
            Id = branch,
            OrganizationId = organization,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = now,
        });

        if (includeProduct)
        {
            context.Add(new ProductRecord
            {
                Id = product,
                OrganizationId = organization,
                Sku = "SKU-001",
                Name = "Producto de prueba",
                SalePriceAmount = 10m,
                SalePriceCurrency = "MXN",
                TracksInventory = true,
                IsActive = true,
                CreatedAtUtc = now,
            });
        }

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return new ProductCatalog(organization, branch, product);
    }

    public static async Task<InventoryItemRecord> SeedInventoryItemAsync(
        PosDbContext context,
        Guid branchId,
        Guid productId,
        Guid? id = null,
        decimal quantity = 10m,
        decimal reorderPoint = 2m,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        var now = createdAtUtc ?? DefaultTimestamp;

        var record = new InventoryItemRecord
        {
            Id = id ?? Guid.NewGuid(),
            BranchId = branchId,
            ProductId = productId,
            Quantity = quantity,
            ReorderPoint = reorderPoint,
            CreatedAtUtc = now,
            UpdatedAtUtc = updatedAtUtc ?? now,
        };

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return record;
    }

    public static async Task<InventoryMovementRecord> SeedInventoryMovementAsync(
        PosDbContext context,
        Guid inventoryItemId,
        Guid branchId,
        Guid productId,
        Guid performedByUserId,
        InventoryMovementType type = InventoryMovementType.ManualIncrease,
        decimal quantity = 1m,
        decimal quantityBefore = 0m,
        decimal quantityAfter = 1m,
        Guid? saleId = null,
        Guid? saleLineId = null,
        Guid? id = null,
        DateTimeOffset? occurredAtUtc = null)
    {
        var record = new InventoryMovementRecord
        {
            Id = id ?? Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            PerformedByUserId = performedByUserId,
            Type = type,
            Quantity = quantity,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            SaleId = saleId,
            SaleLineId = saleLineId,
            OccurredAtUtc = occurredAtUtc ?? DefaultTimestamp,
        };

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return record;
    }

    // Siembra una Sale Draft con una SaleLine y un Payment, referenciando el catálogo indicado
    // (Organization/Branch/RegisterSession/User/Product ya deben existir).
    public static async Task<SaleRecord> SeedSaleAsync(
        PosDbContext context,
        Guid organizationId,
        Guid branchId,
        Guid registerSessionId,
        Guid createdByUserId,
        Guid productId,
        Guid? saleId = null,
        Guid? saleLineId = null,
        Guid? paymentId = null,
        decimal quantity = 2m,
        decimal unitPriceAmount = 10m,
        decimal paymentAmount = 20m,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? completedAtUtc = null,
        SaleStatus status = SaleStatus.Draft)
    {
        var now = createdAtUtc ?? DefaultTimestamp;
        var id = saleId ?? Guid.NewGuid();

        var record = new SaleRecord
        {
            Id = id,
            OrganizationId = organizationId,
            BranchId = branchId,
            RegisterSessionId = registerSessionId,
            CreatedByUserId = createdByUserId,
            Currency = "MXN",
            Status = status,
            CreatedAtUtc = now,
            CompletedAtUtc = completedAtUtc,
        };

        record.Lines.Add(new SaleLineRecord
        {
            Id = saleLineId ?? Guid.NewGuid(),
            SaleId = id,
            ProductId = productId,
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = quantity,
            UnitPriceAmount = unitPriceAmount,
            Currency = "MXN",
            Sale = record,
        });

        record.Payments.Add(new PaymentRecord
        {
            Id = paymentId ?? Guid.NewGuid(),
            SaleId = id,
            Method = PaymentMethod.Cash,
            Amount = paymentAmount,
            Currency = "MXN",
            PaidAtUtc = now,
            Sale = record,
        });

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return record;
    }
}
