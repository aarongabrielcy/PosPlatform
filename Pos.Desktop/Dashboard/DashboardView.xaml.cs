using System.Windows.Controls;

namespace Pos.Desktop.Dashboard;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un DashboardViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }
}
