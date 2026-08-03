using System.Globalization;

namespace Pos.Application.ProductAudit;

// Conversión determinística de valores a string para ProductAuditChange.OldValue/NewValue (TAREA
// 24D, sección 7). La UI (Pos.Desktop) es responsable de traducir estos valores a texto legible
// (true/false -> Activo/Inactivo, etc.); aquí solo se persiste una representación estable.
internal static class ProductAuditValueFormatter
{
    // decimal conserva la escala con la que se construyó (p. ej. 10.000000m vs 10m tras un
    // viaje de ida y vuelta por una columna decimal(18,6)): "0.####################" recorta
    // ceros insignificantes para que el mismo valor lógico produzca siempre el mismo texto,
    // sin importar la escala interna con la que llegó desde Domain o desde la base de datos.
    public static string FormatDecimal(decimal value) =>
        value.ToString("0.####################", CultureInfo.InvariantCulture);

    public static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount.ToString("F2", CultureInfo.InvariantCulture)}";

    public static string FormatBool(bool value) => value ? "true" : "false";
}
