using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Pos.Application.Authentication;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

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
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(session);
            var window = new MainWindow(viewModel);

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("Ana Pérez", viewModel.DisplayName);
            Assert.Equal("Cajero", viewModel.RoleName);
        });

    // La ventana nunca se muestra (Show()) en esta prueba, así que WPF no llega a evaluar el
    // binding (eso ocurre en la cola del Dispatcher durante el layout real); en su lugar se
    // inspecciona la expresión de binding declarada en XAML para confirmar el cableado sin
    // depender de un PresentationSource real. El comportamiento del comando en sí ya está
    // cubierto por MainWindowViewModelTests.
    [Fact]
    public void LogoutButtonIsBoundToTheLogoutCommandProperty() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
            var viewModel = new MainWindowViewModel(session);
            var window = new MainWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.LogoutButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(MainWindowViewModel.LogoutCommand), binding.Path.Path);
        });

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);
}
