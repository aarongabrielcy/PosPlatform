namespace Pos.Application.Activation;

// Almacenamiento local del secreto de activación (Installation Credential). La implementación de
// Pos.Infrastructure es responsable de protegerlo (no debe persistirse en texto plano); este
// puerto lo trata como un valor opaco.
public interface IInstallationCredentialStore
{
    Task<bool> SaveAsync(string credential, CancellationToken cancellationToken);

    Task<string?> TryLoadAsync(CancellationToken cancellationToken);
}
