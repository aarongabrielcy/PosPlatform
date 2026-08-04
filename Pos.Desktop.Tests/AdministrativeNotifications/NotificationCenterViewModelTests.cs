using Pos.Application.AdministrativeNotifications;
using Pos.Application.ProductAudit;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Desktop.Tests.AdministrativeNotifications;

public class NotificationCenterViewModelTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 8, 2, 14, 22, 0, TimeSpan.Zero);

    private static AdministrativeNotificationItem CreateItem(DateTimeOffset? readAtUtc = null) =>
        new(
            AdministrativeNotificationId.New(),
            ProductAuditEventId.New(),
            ProductId.New(),
            "SKU-001",
            "Agua 1L",
            "Administrador",
            ProductAuditAction.Updated,
            OccurredAtUtc,
            readAtUtc,
            [new ProductAuditFieldChange(ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50")]);

    // ---------- Toggle / carga ----------

    [Fact]
    public void ToggleCommandTogglesIsOpen()
    {
        var viewModel = new NotificationCenterViewModel(new FakeAdministrativeNotificationService());

        Assert.False(viewModel.IsOpen);

        viewModel.ToggleCommand.Execute(null);

        Assert.True(viewModel.IsOpen);

        viewModel.ToggleCommand.Execute(null);

        Assert.False(viewModel.IsOpen);
    }

    [Fact]
    public async Task OpeningThePanelLoadsTheNotificationList()
    {
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([CreateItem()], false),
        };
        var viewModel = new NotificationCenterViewModel(service);

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Notifications);
        Assert.Equal(1, service.GetNotificationsCallCount);
    }

    [Fact]
    public async Task OpeningThePanelNeverMarksAnythingAsRead()
    {
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([CreateItem()], false),
        };
        var viewModel = new NotificationCenterViewModel(service);

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(0, service.MarkReadCallCount);
    }

    [Fact]
    public async Task EmptyResultSetsIsEmptyTrue()
    {
        var viewModel = new NotificationCenterViewModel(new FakeAdministrativeNotificationService());

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.True(viewModel.IsEmpty);
        Assert.Empty(viewModel.Notifications);
    }

    [Fact]
    public async Task NonEmptyResultSetsIsEmptyFalse()
    {
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([CreateItem()], false),
        };
        var viewModel = new NotificationCenterViewModel(service);

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task RowsReflectReadAndUnreadState()
    {
        var readItem = CreateItem(readAtUtc: OccurredAtUtc.AddMinutes(5));
        var unreadItem = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([unreadItem, readItem], false),
        };
        var viewModel = new NotificationCenterViewModel(service);

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.Contains(viewModel.Notifications, row => !row.IsRead && row.Item == unreadItem);
        Assert.Contains(viewModel.Notifications, row => row.IsRead && row.Item == readItem);
    }

    [Fact]
    public async Task AnUnexpectedErrorWhileLoadingSetsGeneralError()
    {
        var service = new FakeAdministrativeNotificationService { ThrowOnGetNotifications = new InvalidOperationException("boom") };
        var viewModel = new NotificationCenterViewModel(service);

        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ocurrió un error inesperado.", viewModel.GeneralError);
    }

    // ---------- Unread count / badge ----------

    [Fact]
    public async Task RefreshUnreadCountAsyncUpdatesUnreadCountAndHasUnread()
    {
        var service = new FakeAdministrativeNotificationService { UnreadCount = 3 };
        var viewModel = new NotificationCenterViewModel(service);

        await viewModel.RefreshUnreadCountAsync();

        Assert.Equal(3, viewModel.UnreadCount);
        Assert.True(viewModel.HasUnread);
    }

    [Fact]
    public async Task HasUnreadIsFalseWhenUnreadCountIsZero()
    {
        var service = new FakeAdministrativeNotificationService { UnreadCount = 0 };
        var viewModel = new NotificationCenterViewModel(service);

        await viewModel.RefreshUnreadCountAsync();

        Assert.False(viewModel.HasUnread);
    }

    // ---------- OpenNotificationCommand ----------

    [Fact]
    public async Task OpenNotificationCommandMarksTheNotificationAsRead()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        Assert.Equal(1, service.MarkReadCallCount);
        Assert.Equal(item.NotificationId, service.LastMarkReadNotificationId);
    }

    [Fact]
    public async Task OpenNotificationCommandSetsSelectedNotification()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        Assert.Same(row, viewModel.SelectedNotification);
    }

    [Fact]
    public async Task OpenNotificationCommandClosesThePanel()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        Assert.False(viewModel.IsOpen);
    }

    [Fact]
    public async Task OpenNotificationCommandRefreshesTheBadge()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
            UnreadCount = 1,
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        // Simula que, tras marcar como leída, el backend ya no la cuenta como no leída (TAREA 24E,
        // sección 29): el badge debe reflejar el nuevo valor sin reiniciar la app.
        service.UnreadCount = 0;
        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        Assert.Equal(0, viewModel.UnreadCount);
        Assert.False(viewModel.HasUnread);
    }

    [Fact]
    public async Task OpenNotificationCommandNeverRemovesTheNotificationFromTheList()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        // No desaparece por leerla (TAREA 24E, sección 23): sigue en la lista.
        Assert.Single(viewModel.Notifications);
    }

    [Fact]
    public async Task OpenNotificationCommandRaisesOpenNotificationRequestedWithTheUnderlyingItem()
    {
        var item = CreateItem();
        var service = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var viewModel = new NotificationCenterViewModel(service);
        viewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.Notifications);

        AdministrativeNotificationItem? raised = null;
        viewModel.OpenNotificationRequested += (_, raisedItem) => raised = raisedItem;

        viewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        Assert.Same(item, raised);
    }

    [Fact]
    public void OpenNotificationCommandCannotExecuteWithoutARow()
    {
        var viewModel = new NotificationCenterViewModel(new FakeAdministrativeNotificationService());

        Assert.False(viewModel.OpenNotificationCommand.CanExecute(null));
    }
}
