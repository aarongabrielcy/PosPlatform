using System.Runtime.Versioning;
using Pos.Application.Receipts;

namespace Pos.Hardware.Printing;

// Implementación Windows de IPrinterDiscovery (BASIC-CFG-01, sección 13/31): enumera las impresoras
// que Windows ya expone como instaladas (USB o Bluetooth emparejada, sin distinción - el transporte
// físico es un detalle de Windows, sección 4/14 de la tarea), para que Configuración > Impresora
// nunca pida al usuario escribir el nombre a mano. Cualquier falla de enumeración se traduce en una
// lista vacía en vez de una excepción (sección 44: "discovery failure handled").
[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterDiscovery : IPrinterDiscovery
{
    private readonly IPrinterEnumerationGateway _gateway;

    public WindowsPrinterDiscovery()
        : this(new Win32PrinterEnumerationGateway())
    {
    }

    internal WindowsPrinterDiscovery(IPrinterEnumerationGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public Task<IReadOnlyList<string>> GetInstalledPrinterNamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_gateway.GetInstalledPrinterNames());
        }
        catch (Exception)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
