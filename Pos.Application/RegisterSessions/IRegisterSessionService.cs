namespace Pos.Application.RegisterSessions;

public interface IRegisterSessionService
{
    Task<RegisterSessionStatusResult> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableRegister>> GetAvailableRegistersAsync(CancellationToken cancellationToken = default);

    Task<RegisterSessionResult> OpenAsync(
        OpenRegisterSessionRequest request, CancellationToken cancellationToken = default);

    Task<RegisterSessionResult> CloseAsync(
        CloseRegisterSessionRequest request, CancellationToken cancellationToken = default);
}
