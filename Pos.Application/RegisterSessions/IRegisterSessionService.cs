namespace Pos.Application.RegisterSessions;

public interface IRegisterSessionService
{
    Task<RegisterSessionStatusResult> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableRegister>> GetAvailableRegistersAsync(CancellationToken cancellationToken = default);

    Task<RegisterSessionResult> OpenAsync(
        OpenRegisterSessionRequest request, CancellationToken cancellationToken = default);

    Task<RegisterSessionResult> CloseAsync(
        CloseRegisterSessionRequest request, CancellationToken cancellationToken = default);

    // Vista previa de cierre (TAREA 25A-FIX sección 5): calcula ExpectedCash bajo demanda, sin
    // persistir nada, para que la UI pueda mostrarlo antes de que el cajero confirme el cierre.
    Task<RegisterClosingSummaryResult> GetClosingSummaryAsync(CancellationToken cancellationToken = default);
}
