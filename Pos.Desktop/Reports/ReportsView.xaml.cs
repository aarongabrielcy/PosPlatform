using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pos.Desktop.Reports;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel es
// un ReportsViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class ReportsView : UserControl
{
    // Mismo ajuste puntual por reflexión que InventoryView/ProductAuditView/SalesHistoryView: el
    // watermark "Select a date" del DatePickerTextBox interno de WPF no responde a Language/cultura.
    private static readonly PropertyInfo? DatePickerTextBoxPartProperty =
        typeof(DatePicker).GetProperty("TextBox", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? WatermarkPropertyField =
        typeof(DatePickerTextBox).GetField("WatermarkProperty", BindingFlags.NonPublic | BindingFlags.Static);

    public ReportsView()
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
