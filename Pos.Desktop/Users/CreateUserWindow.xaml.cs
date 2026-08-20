using System.Windows;

namespace Pos.Desktop.Users;

public partial class CreateUserWindow : Window
{
    private readonly CreateUserViewModel _viewModel;

    public CreateUserWindow(CreateUserViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.UserCreated += OnUserCreated;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LoadRolesCommand.CanExecute(null))
        {
            _viewModel.LoadRolesCommand.Execute(null);
        }

        UsernameTextBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPendingPassword(PasswordBox.Password, ConfirmPasswordBox.Password);

        if (_viewModel.SaveCommand.CanExecute(null))
        {
            _viewModel.SaveCommand.Execute(null);
        }
    }

    private void OnUserCreated(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.UserCreated -= OnUserCreated;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;

        PasswordBox.Clear();
        ConfirmPasswordBox.Clear();
    }
}
