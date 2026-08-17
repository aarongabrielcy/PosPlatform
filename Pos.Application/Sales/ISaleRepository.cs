using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken);

    Task AddAsync(Sale sale, CancellationToken cancellationToken);

    Task UpdateAsync(Sale sale, CancellationToken cancellationToken);

    // Suma de Payments con Method Cash de todas las Sales Completed de la sesión de caja
    // indicada. Usada por RegisterSessionService.CloseAsync para calcular ExpectedCash sin
    // cargar los agregados Sale completos.
    Task<decimal> GetCompletedCashTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    // Suma de Payments con Method Card de todas las Sales Completed de la sesión de caja
    // indicada (TAREA 25C). Un pago Card nunca forma parte del efectivo físico del cajón
    // (RegisterSessionService.CloseAsync no lo suma a ExpectedCash); se usa únicamente para el
    // desglose del cierre de caja.
    Task<decimal> GetCompletedCardTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    // Suma de Sale.Total de todas las Sales Completed de la sesión de caja indicada (TAREA
    // 25C-FIX sección 8-10): fuente de "Ventas totales" (GrossSales), independiente del método de
    // pago. Nunca se deriva sumando totales por método (Cash + Card): Sale ya admite varios Payment
    // por venta y el dominio ya define métodos además de Cash/Card (p. ej. BankTransfer), así que
    // sumar solo los métodos con desglose propio subcontaría o duplicaría según cómo evolucione el
    // pago. Se calcula desde SaleLine (Sale.Total = suma de LineSubtotal), nunca desde Payment.
    Task<decimal> GetCompletedGrossTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken);
}
