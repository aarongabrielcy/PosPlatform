namespace Pos.Domain.Security;

public enum Permission
{
    ProcessSale,
    ApplyDiscount,
    CancelSale,
    ProcessReturn,
    OpenCashDrawer,
    OpenRegisterSession,
    CloseRegisterSession,
    ViewCashTotals,
    ManageCashMovements,
    ManageProducts,
    AdjustInventory,
    ViewReports,
    ManageUsers,
    ManageRoles,
    ViewProductAudit,
    ViewSalesHistory,
    ViewProducts,
    ViewInventory,
    ReprintReceipt,

    // BASIC-CFG-01: administración de la configuración local de la máquina (impresora/cajón).
    // Agregado al final del enum a propósito (los valores se persisten por nombre, ver RoleMapper,
    // así que el orden no importa para datos ya guardados). AdministrativePermissionSet.All() lo
    // recoge automáticamente para Administrator; Manager/Cashier NUNCA lo reciben (no se agrega a
    // StandardRoles.ManagerPermissions()/CashierPermissions()).
    ManageSettings,
}
