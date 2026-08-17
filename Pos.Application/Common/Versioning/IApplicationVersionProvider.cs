namespace Pos.Application.Common.Versioning;

// Expone la versión real de la aplicación Desktop en ejecución (ver Pos.Infrastructure, que la lee
// de los metadatos del ensamblado). Centralizado aquí porque el heartbeat de instalación no es el
// único consumidor concebible de este dato.
public interface IApplicationVersionProvider
{
    string GetVersion();
}
