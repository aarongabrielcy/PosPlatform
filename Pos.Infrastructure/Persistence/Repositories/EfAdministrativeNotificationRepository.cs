using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfAdministrativeNotificationRepository : IAdministrativeNotificationRepository
{
    private readonly PosDbContext _context;

    public EfAdministrativeNotificationRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(AdministrativeNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var record = AdministrativeNotificationMapper.ToRecord(notification);

        await _context.AdministrativeNotifications.AddAsync(record, cancellationToken);
    }

    public async Task MarkReadAsync(
        AdministrativeNotificationId notificationId,
        UserId userId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        var recipient = await _context.AdministrativeNotificationRecipients
            .FirstOrDefaultAsync(
                r => r.NotificationId == notificationId.Value && r.UserId == userId.Value, cancellationToken);

        // Sin receipt para este (NotificationId, UserId) no hay nada que marcar: no-op defensivo
        // (nunca debería ocurrir porque el NotificationId siempre viene de una consulta ya scoped
        // al usuario actual).
        if (recipient is null)
        {
            return;
        }

        // Idempotente: un receipt ya leído no cambia su ReadAtUtc (TAREA 24E, sección 22).
        recipient.ReadAtUtc ??= readAtUtc;
    }
}
