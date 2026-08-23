using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

// Separado de IProductManagementService a propósito (BASIC-UX-01): IProductManagementService se
// registra dentro de Pos.Infrastructure.DependencyInjection.AddPosInfrastructure, cuya composición
// se valida de forma aislada (ValidateOnBuild) sin los registros exclusivos de Pos.Desktop. Su
// única implementación de IProductImageStore capaz de decodificar/normalizar imágenes usa APIs de
// WPF (ver WpfProductImageStore en Pos.Desktop), así que esta funcionalidad se registra únicamente
// en Pos.Desktop/App.xaml.cs — igual patrón que IReceiptPrintingService, que tampoco vive en
// AddPosInfrastructure por depender de tipos exclusivos de Pos.Hardware/Pos.Desktop.
public interface IProductPhotoService
{
    // Requiere ManageProducts, igual que ProductManagementService.UpdateAsync/SetActiveAsync.
    Task<UpdateProductResult> SetProductImageAsync(
        ProductId productId, byte[] imageContent, CancellationToken cancellationToken = default);

    Task<UpdateProductResult> RemoveProductImageAsync(
        ProductId productId, CancellationToken cancellationToken = default);
}
