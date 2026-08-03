using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.ProductAudit;

// Registro append-only de auditoría de Producto (TAREA 24D): qué ocurrió, a qué producto, quién y
// cuándo. Sin constructor público: solo factories estáticos por Action, igual que
// InventoryMovement (Pos.Domain.Inventory), que es el modelo append-only ya existente en el
// proyecto. ActorUsernameSnapshot/ActorDisplayNameSnapshot y ProductSkuSnapshot/
// ProductNameSnapshot capturan cómo se identificaban el actor y el producto en el momento del
// evento: si luego cambian, el historial sigue siendo entendible.
public sealed class ProductAuditEvent
{
    private readonly List<ProductAuditChange> _changes;

    public ProductAuditEventId Id { get; }

    public OrganizationId OrganizationId { get; }

    public ProductId ProductId { get; }

    public UserId ActorUserId { get; }

    public string ActorUsernameSnapshot { get; }

    public string ActorDisplayNameSnapshot { get; }

    public string ProductSkuSnapshot { get; }

    public string ProductNameSnapshot { get; }

    public ProductAuditAction Action { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public IReadOnlyList<ProductAuditChange> Changes => _changes;

    private ProductAuditEvent(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        ProductAuditAction action,
        DateTimeOffset occurredAtUtc,
        IReadOnlyList<(ProductAuditField FieldName, string? OldValue, string? NewValue)> changes)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        ProductId = EnsureNotEmpty(productId);
        ActorUserId = EnsureNotEmpty(actorUserId);
        ActorUsernameSnapshot = EnsureNotEmpty(actorUsernameSnapshot, nameof(actorUsernameSnapshot));
        ActorDisplayNameSnapshot = EnsureNotEmpty(actorDisplayNameSnapshot, nameof(actorDisplayNameSnapshot));
        ProductSkuSnapshot = EnsureNotEmpty(productSkuSnapshot, nameof(productSkuSnapshot));
        ProductNameSnapshot = EnsureNotEmpty(productNameSnapshot, nameof(productNameSnapshot));
        Action = EnsureDefined(action);
        OccurredAtUtc = EnsureUtc(occurredAtUtc);

        ArgumentNullException.ThrowIfNull(changes);

        _changes = changes
            .Select(change => new ProductAuditChange(ProductAuditChangeId.New(), Id, change.FieldName, change.OldValue, change.NewValue))
            .ToList();

        EnsureActionRules(Action, _changes);
    }

    public static ProductAuditEvent CreateCreated(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        DateTimeOffset occurredAtUtc,
        IReadOnlyList<(ProductAuditField FieldName, string? OldValue, string? NewValue)> changes) =>
        new(
            id, organizationId, productId, actorUserId, actorUsernameSnapshot, actorDisplayNameSnapshot,
            productSkuSnapshot, productNameSnapshot, ProductAuditAction.Created, occurredAtUtc, changes);

    public static ProductAuditEvent CreateUpdated(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        DateTimeOffset occurredAtUtc,
        IReadOnlyList<(ProductAuditField FieldName, string? OldValue, string? NewValue)> changes) =>
        new(
            id, organizationId, productId, actorUserId, actorUsernameSnapshot, actorDisplayNameSnapshot,
            productSkuSnapshot, productNameSnapshot, ProductAuditAction.Updated, occurredAtUtc, changes);

    public static ProductAuditEvent CreateActivated(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        DateTimeOffset occurredAtUtc) =>
        new(
            id, organizationId, productId, actorUserId, actorUsernameSnapshot, actorDisplayNameSnapshot,
            productSkuSnapshot, productNameSnapshot, ProductAuditAction.Activated, occurredAtUtc,
            [(ProductAuditField.IsActive, "false", "true")]);

    public static ProductAuditEvent CreateDeactivated(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        DateTimeOffset occurredAtUtc) =>
        new(
            id, organizationId, productId, actorUserId, actorUsernameSnapshot, actorDisplayNameSnapshot,
            productSkuSnapshot, productNameSnapshot, ProductAuditAction.Deactivated, occurredAtUtc,
            [(ProductAuditField.IsActive, "true", "false")]);

    public static ProductAuditEvent CreateInventoryAdjusted(
        ProductAuditEventId id,
        OrganizationId organizationId,
        ProductId productId,
        UserId actorUserId,
        string actorUsernameSnapshot,
        string actorDisplayNameSnapshot,
        string productSkuSnapshot,
        string productNameSnapshot,
        DateTimeOffset occurredAtUtc,
        string quantityBefore,
        string quantityAfter) =>
        new(
            id, organizationId, productId, actorUserId, actorUsernameSnapshot, actorDisplayNameSnapshot,
            productSkuSnapshot, productNameSnapshot, ProductAuditAction.InventoryAdjusted, occurredAtUtc,
            [(ProductAuditField.InventoryQuantity, quantityBefore, quantityAfter)]);

    private static void EnsureActionRules(ProductAuditAction action, List<ProductAuditChange> changes)
    {
        switch (action)
        {
            case ProductAuditAction.Created:
                if (changes.Count == 0)
                {
                    throw new DomainValidationException("Un evento Created debe registrar al menos un cambio inicial.");
                }

                break;
            case ProductAuditAction.Updated:
                if (changes.Count == 0)
                {
                    throw new DomainValidationException("Un evento Updated debe registrar al menos un cambio real.");
                }

                break;
            case ProductAuditAction.Activated:
                EnsureSingleFieldTransition(changes, ProductAuditField.IsActive, "false", "true");
                break;
            case ProductAuditAction.Deactivated:
                EnsureSingleFieldTransition(changes, ProductAuditField.IsActive, "true", "false");
                break;
            case ProductAuditAction.InventoryAdjusted:
                EnsureSingleFieldChange(changes, ProductAuditField.InventoryQuantity);
                break;
            default:
                throw new DomainValidationException("Action no es un valor válido de ProductAuditAction.");
        }
    }

    private static void EnsureSingleFieldTransition(
        List<ProductAuditChange> changes, ProductAuditField expectedField, string expectedOldValue, string expectedNewValue)
    {
        EnsureSingleFieldChange(changes, expectedField);

        if (changes[0].OldValue != expectedOldValue || changes[0].NewValue != expectedNewValue)
        {
            throw new DomainValidationException(
                $"El cambio de '{expectedField}' debe ir de '{expectedOldValue}' a '{expectedNewValue}'.");
        }
    }

    private static void EnsureSingleFieldChange(List<ProductAuditChange> changes, ProductAuditField expectedField)
    {
        if (changes.Count != 1 || changes[0].FieldName != expectedField)
        {
            throw new DomainValidationException(
                $"Un evento de este tipo debe registrar exactamente un cambio en '{expectedField}'.");
        }
    }

    private static ProductAuditEventId EnsureNotEmpty(ProductAuditEventId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static OrganizationId EnsureNotEmpty(OrganizationId organizationId)
    {
        if (organizationId.Value == Guid.Empty)
        {
            throw new DomainValidationException("OrganizationId no puede ser vacío.");
        }

        return organizationId;
    }

    private static ProductId EnsureNotEmpty(ProductId productId)
    {
        if (productId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductId no puede ser vacío.");
        }

        return productId;
    }

    private static UserId EnsureNotEmpty(UserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ActorUserId no puede ser vacío.");
        }

        return userId;
    }

    private static string EnsureNotEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{parameterName} es obligatorio.");
        }

        return value;
    }

    private static ProductAuditAction EnsureDefined(ProductAuditAction action)
    {
        if (!Enum.IsDefined(action))
        {
            throw new DomainValidationException("Action no es un valor válido de ProductAuditAction.");
        }

        return action;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("OccurredAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
