using System.Runtime.Versioning;
using Pos.Application.Receipts;

namespace Pos.Hardware.Printing;

// Implementación de IReceiptPrinter para BASIC-PRN-01 (sección 14 de la tarea): envía el payload
// ESC/POS ya formateado como un job RAW al spooler de Windows para la impresora configurada. Nunca
// lanza para fallas de hardware esperables (impresora apagada/sin papel/no instalada): todo se
// traduce a PrinterOutcome (sección 29). Enviar exitosamente al spooler se considera PrintSuccess
// para V1 (sección 30): no hay telemetría de "papel realmente salió".
[SupportedOSPlatform("windows")]
public sealed class WindowsSpoolReceiptPrinter : IReceiptPrinter
{
    private const string DocumentName = "Ticket PosPlatform";

    private readonly ReceiptPrinterOptions _options;
    private readonly IWinSpoolGateway _gateway;

    public WindowsSpoolReceiptPrinter(ReceiptPrinterOptions options)
        : this(options, new Win32SpoolGateway())
    {
    }

    internal WindowsSpoolReceiptPrinter(ReceiptPrinterOptions options, IWinSpoolGateway gateway)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public Task<PrinterOutcome> PrintAsync(FormattedReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.PrinterName))
        {
            return Task.FromResult(PrinterOutcome.NotConfigured());
        }

        return Task.FromResult(PrintCore(receipt));
    }

    private PrinterOutcome PrintCore(FormattedReceipt receipt)
    {
        var handle = IntPtr.Zero;
        var pageStarted = false;
        var docStarted = false;

        try
        {
            if (!_gateway.OpenPrinter(_options.PrinterName!, out handle))
            {
                return PrinterOutcome.Unavailable($"OpenPrinter falló (código {_gateway.GetLastError()}).");
            }

            if (!_gateway.StartDocPrinter(handle, DocumentName, out _))
            {
                return PrinterOutcome.Failed($"StartDocPrinter falló (código {_gateway.GetLastError()}).");
            }

            docStarted = true;

            if (!_gateway.StartPagePrinter(handle))
            {
                return PrinterOutcome.Failed($"StartPagePrinter falló (código {_gateway.GetLastError()}).");
            }

            pageStarted = true;

            var payload = receipt.Payload as byte[] ?? [.. receipt.Payload];

            if (!_gateway.WritePrinter(handle, payload, out var bytesWritten) || bytesWritten != payload.Length)
            {
                return PrinterOutcome.Failed($"WritePrinter falló (código {_gateway.GetLastError()}).");
            }

            return PrinterOutcome.Success();
        }
        catch (Exception ex)
        {
            return PrinterOutcome.Failed(ex.Message);
        }
        finally
        {
            if (pageStarted)
            {
                _gateway.EndPagePrinter(handle);
            }

            if (docStarted)
            {
                _gateway.EndDocPrinter(handle);
            }

            if (handle != IntPtr.Zero)
            {
                _gateway.ClosePrinter(handle);
            }
        }
    }
}
