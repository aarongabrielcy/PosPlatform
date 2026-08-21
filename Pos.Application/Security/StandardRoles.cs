using Pos.Domain.Security;

namespace Pos.Application.Security;

// BASIC-USR-01: los tres niveles de capacidad congelados por el roadmap (OWNER/ADMIN, MANAGER,
// CASHIER). "Administrator" (creado por InitialBusinessBootstrapService con
// AdministrativePermissionSet.All()) YA es el nivel OWNER/ADMIN: no se crea un Role "Owner"
// adicional ni se renombra "Administrator" (evita una migración de datos innecesaria y preserva
// 100% de compatibilidad con instalaciones existentes - ver sección 5/8 de la tarea). Este tipo
// solo define los DOS roles nuevos (Manager/Cashier); Administrator sigue siendo responsabilidad
// exclusiva de AdministrativePermissionSet.
internal static class StandardRoles
{
    public const string ManagerRoleName = "Manager";

    public const string CashierRoleName = "Cashier";

    // Ventas, caja, productos, inventario, reportes operativos - NUNCA ManageUsers/ManageRoles
    // (sección 6 de la tarea: "NO user/role administration"). Incluye los permisos de solo lectura
    // (ViewSalesHistory/ViewProducts/ViewInventory) además de los de administración: Manager puede
    // hacer todo lo que Cashier puede (READ-ONLY-CORRECTION sección 3/17), más gestión real.
    public static Permission[] ManagerPermissions() =>
    [
        Permission.ProcessSale,
        Permission.ApplyDiscount,
        Permission.CancelSale,
        Permission.ProcessReturn,
        Permission.OpenCashDrawer,
        Permission.OpenRegisterSession,
        Permission.CloseRegisterSession,
        Permission.ViewCashTotals,
        Permission.ManageCashMovements,
        Permission.ManageProducts,
        Permission.AdjustInventory,
        Permission.ViewReports,
        Permission.ViewProductAudit,
        Permission.ViewSalesHistory,
        Permission.ViewProducts,
        Permission.ViewInventory,
    ];

    // Ventas y caja, más lectura operativa de Historial/Productos/Inventario - NUNCA
    // ManageProducts/AdjustInventory/ManageUsers/ViewReports (sección 6 de la tarea: cajero sin
    // administración de productos/inventario/usuarios y sin reportes administrativos). READ-ONLY
    // CORRECTION (sección 2/3): READ != MODIFY, así que ViewSalesHistory/ViewProducts/ViewInventory
    // se agregan sin tocar ninguno de los permisos de escritura, que Cashier sigue sin tener.
    public static Permission[] CashierPermissions() =>
    [
        Permission.ProcessSale,
        Permission.OpenCashDrawer,
        Permission.OpenRegisterSession,
        Permission.CloseRegisterSession,
        Permission.ViewSalesHistory,
        Permission.ViewProducts,
        Permission.ViewInventory,
    ];
}
