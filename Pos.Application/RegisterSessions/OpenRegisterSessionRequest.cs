using Pos.Domain.Common.Identifiers;

namespace Pos.Application.RegisterSessions;

// RegisterId es nulo cuando existe exactamente una caja activa disponible: el servicio la
// selecciona automáticamente. Con más de una caja activa, RegisterId es obligatorio.
public sealed record OpenRegisterSessionRequest(RegisterId? RegisterId, decimal OpeningAmount);
