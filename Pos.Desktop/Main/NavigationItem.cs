namespace Pos.Desktop.Main;

// Ítem de la barra de navegación del shell: solo aparece en MainWindowViewModel.NavigationItems
// cuando el usuario actual tiene el permiso correspondiente (TAREA 24C, sección 23). Children
// soporta un único nivel de submenú (TAREA 24D, sección 23: Auditoría > Productos); un ítem con
// Children no navega directamente, solo expande/colapsa (ver MainWindowViewModel.SelectedNavigationItem).
public sealed record NavigationItem(NavigationSection Section, string Label, IReadOnlyList<NavigationItem>? Children = null)
{
    public bool HasChildren => Children is { Count: > 0 };
}
