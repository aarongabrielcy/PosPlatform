namespace Pos.Application.CashMovements;

public interface ICashMovementService
{
    Task<CashMovementResult> RecordCashInAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default);

    Task<CashMovementResult> RecordCashOutAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default);

    // Movimientos de la sesión de caja actualmente abierta (ICurrentRegisterSession), más
    // recientes primero. Usado tanto por el resumen operativo de Caja (sección 21 de la tarea)
    // como para refrescar la lista tras registrar un movimiento nuevo.
    Task<CashMovementListResult> GetCurrentSessionMovementsAsync(CancellationToken cancellationToken = default);
}
