using System.ComponentModel;
using System.Windows;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;

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
            _viewModel.NewProductRequested += OnViewModelNewProductRequested;
            _viewModel.EditProductRequested += OnViewModelEditProductRequested;
            _viewModel.CheckoutRequested += OnViewModelCheckoutRequested;

            DataContext = _viewModel;

            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
        }

        public event EventHandler? LogoutRequested;

        public event EventHandler? CloseRegisterRequested;

        public event EventHandler? NewProductRequested;

        public event EventHandler<ProductId>? EditProductRequested;

        public event EventHandler? CheckoutRequested;

        private void OnViewModelLogoutRequested(object? sender, EventArgs e) =>
            LogoutRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelCloseRegisterRequested(object? sender, EventArgs e) =>
            CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelNewProductRequested(object? sender, EventArgs e) =>
            NewProductRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelEditProductRequested(object? sender, ProductId e) =>
            EditProductRequested?.Invoke(this, e);

        private void OnViewModelCheckoutRequested(object? sender, EventArgs e) =>
            CheckoutRequested?.Invoke(this, EventArgs.Empty);

        // Llamado desde App.xaml.cs tras crear un producto exitosamente en CreateProductWindow.
        public void ApplyProductCreated(string sku) => _viewModel.ApplyProductCreated(sku);

        // Llamado desde App.xaml.cs tras cerrar EditProductWindow (edición, activar/desactivar o
        // ajuste de inventario).
        public void ApplyProductUpdated(string sku) => _viewModel.ApplyProductUpdated(sku);

        // Llamado desde App.xaml.cs tras cerrar CheckoutWindow con un cobro exitoso.
        public void ApplyCheckoutCompleted() => _viewModel.ApplyCheckoutCompleted();

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
            _viewModel.NewProductRequested -= OnViewModelNewProductRequested;
            _viewModel.EditProductRequested -= OnViewModelEditProductRequested;
            _viewModel.CheckoutRequested -= OnViewModelCheckoutRequested;
            Closing -= OnWindowClosing;
            Closed -= OnWindowClosed;
        }
    }
}
