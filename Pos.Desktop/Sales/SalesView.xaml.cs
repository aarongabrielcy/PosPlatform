using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pos.Desktop.Sales;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un SalesViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class SalesView : UserControl
{
    private SalesViewModel? _viewModel;

    public SalesView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CancelSaleConfirmationRequested -= OnCancelSaleConfirmationRequested;
            _viewModel.SearchFocusRequested -= OnSearchFocusRequested;
        }

        _viewModel = e.NewValue as SalesViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CancelSaleConfirmationRequested += OnCancelSaleConfirmationRequested;
            _viewModel.SearchFocusRequested += OnSearchFocusRequested;
        }
    }

    // El ViewModel nunca muestra ventanas ni MessageBox: solo pide confirmación. Confirmar o
    // cancelar el diálogo es responsabilidad exclusiva del código detrás de la vista.
    private void OnCancelSaleConfirmationRequested(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "¿Deseas cancelar la venta actual? Se perderán los productos agregados al carrito.",
            "PosPlatform",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel?.ConfirmCancelSale();
        }
    }

    // El ViewModel no conoce TextBox/Keyboard (TAREA 24F, sección 13): solo pide que el foco
    // regrese al buscador tras agregar un producto exitosamente.
    private void OnSearchFocusRequested(object? sender, EventArgs e)
    {
        SearchTextBox.Focus();
        Keyboard.Focus(SearchTextBox);
    }

    // Navegación por teclado sobre los resultados de búsqueda (TAREA 24F, sección 10) mientras el
    // foco permanece en el SearchBox: el code-behind solo coordina foco/selección de UI y delega
    // los comandos existentes (Add/Search) al ViewModel, sin tocar SalesCart directamente.
    private void OnSearchTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Enter or Key.Escape))
        {
            return;
        }

        HandleSearchKeyDown(e.Key);
        e.Handled = true;
    }

    // Separado de OnSearchTextBoxPreviewKeyDown para que las pruebas puedan ejercitar la lógica de
    // navegación sin construir un KeyEventArgs/PresentationSource reales (TAREA 24F, sección 28).
    internal void HandleSearchKeyDown(Key key)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (key)
        {
            case Key.Down:
                MoveSearchResultSelection(1);
                break;

            case Key.Up:
                MoveSearchResultSelection(-1);
                break;

            case Key.Enter:
                HandleSearchEnter();
                break;

            case Key.Escape:
                _viewModel.SearchText = string.Empty;
                break;
        }
    }

    private void MoveSearchResultSelection(int delta)
    {
        if (_viewModel is null || _viewModel.SearchResults.Count == 0)
        {
            return;
        }

        var results = _viewModel.SearchResults;
        var currentIndex = _viewModel.SelectedSearchResult is null
            ? -1
            : results.IndexOf(_viewModel.SelectedSearchResult);

        var nextIndex = Math.Clamp(currentIndex + delta, 0, results.Count - 1);
        var nextSelection = results[nextIndex];

        _viewModel.SelectedSearchResult = nextSelection;
        SearchResultsGrid.ScrollIntoView(nextSelection);
    }

    // Enter con un resultado seleccionado agrega (mismo flujo que el botón "Agregar"); sin
    // selección, fuerza la búsqueda inmediata (Buscar/lector de código de barras + Enter).
    private void HandleSearchEnter()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.SelectedSearchResult is not null && _viewModel.AddSelectedProductCommand.CanExecute(null))
        {
            _viewModel.AddSelectedProductCommand.Execute(null);
        }
        else if (_viewModel.SearchCommand.CanExecute(null))
        {
            _viewModel.SearchCommand.Execute(null);
        }
    }
}
