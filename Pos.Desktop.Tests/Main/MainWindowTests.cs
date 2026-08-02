using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Pos.Application.Authentication;
using Pos.Application.SalesCart;
using Pos.Desktop.Common;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Main;

public class MainWindowTests
{
    private static readonly string[] SearchResultsTextHeaders = { "SKU", "Producto" };

    private static readonly string[] CartGridCalculatedHeaders =
        { "Producto", "Precio", "Subtotal", "Impuesto", "Total" };

    // MainWindow solo puede crearse en un hilo STA.
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
    public void MainWindowBindsDisplayNameAndRoleNameFromTheViewModel() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("Ana Pérez", viewModel.DisplayName);
            Assert.Equal("Cajero", viewModel.RoleName);
        });

    // La ventana nunca se muestra (Show()) en esta prueba, así que WPF no llega a evaluar el
    // binding (eso ocurre en la cola del Dispatcher durante el layout real); en su lugar se
    // inspecciona la expresión de binding declarada en XAML para confirmar el cableado sin
    // depender de un PresentationSource real. El comportamiento del comando en sí ya está
    // cubierto por MainWindowViewModelTests.
    [Fact]
    public void LogoutButtonIsBoundToTheLogoutCommandProperty() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.LogoutButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.LogoutCommand), binding.Path.Path);
        });

    // El comportamiento del comando en sí ya está cubierto por MainWindowViewModelTests; aquí solo
    // se confirma que el botón "Nuevo producto" está cableado al comando correcto.
    [Fact]
    public void NewProductButtonIsBoundToTheNewProductCommandProperty() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NewProductButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.NewProductCommand), binding.Path.Path);
        });

    // Verifica el mismo defecto que OpenRegisterSessionWindowTests para NewProductRequested: el
    // evento del ViewModel debe reenviarse hasta el evento público de MainWindow, único punto que
    // App.xaml.cs usa para resolver CreateProductWindow.
    [Fact]
    public void ViewModelNewProductRequestedBubblesUpToMainWindowEvent() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            var raised = false;
            window.NewProductRequested += (_, _) => raised = true;

            viewModel.NewProductCommand.Execute(null);

            Assert.True(raised);
        });

    // El comportamiento del comando en sí ya está cubierto por MainWindowViewModelTests; aquí solo
    // se confirma que el botón "Editar producto" está cableado al comando correcto.
    [Fact]
    public void EditProductButtonIsBoundToTheEditProductCommandProperty() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.EditProductButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.EditProductCommand), binding.Path.Path);
        });

    [Fact]
    public void IncludeInactiveCheckBoxIsBoundToTheIncludeInactivePropertyAndItsVisibilityToCanManageProducts() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();

            var checkedBinding = BindingOperations.GetBinding(window.IncludeInactiveCheckBox, ToggleButton.IsCheckedProperty);
            Assert.NotNull(checkedBinding);
            Assert.Equal(nameof(MainWindowViewModel.IncludeInactive), checkedBinding.Path.Path);

            var visibilityBinding = BindingOperations.GetBinding(window.IncludeInactiveCheckBox, System.Windows.UIElement.VisibilityProperty);
            Assert.NotNull(visibilityBinding);
            Assert.Equal(nameof(MainWindowViewModel.CanManageProducts), visibilityBinding.Path.Path);
        });

    // Verifica el mismo defecto que OpenRegisterSessionWindowTests para NewProductRequested: el
    // evento del ViewModel debe reenviarse hasta el evento público de MainWindow, único punto que
    // App.xaml.cs usa para resolver EditProductWindow.
    [Fact]
    public void ViewModelEditProductRequestedBubblesUpToMainWindowEvent() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);
            viewModel.SelectedSearchResult = CreateSearchResult(true);

            ProductId? raisedProductId = null;
            window.EditProductRequested += (_, productId) => raisedProductId = productId;

            viewModel.EditProductCommand.Execute(null);

            Assert.Equal(viewModel.SelectedSearchResult.ProductId, raisedProductId);
        });

    [Fact]
    public void ApplyProductUpdatedForwardsToTheViewModel() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var salesCartService = new FakeSalesCartService();
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), salesCartService,
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            window.ApplyProductUpdated("SKU-EDITED");

            Assert.Equal("SKU-EDITED", viewModel.SearchText);
            Assert.Equal(1, salesCartService.SearchCallCount);
        });

    [Fact]
    public void ApplyProductCreatedForwardsToTheViewModel() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var salesCartService = new FakeSalesCartService();
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), salesCartService,
                new FakeProductManagementService(), new FakeCurrentSalesCart());
            var window = new MainWindow(viewModel);

            window.ApplyProductCreated("SKU-NEW");

            Assert.Equal("SKU-NEW", viewModel.SearchText);
            Assert.Equal(1, salesCartService.SearchCallCount);
        });

    // Regresión: SearchResultsGrid.IsAvailable usaba Binding sin Mode explícito, lo que WPF
    // resuelve a TwoWay para DataGridCheckBoxColumn (misma resolución que CheckBox.IsChecked).
    // ProductSearchResult.IsAvailable no tiene setter público, así que WPF lanzaba
    // InvalidOperationException al generar la celda, cerrando la aplicación en cuanto la
    // búsqueda devolvía resultados. El grid completo ya es IsReadOnly, pero eso no evita el
    // error porque DataGridCheckBoxColumn reutiliza el mismo CheckBox para mostrar y editar.
    [Fact]
    public void SearchResultsGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(CreateWindow().SearchResultsGrid.IsReadOnly));

    [Fact]
    public void CartGridIsReadOnly() =>
        RunOnStaThread(() => Assert.True(CreateWindow().CartGrid.IsReadOnly));

    [Fact]
    public void IsAvailableColumnBindingIsOneWay() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridCheckBoxColumn>(
                window.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Disponible"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.Equal(nameof(ProductSearchResult.IsAvailable), binding.Path.Path);
            Assert.Equal(BindingMode.OneWay, binding.Mode);
        });

    [Fact]
    public void AvailableQuantityColumnBindingIsNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridTextColumn>(
                window.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Existencia"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.Equal(nameof(ProductSearchResult.AvailableQuantity), binding.Path.Path);
            AssertBindingIsNotTwoWay(binding);
        });

    [Fact]
    public void SearchResultsGridPriceAndTextColumnsAreNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();

            foreach (var header in SearchResultsTextHeaders)
            {
                var column = Assert.IsType<DataGridTextColumn>(
                    window.SearchResultsGrid.Columns.Single(c => (string)c.Header == header));
                AssertBindingIsNotTwoWay(column.Binding);
            }

            var priceColumn = Assert.IsType<DataGridTextColumn>(
                window.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Precio"));
            AssertBindingIsNotTwoWay(priceColumn.Binding);
        });

    [Fact]
    public void CartGridCalculatedColumnsAreNotTwoWay() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();

            foreach (var header in CartGridCalculatedHeaders)
            {
                var column = Assert.IsType<DataGridTextColumn>(
                    window.CartGrid.Columns.Single(c => (string)c.Header == header));
                AssertBindingIsNotTwoWay(column.Binding);
            }
        });

    // Reproduce exactamente el camino que provocaba el crash: toma el Binding declarado en el
    // XAML para la columna "Disponible" y lo aplica a un CheckBox real, tal como hace
    // DataGridCheckBoxColumn internamente al generar la celda. Antes de la corrección esto
    // lanzaba InvalidOperationException; ahora debe completarse sin error.
    [Fact]
    public void IsAvailableColumnBindingDoesNotThrowWhenAppliedToACheckBox() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridCheckBoxColumn>(
                window.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Disponible"));
            var binding = Assert.IsType<Binding>(column.Binding);

            var checkBox = new CheckBox { DataContext = CreateSearchResult(isAvailable: true) };

            var exception = Record.Exception(
                () => BindingOperations.SetBinding(checkBox, ToggleButton.IsCheckedProperty, binding));

            Assert.Null(exception);
            Assert.True(checkBox.IsChecked);
        });

    // Revisión preventiva del carrito: Cantidad se controla solo por los botones +/-, nunca por
    // edición directa de celda.
    [Fact]
    public void CartQuantityTemplateKeepsIncreaseAndDecreaseButtonsBoundToCommands() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridTemplateColumn>(
                window.CartGrid.Columns.Single(c => (string)c.Header == "Cantidad"));

            var content = (StackPanel)column.CellTemplate.LoadContent();
            var buttons = content.Children.OfType<Button>().ToList();

            var decreaseButton = Assert.Single(buttons, b => (string)b.Content == "-");
            var increaseButton = Assert.Single(buttons, b => (string)b.Content == "+");

            AssertCommandBindingPath(decreaseButton, "DataContext." + nameof(MainWindowViewModel.DecreaseQuantityCommand));
            AssertCommandBindingPath(increaseButton, "DataContext." + nameof(MainWindowViewModel.IncreaseQuantityCommand));
        });

    // ---------- Mensajes de búsqueda y existencia (CORRECCIÓN UX FINAL TAREA 24A) ----------

    [Fact]
    public void SearchStatusTextIsBoundToTheSearchStatusMessageProperty() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();

            var binding = BindingOperations.GetBinding(window.SearchStatusText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.SearchStatusMessage), binding.Path.Path);
        });

    [Fact]
    public void CartErrorTextIsBoundToGeneralErrorAndPlacedNearTheCartGrid() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();

            var binding = BindingOperations.GetBinding(window.CartErrorText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.GeneralError), binding.Path.Path);
            Assert.True(Grid.GetRow(window.CartErrorText) < Grid.GetRow(window.CartGrid));
        });

    [Fact]
    public void AvailabilityStatusColumnUsesTheProductAvailabilityStatusConverter() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridTextColumn>(
                window.SearchResultsGrid.Columns.Single(c => (string)c.Header == "Estado"));
            var binding = Assert.IsType<Binding>(column.Binding);

            Assert.IsType<ProductAvailabilityStatusConverter>(binding.Converter);
            AssertBindingIsNotTwoWay(binding);
        });

    [Fact]
    public void AvailabilityStatusConverterShowsOutOfStockForATrackedProductWithoutQuantity()
    {
        var converter = new ProductAvailabilityStatusConverter();

        var text = converter.Convert(CreateSearchResult(isAvailable: false), typeof(string), null, null!);

        Assert.Equal("Sin existencia", text);
    }

    [Fact]
    public void AvailabilityStatusConverterShowsNothingForATrackedProductWithStock()
    {
        var converter = new ProductAvailabilityStatusConverter();

        var text = converter.Convert(CreateSearchResult(isAvailable: true), typeof(string), null, null!);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void AvailabilityStatusConverterShowsDoesNotTrackInventoryForAnUntrackedProduct()
    {
        var converter = new ProductAvailabilityStatusConverter();
        var result = new ProductSearchResult(
            ProductId.New(), "SKU-2", "Servicio", 10m, "MXN", availableQuantity: 0m, tracksInventory: false);

        var text = converter.Convert(result, typeof(string), null, null!);

        Assert.Equal("No controla inventario", text);
    }

    [Fact]
    public void CartRemoveTemplateKeepsButtonBoundToRemoveLineCommand() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            var column = Assert.IsType<DataGridTemplateColumn>(
                window.CartGrid.Columns.Single(c => (string)c.Header == "Eliminar"));

            var button = Assert.IsType<Button>(column.CellTemplate.LoadContent());

            AssertCommandBindingPath(button, "DataContext." + nameof(MainWindowViewModel.RemoveLineCommand));
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

    private static MainWindow CreateWindow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(
            session, new FakeCurrentRegisterSession(), new FakeSalesCartService(),
            new FakeProductManagementService(), new FakeCurrentSalesCart());

        return new MainWindow(viewModel);
    }

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);

    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts]);
}
