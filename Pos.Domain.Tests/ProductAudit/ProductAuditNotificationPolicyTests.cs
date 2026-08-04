using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Domain.Tests.ProductAudit;

// TAREA 24E, sección 41: matriz de política "¿este AuditEvent amerita notificación?".
public class ProductAuditNotificationPolicyTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static ProductAuditEvent CreateUpdatedEvent(
        params (ProductAuditField FieldName, string? OldValue, string? NewValue)[] changes) =>
        ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc, changes);

    [Fact]
    public void CreatedNeverNotifies()
    {
        var auditEvent = ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc,
            [(ProductAuditField.Sku, null, "SKU-001"), (ProductAuditField.SalePrice, null, "MXN 10.00")]);

        Assert.False(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Theory]
    [InlineData(ProductAuditField.Name)]
    [InlineData(ProductAuditField.Description)]
    [InlineData(ProductAuditField.Barcode)]
    [InlineData(ProductAuditField.ReorderPoint)]
    public void UpdatedWithOnlyNonSensitiveFieldsDoesNotNotify(ProductAuditField field)
    {
        var auditEvent = CreateUpdatedEvent((field, "antes", "después"));

        Assert.False(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Theory]
    [InlineData(ProductAuditField.Sku)]
    [InlineData(ProductAuditField.SalePrice)]
    [InlineData(ProductAuditField.Cost)]
    public void UpdatedWithASensitiveFieldNotifies(ProductAuditField field)
    {
        var auditEvent = CreateUpdatedEvent((field, "antes", "después"));

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Fact]
    public void UpdatedWithNameAndSalePriceNotifiesBecauseOfSalePrice()
    {
        var auditEvent = CreateUpdatedEvent(
            (ProductAuditField.Name, "Agua", "Agua Natural"),
            (ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"));

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Fact]
    public void UpdatedWithSalePriceCostAndSkuStillNotifiesOnce()
    {
        var auditEvent = CreateUpdatedEvent(
            (ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"),
            (ProductAuditField.Cost, "MXN 10.00", "MXN 12.00"),
            (ProductAuditField.Sku, "SKU-001", "SKU-002"));

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Fact]
    public void ActivatedNotifies()
    {
        var auditEvent = ProductAuditEvent.CreateActivated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc);

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Fact]
    public void DeactivatedNotifies()
    {
        var auditEvent = ProductAuditEvent.CreateDeactivated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc);

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }

    [Fact]
    public void InventoryAdjustedNotifies()
    {
        var auditEvent = ProductAuditEvent.CreateInventoryAdjusted(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc, "10", "7");

        Assert.True(ProductAuditNotificationPolicy.ShouldNotify(auditEvent));
    }
}
