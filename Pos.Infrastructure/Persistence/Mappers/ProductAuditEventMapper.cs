using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

// Solo ToRecord: append-only, nunca se rehidrata como ProductAuditEvent de Domain (nada en el
// proyecto necesita releer un evento de auditoría como entidad mutable). Las consultas de lectura
// (IProductAuditQuery) proyectan directamente desde los Records a DTOs de Application, igual que
// EfProductCatalogQuery nunca materializa un Product de Domain.
internal static class ProductAuditEventMapper
{
    internal static ProductAuditEventRecord ToRecord(ProductAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var record = new ProductAuditEventRecord
        {
            Id = auditEvent.Id.Value,
            OrganizationId = auditEvent.OrganizationId.Value,
            ProductId = auditEvent.ProductId.Value,
            ActorUserId = auditEvent.ActorUserId.Value,
            ActorUsernameSnapshot = auditEvent.ActorUsernameSnapshot,
            ActorDisplayNameSnapshot = auditEvent.ActorDisplayNameSnapshot,
            ProductSkuSnapshot = auditEvent.ProductSkuSnapshot,
            ProductNameSnapshot = auditEvent.ProductNameSnapshot,
            Action = auditEvent.Action,
            OccurredAtUtc = auditEvent.OccurredAtUtc,
        };

        record.Changes = auditEvent.Changes
            .Select(change => new ProductAuditChangeRecord
            {
                Id = change.Id.Value,
                ProductAuditEventId = record.Id,
                FieldName = change.FieldName,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
            })
            .ToList();

        return record;
    }
}
