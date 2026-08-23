using Pos.Application.Authentication;
using Pos.Application.Receipts;
using Pos.Domain.Security;

namespace Pos.Application.Configuration;

public sealed class LocalSettingsService : ILocalSettingsService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly IReceiptPrinterOptionsProvider _printerOptionsProvider;

    public LocalSettingsService(
        ICurrentUserSession currentUserSession,
        ILocalSettingsStore localSettingsStore,
        IReceiptPrinterOptionsProvider printerOptionsProvider)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _localSettingsStore = localSettingsStore ?? throw new ArgumentNullException(nameof(localSettingsStore));
        _printerOptionsProvider = printerOptionsProvider ?? throw new ArgumentNullException(nameof(printerOptionsProvider));
    }

    public async Task<LocalSettingsSaveResult> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ManageSettings))
        {
            return LocalSettingsSaveResult.NotAuthorized;
        }

        var saved = await _localSettingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        if (!saved)
        {
            return LocalSettingsSaveResult.Failed;
        }

        // Sección 32/33: sin este refresh, el ticket seguiría usando la impresora/ancho anteriores
        // hasta reiniciar la app - Configuración pasaría a mostrar un valor que no coincide con lo
        // que la app realmente imprime.
        await _printerOptionsProvider.RefreshAsync(cancellationToken).ConfigureAwait(false);

        return LocalSettingsSaveResult.Saved;
    }
}
