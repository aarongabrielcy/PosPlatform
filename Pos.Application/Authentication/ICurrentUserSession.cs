namespace Pos.Application.Authentication;

public interface ICurrentUserSession
{
    bool IsAuthenticated { get; }

    AuthenticatedUser? CurrentUser { get; }

    event EventHandler? SessionChanged;

    void Clear();
}
