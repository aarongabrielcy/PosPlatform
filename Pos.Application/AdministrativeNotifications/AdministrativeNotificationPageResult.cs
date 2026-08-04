namespace Pos.Application.AdministrativeNotifications;

// HasNextPage se resuelve con "take + 1" filas, igual patrón que ProductAuditPageResult (TAREA
// 24E, sección 20 / TAREA 24D, sección 20): evita un COUNT(*) adicional.
public sealed record AdministrativeNotificationPageResult(
    IReadOnlyList<AdministrativeNotificationItem> Items, bool HasNextPage)
{
    public static AdministrativeNotificationPageResult Empty { get; } = new(Array.Empty<AdministrativeNotificationItem>(), false);
}
