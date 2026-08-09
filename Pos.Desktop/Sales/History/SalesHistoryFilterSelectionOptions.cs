using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Sales.History;

// Wrapper Desktop-only para representar "Todos" (Id nulo) en los ComboBox de Cajero/Caja: los DTO
// de Application (SalesHistoryCashierOption/SalesHistoryRegisterOption) no modelan ese estado, y
// Application nunca debe conocer un concepto de presentación como "Todos".
public sealed record CashierFilterOption(UserId? UserId, string DisplayName)
{
    public static CashierFilterOption All { get; } = new(null, "Todos");
}

public sealed record RegisterFilterOption(RegisterId? RegisterId, string Name)
{
    public static RegisterFilterOption All { get; } = new(null, "Todos");
}
