namespace Pos.Hardware.EscPos;

// Estrategia de codificación elegida para BASIC-PRN-01 (sección 16 de la tarea): la mayoría de las
// impresoras térmicas ESC/POS de bajo costo (Epson TM-T20 y compatibles genéricos) no interpretan
// UTF-8 de forma nativa; la página de códigos IBM/DOS 850 ("Multilingual Latin-1") es la más
// ampliamente soportada para acentos y ñ en firmware ESC/POS genérico. Se implementa una tabla de
// traducción manual en vez de Encoding.GetEncoding(850): esa página de códigos requiere el paquete
// NuGet System.Text.Encoding.CodePages fuera de .NET Core por defecto, y Pos.Hardware no puede
// tener PackageReference alguno (ver CLAUDE.md sección C y
// Pos.Architecture.Tests.ProductionProjectsShouldNotContainPackageReferences). Cualquier carácter
// fuera de ASCII imprimible y de esta tabla se sustituye por '?' en vez de fallar: un ticket impreso
// con un carácter aproximado es preferible a un fallo de impresión (sección 29: nunca romper por un
// problema de hardware/formato recuperable).
internal static class Cp850SpanishEncoder
{
    private static readonly Dictionary<char, byte> Overrides = new()
    {
        ['á'] = 0xA0,
        ['é'] = 0x82,
        ['í'] = 0xA1,
        ['ó'] = 0xA2,
        ['ú'] = 0xA3,
        ['ñ'] = 0xA4,
        ['Ñ'] = 0xA5,
        ['¿'] = 0xA8,
        ['¡'] = 0xAD,
        ['Á'] = 0xB5,
        ['É'] = 0x90,
        ['Í'] = 0xD6,
        ['Ó'] = 0xE0,
        ['Ú'] = 0xE9,
        ['ü'] = 0x81,
        ['Ü'] = 0x9A,
    };

    public static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = new byte[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (Overrides.TryGetValue(c, out var mapped))
            {
                bytes[i] = mapped;
            }
            else if (c is >= (char)0x20 and <= (char)0x7E)
            {
                bytes[i] = (byte)c;
            }
            else
            {
                bytes[i] = (byte)'?';
            }
        }

        return bytes;
    }
}
