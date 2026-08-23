namespace Pos.Application.Products.ManageProduct;

// Abstrae el almacenamiento del archivo administrado de la foto de un producto (BASIC-UX-01,
// sección 7/8). La implementación real vive en Pos.Desktop (WpfProductImageStore) porque decodificar
// y normalizar la imagen usa las APIs de imaging de WPF ya disponibles ahí (PresentationCore, sin
// agregar ningún paquete NuGet nuevo) — igual patrón que IReceiptFormatter/IReceiptPrinter, cuyas
// implementaciones también viven fuera de Pos.Application/Pos.Infrastructure por la misma razón.
// Esta interfaz solo trabaja con bytes crudos: Pos.Application nunca referencia WPF.
public interface IProductImageStore
{
    // Valida que content sea una imagen JPEG/PNG decodificable (nunca confía solo en la extensión
    // del archivo original), normaliza el tamaño para uso como miniatura de POS y guarda una copia
    // administrada con un nombre de archivo generado por la implementación (nunca el nombre
    // original elegido por el usuario). No sobrescribe ningún archivo existente: cada llamada crea
    // un archivo nuevo, para que reemplazar una foto nunca deje al Product apuntando a un archivo
    // inexistente si la operación falla a medio camino (sección 13).
    Task<ProductImageStoreResult> SaveAsync(byte[] content, CancellationToken cancellationToken = default);

    // Best-effort: nunca lanza por un archivo ya ausente o bloqueado (sección 15 — un huérfano
    // ocasional tras una terminación anormal del proceso no es un bloqueante para Basic V1).
    Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default);
}

public enum ProductImageStoreStatus
{
    Success,
    InvalidImage,
    TooLarge,
}

public sealed class ProductImageStoreResult
{
    public ProductImageStoreStatus Status { get; }

    public bool Success => Status == ProductImageStoreStatus.Success;

    public string? ImageFileName { get; }

    private ProductImageStoreResult(ProductImageStoreStatus status, string? imageFileName)
    {
        Status = status;
        ImageFileName = imageFileName;
    }

    public static ProductImageStoreResult SuccessResult(string imageFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageFileName);

        return new ProductImageStoreResult(ProductImageStoreStatus.Success, imageFileName);
    }

    public static ProductImageStoreResult Failure(ProductImageStoreStatus status)
    {
        if (status == ProductImageStoreStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere ImageFileName; use SuccessResult.", nameof(status));
        }

        return new ProductImageStoreResult(status, null);
    }
}
