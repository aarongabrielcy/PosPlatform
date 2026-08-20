using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Pos.Desktop.Products.Catalog;

namespace Pos.Desktop.Tests.Products.Catalog;

public class ProductsViewTests
{
    // ProductsView solo puede crearse en un hilo STA.
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
            var view = new ProductsView();

            var binding = BindingOperations.GetBinding(view.SearchTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductsViewModel.SearchText), binding.Path.Path);
        });

    [Fact]
    public void SearchButtonIsBoundToTheSearchCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new ProductsView();

            var binding = BindingOperations.GetBinding(view.SearchButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductsViewModel.SearchCommand), binding.Path.Path);
        });

    [Fact]
    public void NewProductButtonIsBoundToTheNewProductCommandProperty() =>
        RunOnStaThread(() =>
        {
            var view = new ProductsView();

            var binding = BindingOperations.GetBinding(view.NewProductButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductsViewModel.NewProductCommand), binding.Path.Path);
        });

    // READ-ONLY CORRECTION (sección 8/16 de la tarea): igual patrón que
    // InventoryView "Ajustar existencia" -> CanAdjustInventory, el botón "+ Nuevo producto" se
    // oculta para un usuario sin ManageProducts (p. ej. Cashier).
    [Fact]
    public void NewProductButtonVisibilityIsBoundToCanManageProducts() =>
        RunOnStaThread(() =>
        {
            var view = new ProductsView();

            var binding = BindingOperations.GetBinding(view.NewProductButton, UIElement.VisibilityProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductsViewModel.CanManageProducts), binding.Path.Path);
        });

    [Fact]
    public void PreviousAndNextPageButtonsAreBoundToTheirCommands() =>
        RunOnStaThread(() =>
        {
            var view = new ProductsView();

            var previousBinding = BindingOperations.GetBinding(view.PreviousPageButton, Button.CommandProperty);
            var nextBinding = BindingOperations.GetBinding(view.NextPageButton, Button.CommandProperty);

            Assert.NotNull(previousBinding);
            Assert.Equal(nameof(ProductsViewModel.PreviousPageCommand), previousBinding.Path.Path);
            Assert.NotNull(nextBinding);
            Assert.Equal(nameof(ProductsViewModel.NextPageCommand), nextBinding.Path.Path);
        });

    [Fact]
    public void FilterComboBoxIsBoundToTheSelectedFilterProperty() =>
        RunOnStaThread(() =>
        {
            var view = new ProductsView();

            var binding = BindingOperations.GetBinding(view.FilterComboBox, Selector.SelectedValueProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductsViewModel.SelectedFilter), binding.Path.Path);
            Assert.Equal(5, view.FilterComboBox.Items.Count);
        });

    [Fact]
    public void ProductsGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(new ProductsView().ProductsGrid.IsReadOnly));
}
