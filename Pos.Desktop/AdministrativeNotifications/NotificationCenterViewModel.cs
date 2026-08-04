using System.Collections.ObjectModel;
using System.Windows.Input;
using Pos.Application.AdministrativeNotifications;
using Pos.Desktop.Common;

namespace Pos.Desktop.AdministrativeNotifications;

// Centro de notificaciones administrativas (TAREA 24E, sección 31): la campana del shell abre/
// cierra este panel. No conoce Window ni IServiceProvider; la navegación hacia el AuditEvent
// exacto la resuelve MainWindowViewModel al escuchar OpenNotificationRequested (mismo patrón que
// ProductsViewModel.AuditRequested).
public sealed class NotificationCenterViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly IAdministrativeNotificationService _notificationService;
    private readonly AsyncRelayCommand _toggleCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand<AdministrativeNotificationRowViewModel> _openNotificationCommand;

    private bool _isOpen;
    private bool _isBusy;
    private string? _generalError;
    private int _unreadCount;
    private bool _isEmpty;
    private AdministrativeNotificationRowViewModel? _selectedNotification;

    public NotificationCenterViewModel(IAdministrativeNotificationService notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        _toggleCommand = new AsyncRelayCommand(ExecuteToggleAsync, onError: HandleUnexpectedError);
        _refreshCommand = new AsyncRelayCommand(RefreshUnreadCountAsync, onError: HandleUnexpectedError);
        _openNotificationCommand = new AsyncRelayCommand<AdministrativeNotificationRowViewModel>(
            ExecuteOpenNotificationAsync, notification => notification is not null, HandleUnexpectedError);

        Notifications = new ObservableCollection<AdministrativeNotificationRowViewModel>();
    }

    // Solo Desktop escucha este evento (MainWindowViewModel): nunca abre ProductHistoryWindow,
    // siempre navega dentro del shell (TAREA 24E, sección 29/30).
    public event EventHandler<AdministrativeNotificationItem>? OpenNotificationRequested;

    public ObservableCollection<AdministrativeNotificationRowViewModel> Notifications { get; }

    public ICommand ToggleCommand => _toggleCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand OpenNotificationCommand => _openNotificationCommand;

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public int UnreadCount
    {
        get => _unreadCount;
        private set
        {
            if (SetProperty(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    // Badge oculto si UnreadCount == 0 (TAREA 24E, sección 24/36).
    public bool HasUnread => UnreadCount > 0;

    // "No tienes notificaciones administrativas." (TAREA 24E, sección 36): solo tras la primera
    // carga, nunca antes de LoadNotificationsAsync.
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public AdministrativeNotificationRowViewModel? SelectedNotification
    {
        get => _selectedNotification;
        private set => SetProperty(ref _selectedNotification, value);
    }

    private async Task ExecuteToggleAsync()
    {
        IsOpen = !IsOpen;

        // Abrir el panel nunca marca nada como leído (TAREA 24E, sección 22): solo carga la
        // lista.
        if (IsOpen)
        {
            await LoadNotificationsAsync();
        }
    }

    private async Task LoadNotificationsAsync()
    {
        IsBusy = true;

        try
        {
            var page = await _notificationService.GetNotificationsAsync(0, PageSize);

            Notifications.Clear();

            foreach (var item in page.Items)
            {
                Notifications.Add(new AdministrativeNotificationRowViewModel(item));
            }

            IsEmpty = Notifications.Count == 0;
            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshUnreadCountAsync();
    }

    // Llamado por MainWindowViewModel al iniciar MainWindow y tras cualquier operación Product
    // local que pudo haber generado una Notification, sin polling ni timer (TAREA 24E, sección
    // 32/33).
    public async Task RefreshUnreadCountAsync()
    {
        UnreadCount = await _notificationService.GetUnreadCountAsync();
    }

    private async Task ExecuteOpenNotificationAsync(AdministrativeNotificationRowViewModel? notification)
    {
        if (notification is null)
        {
            return;
        }

        // 1. MarkReadAsync (TAREA 24E, sección 29).
        await _notificationService.MarkReadAsync(notification.Item.NotificationId);

        SelectedNotification = notification;

        // 2. refresca badge.
        await RefreshUnreadCountAsync();

        // 3. cierra panel.
        IsOpen = false;

        // 4/5. MainWindowViewModel navega y selecciona el AuditEvent exacto.
        OpenNotificationRequested?.Invoke(this, notification.Item);
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
