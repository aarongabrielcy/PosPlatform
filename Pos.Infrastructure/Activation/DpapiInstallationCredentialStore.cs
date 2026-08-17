using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Infrastructure.Security;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Activation;

// Persiste la Installation Credential protegida con DPAPI (atada al perfil de usuario de Windows
// que ejecuta PosPlatform Desktop), en un archivo separado de las tablas de negocio de SQLite
// (ver sección 13/25 de la tarea: "no en tablas SQLite normales sin protección deliberada").
[SupportedOSPlatform("windows")]
public sealed partial class DpapiInstallationCredentialStore : IInstallationCredentialStore
{
    private const string ActivationFolderName = "Activation";
    private const string CredentialFileName = "credential.protected";

    private readonly IApplicationPathProvider _pathProvider;
    private readonly ILogger<DpapiInstallationCredentialStore> _logger;

    public DpapiInstallationCredentialStore(IApplicationPathProvider pathProvider, ILogger<DpapiInstallationCredentialStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SaveAsync(string credential, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);

        try
        {
            var directory = GetActivationDirectory();
            Directory.CreateDirectory(directory);

            var protectedBytes = DpapiProtector.Protect(Encoding.UTF8.GetBytes(credential));
            await File.WriteAllBytesAsync(GetCredentialFilePath(), protectedBytes, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogSaveFailed(_logger, ex);
            return false;
        }
    }

    public async Task<string?> TryLoadAsync(CancellationToken cancellationToken)
    {
        var filePath = GetCredentialFilePath();

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var plaintextBytes = DpapiProtector.Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            LogLoadFailed(_logger, ex);
            return null;
        }
    }

    private string GetActivationDirectory() => Path.Combine(_pathProvider.DataDirectory, ActivationFolderName);

    private string GetCredentialFilePath() => Path.Combine(GetActivationDirectory(), CredentialFileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible guardar la credencial de activación local.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible leer la credencial de activación local.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);
}
