using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Bootstrap;
using Pos.Desktop.Setup;

namespace Pos.Desktop.Tests.Setup;

public class InitialSetupWindowPasswordVisibilityTests
{
    // InitialSetupWindow y sus controles PasswordBox/TextBox solo pueden crearse en un hilo STA.
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

    private static InitialSetupWindow CreateWindow()
    {
        var bootstrapService = new FakeInitialBusinessBootstrapService(
            (_, _) => Task.FromResult(InitialBusinessBootstrapResult.AlreadyInitialized()));
        var viewModel = new InitialSetupViewModel(bootstrapService, NullLogger<InitialSetupViewModel>.Instance);

        return new InitialSetupWindow(viewModel);
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [Fact]
    public void PasswordFieldsAreHiddenInitially() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();

            Assert.Equal(Visibility.Visible, window.PasswordBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.PasswordVisibleTextBox.Visibility);
            Assert.Equal(Visibility.Visible, window.ConfirmPasswordBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.ConfirmPasswordVisibleTextBox.Visibility);
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
    public void ConfirmPasswordVisibilityIsIndependentFromPassword() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";
            window.ConfirmPasswordBox.Password = "Secreto123";

            Click(window.PasswordVisibilityButton);

            Assert.Equal(Visibility.Visible, window.PasswordVisibleTextBox.Visibility);
            Assert.Equal(Visibility.Visible, window.ConfirmPasswordBox.Visibility);
            Assert.Equal(Visibility.Collapsed, window.ConfirmPasswordVisibleTextBox.Visibility);
        });

    [Fact]
    public void SubmitReadsTheCurrentlyActiveHiddenControls() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";
            window.ConfirmPasswordBox.Password = "OtroValor456";

            Assert.Equal("Secreto123", window.GetActivePasswordValue());
            Assert.Equal("OtroValor456", window.GetActiveConfirmPasswordValue());
        });

    [Fact]
    public void SubmitReadsTheCurrentlyActiveVisibleControlsAfterEditingWhileVisible() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            Click(window.PasswordVisibilityButton);
            window.PasswordVisibleTextBox.Text = "NuevoValor789";

            Assert.Equal("NuevoValor789", window.GetActivePasswordValue());
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
            window.ConfirmPasswordBox.Password = "OtroValor456";
            Click(window.PasswordVisibilityButton);

            window.Close();

            Assert.Equal(string.Empty, window.PasswordBox.Password);
            Assert.Equal(string.Empty, window.PasswordVisibleTextBox.Text);
            Assert.Equal(string.Empty, window.ConfirmPasswordBox.Password);
            Assert.Equal(string.Empty, window.ConfirmPasswordVisibleTextBox.Text);
        });

    [Fact]
    public void ViewModelDoesNotReceiveCredentialsBeforeSubmitIsClicked() =>
        RunOnStaThread(() =>
        {
            using var window = CreateWindow();
            window.PasswordBox.Password = "Secreto123";

            var viewModel = Assert.IsType<InitialSetupViewModel>(window.DataContext);
            viewModel.OrganizationName = "Acme";
            viewModel.BranchName = "Main";
            viewModel.RegisterName = "Register 1";
            viewModel.AdministratorUsername = "admin";
            viewModel.AdministratorDisplayName = "Administrator";

            // Sin SetPendingCredentials, el ViewModel no tiene la contraseña: la validación debe
            // fallar exigiéndola, confirmando que no se copió antes de pulsar "Configurar".
            viewModel.SubmitCommand.Execute(null);

            Assert.Equal("La contraseña es obligatoria.", viewModel.GeneralError);
        });
}
