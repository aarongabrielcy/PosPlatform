using Pos.Hardware.Printing;

namespace Pos.Hardware.Tests.Printing;

// Sustituye Win32SpoolGateway (que envuelve winspool.drv) para poder probar
// WindowsSpoolReceiptPrinter sin una impresora física ni Windows real (sección 44 de la tarea).
internal sealed class FakeWinSpoolGateway : IWinSpoolGateway
{
    private static readonly IntPtr FakeHandle = new(42);

    public bool OpenPrinterResult { get; set; } = true;

    public bool StartDocPrinterResult { get; set; } = true;

    public bool StartPagePrinterResult { get; set; } = true;

    public bool WritePrinterResult { get; set; } = true;

    // Cuando es null, WritePrinter reporta haber escrito exactamente el tamaño del buffer recibido
    // (caso feliz). Se puede forzar un valor distinto para simular una escritura parcial.
    public int? WritePrinterBytesWrittenOverride { get; set; }

    public bool EndPagePrinterResult { get; set; } = true;

    public bool EndDocPrinterResult { get; set; } = true;

    public bool ClosePrinterResult { get; set; } = true;

    public Exception? ThrowOnWritePrinter { get; set; }

    public string? LastPrinterName { get; private set; }

    public string? LastDocumentName { get; private set; }

    public byte[]? LastWrittenData { get; private set; }

    public int OpenPrinterCallCount { get; private set; }

    public int ClosePrinterCallCount { get; private set; }

    public int EndPagePrinterCallCount { get; private set; }

    public int EndDocPrinterCallCount { get; private set; }

    public bool OpenPrinter(string printerName, out IntPtr handle)
    {
        OpenPrinterCallCount++;
        LastPrinterName = printerName;
        handle = OpenPrinterResult ? FakeHandle : IntPtr.Zero;
        return OpenPrinterResult;
    }

    public bool StartDocPrinter(IntPtr handle, string documentName, out int jobId)
    {
        LastDocumentName = documentName;
        jobId = StartDocPrinterResult ? 1 : 0;
        return StartDocPrinterResult;
    }

    public bool StartPagePrinter(IntPtr handle) => StartPagePrinterResult;

    public bool WritePrinter(IntPtr handle, byte[] data, out int bytesWritten)
    {
        if (ThrowOnWritePrinter is not null)
        {
            throw ThrowOnWritePrinter;
        }

        LastWrittenData = data;
        bytesWritten = WritePrinterBytesWrittenOverride ?? (WritePrinterResult ? data.Length : 0);
        return WritePrinterResult;
    }

    public bool EndPagePrinter(IntPtr handle)
    {
        EndPagePrinterCallCount++;
        return EndPagePrinterResult;
    }

    public bool EndDocPrinter(IntPtr handle)
    {
        EndDocPrinterCallCount++;
        return EndDocPrinterResult;
    }

    public bool ClosePrinter(IntPtr handle)
    {
        ClosePrinterCallCount++;
        return ClosePrinterResult;
    }

    public int GetLastError() => 1234;
}
