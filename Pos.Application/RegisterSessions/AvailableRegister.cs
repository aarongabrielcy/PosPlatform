using Pos.Domain.Common.Identifiers;

namespace Pos.Application.RegisterSessions;

public sealed record AvailableRegister(RegisterId RegisterId, string Name);
