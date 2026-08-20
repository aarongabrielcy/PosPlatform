using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Users.UserManagement;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Users;

// BASIC-USR-01: pantalla de administración de usuarios locales, único punto de entrada (a
// diferencia de Productos, no existe un segundo origen posible como Venta) - por eso no necesita
// rastrear un "pending source" como ProductsViewModel/SalesViewModel. Carga automáticamente al
// construirse y vuelve a cargar cada vez que se navega aquí (igual patrón que ProductsViewModel).
public sealed class UserManagementViewModel : ViewModelBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _newUserCommand;
    private readonly AsyncRelayCommand<UserListItem> _editUserCommand;

    private UserListItem? _selectedUser;
    private bool _isBusy;
    private string? _generalError;
    private string? _statusMessage;

    public UserManagementViewModel(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, () => !IsBusy, HandleUnexpectedError);
        _newUserCommand = new AsyncRelayCommand(ExecuteNewUserAsync);
        _editUserCommand = new AsyncRelayCommand<UserListItem>(
            ExecuteEditUserAsync, item => item is not null && !IsBusy, HandleUnexpectedError);

        Users = new ObservableCollection<UserListItem>();
    }

    // El ViewModel nunca abre ventanas: solo pide abrir CreateUserWindow/EditUserWindow. El shell
    // reenvía el evento hasta App.xaml.cs, igual patrón que Productos.
    public event EventHandler? NewUserRequested;

    public event EventHandler<UserId>? EditUserRequested;

    public ICommand LoadCommand => _loadCommand;

    public ICommand NewUserCommand => _newUserCommand;

    public ICommand EditUserCommand => _editUserCommand;

    public ObservableCollection<UserListItem> Users { get; }

    public UserListItem? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                _editUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _editUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // Llamado al navegar aquí (ver MainWindowViewModel.ApplySection) y tras cerrar
    // CreateUserWindow/EditUserWindow con éxito.
    public Task RefreshAsync() => ExecuteLoadAsync();

    private async Task ExecuteLoadAsync()
    {
        GeneralError = null;
        IsBusy = true;

        try
        {
            var previouslySelectedId = SelectedUser?.UserId;

            var items = await _userManagementService.GetUsersAsync();

            Users.Clear();

            foreach (var item in items)
            {
                Users.Add(item);
            }

            StatusMessage = Users.Count switch
            {
                0 => "No hay usuarios.",
                1 => "1 usuario mostrado.",
                _ => $"{Users.Count} usuarios mostrados.",
            };

            SelectedUser = previouslySelectedId is { } id
                ? Users.FirstOrDefault(u => u.UserId == id)
                : null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteNewUserAsync()
    {
        NewUserRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteEditUserAsync(UserListItem? item)
    {
        if (item is not null)
        {
            EditUserRequested?.Invoke(this, item.UserId);
        }

        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
