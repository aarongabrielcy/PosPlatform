using Pos.Application.SalesCart;

namespace Pos.Application.Tests.SalesCart;

internal sealed class FakeCurrentSalesCart : ICurrentSalesCart
{
    private SalesCartSnapshot _snapshot = SalesCartSnapshot.Empty("MXN");

    public int SetCallCount { get; private set; }

    public int ClearCallCount { get; private set; }

    public SalesCartSnapshot Snapshot => _snapshot;

    public void SetSnapshot(SalesCartSnapshot snapshot)
    {
        SetCallCount++;
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public void Clear()
    {
        ClearCallCount++;
        _snapshot = SalesCartSnapshot.Empty("MXN");
    }
}
