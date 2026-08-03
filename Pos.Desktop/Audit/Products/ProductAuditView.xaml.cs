using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pos.Desktop.Audit.Products;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un ProductAuditViewModel: nunca se resuelve desde el contenedor de DI ni se construye
// manualmente.
public partial class ProductAuditView : UserControl
{
    // El texto "Select a date" del DatePickerTextBox interno de WPF es un valor fijo en inglés:
    // no depende de Thread.CurrentUICulture ni de FrameworkElement.Language (verificado), por lo
    // que no puede corregirse mediante recursos/cultura sin afectar el resto de la app. Se ajusta
    // puntualmente aquí, por reflexión, solo para los DatePicker de esta vista (TAREA 24D-FIX).
    private static readonly PropertyInfo? DatePickerTextBoxPartProperty =
        typeof(DatePicker).GetProperty("TextBox", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? WatermarkPropertyField =
        typeof(DatePickerTextBox).GetField("WatermarkProperty", BindingFlags.NonPublic | BindingFlags.Static);

    public ProductAuditView()
    {
        InitializeComponent();
    }

    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DatePicker datePicker
            || DatePickerTextBoxPartProperty?.GetValue(datePicker) is not DatePickerTextBox textBox
            || WatermarkPropertyField?.GetValue(null) is not DependencyProperty watermarkProperty)
        {
            return;
        }

        textBox.SetValue(watermarkProperty, "Seleccionar fecha");
    }
}
