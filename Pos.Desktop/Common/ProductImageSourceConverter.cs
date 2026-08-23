using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop.Common;

// Convierte Product*.ImageFileName (string?, BASIC-UX-01) en un ImageSource listo para enlazar en
// un <Image>. Sin archivo (null/vacío), archivo faltante o corrupto simplemente devuelve null: el
// <Image> queda sin fuente y el placeholder declarado debajo en el mismo Grid/UserControl se ve a
// través (sección 20 — nunca lanza, nunca vuelve el catálogo inutilizable).
//
// DecodePixelWidth limita la decodificación a tamaño de miniatura y BitmapCacheOption.OnLoad
// libera el handle del archivo de inmediato tras decodificar (sección 19/56): evita mantener miles
// de imágenes a resolución completa en memoria o archivos abiertos mientras se navega el catálogo.
public sealed class ProductImageSourceConverter : IValueConverter
{
    private const int ThumbnailDecodePixelWidth = 96;

    // ApplicationPathProvider es una computación pura y barata (concatenación bajo LocalAppData):
    // se reutiliza la misma instancia en vez de inyectar dependencias en un value converter
    // declarado estáticamente en XAML. Tipo concreto (no la interfaz) para evitar una interfaz de
    // un solo miembro consumido en un único lugar (CA1859).
    private static readonly ApplicationPathProvider PathProvider = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string imageFileName || string.IsNullOrWhiteSpace(imageFileName))
        {
            return null;
        }

        if (imageFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || imageFileName.Contains(".."))
        {
            return null;
        }

        var fullPath = Path.Combine(PathProvider.ProductImagesDirectory, imageFileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = ThumbnailDecodePixelWidth;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (FileFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
