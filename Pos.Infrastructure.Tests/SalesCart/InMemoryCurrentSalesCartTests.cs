using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.SalesCart;

namespace Pos.Infrastructure.Tests.SalesCart;

public class InMemoryCurrentSalesCartTests
{
    [Fact]
    public void NewCartStartsEmpty()
    {
        var cart = new InMemoryCurrentSalesCart();

        Assert.NotNull(cart.Snapshot);
        Assert.Empty(cart.Snapshot.Lines);
        Assert.False(cart.Snapshot.HasItems);
    }

    [Fact]
    public void SetSnapshotReplacesTheCurrentSnapshot()
    {
        var cart = new InMemoryCurrentSalesCart();
        var snapshot = CreateSnapshotWithOneLine();

        cart.SetSnapshot(snapshot);

        Assert.Same(snapshot, cart.Snapshot);
    }

    [Fact]
    public void ClearRemovesAllLinesAndReturnsToEmpty()
    {
        var cart = new InMemoryCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshotWithOneLine());

        cart.Clear();

        Assert.Empty(cart.Snapshot.Lines);
        Assert.False(cart.Snapshot.HasItems);
    }

    [Fact]
    public void ClearOnAnAlreadyEmptyCartDoesNotThrow()
    {
        var cart = new InMemoryCurrentSalesCart();

        cart.Clear();
        cart.Clear();

        Assert.Empty(cart.Snapshot.Lines);
    }

    [Fact]
    public void SettingANewSnapshotReplacesThePreviousOneAtomically()
    {
        var cart = new InMemoryCurrentSalesCart();
        var first = CreateSnapshotWithOneLine();
        var second = SalesCartSnapshot.Empty("MXN");

        cart.SetSnapshot(first);
        cart.SetSnapshot(second);

        Assert.Same(second, cart.Snapshot);
    }

    [Fact]
    public void SetSnapshotWithNullThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new InMemoryCurrentSalesCart().SetSnapshot(null!));

    [Fact]
    public async Task ConcurrentReadsAndWritesDoNotThrowOrCorruptState()
    {
        var cart = new InMemoryCurrentSalesCart();
        var snapshots = Enumerable.Range(0, 20).Select(_ => CreateSnapshotWithOneLine()).ToList();

        var writers = snapshots.Select(snapshot => Task.Run(() => cart.SetSnapshot(snapshot)));
        var readers = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            _ = cart.Snapshot;
            return i;
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.Single(cart.Snapshot.Lines);
    }

    private static SalesCartSnapshot CreateSnapshotWithOneLine()
    {
        var line = new SalesCartLine(
            ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true);

        return new SalesCartSnapshot([line], "MXN");
    }
}
