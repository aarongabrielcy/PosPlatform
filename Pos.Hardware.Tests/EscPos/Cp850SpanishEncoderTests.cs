using Pos.Hardware.EscPos;

namespace Pos.Hardware.Tests.EscPos;

// Prueba la estrategia de codificación elegida para BASIC-PRN-01 (sección 45 de la tarea):
// caracteres representativos del español mexicano se traducen a sus bytes CP850 documentados.
public sealed class Cp850SpanishEncoderTests
{
    [Theory]
    [InlineData('á', 0xA0)]
    [InlineData('é', 0x82)]
    [InlineData('í', 0xA1)]
    [InlineData('ó', 0xA2)]
    [InlineData('ú', 0xA3)]
    [InlineData('ñ', 0xA4)]
    [InlineData('Ñ', 0xA5)]
    [InlineData('¿', 0xA8)]
    [InlineData('¡', 0xAD)]
    [InlineData('Á', 0xB5)]
    [InlineData('É', 0x90)]
    [InlineData('Í', 0xD6)]
    [InlineData('Ó', 0xE0)]
    [InlineData('Ú', 0xE9)]
    public void EncodeMapsEachSpanishAccentedCharacterToItsCp850Byte(char character, byte expected)
    {
        var encoded = Cp850SpanishEncoder.Encode(character.ToString());

        Assert.Single(encoded);
        Assert.Equal(expected, encoded[0]);
    }

    [Fact]
    public void EncodePreservesPlainAsciiUnchanged()
    {
        var encoded = Cp850SpanishEncoder.Encode("Venta 123");

        Assert.Equal("Venta 123"u8.ToArray(), encoded);
    }

    [Fact]
    public void EncodeRepresentativeSpanishSentenceRoundTripsExpectedBytes()
    {
        var encoded = Cp850SpanishEncoder.Encode("Cajero: Ñoño Muñoz - código");

        // "Cajero: " (ASCII) + 'Ñ'(0xA5) + "o" + 'ñ'(0xA4) + "o Mu" + 'ñ'(0xA4) + "oz - c" + 'ó'(0xA2) + "digo"
        var expected = new List<byte>();
        expected.AddRange("Cajero: "u8.ToArray());
        expected.Add(0xA5);
        expected.AddRange("o"u8.ToArray());
        expected.Add(0xA4);
        expected.AddRange("o Mu"u8.ToArray());
        expected.Add(0xA4);
        expected.AddRange("oz - c"u8.ToArray());
        expected.Add(0xA2);
        expected.AddRange("digo"u8.ToArray());

        Assert.Equal(expected.ToArray(), encoded);
    }

    [Fact]
    public void EncodeUnsupportedCharacterFallsBackToQuestionMarkInsteadOfThrowing()
    {
        var encoded = Cp850SpanishEncoder.Encode("€");

        Assert.Equal((byte)'?', Assert.Single(encoded));
    }
}
