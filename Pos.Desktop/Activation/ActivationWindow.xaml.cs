using System.Windows;

namespace Pos.Desktop.Activation;

public partial class ActivationWindow : Window, IDisposable
{
    private readonly ActivationViewModel _viewModel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    public ActivationWindow(ActivationViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CancellationToken = _cancellationTokenSource.Token;
        _viewModel.ActivationCompleted += OnActivationCompleted;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnrollmentCodeTextBox.Focus();

    private void OnActivarClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActivateCommand.CanExecute(null))
        {
            _viewModel.ActivateCommand.Execute(null);
        }
    }

    private void OnActivationCompleted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.ActivationCompleted -= OnActivationCompleted;
        Dispose();
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
