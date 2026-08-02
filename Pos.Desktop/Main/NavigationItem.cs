namespace Pos.Desktop.Main;

// Ítem de la barra de navegación del shell: solo aparece en MainWindowViewModel.NavigationItems
// cuando el usuario actual tiene el permiso correspondiente (TAREA 24C, sección 23).
public sealed record NavigationItem(NavigationSection Section, string Label);
