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

            DataContext = _viewModel;

            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
        }

        public event EventHandler? LogoutRequested;

        public event EventHandler? CloseRegisterRequested;

        private void OnViewModelLogoutRequested(object? sender, EventArgs e) =>
            LogoutRequested?.Invoke(this, EventArgs.Empty);

        private void OnViewModelCloseRegisterRequested(object? sender, EventArgs e) =>
            CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

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
            Closing -= OnWindowClosing;
            Closed -= OnWindowClosed;
        }
    }
}
