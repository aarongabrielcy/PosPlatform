using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pos.Hardware.Printing;

// Envoltorio directo sobre winspool.drv (EnumPrintersW) vía P/Invoke, mismo criterio que
// Win32SpoolGateway: Pos.Hardware no puede tener PackageReference alguno (ver CLAUDE.md sección C y
// Pos.Architecture.Tests.ProductionProjectsShouldNotContainPackageReferences), así que no se usa
// System.Drawing.Printing.PrinterSettings.InstalledPrinters (requiere el paquete
// System.Drawing.Common). PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS con Level 4 basta para
// listar el nombre de cada impresora instalada (local o conectada), sin requerir abrir cada una
// individualmente.
[SupportedOSPlatform("windows")]
internal sealed class Win32PrinterEnumerationGateway : IPrinterEnumerationGateway
{
    private const int PrinterEnumLocal = 0x00000002;
    private const int PrinterEnumConnections = 0x00000004;
    private const uint Level = 4;

    public IReadOnlyList<string> GetInstalledPrinterNames()
    {
        var flags = PrinterEnumLocal | PrinterEnumConnections;

        EnumPrintersNative(flags, null, Level, IntPtr.Zero, 0, out var neededBytes, out _);

        if (neededBytes <= 0)
        {
            return Array.Empty<string>();
        }

        var buffer = Marshal.AllocHGlobal(neededBytes);

        try
        {
            if (!EnumPrintersNative(flags, null, Level, buffer, neededBytes, out _, out var returned) || returned <= 0)
            {
                return Array.Empty<string>();
            }

            var structSize = Marshal.SizeOf<PrinterInfo4>();
            var names = new List<string>(returned);

            for (var i = 0; i < returned; i++)
            {
                var current = Marshal.PtrToStructure<PrinterInfo4>(buffer + (i * structSize));
                var name = Marshal.PtrToStringUni(current.PrinterName);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        public IntPtr PrinterName;
        public IntPtr ServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrintersNative(
        int flags, string? name, uint level, IntPtr printerEnum, int bufferSize, out int neededBytes, out int returned);
}
