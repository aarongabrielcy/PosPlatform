using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Desktop.Products;

namespace Pos.Desktop.Tests.Products;

public class EditProductWindowTests
{
    // EditProductWindow solo puede crearse en un hilo STA.
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

    private static EditProductViewModel CreateViewModel(FakeProductManagementService service) =>
        new(service, NullLogger<EditProductViewModel>.Instance);

    // TAREA 24B (corrección de layout): verifica que los cuatro botones del área inferior sigan
    // presentes y cableados a sus comandos tras reorganizar el StackPanel en un Grid con zonas
    // izquierda/derecha. La ventana nunca se muestra (Show()), así que WPF no evalúa los bindings;
    // se inspecciona la expresión declarada en XAML igual que en MainWindowTests.
    [Fact]
    public void ToggleActiveButtonIsBoundToTheToggleActiveCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.ToggleActiveButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.ToggleActiveCommand), binding.Path.Path);
        });

    [Fact]
    public void AdjustInventoryButtonIsBoundToTheAdjustInventoryCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.AdjustInventoryButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.AdjustInventoryCommand), binding.Path.Path);
        });

    [Fact]
    public void CancelButtonIsBoundToTheCancelCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.CancelButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.CancelCommand), binding.Path.Path);
        });

    [Fact]
    public void SaveButtonIsBoundToTheSaveCommandPropertyAndIsThePrimaryAction() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.SaveButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.SaveCommand), binding.Path.Path);
            Assert.True(window.SaveButton.IsDefault);
            Assert.Equal("Guardar cambios", window.SaveButton.Content);
        });
}
