using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pos.Hardware.Printing;

// Envoltorio directo sobre winspool.drv (OpenPrinter/StartDocPrinter/WritePrinter/ClosePrinter) vía
// P/Invoke, en vez de un paquete NuGet como System.Drawing.Printing: Pos.Hardware no puede tener
// PackageReference alguno (ver CLAUDE.md sección C y
// Pos.Architecture.Tests.ProductionProjectsShouldNotContainPackageReferences), mismo criterio ya
// aplicado a DpapiProtector en Pos.Infrastructure. StartDocPrinter con pDatatype="RAW" entrega el
// payload ESC/POS al spooler sin reinterpretación (sección 14 de la tarea: "Windows Print Spooler →
// RAW printer job → ESC/POS payload").
[SupportedOSPlatform("windows")]
internal sealed class Win32SpoolGateway : IWinSpoolGateway
{
    public bool OpenPrinter(string printerName, out IntPtr handle) =>
        OpenPrinterNative(printerName, out handle, IntPtr.Zero);

    public bool StartDocPrinter(IntPtr handle, string documentName, out int jobId)
    {
        var docInfo = new DocInfo1
        {
            pDocName = documentName,
            pOutputFile = null,
            pDatatype = "RAW",
        };

        jobId = StartDocPrinterNative(handle, 1, ref docInfo);
        return jobId != 0;
    }

    public bool StartPagePrinter(IntPtr handle) => StartPagePrinterNative(handle);

    public bool WritePrinter(IntPtr handle, byte[] data, out int bytesWritten) =>
        WritePrinterNative(handle, data, data.Length, out bytesWritten);

    public bool EndPagePrinter(IntPtr handle) => EndPagePrinterNative(handle);

    public bool EndDocPrinter(IntPtr handle) => EndDocPrinterNative(handle);

    public bool ClosePrinter(IntPtr handle) => ClosePrinterNative(handle);

    public int GetLastError() => Marshal.GetLastWin32Error();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDatatype;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinterNative(string printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinterNative(IntPtr handle, int level, ref DocInfo1 docInfo);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinterNative(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinterNative(IntPtr handle, byte[] data, int bufferSize, out int bytesWritten);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinterNative(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinterNative(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinterNative(IntPtr handle);
}
