using Pos.Application.SalesCart;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeCurrentSalesCart : ICurrentSalesCart
{
    private SalesCartSnapshot _snapshot = SalesCartSnapshot.Empty("MXN");

    public int ClearCallCount { get; private set; }

    public SalesCartSnapshot Snapshot => _snapshot;

    public void SetSnapshot(SalesCartSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public void Clear()
    {
        ClearCallCount++;
        _snapshot = SalesCartSnapshot.Empty("MXN");
    }
}
