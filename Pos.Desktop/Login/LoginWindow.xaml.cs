using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pos.Desktop.Login;

public partial class LoginWindow : Window, IDisposable
{
    private readonly LoginViewModel _viewModel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;
    private bool _isPasswordVisible;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CancellationToken = _cancellationTokenSource.Token;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UsernameTextBox.Focus();

    private void OnIniciarSesionClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPendingPassword(GetActivePasswordValue());

        if (_viewModel.LoginCommand.CanExecute(null))
        {
            _viewModel.LoginCommand.Execute(null);
        }
    }

    private void OnTogglePasswordVisibilityClick(object sender, RoutedEventArgs e) =>
        SetPasswordVisibility(!_isPasswordVisible);

    internal string GetActivePasswordValue() =>
        _isPasswordVisible ? PasswordVisibleTextBox.Text : PasswordBox.Password;

    private void SetPasswordVisibility(bool visible)
    {
        if (visible)
        {
            PasswordVisibleTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordVisibleTextBox.Visibility = Visibility.Visible;
            PasswordVisibleTextBox.Focus();
            PasswordVisibleTextBox.CaretIndex = PasswordVisibleTextBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = PasswordVisibleTextBox.Text;
            PasswordVisibleTextBox.Clear();
            PasswordVisibleTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordBox.Focus();
        }

        _isPasswordVisible = visible;
        UpdateVisibilityToggleAppearance(visible);
    }

    private void UpdateVisibilityToggleAppearance(bool visible)
    {
        var tooltip = visible ? "Ocultar contraseña" : "Mostrar contraseña";

        PasswordVisibilityButton.ToolTip = tooltip;
        AutomationProperties.SetName(PasswordVisibilityButton, tooltip);
        PasswordVisibilityIcon.Data = (Geometry)PasswordVisibilityButton.FindResource(
            visible ? "EyeOffIconGeometry" : "EyeIconGeometry");
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.GeneralError) && _viewModel.GeneralError is not null)
        {
            ClearPasswordFields();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.LoginSucceeded -= OnLoginSucceeded;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ClearPasswordFields();

        Dispose();
    }

    private void ClearPasswordFields()
    {
        PasswordBox.Clear();
        PasswordVisibleTextBox.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
