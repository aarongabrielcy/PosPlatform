namespace Pos.Hardware.Printing;

// Seam sobre winspool.drv (sección 44 de la tarea: "test without requiring a real physical
// printer"). WindowsSpoolReceiptPrinter solo conoce esta interfaz; Pos.Hardware.Tests sustituye
// Win32SpoolGateway por un fake que nunca toca el spooler real de Windows.
internal interface IWinSpoolGateway
{
    bool OpenPrinter(string printerName, out IntPtr handle);

    bool StartDocPrinter(IntPtr handle, string documentName, out int jobId);

    bool StartPagePrinter(IntPtr handle);

    bool WritePrinter(IntPtr handle, byte[] data, out int bytesWritten);

    bool EndPagePrinter(IntPtr handle);

    bool EndDocPrinter(IntPtr handle);

    bool ClosePrinter(IntPtr handle);

    int GetLastError();
}
