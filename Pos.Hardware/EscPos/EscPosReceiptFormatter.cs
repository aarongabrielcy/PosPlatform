using System.Globalization;
using Pos.Application.Receipts;
using Pos.Domain.Sales;

namespace Pos.Hardware.EscPos;

// Implementación concreta de IReceiptFormatter (sección 11/15 de la tarea): la única pieza que
// conoce ESC/POS dentro de la solución. BuildTextLines es internal y deliberadamente separado de
// Format para que las pruebas (Pos.Hardware.Tests) puedan verificar el contenido textual del ticket
// sin decodificar bytes ESC/POS (sección 42), mientras que Format/BuildPayload cubren por separado
// el comando binario (init/feed/corte) y la codificación (sección 45).
public sealed class EscPosReceiptFormatter : IReceiptFormatter
{
    // V1 fija un ancho por configuración (sección 17 de la tarea): 32 columnas para 58mm, 48 para
    // 80mm son los valores típicos de fuente A en impresoras térmicas genéricas. Sin motor de layout
    // visual: solo wrap de texto y alineación derecha de importes.
    private const int Columns58mm = 32;
    private const int Columns80mm = 48;

    private readonly ReceiptPrinterOptions _options;

    public EscPosReceiptFormatter(ReceiptPrinterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public FormattedReceipt Format(Receipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var columns = GetColumns(_options.PaperWidth);
        var lines = BuildTextLines(receipt, columns);

        return new FormattedReceipt(BuildPayload(lines, columns, _options.CutPaper));
    }

    internal static int GetColumns(ReceiptPaperWidth paperWidth) => paperWidth switch
    {
        ReceiptPaperWidth.Mm58 => Columns58mm,
        ReceiptPaperWidth.Mm80 => Columns80mm,
        _ => Columns80mm,
    };

    // Contenido textual completo del ticket, una entrada de lista por línea impresa (sin saltos de
    // línea embebidos). Nunca recalcula precios/nombres: todo proviene ya resuelto en Receipt
    // (snapshot histórico, ver ReceiptBuilder). El nombre/título se centran manualmente con
    // relleno de espacios (en vez de depender del comando ESC a) para que el contenido en sí ya sea
    // el que se verifica en las pruebas de formato (sección 42).
    internal static IReadOnlyList<string> BuildTextLines(Receipt receipt, int columns)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var result = new List<string>();
        result.AddRange(CenterWrapped(receipt.BusinessName, columns));
        result.AddRange(CenterWrapped("TICKET DE VENTA", columns));

        if (receipt.IsReprint)
        {
            result.AddRange(CenterWrapped("*** REIMPRESION ***", columns));
        }

        result.Add($"Venta: {receipt.SaleReference}");
        result.Add($"Fecha: {receipt.CompletedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
        result.Add($"Caja: {receipt.RegisterName}");
        result.Add($"Cajero: {receipt.CashierDisplayName}");
        result.Add(Divider(columns));

        foreach (var line in receipt.Lines)
        {
            result.AddRange(WrapText(line.ProductName, columns));

            var left = $"{FormatQuantity(line.Quantity)} x {FormatAmount(line.UnitPrice)}";
            var right = FormatAmount(line.LineTotal);
            result.AddRange(Justify(left, right, columns));
        }

        result.Add(Divider(columns));
        result.AddRange(Justify("Subtotal", $"{FormatAmount(receipt.Subtotal)} {receipt.Currency}", columns));
        result.AddRange(Justify("TOTAL", $"{FormatAmount(receipt.Total)} {receipt.Currency}", columns));
        result.Add(Divider(columns));

        foreach (var payment in receipt.Payments)
        {
            result.AddRange(Justify(ToMethodLabel(payment.Method), $"{FormatAmount(payment.Amount)} {receipt.Currency}", columns));

            if (!string.IsNullOrWhiteSpace(payment.Reference))
            {
                result.Add($"  Ref: {payment.Reference}");
            }
        }

        if (receipt.CashTendered is { } cashTendered)
        {
            result.AddRange(Justify("Efectivo recibido", $"{FormatAmount(cashTendered)} {receipt.Currency}", columns));
        }

        if (receipt.ChangeDue is { } changeDue)
        {
            result.AddRange(Justify("Cambio", $"{FormatAmount(changeDue)} {receipt.Currency}", columns));
        }

        result.Add(string.Empty);
        result.AddRange(CenterWrapped("Comprobante de venta - No es CFDI", columns));

        return result;
    }

    private static List<byte> BuildPayload(IReadOnlyList<string> lines, int columns, bool cutPaper)
    {
        var payload = new List<byte>();
        payload.AddRange(EscPosCommands.Initialize);
        payload.AddRange(EscPosCommands.SelectCodePage850);
        payload.AddRange(EscPosCommands.AlignLeft);

        foreach (var line in lines)
        {
            var emphasize = IsEmphasized(line);

            if (emphasize)
            {
                payload.AddRange(EscPosCommands.EmphasizeOn);
            }

            payload.AddRange(Cp850SpanishEncoder.Encode(line));

            if (emphasize)
            {
                payload.AddRange(EscPosCommands.EmphasizeOff);
            }

            payload.AddRange(EscPosCommands.LineFeed);
        }

        payload.AddRange(EscPosCommands.FeedLines(3));

        if (cutPaper)
        {
            payload.AddRange(EscPosCommands.PartialCut);
        }

        return payload;
    }

    // Solo el nombre del negocio, el título y la marca de REIMPRESIÓN/TOTAL se resaltan (sección 8
    // de la tarea): suficiente para que el ticket sea legible sin un motor de layout completo
    // (sección 17/31: no se requiere una vista previa visual ni un layout complejo).
    private static bool IsEmphasized(string line) =>
        line.Contains("REIMPRESION", StringComparison.Ordinal) || line.TrimStart().StartsWith("TOTAL", StringComparison.Ordinal);

    private static string ToMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.BankTransfer => "Transferencia bancaria",
        _ => method.ToString(),
    };

    private static string FormatQuantity(decimal quantity) => quantity.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatAmount(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

    private static string Divider(int columns) => new('-', columns);

    // Igual que Center, pero primero envuelve el texto en varias líneas si no cabe en una sola
    // (sección 17 de la tarea: ninguna línea debe exceder el ancho del papel, ni siquiera el
    // encabezado/pie de página en 58mm).
    private static List<string> CenterWrapped(string text, int columns) =>
        WrapText(text, columns).Select(line => Center(line, columns)).ToList();

    private static string Center(string text, int columns)
    {
        if (text.Length >= columns)
        {
            return text;
        }

        var totalPadding = columns - text.Length;
        var leftPadding = totalPadding / 2;

        return new string(' ', leftPadding) + text;
    }

    // Nunca trunca un importe (sección 17 de la tarea): si left+right no caben en una sola línea, el
    // importe se imprime en su propia línea alineado a la derecha, en vez de recortar.
    private static IReadOnlyList<string> Justify(string left, string right, int columns)
    {
        var combinedLength = left.Length + right.Length;

        if (combinedLength + 1 <= columns)
        {
            var padding = columns - combinedLength;
            return [left + new string(' ', padding) + right];
        }

        if (right.Length >= columns)
        {
            return [left, right];
        }

        return [left, new string(' ', columns - right.Length) + right];
    }

    // Wrap simple por palabras (sección 17: nunca un motor de layout complejo). Una palabra más
    // larga que el ancho disponible se corta duro para no exceder el ancho del papel.
    private static List<string> WrapText(string text, int columns)
    {
        if (text.Length <= columns)
        {
            return [text];
        }

        var result = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";

            if (candidate.Length <= columns)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                result.Add(current);
                current = string.Empty;
            }

            var remaining = word;

            while (remaining.Length > columns)
            {
                result.Add(remaining[..columns]);
                remaining = remaining[columns..];
            }

            current = remaining;
        }

        if (current.Length > 0)
        {
            result.Add(current);
        }

        return result.Count == 0 ? [string.Empty] : result;
    }
}
