namespace Pos.Infrastructure.Persistence.Initialization;

// Resultado interno de PRAGMA integrity_check / foreign_key_check; no se expone fuera del inicializador.
internal sealed record DatabaseIntegrityStatus(bool IsHealthy, string? Detail);
