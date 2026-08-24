namespace Pos.Infrastructure.Storage;

public interface IApplicationPathProvider
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string BackupDirectory { get; }

    // Fotografías de producto administradas (BASIC-UX-01, sección 7/46): archivos con nombre
    // generado por IProductImageStore, nunca la ruta original elegida por el usuario. Vive bajo
    // DataDirectory para sobrevivir reinicios/actualizaciones igual que el resto de datos locales.
    string ProductImagesDirectory { get; }

    // BASIC-REL-01, sección 12/34: registros de diagnóstico persistentes. Deliberadamente un
    // hermano de DataDirectory (PosPlatform\Logs, no PosPlatform\Data\Logs): los registros son datos
    // de soporte desechables según su propia política de retención (sección 36), no datos de negocio
    // que deban respaldarse junto con DataDirectory.
    string LogsDirectory { get; }

    void EnsureDataDirectoryExists();

    void EnsureBackupDirectoryExists();

    void EnsureProductImagesDirectoryExists();

    void EnsureLogsDirectoryExists();
}
