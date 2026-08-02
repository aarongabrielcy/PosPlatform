using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Pos.Application.SalesCart;
using Pos.Desktop.Common;
using Pos.Desktop.Sales;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Sales;

public class SalesViewTests
{
    private static readonly string[] SearchResultsTextHeaders = { "SKU", "Producto" };

    private static readonly string[] CartGridCalculatedHeaders =
        { "Producto", "Precio", "Subtotal", "Impuesto", "Total" };

    // SalesView solo puede crearse en un hilo STA.
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
    public void NewProductButtonIsBoundToTheNewProductCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            var binding = BindingOperations.GetBinding(view.NewProductButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(SalesViewModel.NewProductCommand), binding.Path.Path);
        });

    [Fact]
    public void EditProductButtonIsBoundToTheEditProductCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            var binding = BindingOperations.GetBinding(view.EditProductButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(SalesViewModel.EditProductCommand), binding.Path.Path);
        });

    [Fact]
    public void IncludeInactiveCheckBoxIsBoundToTheIncludeInactivePropertyAndItsVisibilityToCanManageProducts() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            var checkedBinding = BindingOperations.GetBinding(view.IncludeInactiveCheckBox, ToggleButton.IsCheckedProperty);
            Assert.NotNull(checkedBinding);
            Assert.Equal(nameof(SalesViewModel.IncludeInactive), checkedBinding.Path.Path);

            var visibilityBinding = BindingOperations.GetBinding(view.IncludeInactiveCheckBox, System.Windows.UIElement.VisibilityProperty);
            Assert.NotNull(visibilityBinding);
            Assert.Equal(nameof(SalesViewModel.CanManageProducts), visibilityBinding.Path.Path);
        });

    [Fact]
    public void SearchResultsGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new SalesView().SearchResultsGrid.IsReadOnly));

    [Fact]
    public void CartGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new SalesView().CartGrid.IsReadOnly));

    [Fact]
    public void IsAvailableColumnBindingIsOneWay() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridCheckBoxColumn>(
                view.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Disponible"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.Equal(nameof(ProductSearchResult.IsAvailable), binding.Path.Path);
            Assert.Equal(BindingMode.OneWay, binding.Mode);
        });

    [Fact]
    public void AvailableQuantityColumnBindingIsNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridTextColumn>(
                view.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Existencia"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.Equal(nameof(ProductSearchResult.AvailableQuantity), binding.Path.Path);
            AssertBindingIsNotTwoWay(binding);
        });

    [Fact]
    public void SearchResultsGridPriceAndTextColumnsAreNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            foreach (var header in SearchResultsTextHeaders)
            {
                var column = Assert.IsType<DataGridTextColumn>(
                    view.SearchResultsGrid.Columns.Single(c => (string)c.Header == header));
                AssertBindingIsNotTwoWay(column.Binding);
            }

            var priceColumn = Assert.IsType<DataGridTextColumn>(
                view.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Precio"));
            AssertBindingIsNotTwoWay(priceColumn.Binding);
        });

    [Fact]
    public void CartGridCalculatedColumnsAreNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            foreach (var header in CartGridCalculatedHeaders)
            {
                var column = Assert.IsType<DataGridTextColumn>(
                    view.CartGrid.Columns.Single(c => (string)c.Header == header));
                AssertBindingIsNotTwoWay(column.Binding);
            }
        });

    [Fact]
    public void IsAvailableColumnBindingDoesNotThrowWhenAppliedToACheckBox() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridCheckBoxColumn>(
                view.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Disponible"));
            var binding = Assert.IsType<Binding>(column.Binding);

            var checkBox = new CheckBox { DataContext = CreateSearchResult(isAvailable: true) };

            var exception = Record.Exception(
                () => BindingOperations.SetBinding(checkBox, ToggleButton.IsCheckedProperty, binding));

            Assert.Null(exception);
            Assert.True(checkBox.IsChecked);
        });

    [Fact]
    public void CartQuantityTemplateKeepsIncreaseAndDecreaseButtonsBoundToCommands() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridTemplateColumn>(
                view.CartGrid.Columns.Single(c => (string)c.Header == "Cantidad"));

            var content = (StackPanel)column.CellTemplate.LoadContent();
            var buttons = content.Children.OfType<Button>().ToList();

            var decreaseButton = Assert.Single(buttons, b => (string)b.Content == "-");
            var increaseButton = Assert.Single(buttons, b => (string)b.Content == "+");

            AssertCommandBindingPath(decreaseButton, "DataContext." + nameof(SalesViewModel.DecreaseQuantityCommand));
            AssertCommandBindingPath(increaseButton, "DataContext." + nameof(SalesViewModel.IncreaseQuantityCommand));
        });

    [Fact]
    public void SearchStatusTextIsBoundToTheSearchStatusMessageProperty() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            var binding = BindingOperations.GetBinding(view.SearchStatusText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(SalesViewModel.SearchStatusMessage), binding.Path.Path);
        });

    [Fact]
    public void CartErrorTextIsBoundToGeneralErrorAndPlacedNearTheCartGrid() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();

            var binding = BindingOperations.GetBinding(view.CartErrorText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(SalesViewModel.GeneralError), binding.Path.Path);

            // CartGrid vive dentro de una card (Border); se compara contra la fila del Border,
            // no la del DataGrid, que ya no tiene Grid.Row propio.
            var cartGridContainerRow = Grid.GetRow((FrameworkElement)view.CartGrid.Parent);
            Assert.True(Grid.GetRow(view.CartErrorText) < cartGridContainerRow);
        });

    [Fact]
    public void AvailabilityStatusColumnUsesTheProductAvailabilityStatusConverter() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridTextColumn>(
                view.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Estado"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.IsType<ProductAvailabilityStatusConverter>(binding.Converter);
            AssertBindingIsNotTwoWay(binding);
        });

    [Fact]
    public void CartRemoveTemplateKeepsButtonBoundToRemoveLineCommand() =>
        RunOnStaThread(() =>
        {
            var view = new SalesView();
            var column = Assert.IsType<DataGridTemplateColumn>(
                view.CartGrid.Columns.Single(c => (string)c.Header == "Eliminar"));

            var button = Assert.IsType<Button>(column.CellTemplate.LoadContent());

            AssertCommandBindingPath(button, "DataContext." + nameof(SalesViewModel.RemoveLineCommand));
        });

    private static void AssertBindingIsNotTwoWay(BindingBase bindingBase)
    {
        switch (bindingBase)
        {
            case Binding binding:
                Assert.NotEqual(BindingMode.TwoWay, binding.Mode);
                Assert.NotEqual(BindingMode.OneWayToSource, binding.Mode);
                break;
            case MultiBinding multiBinding:
                Assert.NotEqual(BindingMode.TwoWay, multiBinding.Mode);
                Assert.NotEqual(BindingMode.OneWayToSource, multiBinding.Mode);

                foreach (var inner in multiBinding.Bindings)
                {
                    AssertBindingIsNotTwoWay(inner);
                }

                break;
            default:
                throw new InvalidOperationException($"Tipo de binding no soportado en la prueba: {bindingBase.GetType()}");
        }
    }

    private static void AssertCommandBindingPath(Button button, string expectedPath)
    {
        var binding = BindingOperations.GetBinding(button, Button.CommandProperty);

        Assert.NotNull(binding);
        Assert.Equal(expectedPath, binding.Path.Path);
    }

    private static ProductSearchResult CreateSearchResult(bool isAvailable) =>
        new(
            ProductId.New(),
            "SKU-1",
            "Producto de prueba",
            10m,
            "MXN",
            availableQuantity: isAvailable ? 5m : 0m,
            tracksInventory: true);
}
