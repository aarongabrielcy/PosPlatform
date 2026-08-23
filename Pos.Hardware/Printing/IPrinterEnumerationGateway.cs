namespace Pos.Hardware.Printing;

// Seam sobre winspool.drv (mismo criterio que IWinSpoolGateway): WindowsPrinterDiscovery solo
// conoce esta interfaz; Pos.Hardware.Tests sustituye Win32PrinterEnumerationGateway por un fake que
// nunca toca el spooler real de Windows (sección 44 de la tarea: "no physical printer required").
internal interface IPrinterEnumerationGateway
{
    IReadOnlyList<string> GetInstalledPrinterNames();
}
