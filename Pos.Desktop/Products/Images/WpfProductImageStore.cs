using System.IO;
using System.Windows.Media.Imaging;
using Pos.Application.Products.ManageProduct;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop.Products.Images;

// Implementa IProductImageStore usando las APIs de imaging de WPF (PresentationCore), ya
// disponibles en Pos.Desktop (UseWPF=true) sin agregar ningún paquete NuGet nuevo (BASIC-UX-01,
// sección 10/11: "usar capacidades existentes de .NET/WPF"). Vive en Pos.Desktop, no en
// Pos.Infrastructure/Pos.Hardware, porque solo Pos.Desktop referencia WPF — igual motivo que
// IReceiptFormatter/IReceiptPrinter.
//
// Política de normalización (documentada explícitamente, sección 11):
// - Entrada máxima: 8 MB (antes de intentar decodificar).
// - Formatos aceptados: cualquiera que BitmapDecoder pueda decodificar de forma nativa (en la
//   práctica JPEG/PNG, sección 10) — la validez se determina intentando decodificar realmente el
//   contenido, nunca confiando en la extensión del archivo original.
// - Salida: siempre JPEG, lado mayor limitado a 800px (preserva proporción), calidad 85. Esto
//   evita que cámaras/fotos de alta resolución se conviertan en un problema de almacenamiento sin
//   control, manteniendo calidad visual razonable para una miniatura de POS.
// - Nombre de archivo: GUID generado por la aplicación + ".jpg", nunca el nombre original elegido
//   por el usuario (previene colisiones, caracteres inseguros y path traversal).
public sealed class WpfProductImageStore : IProductImageStore
{
    private const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxDimensionPixels = 800;
    private const int JpegQualityLevel = 85;

    private readonly IApplicationPathProvider _pathProvider;

    public WpfProductImageStore(IApplicationPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    }

    public Task<ProductImageStoreResult> SaveAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0 || content.Length > MaxInputBytes)
        {
            return Task.FromResult(ProductImageStoreResult.Failure(ProductImageStoreStatus.TooLarge));
        }

        BitmapFrame decodedFrame;

        try
        {
            using var inputStream = new MemoryStream(content, writable: false);
            var decoder = BitmapDecoder.Create(
                inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return Task.FromResult(ProductImageStoreResult.Failure(ProductImageStoreStatus.InvalidImage));
            }

            decodedFrame = decoder.Frames[0];
        }
        catch (Exception ex) when (
            ex is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            // El archivo no se pudo decodificar como imagen real: nunca se confía únicamente en la
            // extensión (sección 10/43).
            return Task.FromResult(ProductImageStoreResult.Failure(ProductImageStoreStatus.InvalidImage));
        }

        var normalized = Normalize(decodedFrame);

        var encoder = new JpegBitmapEncoder { QualityLevel = JpegQualityLevel };
        encoder.Frames.Add(BitmapFrame.Create(normalized));

        _pathProvider.EnsureProductImagesDirectoryExists();
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var fullPath = Path.Combine(_pathProvider.ProductImagesDirectory, fileName);

        using (var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            encoder.Save(outputStream);
        }

        return Task.FromResult(ProductImageStoreResult.SuccessResult(fileName));
    }

    public Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageFileName);

        try
        {
            var fullPath = ResolveManagedPath(imageFileName);

            if (fullPath is not null && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Best-effort (sección 15): un huérfano ocasional por un archivo bloqueado no es un
            // bloqueante para Basic V1.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.CompletedTask;
    }

    private static System.Windows.Media.Imaging.BitmapSource Normalize(BitmapFrame source)
    {
        var largestSide = Math.Max(source.PixelWidth, source.PixelHeight);

        if (largestSide <= MaxDimensionPixels)
        {
            return source;
        }

        var scale = (double)MaxDimensionPixels / largestSide;

        return new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
    }

    // Solo acepta nombres de archivo planos generados por esta misma clase: cualquier separador de
    // ruta o ".." se rechaza, para que un ImageFileName corrupto/manipulado nunca pueda escapar de
    // ProductImagesDirectory (sección 12/48 — path traversal imposible).
    private string? ResolveManagedPath(string imageFileName)
    {
        if (imageFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || imageFileName.Contains(".."))
        {
            return null;
        }

        return Path.Combine(_pathProvider.ProductImagesDirectory, imageFileName);
    }
}
