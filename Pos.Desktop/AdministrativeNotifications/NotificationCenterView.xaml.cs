using System.Windows.Controls;

namespace Pos.Desktop.AdministrativeNotifications;

// Panel lateral del centro de notificaciones (TAREA 24E, sección 25): se embebe directamente en
// MainWindow.xaml con DataContext="{Binding NotificationCenterViewModel}"; nunca se resuelve desde
// el contenedor de DI ni se abre como Window independiente.
public partial class NotificationCenterView : UserControl
{
    public NotificationCenterView()
    {
        InitializeComponent();
    }
}
