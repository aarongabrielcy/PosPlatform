using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Sales.History;

// Opciones reales para los dropdowns Cajero/Caja (TAREA 25B, sección 13/30): se derivan de los
// cajeros/registers que efectivamente tienen ventas Completed en la Organization actual, nunca de
// la lista completa de usuarios/registers (evita exponer cajeros/cajas de otra Branch/Organization
// o que nunca vendieron). El método de pago no necesita consulta: se enumera directamente el enum
// PaymentMethod real (sección 13), sin depender de datos existentes.
public sealed class SalesHistoryFilterOptions
{
    public IReadOnlyList<SalesHistoryCashierOption> Cashiers { get; }

    public IReadOnlyList<SalesHistoryRegisterOption> Registers { get; }

    public SalesHistoryFilterOptions(
        IReadOnlyList<SalesHistoryCashierOption> cashiers, IReadOnlyList<SalesHistoryRegisterOption> registers)
    {
        ArgumentNullException.ThrowIfNull(cashiers);
        ArgumentNullException.ThrowIfNull(registers);

        Cashiers = cashiers;
        Registers = registers;
    }

    public static SalesHistoryFilterOptions Empty { get; } =
        new(Array.Empty<SalesHistoryCashierOption>(), Array.Empty<SalesHistoryRegisterOption>());
}

public sealed record SalesHistoryCashierOption(UserId UserId, string DisplayName);

public sealed record SalesHistoryRegisterOption(RegisterId RegisterId, string Name);
