using Pos.Application.RegisterSessions;

namespace Pos.Desktop.Tests.RegisterSessions;

internal sealed class FakeRegisterSessionService : IRegisterSessionService
{
    private readonly Func<CancellationToken, Task<RegisterSessionStatusResult>>? _getCurrentHandler;
    private readonly Func<CancellationToken, Task<IReadOnlyList<AvailableRegister>>>? _getAvailableRegistersHandler;
    private readonly Func<OpenRegisterSessionRequest, CancellationToken, Task<RegisterSessionResult>>? _openHandler;
    private readonly Func<CloseRegisterSessionRequest, CancellationToken, Task<RegisterSessionResult>>? _closeHandler;
    private readonly Func<CancellationToken, Task<RegisterClosingSummaryResult>>? _getClosingSummaryHandler;

    public FakeRegisterSessionService(
        Func<CancellationToken, Task<RegisterSessionStatusResult>>? getCurrentHandler = null,
        Func<CancellationToken, Task<IReadOnlyList<AvailableRegister>>>? getAvailableRegistersHandler = null,
        Func<OpenRegisterSessionRequest, CancellationToken, Task<RegisterSessionResult>>? openHandler = null,
        Func<CloseRegisterSessionRequest, CancellationToken, Task<RegisterSessionResult>>? closeHandler = null,
        Func<CancellationToken, Task<RegisterClosingSummaryResult>>? getClosingSummaryHandler = null)
    {
        _getCurrentHandler = getCurrentHandler;
        _getAvailableRegistersHandler = getAvailableRegistersHandler;
        _openHandler = openHandler;
        _closeHandler = closeHandler;
        _getClosingSummaryHandler = getClosingSummaryHandler;
    }

    public int GetCurrentCallCount { get; private set; }

    public int GetAvailableRegistersCallCount { get; private set; }

    public int OpenCallCount { get; private set; }

    public int CloseCallCount { get; private set; }

    public int GetClosingSummaryCallCount { get; private set; }

    public OpenRegisterSessionRequest? LastOpenRequest { get; private set; }

    public CloseRegisterSessionRequest? LastCloseRequest { get; private set; }

    public Task<RegisterSessionStatusResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        GetCurrentCallCount++;

        return _getCurrentHandler is null
            ? Task.FromResult(RegisterSessionStatusResult.NoneOpen())
            : _getCurrentHandler(cancellationToken);
    }

    public Task<IReadOnlyList<AvailableRegister>> GetAvailableRegistersAsync(CancellationToken cancellationToken = default)
    {
        GetAvailableRegistersCallCount++;

        return _getAvailableRegistersHandler is null
            ? Task.FromResult<IReadOnlyList<AvailableRegister>>(Array.Empty<AvailableRegister>())
            : _getAvailableRegistersHandler(cancellationToken);
    }

    public Task<RegisterSessionResult> OpenAsync(
        OpenRegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        OpenCallCount++;
        LastOpenRequest = request;

        return _openHandler is null
            ? Task.FromResult(RegisterSessionResult.Failure(RegisterSessionResultStatus.RegisterNotFound))
            : _openHandler(request, cancellationToken);
    }

    public Task<RegisterSessionResult> CloseAsync(
        CloseRegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        CloseCallCount++;
        LastCloseRequest = request;

        return _closeHandler is null
            ? Task.FromResult(RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionNotFound))
            : _closeHandler(request, cancellationToken);
    }

    public Task<RegisterClosingSummaryResult> GetClosingSummaryAsync(CancellationToken cancellationToken = default)
    {
        GetClosingSummaryCallCount++;

        return _getClosingSummaryHandler is null
            ? Task.FromResult(RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.SessionNotFound))
            : _getClosingSummaryHandler(cancellationToken);
    }
}
