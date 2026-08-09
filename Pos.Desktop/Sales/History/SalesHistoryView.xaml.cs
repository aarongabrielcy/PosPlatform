using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pos.Desktop.Sales.History;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un SalesHistoryViewModel: nunca se resuelve desde el contenedor de DI ni se construye
// manualmente.
public partial class SalesHistoryView : UserControl
{
    // Mismo ajuste puntual que ProductAuditView (TAREA 24D-FIX): el watermark en inglés del
    // DatePickerTextBox interno de WPF no depende de Language/CurrentUICulture.
    private static readonly PropertyInfo? DatePickerTextBoxPartProperty =
        typeof(DatePicker).GetProperty("TextBox", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? WatermarkPropertyField =
        typeof(DatePickerTextBox).GetField("WatermarkProperty", BindingFlags.NonPublic | BindingFlags.Static);

    public SalesHistoryView()
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
