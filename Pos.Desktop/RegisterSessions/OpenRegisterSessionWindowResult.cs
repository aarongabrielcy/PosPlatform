namespace Pos.Desktop.RegisterSessions;

// Distingue explícitamente los tres desenlaces posibles de OpenRegisterSessionWindow, ya que
// bool? DialogResult no alcanza a diferenciar un logout explícito de un cierre por la X: ambos
// dejan DialogResult en false/null según cómo se cierre la ventana.
public enum OpenRegisterSessionWindowResult
{
    ExitRequested,
    RegisterOpened,
    LogoutRequested,
}
