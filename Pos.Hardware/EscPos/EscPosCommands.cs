namespace Pos.Hardware.EscPos;

// Subconjunto mínimo de comandos ESC/POS que necesita BASIC-PRN-01 (sección 15 de la tarea): no es
// un SDK ESC/POS completo. Selecciona la página de códigos 850 (ESC t 2) para que el firmware de la
// impresora interprete los mismos bytes que produce Cp850SpanishEncoder.
internal static class EscPosCommands
{
    public static readonly byte[] Initialize = [0x1B, 0x40];

    public static readonly byte[] SelectCodePage850 = [0x1B, 0x74, 0x02];

    public static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];

    public static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];

    public static readonly byte[] EmphasizeOn = [0x1B, 0x45, 0x01];

    public static readonly byte[] EmphasizeOff = [0x1B, 0x45, 0x00];

    public static readonly byte[] LineFeed = [0x0A];

    // GS V 1: corte parcial (deja una pequeña unión de papel). Se prefiere sobre el corte total
    // (GS V 0) por ser el comportamiento más común/seguro en impresoras retail genéricas cuando el
    // corte está habilitado por configuración (sección 15/33 de la tarea).
    public static readonly byte[] PartialCut = [0x1D, 0x56, 0x01];

    public static byte[] FeedLines(int lines)
    {
        if (lines <= 0)
        {
            return [];
        }

        return [0x1B, 0x64, (byte)lines];
    }
}
