using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Pos.Application.Sales.History;
using Pos.Desktop.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Desktop.Tests.Sales.History;

// TAREA 25B-FIX-2: pruebas estructurales del bloque de totales del detalle (Subtotal/Total),
// no pixel tests. Cubren la regresión del bug de montado (labels y valores compartiendo la
// misma celda de Grid por falta de ColumnDefinitions).
public class SalesHistoryViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    // SalesHistoryView solo puede crearse en un hilo STA.
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

    private static SaleHistoryDetail CreateDetail(SaleId saleId) => new(
        saleId, SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
        RegisterSessionId.New(), 100m, 116m, "MXN",
        [new SaleHistoryDetailLine("SKU-001", "Agua 1L", 1m, 100m, 100m, "MXN")],
        [new SaleHistoryDetailPayment(PaymentMethod.Cash, 116m, "MXN", Now, null)]);

    private static SalesHistoryView CreateViewShowingDetail()
    {
        var saleId = SaleId.New();
        var detail = CreateDetail(saleId);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeClock(Now));
        var row = new SalesHistoryRowViewModel(new SalesHistoryItem(
            saleId, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1", RegisterSessionId.New(),
            1m, 116m, "MXN", [new SalesHistoryPaymentAmount(PaymentMethod.Cash, 116m)]));

        viewModel.OpenDetailCommand.Execute(row);

        var view = new SalesHistoryView { DataContext = viewModel };
        view.Measure(new Size(1200, 760));
        view.Arrange(new Rect(0, 0, 1200, 760));
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
    public void TotalsBlockIsPresentWithSubtotalAndTotalLabels() =>
        RunOnStaThread(() =>
        {
            var view = CreateViewShowingDetail();
            var texts = GetTextBlockTexts(view);

            Assert.Contains("Subtotal", texts);
            Assert.Contains("Total", texts);
        });

    [Fact]
    public void SubtotalAndTotalValuesAreBoundToTheDetailViewModel() =>
        RunOnStaThread(() =>
        {
            var view = CreateViewShowingDetail();

            var subtotalBinding = BindingOperations.GetBinding(view.SubtotalValueText, TextBlock.TextProperty);
            var totalBinding = BindingOperations.GetBinding(view.TotalValueText, TextBlock.TextProperty);

            Assert.NotNull(subtotalBinding);
            Assert.Equal(nameof(SaleHistoryDetailViewModel.SubtotalText), subtotalBinding.Path.Path);
            Assert.NotNull(totalBinding);
            Assert.Equal(nameof(SaleHistoryDetailViewModel.TotalText), totalBinding.Path.Path);

            Assert.Equal("100.00 MXN", view.SubtotalValueText.Text);
            Assert.Equal("116.00 MXN", view.TotalValueText.Text);
        });

    // Regresión del bug de montado (TAREA 25B-FIX-2): label y valor de cada fila deben ocupar
    // columnas distintas del Grid, y Subtotal/Total deben ocupar filas distintas. Antes del fix,
    // los cuatro TextBlocks caían todos en Grid.Row=0/Grid.Column=0 por defecto.
    [Fact]
    public void TotalsRowsDoNotShareGridCellsBetweenLabelAndValue() =>
        RunOnStaThread(() =>
        {
            var view = CreateViewShowingDetail();

            var subtotalLabelColumn = Grid.GetColumn(view.SubtotalLabelText);
            var subtotalValueColumn = Grid.GetColumn(view.SubtotalValueText);
            var totalLabelColumn = Grid.GetColumn(view.TotalLabelText);
            var totalValueColumn = Grid.GetColumn(view.TotalValueText);

            Assert.NotEqual(subtotalLabelColumn, subtotalValueColumn);
            Assert.NotEqual(totalLabelColumn, totalValueColumn);

            var subtotalRow = Grid.GetRow(view.SubtotalLabelText);
            var totalRow = Grid.GetRow(view.TotalLabelText);

            Assert.NotEqual(subtotalRow, totalRow);
        });

    [Fact]
    public void TotalIsVisuallyMoreProminentThanSubtotal() =>
        RunOnStaThread(() =>
        {
            var view = CreateViewShowingDetail();

            Assert.True(view.TotalValueText.FontSize >= view.SubtotalValueText.FontSize);
            Assert.Equal(FontWeights.Bold, view.TotalValueText.FontWeight);
        });
}
