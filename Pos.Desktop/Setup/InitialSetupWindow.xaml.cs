using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pos.Desktop.Setup;

public partial class InitialSetupWindow : Window, IDisposable
{
    private readonly InitialSetupViewModel _viewModel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;
    private bool _isPasswordVisible;
    private bool _isConfirmPasswordVisible;

    public InitialSetupWindow(InitialSetupViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CancellationToken = _cancellationTokenSource.Token;
        _viewModel.SetupCompleted += OnSetupCompleted;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => OrganizationNameTextBox.Focus();

    private void OnConfigurarClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPendingCredentials(GetActivePasswordValue(), GetActiveConfirmPasswordValue());

        if (_viewModel.SubmitCommand.CanExecute(null))
        {
            _viewModel.SubmitCommand.Execute(null);
        }
    }

    private void OnTogglePasswordVisibilityClick(object sender, RoutedEventArgs e) =>
        SetPasswordVisibility(!_isPasswordVisible);

    private void OnToggleConfirmPasswordVisibilityClick(object sender, RoutedEventArgs e) =>
        SetConfirmPasswordVisibility(!_isConfirmPasswordVisible);

    internal string GetActivePasswordValue() =>
        _isPasswordVisible ? PasswordVisibleTextBox.Text : PasswordBox.Password;

    internal string GetActiveConfirmPasswordValue() =>
        _isConfirmPasswordVisible ? ConfirmPasswordVisibleTextBox.Text : ConfirmPasswordBox.Password;

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
        UpdateVisibilityToggleAppearance(PasswordVisibilityButton, PasswordVisibilityIcon, visible);
    }

    private void SetConfirmPasswordVisibility(bool visible)
    {
        if (visible)
        {
            ConfirmPasswordVisibleTextBox.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordVisibleTextBox.Visibility = Visibility.Visible;
            ConfirmPasswordVisibleTextBox.Focus();
            ConfirmPasswordVisibleTextBox.CaretIndex = ConfirmPasswordVisibleTextBox.Text.Length;
        }
        else
        {
            ConfirmPasswordBox.Password = ConfirmPasswordVisibleTextBox.Text;
            ConfirmPasswordVisibleTextBox.Clear();
            ConfirmPasswordVisibleTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordBox.Focus();
        }

        _isConfirmPasswordVisible = visible;
        UpdateVisibilityToggleAppearance(ConfirmPasswordVisibilityButton, ConfirmPasswordVisibilityIcon, visible);
    }

    private static void UpdateVisibilityToggleAppearance(Button button, Path icon, bool visible)
    {
        var tooltip = visible ? "Ocultar contraseña" : "Mostrar contraseña";

        button.ToolTip = tooltip;
        AutomationProperties.SetName(button, tooltip);
        icon.Data = (Geometry)button.FindResource(visible ? "EyeOffIconGeometry" : "EyeIconGeometry");
    }

    private void OnSetupCompleted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InitialSetupViewModel.GeneralError) && _viewModel.GeneralError is not null)
        {
            ClearPasswordFields();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.SetupCompleted -= OnSetupCompleted;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ClearPasswordFields();

        Dispose();
    }

    private void ClearPasswordFields()
    {
        PasswordBox.Clear();
        PasswordVisibleTextBox.Clear();
        ConfirmPasswordBox.Clear();
        ConfirmPasswordVisibleTextBox.Clear();
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
