using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Fila del reporte "Movimientos de caja" (sección 13 de la tarea): a diferencia de
// CashMovementEntry (Application.CashMovements, acotado a la sesión actual), esta proyección cubre
// un período completo a través de sesiones/cajas, e incluye RegisterName para distinguir el origen
// cuando hay varias cajas. Solo lectura: el reporte nunca permite editar/eliminar (sección 13).
public sealed class CashMovementReportEntry
{
    public Guid Id { get; }

    public RegisterSessionId RegisterSessionId { get; }

    public string RegisterName { get; }

    public CashMovementType Type { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public string Reason { get; }

    public UserId ActorUserId { get; }

    public string ActorDisplayName { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public CashMovementReportEntry(
        Guid id,
        RegisterSessionId registerSessionId,
        string registerName,
        CashMovementType type,
        decimal amount,
        string currency,
        string reason,
        UserId actorUserId,
        string actorDisplayName,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);

        Id = id;
        RegisterSessionId = registerSessionId;
        RegisterName = registerName;
        Type = type;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        CreatedAtUtc = createdAtUtc;
    }
}
