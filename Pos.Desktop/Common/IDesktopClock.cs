namespace Pos.Desktop.Common;

// Hora local del sistema operativo para presentación en el header (BASIC-UX-01, sección 22/24):
// deliberadamente separado de Pos.Application.Common.Time.IClock, que expone UtcNow para
// persistencia/lógica de negocio. Esta abstracción existe solo para poder probar el formateo de
// MainWindowViewModel sin depender del reloj real de la máquina (sección 51).
public interface IDesktopClock
{
    DateTime Now { get; }
}
