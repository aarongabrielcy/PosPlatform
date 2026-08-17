namespace Pos.Application.Activation;

// Almacenamiento local de los metadatos no secretos de activación (InstallationActivationRecord).
public interface IInstallationActivationRecordStore
{
    Task<bool> SaveAsync(InstallationActivationRecord record, CancellationToken cancellationToken);

    Task<InstallationActivationRecord?> TryLoadAsync(CancellationToken cancellationToken);
}
