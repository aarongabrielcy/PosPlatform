using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Pos.Desktop.Dashboard;

namespace Pos.Desktop.Tests.Dashboard;

public class DashboardViewTests
{
    // DashboardView solo puede crearse en un hilo STA.
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void LowStockCountTextIsBoundToTheLowStockCountProperty() =>
        RunOnStaThread(() =>
        {
            var view = new DashboardView();

            var binding = BindingOperations.GetBinding(view.LowStockCountText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(DashboardViewModel.LowStockCount), binding.Path.Path);
        });

    [Fact]
    public void OutOfStockCountTextIsBoundToTheOutOfStockCountProperty() =>
        RunOnStaThread(() =>
        {
            var view = new DashboardView();

            var binding = BindingOperations.GetBinding(view.OutOfStockCountText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(DashboardViewModel.OutOfStockCount), binding.Path.Path);
        });

    [Fact]
    public void RegisterStatusTextIsBoundToTheRegisterStatusTextProperty() =>
        RunOnStaThread(() =>
        {
            var view = new DashboardView();

            var binding = BindingOperations.GetBinding(view.RegisterStatusTextBlock, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(DashboardViewModel.RegisterStatusText), binding.Path.Path);
        });

    [Fact]
    public void CartTotalTextIsBoundToTheCartTotalTextProperty() =>
        RunOnStaThread(() =>
        {
            var view = new DashboardView();

            var binding = BindingOperations.GetBinding(view.CartTotalTextBlock, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(DashboardViewModel.CartTotalText), binding.Path.Path);
        });

    [Fact]
    public void LowStockGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new DashboardView().LowStockGrid.IsReadOnly));

    [Fact]
    public void LowStockStatusTextIsBoundToTheLowStockStatusMessageProperty() =>
        RunOnStaThread(() =>
        {
            var view = new DashboardView();

            var binding = BindingOperations.GetBinding(view.LowStockStatusText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(DashboardViewModel.LowStockStatusMessage), binding.Path.Path);
        });
}
