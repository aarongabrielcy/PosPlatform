using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Pos.Desktop.Audit.Products;

namespace Pos.Desktop.Tests.Audit.Products;

// TAREA 24D-FIX: pruebas estructurales de layout (labels, bindings, columnas), no pixel-perfect.
public class ProductAuditViewTests
{
    private static readonly string[] ExpectedColumnHeaders =
        { "Fecha/Hora", "Usuario", "SKU", "Producto", "Acción", "Resumen" };

    // ProductAuditView solo puede crearse en un hilo STA.
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

    private static ProductAuditView CreateView()
    {
        var viewModel = new ProductAuditViewModel(new FakeProductAuditService());
        var view = new ProductAuditView { DataContext = viewModel };
        view.Measure(new Size(1100, 700));
        view.Arrange(new Rect(0, 0, 1100, 700));
        view.UpdateLayout();
        return view;
    }

    private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
    {
        yield return root;

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            foreach (var descendant in EnumerateVisualTree(VisualTreeHelper.GetChild(root, i)))
            {
                yield return descendant;
            }
        }
    }

    private static List<string> GetTextBlockTexts(DependencyObject root) =>
        EnumerateVisualTree(root)
            .OfType<TextBlock>()
            .Select(tb => tb.Text)
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

    [Fact]
    public void FilterLabelsAreVisibleInTheVisualTree() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();
            var texts = GetTextBlockTexts(view);

            Assert.Contains("Producto / SKU", texts);
            Assert.Contains("Usuario", texts);
            Assert.Contains("Acción", texts);
            Assert.Contains("Desde", texts);
            Assert.Contains("Hasta", texts);
        });

    [Fact]
    public void SearchTextBoxIsBoundToSearchText() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var binding = BindingOperations.GetBinding(view.SearchTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductAuditViewModel.SearchText), binding.Path.Path);
        });

    [Fact]
    public void ActorSearchTextBoxIsBoundToActorSearchText() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var binding = BindingOperations.GetBinding(view.ActorSearchTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductAuditViewModel.ActorSearchText), binding.Path.Path);
        });

    [Fact]
    public void ActionComboBoxIsBoundToSelectedAction() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var binding = BindingOperations.GetBinding(view.ActionComboBox, Selector.SelectedValueProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(ProductAuditViewModel.SelectedAction), binding.Path.Path);
        });

    [Fact]
    public void DatePickersAreBoundToFromDateAndToDate() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var fromBinding = BindingOperations.GetBinding(view.FromDatePicker, DatePicker.SelectedDateProperty);
            var toBinding = BindingOperations.GetBinding(view.ToDatePicker, DatePicker.SelectedDateProperty);

            Assert.NotNull(fromBinding);
            Assert.Equal(nameof(ProductAuditViewModel.FromDate), fromBinding.Path.Path);
            Assert.NotNull(toBinding);
            Assert.Equal(nameof(ProductAuditViewModel.ToDate), toBinding.Path.Path);
        });

    [Fact]
    public void SearchAndClearFiltersButtonsAreBoundToTheirCommands() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var searchBinding = BindingOperations.GetBinding(view.SearchButton, Button.CommandProperty);
            var clearBinding = BindingOperations.GetBinding(view.ClearFiltersButton, Button.CommandProperty);

            Assert.NotNull(searchBinding);
            Assert.Equal(nameof(ProductAuditViewModel.SearchCommand), searchBinding.Path.Path);
            Assert.NotNull(clearBinding);
            Assert.Equal(nameof(ProductAuditViewModel.ClearFiltersCommand), clearBinding.Path.Path);
        });

    [Fact]
    public void AuditGridIsBoundToEntriesAndKeepsItsSixColumns() =>
        RunOnStaThread(() =>
        {
            var view = CreateView();

            var itemsBinding = BindingOperations.GetBinding(view.AuditGrid, ItemsControl.ItemsSourceProperty);

            Assert.NotNull(itemsBinding);
            Assert.Equal(nameof(ProductAuditViewModel.Entries), itemsBinding.Path.Path);

            var headers = view.AuditGrid.Columns.Select(c => c.Header?.ToString()).ToList();
            Assert.Equal(ExpectedColumnHeaders, headers);
        });
}
