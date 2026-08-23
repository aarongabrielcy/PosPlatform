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
    // Reportes operativos (BASIC-RPT-01): un único módulo con tabs internas (Sales Summary,
    // Register Closures, Cash Movements, Product Sales, Low Stock, Operator Activity), no seis
    // entradas de navegación separadas (sección 7 de la tarea).
    Reports,
    // Configuración local de máquina (BASIC-CFG-01, sección 28-29): un ítem de navegación nuevo,
    // separado de "Usuarios" (que reutiliza NavigationSection.Settings desde BASIC-USR-01). No
    // reutiliza ese valor: "Settings" ya significa "Usuarios" en este enum.
    LocalConfiguration,
}
