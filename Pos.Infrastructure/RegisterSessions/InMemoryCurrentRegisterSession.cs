using Pos.Application.RegisterSessions;

namespace Pos.Infrastructure.RegisterSessions;

// Registrada como singleton: la sesión de caja vive únicamente en memoria de proceso, separada
// de ICurrentUserSession, y se reemplaza de forma atómica bajo lock. Nunca conserva un
// DbContext ni una entidad Domain, solo la proyección inmutable ActiveRegisterSession.
public sealed class InMemoryCurrentRegisterSession : ICurrentRegisterSession
{
    private readonly object _gate = new();
    private ActiveRegisterSession? _current;

    public ActiveRegisterSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    public void SetActiveSession(ActiveRegisterSession activeSession)
    {
        ArgumentNullException.ThrowIfNull(activeSession);

        lock (_gate)
        {
            _current = activeSession;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }
}
