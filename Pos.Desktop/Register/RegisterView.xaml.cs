using System.Windows.Controls;

namespace Pos.Desktop.Register;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un RegisterViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }
}
