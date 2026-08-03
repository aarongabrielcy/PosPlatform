using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class ProductAuditEventMapperTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

    private static ProductAuditEvent CreateUpdatedEvent() =>
        ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(),
            OrganizationId.New(),
            ProductId.New(),
            UserId.New(),
            "JPEREZ",
            "Juan Pérez",
            "SKU-001",
            "Agua Natural",
            OccurredAtUtc,
            [
                (ProductAuditField.Name, "Agua", "Agua Natural"),
                (ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"),
            ]);

    [Fact]
    public void ToRecordPreservesEventLevelFields()
    {
        var auditEvent = CreateUpdatedEvent();

        var record = ProductAuditEventMapper.ToRecord(auditEvent);

        Assert.Equal(auditEvent.Id.Value, record.Id);
        Assert.Equal(auditEvent.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(auditEvent.ProductId.Value, record.ProductId);
        Assert.Equal(auditEvent.ActorUserId.Value, record.ActorUserId);
        Assert.Equal(auditEvent.ActorUsernameSnapshot, record.ActorUsernameSnapshot);
        Assert.Equal(auditEvent.ActorDisplayNameSnapshot, record.ActorDisplayNameSnapshot);
        Assert.Equal(auditEvent.ProductSkuSnapshot, record.ProductSkuSnapshot);
        Assert.Equal(auditEvent.ProductNameSnapshot, record.ProductNameSnapshot);
        Assert.Equal(auditEvent.Action, record.Action);
        Assert.Equal(auditEvent.OccurredAtUtc, record.OccurredAtUtc);
    }

    [Fact]
    public void ToRecordFlattensChangesWithTheEventIdAsForeignKey()
    {
        var auditEvent = CreateUpdatedEvent();

        var record = ProductAuditEventMapper.ToRecord(auditEvent);

        Assert.Equal(2, record.Changes.Count);
        Assert.All(record.Changes, change => Assert.Equal(record.Id, change.ProductAuditEventId));

        var nameChange = Assert.Single(record.Changes, c => c.FieldName == ProductAuditField.Name);
        Assert.Equal("Agua", nameChange.OldValue);
        Assert.Equal("Agua Natural", nameChange.NewValue);
    }

    [Fact]
    public void ToRecordRejectsNullEvent()
    {
        Assert.Throws<ArgumentNullException>(() => ProductAuditEventMapper.ToRecord(null!));
    }
}
