using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Pos.Desktop.Inventory;

namespace Pos.Desktop.Tests.Inventory;

public class InventoryViewTests
{
    // InventoryView solo puede crearse en un hilo STA.
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
    public void SearchTextBoxIsBoundToTheSearchTextProperty() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.SearchTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.SearchText), binding.Path.Path);
        });

    [Fact]
    public void SearchButtonIsBoundToTheSearchCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.SearchButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.SearchCommand), binding.Path.Path);
        });

    [Fact]
    public void StockFilterComboBoxIsBoundToTheSelectedStockFilterPropertyWithFourOptions() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.StockFilterComboBox, Selector.SelectedValueProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.SelectedStockFilter), binding.Path.Path);
            Assert.Equal(4, view.StockFilterComboBox.Items.Count);
        });

    [Fact]
    public void ClearFiltersButtonIsBoundToTheClearFiltersCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.ClearFiltersButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.ClearFiltersCommand), binding.Path.Path);
        });

    [Fact]
    public void InventoryGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new InventoryView().InventoryGrid.IsReadOnly));

    [Fact]
    public void MovementSearchTextBoxIsBoundToTheMovementSearchTextProperty() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.MovementSearchTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.MovementSearchText), binding.Path.Path);
        });

    [Fact]
    public void MovementTypeComboBoxIsBoundToTheSelectedMovementTypeProperty() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var binding = BindingOperations.GetBinding(view.MovementTypeComboBox, Selector.SelectedValueProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(InventoryViewModel.SelectedMovementType), binding.Path.Path);
        });

    [Fact]
    public void MovementsGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new InventoryView().MovementsGrid.IsReadOnly));

    // TAREA 24G-FIX, sección 1: el tab debe decir "Movimientos de stock", no "Movimientos" a
    // secas, para diferenciarlo de Auditoría (que también tiene su propio historial).
    [Fact]
    public void MovementsTabHeaderDistinguishesStockMovementsFromAudit() =>
        RunOnStaThread(() =>
        {
            var view = new InventoryView();

            var headers = view.InventoryTabControl.Items.Cast<TabItem>().Select(item => item.Header).ToList();

            Assert.Contains("Movimientos de stock", headers);
            Assert.DoesNotContain("Movimientos", headers);
        });
}
