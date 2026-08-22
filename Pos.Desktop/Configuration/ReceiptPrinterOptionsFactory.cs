using Microsoft.Extensions.Configuration;
using Pos.Application.Receipts;

namespace Pos.Desktop.Configuration
{
    // Lee la sección "ReceiptPrinter" de appsettings.json/appsettings.Local.json (sección 18/55 de
    // la tarea) leyendo claves individuales por índice, mismo criterio que
    // App.xaml.cs ya usa para "Activation:BaseUrl": Pos.Application no puede tener PackageReference
    // alguno (ver Pos.Architecture.Tests), así que no se usa IConfiguration.Get&lt;T&gt;()/Bind()
    // sobre ReceiptPrinterOptions, se construye manualmente. appsettings.json trae un default seguro
    // (Enabled=false): el nombre real de impresora es específico de cada máquina y debe
    // sobrescribirse en appsettings.Local.json (no versionado).
    internal static class ReceiptPrinterOptionsFactory
    {
        public static ReceiptPrinterOptions Create(IConfiguration configuration)
        {
            var section = configuration.GetSection("ReceiptPrinter");

            return new ReceiptPrinterOptions
            {
                Enabled = section.GetValue("Enabled", false),
                PrinterName = section["PrinterName"],
                PaperWidth = ParsePaperWidth(section["PaperWidth"]),
                AutoPrint = section.GetValue("AutoPrint", true),
                CutPaper = section.GetValue("CutPaper", false),
            };
        }

        private static ReceiptPaperWidth ParsePaperWidth(string? value) =>
            Enum.TryParse<ReceiptPaperWidth>(value, ignoreCase: true, out var parsed) ? parsed : ReceiptPaperWidth.Mm80;
    }
}
