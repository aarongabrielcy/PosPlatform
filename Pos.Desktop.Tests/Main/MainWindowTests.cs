using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Pos.Application.Authentication;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Inventory;
using Pos.Desktop.Main;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.Sales;
using Pos.Desktop.Sales.History;
using Pos.Desktop.Settings;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using CatalogFakeProductManagementService = Pos.Desktop.Tests.Products.Catalog.FakeProductManagementService;
using FakeAdministrativeNotificationService = Pos.Desktop.Tests.AdministrativeNotifications.FakeAdministrativeNotificationService;
using FakeInventoryService = Pos.Desktop.Tests.Inventory.FakeInventoryService;
using FakeClock = Pos.Desktop.Tests.Sales.History.FakeClock;
using FakeSalesHistoryService = Pos.Desktop.Tests.Sales.History.FakeSalesHistoryService;

namespace Pos.Desktop.Tests.Main;

public class MainWindowTests
{
    // MainWindow solo puede crearse en un hilo STA.
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void MainWindowBindsDisplayNameAndRoleNameFromTheViewModel() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("Ana Pérez", viewModel.DisplayName);
            Assert.Equal("Cajero", viewModel.RoleName);
        });

    [Fact]
    public void LogoutButtonIsBoundToTheLogoutCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.LogoutButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.LogoutCommand), binding.Path.Path);
        });

    // TAREA 24C.2: el botón de logout debe usar el mismo lenguaje visual del sidebar
    // (SidebarFooterButtonStyle) en vez del chrome de Button por defecto, y conservar su ToolTip
    // tanto expandido como colapsado.
    [Fact]
    public void LogoutButtonUsesTheSidebarFooterButtonStyleAndKeepsItsToolTip() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var expectedStyle = window.LogoutButton.TryFindResource("SidebarFooterButtonStyle");

            Assert.NotNull(expectedStyle);
            Assert.Same(expectedStyle, window.LogoutButton.Style);
            Assert.Equal("Cerrar sesión", window.LogoutButton.ToolTip);
        });

    [Fact]
    public void CloseRegisterButtonIsBoundToTheCloseRegisterCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.CloseRegisterButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CloseRegisterCommand), binding.Path.Path);
        });

    [Fact]
    public void NavigationListBoxIsBoundToNavigationItemsAndSelectedNavigationItem() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var itemsBinding = BindingOperations.GetBinding(window.NavigationListBox, ItemsControl.ItemsSourceProperty);
            var selectedBinding = BindingOperations.GetBinding(window.NavigationListBox, Selector.SelectedItemProperty);

            Assert.NotNull(itemsBinding);
            Assert.Equal(nameof(MainWindowViewModel.NavigationItems), itemsBinding.Path.Path);
            Assert.NotNull(selectedBinding);
            Assert.Equal(nameof(MainWindowViewModel.SelectedNavigationItem), selectedBinding.Path.Path);
        });

    [Fact]
    public void ToggleSidebarButtonIsBoundToTheToggleSidebarCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.ToggleSidebarButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.ToggleSidebarCommand), binding.Path.Path);
        });

    [Fact]
    public void SidebarStartsExpandedAndTogglingChangesTheWidth() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            _ = new MainWindow(viewModel);

            Assert.True(viewModel.IsSidebarExpanded);
            var expandedWidth = viewModel.SidebarWidth;

            viewModel.ToggleSidebarCommand.Execute(null);

            Assert.False(viewModel.IsSidebarExpanded);
            Assert.NotEqual(expandedWidth, viewModel.SidebarWidth);

            viewModel.ToggleSidebarCommand.Execute(null);

            Assert.True(viewModel.IsSidebarExpanded);
            Assert.Equal(expandedWidth, viewModel.SidebarWidth);
        });

    [Fact]
    public void TogglingTheSidebarDoesNotChangeTheCurrentNavigationOrClearTheCart() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
            var registerSession = new FakeCurrentRegisterSession();
            var currentSalesCart = new FakeCurrentSalesCart();
            currentSalesCart.SetSnapshot(new Pos.Application.SalesCart.SalesCartSnapshot(
                [new Pos.Application.SalesCart.SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
            var dashboardViewModel = new DashboardViewModel(session, registerSession, currentSalesCart, new FakeProductManagementService());
            var salesViewModel = new SalesViewModel(session, registerSession, new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
            var salesHistoryViewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeClock(DateTimeOffset.UtcNow));
            var productsViewModel = new ProductsViewModel(new CatalogFakeProductManagementService());
            var viewModel = new MainWindowViewModel(
                session, registerSession, currentSalesCart, new Enforcement.FakeInstallationEnforcementStateService(),
                dashboardViewModel, salesViewModel, salesHistoryViewModel, productsViewModel,
                new InventoryViewModel(new FakeInventoryService(), session), new RegisterViewModel(registerSession), new SettingsViewModel(),
                new ProductAuditViewModel(new FakeProductAuditService()),
                new NotificationCenterViewModel(new FakeAdministrativeNotificationService()));
            _ = new MainWindow(viewModel);

            var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
            viewModel.SelectedNavigationItem = salesItem;
            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesPointOfSale);

            viewModel.ToggleSidebarCommand.Execute(null);

            Assert.Same(salesViewModel, viewModel.CurrentViewModel);
            Assert.Single(salesViewModel.CartLines);
        });

    [Fact]
    public void SidebarFooterShowsDisplayNameAndRoleName() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var displayNameBinding = BindingOperations.GetBinding(window.DisplayNameText, TextBlock.TextProperty);
            var roleNameBinding = BindingOperations.GetBinding(window.RoleNameText, TextBlock.TextProperty);

            Assert.NotNull(displayNameBinding);
            Assert.Equal(nameof(MainWindowViewModel.DisplayName), displayNameBinding.Path.Path);
            Assert.NotNull(roleNameBinding);
            Assert.Equal(nameof(MainWindowViewModel.RoleName), roleNameBinding.Path.Path);
        });

    [Fact]
    public void BrandNameTextIsPresent() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            Assert.Equal("POSPlatform", window.BrandNameText.Text);
        });

    [Fact]
    public void SectionTitleTextIsBoundToCurrentNavigationTitle() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.SectionTitleText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CurrentNavigationTitle), binding.Path.Path);
        });

    [Fact]
    public void ShellContentControlIsBoundToCurrentViewModel() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.ShellContentControl, ContentControl.ContentProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CurrentViewModel), binding.Path.Path);
        });

    // Dashboard debe estar seleccionado inicialmente para cualquier usuario autenticado (TAREA
    // 24C.1, sección 10).
    [Fact]
    public void DashboardIsSelectedInitially() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateAuthenticatedUser("Ana Pérez", "Cajero"));
            _ = new MainWindow(viewModel);

            Assert.IsType<DashboardViewModel>(viewModel.CurrentViewModel);
        });

    [Fact]
    public void NavigatingToEachAllowedSectionChangesTheContentToTheMatchingViewModel() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateManageProductsUser());
            _ = new MainWindow(viewModel);

            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
            Assert.IsType<ProductsViewModel>(viewModel.CurrentViewModel);

            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Inventory);
            Assert.IsType<InventoryViewModel>(viewModel.CurrentViewModel);

            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesPointOfSale);
            Assert.IsType<SalesViewModel>(viewModel.CurrentViewModel);

            viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Dashboard);
            Assert.IsType<DashboardViewModel>(viewModel.CurrentViewModel);
        });

    // Confirma que el shell reenvía ApplyProductUpdated al ViewModel que originó la solicitud de
    // edición (Venta), tal como pide App.xaml.cs tras cerrar EditProductWindow.
    [Fact]
    public void ApplyProductUpdatedForwardsToTheOriginatingChildViewModel() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
            var salesCartService = new FakeSalesCartService();
            var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), salesCartService, new FakeProductManagementService(), new FakeCurrentSalesCart());
            var dashboardViewModel = new DashboardViewModel(
                session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new FakeProductManagementService());
            var salesHistoryViewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeClock(DateTimeOffset.UtcNow));
            var viewModel = new MainWindowViewModel(
                session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new Enforcement.FakeInstallationEnforcementStateService(),
                dashboardViewModel, salesViewModel, salesHistoryViewModel,
                new ProductsViewModel(new CatalogFakeProductManagementService()), new InventoryViewModel(new FakeInventoryService(), session),
                new RegisterViewModel(new FakeCurrentRegisterSession()), new SettingsViewModel(),
                new ProductAuditViewModel(new FakeProductAuditService()),
                new NotificationCenterViewModel(new FakeAdministrativeNotificationService()));
            var window = new MainWindow(viewModel);

            salesViewModel.SelectedSearchResult = new Pos.Application.SalesCart.ProductSearchResult(
                ProductId.New(), "SKU-001", "Producto", 10m, "MXN", 5m, true);
            salesViewModel.EditProductCommand.Execute(null);

            window.ApplyProductUpdated("SKU-EDITED");

            Assert.Equal("SKU-EDITED", salesViewModel.SearchText);
        });

    // ---------- Campana / badge de notificaciones (TAREA 24E, sección 24/46) ----------
    //
    // Igual patrón que SalesViewTests.IncludeInactiveCheckBoxIsBoundToTheIncludeInactivePropertyAndItsVisibilityToCanManageProducts:
    // estos tests corren en un hilo STA sin bombear el Dispatcher, así que WPF nunca evalúa los
    // bindings (Visibility/Text quedan en su valor por defecto). Se verifica que el binding
    // correcto está declarado; el valor calculado (CanViewNotifications/HasUnread/UnreadCount) ya
    // se prueba a nivel de ViewModel en MainWindowViewModelTests y NotificationCenterViewModelTests.

    [Fact]
    public void NotificationBellButtonVisibilityIsBoundToCanViewNotifications() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationBellButton, UIElement.VisibilityProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.CanViewNotifications), binding.Path.Path);
        });

    [Fact]
    public void NotificationBellButtonCommandIsBoundToTheToggleCommand() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationBellButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(
                $"{nameof(MainWindowViewModel.NotificationCenterViewModel)}.{nameof(NotificationCenterViewModel.ToggleCommand)}",
                binding.Path.Path);
        });

    [Fact]
    public void NotificationBadgeVisibilityIsBoundToHasUnread() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationBadge, UIElement.VisibilityProperty);

            Assert.NotNull(binding);
            Assert.Equal(
                $"{nameof(MainWindowViewModel.NotificationCenterViewModel)}.{nameof(NotificationCenterViewModel.HasUnread)}",
                binding.Path.Path);
        });

    [Fact]
    public void NotificationBadgeTextIsBoundToUnreadCount() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationBadgeText, TextBlock.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(
                $"{nameof(MainWindowViewModel.NotificationCenterViewModel)}.{nameof(NotificationCenterViewModel.UnreadCount)}",
                binding.Path.Path);
        });

    [Fact]
    public void NotificationCenterPanelDataContextIsBoundToTheNotificationCenterViewModel() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationCenterPanel, FrameworkElement.DataContextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.NotificationCenterViewModel), binding.Path.Path);
        });

    [Fact]
    public void NotificationCenterPanelVisibilityIsBoundToIsOpen() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(CreateViewProductAuditUser());
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.NotificationCenterPanel, UIElement.VisibilityProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(NotificationCenterViewModel.IsOpen), binding.Path.Path);
        });

    private static AuthenticatedUser CreateViewProductAuditUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "ADMIN",
            "Administrador",
            "Administrador",
            [Permission.ManageProducts, Permission.ViewProductAudit]);

    private static MainWindowViewModel CreateViewModel(AuthenticatedUser user)
    {
        var session = new FakeCurrentUserSession { CurrentUser = user };
        var registerSession = new FakeCurrentRegisterSession();
        var currentSalesCart = new FakeCurrentSalesCart();

        var dashboardViewModel = new DashboardViewModel(
            session, registerSession, currentSalesCart, new FakeProductManagementService());
        var salesViewModel = new SalesViewModel(
            session, registerSession, new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        var salesHistoryViewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeClock(DateTimeOffset.UtcNow));
        var productsViewModel = new ProductsViewModel(new CatalogFakeProductManagementService());
        var inventoryViewModel = new InventoryViewModel(new FakeInventoryService(), session);
        var registerViewModel = new RegisterViewModel(registerSession);
        var settingsViewModel = new SettingsViewModel();
        var productAuditViewModel = new ProductAuditViewModel(new FakeProductAuditService());
        var notificationCenterViewModel = new NotificationCenterViewModel(new FakeAdministrativeNotificationService());

        return new MainWindowViewModel(
            session, registerSession, currentSalesCart, new Enforcement.FakeInstallationEnforcementStateService(),
            dashboardViewModel, salesViewModel, salesHistoryViewModel, productsViewModel,
            inventoryViewModel, registerViewModel, settingsViewModel, productAuditViewModel, notificationCenterViewModel);
    }

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);

    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts, Permission.AdjustInventory]);
}
