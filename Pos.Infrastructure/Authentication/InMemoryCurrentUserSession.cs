using Pos.Application.Authentication;

namespace Pos.Infrastructure.Authentication;

// Registrada como singleton único (bajo ICurrentUserSession e ICurrentUserSessionWriter): la
// sesión vive únicamente en memoria de proceso, nunca se persiste en disco y se limpia al cerrar
// la aplicación o al cerrar sesión.
public sealed class InMemoryCurrentUserSession : ICurrentUserSession, ICurrentUserSessionWriter
{
    private readonly object _gate = new();
    private AuthenticatedUser? _currentUser;

    public bool IsAuthenticated
    {
        get
        {
            lock (_gate)
            {
                return _currentUser is not null;
            }
        }
    }

    public AuthenticatedUser? CurrentUser
    {
        get
        {
            lock (_gate)
            {
                return _currentUser;
            }
        }
    }

    public event EventHandler? SessionChanged;

    public void SetAuthenticatedUser(AuthenticatedUser authenticatedUser)
    {
        ArgumentNullException.ThrowIfNull(authenticatedUser);

        lock (_gate)
        {
            _currentUser = authenticatedUser;
        }

        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        bool changed;

        lock (_gate)
        {
            changed = _currentUser is not null;
            _currentUser = null;
        }

        if (changed)
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
