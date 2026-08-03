using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Domain.Tests.ProductAudit;

public class ProductAuditEventTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // ---------- CreateCreated ----------

    [Fact]
    public void CreateCreatedKeepsIdentifiersAndSnapshots()
    {
        var id = ProductAuditEventId.New();
        var organizationId = OrganizationId.New();
        var productId = ProductId.New();
        var actorUserId = UserId.New();

        var auditEvent = ProductAuditEvent.CreateCreated(
            id, organizationId, productId, actorUserId, "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L",
            OccurredAtUtc, [(ProductAuditField.Sku, null, "SKU-001")]);

        Assert.Equal(id, auditEvent.Id);
        Assert.Equal(organizationId, auditEvent.OrganizationId);
        Assert.Equal(productId, auditEvent.ProductId);
        Assert.Equal(actorUserId, auditEvent.ActorUserId);
        Assert.Equal("JPEREZ", auditEvent.ActorUsernameSnapshot);
        Assert.Equal("Juan Pérez", auditEvent.ActorDisplayNameSnapshot);
        Assert.Equal("SKU-001", auditEvent.ProductSkuSnapshot);
        Assert.Equal("Agua 1L", auditEvent.ProductNameSnapshot);
    }

    [Fact]
    public void CreateCreatedSetsActionCreated()
    {
        var auditEvent = CreateCreatedEvent([(ProductAuditField.Sku, null, "SKU-001")]);

        Assert.Equal(ProductAuditAction.Created, auditEvent.Action);
    }

    [Fact]
    public void CreateCreatedKeepsAllProvidedChanges()
    {
        var auditEvent = CreateCreatedEvent(
        [
            (ProductAuditField.Sku, null, "SKU-001"),
            (ProductAuditField.Name, null, "Agua 1L"),
            (ProductAuditField.SalePrice, null, "MXN 10.00"),
        ]);

        Assert.Equal(3, auditEvent.Changes.Count);
        Assert.Equal(ProductAuditField.Sku, auditEvent.Changes[0].FieldName);
        Assert.Null(auditEvent.Changes[0].OldValue);
        Assert.Equal("SKU-001", auditEvent.Changes[0].NewValue);
    }

    [Fact]
    public void CreateCreatedRejectsEmptyChanges()
    {
        Assert.Throws<DomainValidationException>(() => CreateCreatedEvent([]));
    }

    [Fact]
    public void CreateCreatedRejectsNonUtcOccurredAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", nonUtc, [(ProductAuditField.Sku, null, "SKU-001")]));
    }

    [Fact]
    public void CreateCreatedRejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), default, ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc, [(ProductAuditField.Sku, null, "SKU-001")]));
    }

    [Fact]
    public void CreateCreatedRejectsEmptyActorUsernameSnapshot()
    {
        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc, [(ProductAuditField.Sku, null, "SKU-001")]));
    }

    // ---------- CreateUpdated ----------

    [Fact]
    public void CreateUpdatedSetsActionUpdated()
    {
        var auditEvent = ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua Natural", OccurredAtUtc,
            [(ProductAuditField.Name, "Agua", "Agua Natural")]);

        Assert.Equal(ProductAuditAction.Updated, auditEvent.Action);
    }

    [Fact]
    public void CreateUpdatedAllowsMultipleChanges()
    {
        var auditEvent = ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua Natural", OccurredAtUtc,
            [
                (ProductAuditField.Name, "Agua", "Agua Natural"),
                (ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"),
            ]);

        Assert.Equal(2, auditEvent.Changes.Count);
    }

    [Fact]
    public void CreateUpdatedRejectsEmptyChanges()
    {
        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc, []));
    }

    [Fact]
    public void CreateUpdatedRejectsAChangeWithEqualOldAndNewValue()
    {
        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc,
            [(ProductAuditField.Name, "Agua", "Agua")]));
    }

    // FieldName es un enum controlado (ProductAuditChange), no un string arbitrario: un valor
    // fuera de rango debe rechazarse igual que cualquier otro Enum.IsDefined en el proyecto.
    [Fact]
    public void CreateUpdatedRejectsAnUndefinedFieldName()
    {
        Assert.Throws<DomainValidationException>(() => ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc,
            [((ProductAuditField)999, "Agua", "Agua Natural")]));
    }

    // ---------- CreateActivated / CreateDeactivated ----------

    [Fact]
    public void CreateActivatedProducesASingleIsActiveChangeFromFalseToTrue()
    {
        var auditEvent = ProductAuditEvent.CreateActivated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc);

        Assert.Equal(ProductAuditAction.Activated, auditEvent.Action);
        var change = Assert.Single(auditEvent.Changes);
        Assert.Equal(ProductAuditField.IsActive, change.FieldName);
        Assert.Equal("false", change.OldValue);
        Assert.Equal("true", change.NewValue);
    }

    [Fact]
    public void CreateDeactivatedProducesASingleIsActiveChangeFromTrueToFalse()
    {
        var auditEvent = ProductAuditEvent.CreateDeactivated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc);

        Assert.Equal(ProductAuditAction.Deactivated, auditEvent.Action);
        var change = Assert.Single(auditEvent.Changes);
        Assert.Equal(ProductAuditField.IsActive, change.FieldName);
        Assert.Equal("true", change.OldValue);
        Assert.Equal("false", change.NewValue);
    }

    // ---------- CreateInventoryAdjusted ----------

    [Fact]
    public void CreateInventoryAdjustedProducesASingleInventoryQuantityChange()
    {
        var auditEvent = ProductAuditEvent.CreateInventoryAdjusted(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc, "2", "0");

        Assert.Equal(ProductAuditAction.InventoryAdjusted, auditEvent.Action);
        var change = Assert.Single(auditEvent.Changes);
        Assert.Equal(ProductAuditField.InventoryQuantity, change.FieldName);
        Assert.Equal("2", change.OldValue);
        Assert.Equal("0", change.NewValue);
    }

    [Fact]
    public void CreateInventoryAdjustedAllowsGoingFromZeroToPositive()
    {
        var auditEvent = ProductAuditEvent.CreateInventoryAdjusted(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc, "0", "5");

        var change = Assert.Single(auditEvent.Changes);
        Assert.Equal("0", change.OldValue);
        Assert.Equal("5", change.NewValue);
    }

    private static ProductAuditEvent CreateCreatedEvent(
        IReadOnlyList<(ProductAuditField FieldName, string? OldValue, string? NewValue)> changes) =>
        ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc, changes);
}
