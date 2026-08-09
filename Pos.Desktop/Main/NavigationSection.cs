namespace Pos.Desktop.Main;

public enum NavigationSection
{
    Dashboard,
    // Ventas (TAREA 25B, sección 9): padre puro, igual patrón que Audit — no navega por sí mismo,
    // solo expande/colapsa sus hijos SalesPointOfSale/SalesHistory.
    Sales,
    SalesPointOfSale,
    SalesHistory,
    Products,
    Inventory,
    Register,
    Settings,
    Audit,
    AuditProducts,
}
