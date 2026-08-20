using System.Windows;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Users;

public partial class EditUserWindow : Window
{
    private readonly EditUserViewModel _viewModel;

    public EditUserWindow(EditUserViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Updated += OnUpdated;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Establecido a true si al menos un cambio se guardó con éxito mientras la ventana estuvo
    // abierta (edición, activar/desactivar o restablecer contraseña): App.xaml.cs lo usa para
    // decidir si la lista de UserManagementView necesita refrescarse al cerrar.
    public bool AnyChangeApplied { get; private set; }

    public async Task LoadAsync(UserId userId) => await _viewModel.LoadAsync(userId);

    private void OnResetPasswordClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPendingNewPassword(NewPasswordBox.Password, ConfirmNewPasswordBox.Password);

        if (_viewModel.ResetPasswordCommand.CanExecute(null))
        {
            _viewModel.ResetPasswordCommand.Execute(null);
        }

        NewPasswordBox.Clear();
        ConfirmNewPasswordBox.Clear();
    }

    private void OnUpdated(object? sender, EventArgs e) => AnyChangeApplied = true;

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = AnyChangeApplied;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.Updated -= OnUpdated;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;

        NewPasswordBox.Clear();
        ConfirmNewPasswordBox.Clear();
    }
}
