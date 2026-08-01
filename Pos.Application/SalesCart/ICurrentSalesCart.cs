namespace Pos.Application.SalesCart;

public interface ICurrentSalesCart
{
    SalesCartSnapshot Snapshot { get; }

    void SetSnapshot(SalesCartSnapshot snapshot);

    void Clear();
}
