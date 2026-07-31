using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Desktop.RegisterSessions;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.RegisterSessions;

// Cubre el defecto original: "Cerrar sesión" y la X cerraban ambos la ventana con el mismo
// desenlace efectivo (DialogResult false/null tratados igual por el llamador). Estas pruebas
// muestran ShowDialog() en un hilo STA propio para verificar el DialogResult real que WPF asigna
// en cada caso, además del OpenRegisterSessionWindowResult explícito que ahora los distingue.
public class OpenRegisterSessionWindowTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    // OpenRegisterSessionWindow solo puede crearse y mostrarse (ShowDialog) en un hilo STA con
    // bucle de mensajes propio.
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
    public void LogoutCommandProducesLogoutRequestedResultWithDialogResultFalse() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser() };
            var service = new FakeRegisterSessionService();
            var viewModel = new OpenRegisterSessionViewModel(service, session, NullLogger<OpenRegisterSessionViewModel>.Instance);
            using var window = new OpenRegisterSessionWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => viewModel.LogoutCommand.Execute(null)));

            var dialogResult = window.ShowDialog();

            Assert.Equal(false, dialogResult);
            Assert.Equal(OpenRegisterSessionWindowResult.LogoutRequested, window.Result);
            Assert.Equal(1, session.ClearCallCount);
        });

    [Fact]
    public void ClosingLikeTheXButtonProducesExitRequestedResultEvenThoughDialogResultIsAlsoFalse() =>
        RunOnStaThread(() =>
        {
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser() };
            var service = new FakeRegisterSessionService();
            var viewModel = new OpenRegisterSessionViewModel(service, session, NullLogger<OpenRegisterSessionViewModel>.Instance);
            using var window = new OpenRegisterSessionWindow(viewModel);

            // Simula la X: cierra la ventana sin pasar por LogoutCommand ni por una apertura
            // exitosa, es decir, sin que nada establezca DialogResult de antemano. WPF, al no
            // haberse fijado DialogResult explícitamente, devuelve false desde ShowDialog() —el
            // mismo valor que produce un logout explícito— por lo que DialogResult por sí solo no
            // basta para distinguir ambos casos: de ahí la necesidad de Result.
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => window.Close()));

            var dialogResult = window.ShowDialog();

            Assert.Equal(false, dialogResult);
            Assert.Equal(OpenRegisterSessionWindowResult.ExitRequested, window.Result);
        });

    [Fact]
    public void SuccessfulOpenProducesRegisterOpenedResultWithDialogResultTrue() =>
        RunOnStaThread(() =>
        {
            var activeSession = CreateActiveRegisterSession();
            var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser() };
            var service = new FakeRegisterSessionService(
                getAvailableRegistersHandler: _ => Task.FromResult<IReadOnlyList<AvailableRegister>>(
                    [new AvailableRegister(RegisterId.New(), "Caja 1")]),
                openHandler: (_, _) => Task.FromResult(RegisterSessionResult.OpenSuccess(activeSession)));
            var viewModel = new OpenRegisterSessionViewModel(service, session, NullLogger<OpenRegisterSessionViewModel>.Instance);
            using var window = new OpenRegisterSessionWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    viewModel.OpeningAmountText = "100";
                    viewModel.OpenCommand.Execute(null);
                }));

            var dialogResult = window.ShowDialog();

            Assert.Equal(true, dialogResult);
            Assert.Equal(OpenRegisterSessionWindowResult.RegisterOpened, window.Result);
            Assert.Same(activeSession, window.OpenedSession);
        });

    private static AuthenticatedUser CreateAuthenticatedUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            "Ana Pérez",
            "Cajero",
            [Permission.OpenRegisterSession]);

    private static ActiveRegisterSession CreateActiveRegisterSession() =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            "Caja 1",
            UserId.New(),
            "Ana Pérez",
            OpenedAtUtc,
            100m,
            "MXN");
}
