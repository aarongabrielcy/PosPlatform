using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Desktop.Products;
using Pos.Desktop.Tests.Main;

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
        new(service, new FakeProductPhotoService(), new FakeCurrentSalesCart(), NullLogger<EditProductViewModel>.Instance);

    // TAREA 24B (corrección de layout): verifica que los cuatro botones del área inferior sigan
    // presentes y cableados a sus comandos tras reorganizar el StackPanel en un Grid con zonas
    // izquierda/derecha. La ventana nunca se muestra (Show()), así que WPF no evalúa los bindings;
    // se inspecciona la expresión declarada en XAML igual que en MainWindowTests.
    // TAREA 24C: SKU pasa de read-only a editable; verifica que el TextBox esté enlazado en modo
    // que permita escribir (TwoWay, valor por defecto de Binding sobre TextBox.Text).
    [Fact]
    public void SkuTextBoxIsBoundToTheSkuPropertyForEditing() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.SkuTextBox, TextBox.TextProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.Sku), binding.Path.Path);
            Assert.NotEqual(BindingMode.OneWay, binding.Mode);
        });

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
    public void CancelButtonIsBoundToTheCancelCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.CancelButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.CancelCommand), binding.Path.Path);
        });

    // ---------- Foto del producto (BASIC-UX-01, sección 55) ----------

    [Fact]
    public void SelectImageButtonIsBoundToTheSelectImageCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.SelectImageButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.SelectImageCommand), binding.Path.Path);
        });

    [Fact]
    public void RemoveImageButtonIsBoundToTheRemoveImageCommandProperty() =>
        RunOnStaThread(() =>
        {
            var viewModel = CreateViewModel(new FakeProductManagementService());
            var window = new EditProductWindow(viewModel);

            var binding = BindingOperations.GetBinding(window.RemoveImageButton, Button.CommandProperty);

            Assert.NotNull(binding);
            Assert.Equal(nameof(EditProductViewModel.RemoveImageCommand), binding.Path.Path);
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
