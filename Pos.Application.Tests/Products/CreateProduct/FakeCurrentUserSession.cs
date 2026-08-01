using Pos.Application.Authentication;

namespace Pos.Application.Tests.Products.CreateProduct;

internal sealed class FakeCurrentUserSession : ICurrentUserSession
{
    public AuthenticatedUser? CurrentUser { get; set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public event EventHandler? SessionChanged;

    public void Clear()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
