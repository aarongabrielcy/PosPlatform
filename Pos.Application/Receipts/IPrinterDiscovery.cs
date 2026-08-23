namespace Pos.Application.Receipts;

// BASIC-CFG-01, sección 13/31: el usuario nunca debe escribir a mano el nombre exacto de la
// impresora de Windows. La implementación Windows (Pos.Hardware.Printing.WindowsPrinterDiscovery)
// enumera las impresoras instaladas vía winspool.drv; una falla de enumeración se traduce en una
// lista vacía, nunca en una excepción (sección 44: "discovery failure handled").
public interface IPrinterDiscovery
{
    Task<IReadOnlyList<string>> GetInstalledPrinterNamesAsync(CancellationToken cancellationToken = default);
}
