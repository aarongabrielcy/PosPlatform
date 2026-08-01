using System.Windows.Input;

namespace Pos.Desktop.Common;

// Variante con parámetro de AsyncRelayCommand, necesaria para acciones por línea del carrito
// (aumentar/disminuir cantidad, eliminar) donde cada fila del DataGrid pasa su propia
// SalesCartLine como CommandParameter.
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(CastParameter(parameter)) ?? true);

    // async void es inevitable aquí: ICommand.Execute no puede ser async Task. Los errores no se
    // dejan escapar sin control: se capturan y se canalizan mediante el delegado _onError.
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(CastParameter(parameter));
        }
        catch (Exception ex)
        {
            if (_onError is null)
            {
                throw;
            }

            _onError(ex);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? CastParameter(object? parameter) => parameter is T typed ? typed : default;
}
