using Pos.Application.AdministrativeNotifications;
using Pos.Application.Tests.Common.Time;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Application.Tests.AdministrativeNotifications;

// TAREA 24E, sección 10/11/42: el writer evalúa política, resuelve destinatarios y prepara la
// Notification, pero nunca hace su propio CommitAsync.
public class AdministrativeNotificationWriterTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UtcNow = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static (
        AdministrativeNotificationWriter Writer,
        FakeAdministrativeNotificationAudienceQuery AudienceQuery,
        FakeAdministrativeNotificationRepository Repository) CreateWriter(IReadOnlyList<UserId>? eligibleUserIds = null)
    {
        var audienceQuery = new FakeAdministrativeNotificationAudienceQuery(eligibleUserIds ?? [UserId.New()]);
        var repository = new FakeAdministrativeNotificationRepository();
        var writer = new AdministrativeNotificationWriter(audienceQuery, repository, new FakeClock(UtcNow));

        return (writer, audienceQuery, repository);
    }

    private static ProductAuditEvent CreateUpdatedEvent(
        params (ProductAuditField FieldName, string? OldValue, string? NewValue)[] changes) =>
        ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc, changes);

    [Fact]
    public async Task TryAddDoesNotPersistWhenThePolicySaysNo()
    {
        var (writer, audienceQuery, repository) = CreateWriter();
        var auditEvent = CreateUpdatedEvent((ProductAuditField.Name, "Agua", "Agua Natural"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(0, audienceQuery.CallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task TryAddPersistsWhenThePolicySaysYes()
    {
        var (writer, audienceQuery, repository) = CreateWriter();
        var auditEvent = CreateUpdatedEvent((ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(1, audienceQuery.CallCount);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task TryAddCreatesExactlyOneNotificationEvenWithMultipleSensitiveChanges()
    {
        var (writer, _, repository) = CreateWriter();
        var auditEvent = CreateUpdatedEvent(
            (ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"),
            (ProductAuditField.Cost, "MXN 10.00", "MXN 12.00"),
            (ProductAuditField.Sku, "SKU-001", "SKU-002"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(1, repository.AddCallCount);
        Assert.Single(repository.Added);
    }

    [Fact]
    public async Task TryAddPointsTheNotificationAtTheAuditEvent()
    {
        var (writer, _, repository) = CreateWriter();
        var auditEvent = CreateUpdatedEvent((ProductAuditField.Sku, "SKU-001", "SKU-002"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        var notification = Assert.Single(repository.Added);
        Assert.Equal(auditEvent.Id, notification.ProductAuditEventId);
        Assert.Equal(auditEvent.OrganizationId, notification.OrganizationId);
        Assert.Equal(UtcNow, notification.CreatedAtUtc);
    }

    [Fact]
    public async Task TryAddQueriesTheAudienceForViewProductAuditInTheAuditEventOrganization()
    {
        var (writer, audienceQuery, _) = CreateWriter();
        var auditEvent = CreateUpdatedEvent((ProductAuditField.Cost, "MXN 10.00", "MXN 12.00"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(auditEvent.OrganizationId, audienceQuery.LastOrganizationId);
        Assert.Equal(Pos.Domain.Security.Permission.ViewProductAudit, audienceQuery.LastPermission);
    }

    [Fact]
    public async Task TryAddDoesNotPersistWhenThereAreNoEligibleRecipients()
    {
        var (writer, _, repository) = CreateWriter(eligibleUserIds: []);
        var auditEvent = CreateUpdatedEvent((ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task TryAddCreatesOneRecipientPerEligibleUser()
    {
        var userA = UserId.New();
        var userB = UserId.New();
        var (writer, _, repository) = CreateWriter([userA, userB]);
        var auditEvent = CreateUpdatedEvent((ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50"));

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        var notification = Assert.Single(repository.Added);
        Assert.Equal(2, notification.Recipients.Count);
    }

    [Fact]
    public async Task TryAddIncludesTheActorAsARecipientWhenTheActorIsEligible()
    {
        var actorUserId = UserId.New();
        var (writer, _, repository) = CreateWriter([actorUserId]);
        var auditEvent = ProductAuditEvent.CreateUpdated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), actorUserId,
            "ADMIN", "Administrador", "SKU-001", "Agua 1L", OccurredAtUtc,
            [(ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50")]);

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        var notification = Assert.Single(repository.Added);
        Assert.Contains(notification.Recipients, r => r.UserId == actorUserId);
    }

    [Fact]
    public async Task TryAddPersistsForActivated()
    {
        var (writer, _, repository) = CreateWriter();
        var auditEvent = ProductAuditEvent.CreateActivated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc);

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task TryAddPersistsForInventoryAdjusted()
    {
        var (writer, _, repository) = CreateWriter();
        var auditEvent = ProductAuditEvent.CreateInventoryAdjusted(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua", OccurredAtUtc, "10", "7");

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task TryAddDoesNotPersistForCreated()
    {
        var (writer, audienceQuery, repository) = CreateWriter();
        var auditEvent = ProductAuditEvent.CreateCreated(
            ProductAuditEventId.New(), OrganizationId.New(), ProductId.New(), UserId.New(),
            "JPEREZ", "Juan Pérez", "SKU-001", "Agua 1L", OccurredAtUtc,
            [(ProductAuditField.Sku, null, "SKU-001"), (ProductAuditField.SalePrice, null, "MXN 10.00")]);

        await writer.TryAddForProductAuditAsync(auditEvent, CancellationToken.None);

        Assert.Equal(0, audienceQuery.CallCount);
        Assert.Equal(0, repository.AddCallCount);
    }
}
