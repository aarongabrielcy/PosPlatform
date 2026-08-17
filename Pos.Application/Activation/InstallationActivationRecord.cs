namespace Pos.Application.Activation;

// Metadatos no secretos de la activación en la nube: identifican la Installation ante pos-cloud
// pero no autentican nada por sí mismos. El secreto (Installation Credential) vive por separado
// en IInstallationCredentialStore.
public sealed record InstallationActivationRecord(string InstallationId, DateTimeOffset ActivatedAtUtc);
