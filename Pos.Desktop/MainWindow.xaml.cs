using System.ComponentModel;
using System.Windows;
using Pos.Desktop.Main;

namespace Pos.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.LogoutRequested += OnViewModelLogoutRequested;
            _viewModel.CloseRegisterRequested += OnViewModelCloseRegisterRequested;
            _viewModel.CancelSaleConfirmationRequested += OnViewModelCancelSaleConfirmationRequested;
            _viewModel.NewProductRequested += OnViewModelNewProductRequested;

            DataContext = _viewModel;

            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
        }

        public event EventHandler? LogoutRequested;

        public event EventHandler? CloseRegisterRequested;

        public event EventHandler? NewProductRequested;

        private void OnViewModelLogoutRequested(object? sender, EventArgs e) =>
            LogoutRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelCloseRegisterRequested(object? sender, EventArgs e) =>
            CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelNewProductRequested(object? sender, EventArgs e) =>
            NewProductRequested?.Invoke(this, EventArgs.Empty);

        // Llamado desde App.xaml.cs tras crear un producto exitosamente en CreateProductWindow.
        public void ApplyProductCreated(string sku) => _viewModel.ApplyProductCreated(sku);

        // El ViewModel nunca muestra ventanas ni MessageBox: solo pide confirmación. Confirmar o
        // cancelar el diálogo es responsabilidad exclusiva del código detrás de la vista.
        private void OnViewModelCancelSaleConfirmationRequested(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "¿Deseas cancelar la venta actual? Se perderán los productos agregados al carrito.",
                "PosPlatform",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ConfirmCancelSale();
            }
        }

        // Nunca permite terminar el proceso silenciosamente con una caja abierta: cierra la
        // ventana con la X requiere primero confirmar o completar el cierre de caja.
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.IsRegisterOpen)
            {
                return;
            }

            e.Cancel = true;

            var result = MessageBox.Show(
                "La caja sigue abierta. Debes cerrarla antes de salir.\n\n¿Desea cerrar la caja ahora?",
                "PosPlatform",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && _viewModel.CloseRegisterCommand.CanExecute(null))
            {
                _viewModel.CloseRegisterCommand.Execute(null);
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.LogoutRequested -= OnViewModelLogoutRequested;
            _viewModel.CloseRegisterRequested -= OnViewModelCloseRegisterRequested;
            _viewModel.CancelSaleConfirmationRequested -= OnViewModelCancelSaleConfirmationRequested;
            _viewModel.NewProductRequested -= OnViewModelNewProductRequested;
            Closing -= OnWindowClosing;
            Closed -= OnWindowClosed;
        }
    }
}
