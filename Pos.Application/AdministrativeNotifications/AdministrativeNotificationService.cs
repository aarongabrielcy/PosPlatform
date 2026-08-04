using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.AdministrativeNotifications;

// Único punto de entrada Desktop (TAREA 24E, sección 21): siempre deriva OrganizationId/UserId de
// ICurrentUserSession, nunca de la UI. MarkReadAsync hace su propio CommitAsync porque es una
// operación independiente del flujo de creación de la Notification (que ya viaja dentro del
// commit de Product/Audit).
public sealed class AdministrativeNotificationService : IAdministrativeNotificationService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly IAdministrativeNotificationQuery _notificationQuery;
    private readonly IAdministrativeNotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AdministrativeNotificationService(
        ICurrentUserSession currentUserSession,
        IAdministrativeNotificationQuery notificationQuery,
        IAdministrativeNotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _notificationQuery = notificationQuery ?? throw new ArgumentNullException(nameof(notificationQuery));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<AdministrativeNotificationPageResult> GetNotificationsAsync(
        int skip, int take, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewProductAudit))
        {
            return AdministrativeNotificationPageResult.Empty;
        }

        return await _notificationQuery.GetForUserAsync(user.OrganizationId, user.UserId, skip, take, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewProductAudit))
        {
            return 0;
        }

        return await _notificationQuery.GetUnreadCountAsync(user.OrganizationId, user.UserId, cancellationToken);
    }

    public async Task<bool> MarkReadAsync(AdministrativeNotificationId notificationId, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewProductAudit))
        {
            return false;
        }

        // Solo puede modificar el receipt del usuario actual: UserId nunca llega desde la UI
        // (TAREA 24E, sección 21/22).
        await _notificationRepository.MarkReadAsync(notificationId, user.UserId, _clock.UtcNow, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
