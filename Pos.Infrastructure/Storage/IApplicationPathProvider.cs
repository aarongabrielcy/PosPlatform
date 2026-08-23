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

    void EnsureDataDirectoryExists();

    void EnsureBackupDirectoryExists();

    void EnsureProductImagesDirectoryExists();
}
