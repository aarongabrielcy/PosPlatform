using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Authentication;
using Pos.Desktop.Login;

namespace Pos.Desktop.Tests.Login;

public class LoginWindowPasswordVisibilityTests
{
    // LoginWindow y sus controles PasswordBox/TextBox solo pueden crearse en un hilo STA.
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

    private static LoginWindow CreateWindow()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(
                AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials)));
        var viewModel = new LoginViewModel(authService, NullLogger<LoginViewModel>.Instance);

        return new LoginWindow(viewModel);
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void PasswordFieldIsHiddenInitially() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();

            Assert.Equal(Visibility.Visible, window.PasswordBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.PasswordVisibleTextBox.Visibility);
        });

    [Fact]
    public void TogglingPasswordVisibilityRevealsTheSameValue() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            Click(window.PasswordVisibilityButton);

            Assert.Equal("Secreto123", window.PasswordVisibleTextBox.Text);
            Assert.Equal(Visibility.Collapsed, window.PasswordBox.Visibility);
            Assert.Equal(Visibility.Visible, window.PasswordVisibleTextBox.Visibility);
        });

    [Fact]
    public void TogglingPasswordVisibilityTwiceHidesItAgainWithoutLosingTheValue() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            Click(window.PasswordVisibilityButton);
            Click(window.PasswordVisibilityButton);

            Assert.Equal("Secreto123", window.PasswordBox.Password);
            Assert.Equal(string.Empty, window.PasswordVisibleTextBox.Text);
            Assert.Equal(Visibility.Visible, window.PasswordBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.PasswordVisibleTextBox.Visibility);
        });

    [Fact]
    public void SubmitReadsTheCurrentlyActiveHiddenControl() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            Assert.Equal("Secreto123", window.GetActivePasswordValue());
        });

    [Fact]
    public void TogglingVisibilityUpdatesTooltipAndAutomationName() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();

            Assert.Equal("Mostrar contraseña", window.PasswordVisibilityButton.ToolTip);

            Click(window.PasswordVisibilityButton);

            Assert.Equal("Ocultar contraseña", window.PasswordVisibilityButton.ToolTip);
            Assert.Equal(
                "Ocultar contraseña",
                System.Windows.Automation.AutomationProperties.GetName(window.PasswordVisibilityButton));
        });

    [Fact]
    public void ClosingTheWindowClearsBothHiddenAndVisibleControls() =>
        RunOnStaThread(() =>
        {
            var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";
            Click(window.PasswordVisibilityButton);

            window.Close();

            Assert.Equal(string.Empty, window.PasswordBox.Password);
            Assert.Equal(string.Empty, window.PasswordVisibleTextBox.Text);
        });

    [Fact]
    public void ViewModelDoesNotReceivePasswordBeforeSubmitIsClicked() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            var viewModel = Assert.IsType<LoginViewModel>(window.DataContext);
            viewModel.Username = "admin";

            // Sin SetPendingPassword, el ViewModel no tiene la contraseña: la validación debe
            // fallar exigiéndola, confirmando que no se copió antes de pulsar "Iniciar sesión".
            viewModel.LoginCommand.Execute(null);

            Assert.Equal("La contraseña es obligatoria.", viewModel.GeneralError);
        });
}
