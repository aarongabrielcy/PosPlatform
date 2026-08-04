using Pos.Application.Common.Time;
using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;

namespace Pos.Application.AdministrativeNotifications;

// Nunca hace CommitAsync (TAREA 24E, sección 10/11): el servicio Product que crea el
// ProductAuditEvent es quien hace el único CommitAsync de la operación, después de invocar este
// writer.
public sealed class AdministrativeNotificationWriter : IAdministrativeNotificationWriter
{
    private readonly IAdministrativeNotificationAudienceQuery _audienceQuery;
    private readonly IAdministrativeNotificationRepository _notificationRepository;
    private readonly IClock _clock;

    public AdministrativeNotificationWriter(
        IAdministrativeNotificationAudienceQuery audienceQuery,
        IAdministrativeNotificationRepository notificationRepository,
        IClock clock)
    {
        _audienceQuery = audienceQuery ?? throw new ArgumentNullException(nameof(audienceQuery));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task TryAddForProductAuditAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (!ProductAuditNotificationPolicy.ShouldNotify(auditEvent))
        {
            return;
        }

        var recipientUserIds = await _audienceQuery.GetEligibleUserIdsAsync(
            auditEvent.OrganizationId, Permission.ViewProductAudit, cancellationToken);

        // Sin destinatarios elegibles, no se persiste una Notification vacía (TAREA 24E, sección 42).
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var notification = AdministrativeNotification.Create(
            AdministrativeNotificationId.New(),
            auditEvent.OrganizationId,
            auditEvent.Id,
            _clock.UtcNow,
            recipientUserIds);

        await _notificationRepository.AddAsync(notification, cancellationToken);
    }
}
