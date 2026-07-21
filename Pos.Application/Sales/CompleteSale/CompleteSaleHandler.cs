using Pos.Application.Common.Exceptions;
using Pos.Application.Common.Persistence;
using Pos.Application.Inventory;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.CompleteSale;

public sealed class CompleteSaleHandler
{
    private readonly ISaleRepository _saleRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryMovementRepository _inventoryMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteSaleHandler(
        ISaleRepository saleRepository,
        IInventoryItemRepository inventoryItemRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _saleRepository = saleRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _inventoryMovementRepository = inventoryMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CompleteSaleResult> HandleAsync(
        CompleteSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureValidCommand(command);

        // FASE 1 — Lectura y validación.
        var sale = await _saleRepository.GetByIdAsync(command.SaleId, cancellationToken)
            ?? throw new EntityNotFoundException("Sale", command.SaleId.ToString());

        sale.EnsureCanComplete(command.CompletedAtUtc);

        var lines = sale.Lines;
        var preparedLines = new List<PreparedLine>(lines.Count);
        var seenInventoryItemIds = new HashSet<InventoryItemId>();

        foreach (var line in lines)
        {
            var inventoryItem = await _inventoryItemRepository.GetByBranchAndProductAsync(
                sale.BranchId, line.ProductId, cancellationToken)
                ?? throw new EntityNotFoundException(
                    "InventoryItem", $"BranchId '{sale.BranchId}', ProductId '{line.ProductId}'");

            if (inventoryItem.BranchId != sale.BranchId)
            {
                throw new DomainValidationException("InventoryItem.BranchId no coincide con Sale.BranchId.");
            }

            if (inventoryItem.ProductId != line.ProductId)
            {
                throw new DomainValidationException("InventoryItem.ProductId no coincide con SaleLine.ProductId.");
            }

            if (inventoryItem.Quantity < line.Quantity)
            {
                throw new DomainValidationException(
                    $"No hay existencia suficiente para el producto '{line.ProductId}'.");
            }

            if (!seenInventoryItemIds.Add(inventoryItem.Id))
            {
                throw new DomainValidationException(
                    "Se encontraron dos InventoryItem distintos con el mismo InventoryItemId.");
            }

            if (command.CompletedAtUtc < inventoryItem.UpdatedAtUtc)
            {
                throw new DomainValidationException(
                    $"CompletedAtUtc no puede ser anterior a InventoryItem.UpdatedAtUtc para el producto '{line.ProductId}'.");
            }

            preparedLines.Add(new PreparedLine(inventoryItem, line));
        }

        // FASE 2 — Creación de todos los movimientos (sin aplicar ni persistir).
        var preparedMovements = new List<PreparedMovement>(preparedLines.Count);

        foreach (var prepared in preparedLines)
        {
            var movement = InventoryMovement.CreateSaleDecrease(
                InventoryMovementId.New(),
                prepared.InventoryItem.Id,
                sale.BranchId,
                prepared.Line.ProductId,
                command.PerformedByUserId,
                sale.Id,
                prepared.Line.Id,
                prepared.Line.Quantity,
                prepared.InventoryItem.Quantity,
                command.CompletedAtUtc);

            preparedMovements.Add(new PreparedMovement(prepared.InventoryItem, prepared.Line, movement));
        }

        // FASE 3 — Mutación en memoria: aplicar todos los movimientos y completar la venta.
        foreach (var prepared in preparedMovements)
        {
            prepared.InventoryItem.ApplyMovement(prepared.Movement);
        }

        sale.Complete(command.CompletedAtUtc);

        // FASE 4 — Preparación de persistencia: ninguna escritura ocurrió antes de este punto.
        foreach (var prepared in preparedMovements)
        {
            await _inventoryItemRepository.UpdateAsync(prepared.InventoryItem, cancellationToken);
        }

        foreach (var prepared in preparedMovements)
        {
            await _inventoryMovementRepository.AddAsync(prepared.Movement, cancellationToken);
        }

        await _saleRepository.UpdateAsync(sale, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new CompleteSaleResult(
            sale.Id,
            sale.Status,
            sale.CompletedAtUtc!.Value,
            preparedMovements.Count);
    }

    private static void EnsureValidCommand(CompleteSaleCommand command)
    {
        if (command is null)
        {
            throw new DomainValidationException("command no puede ser nulo.");
        }

        if (command.SaleId.Value == Guid.Empty)
        {
            throw new DomainValidationException("SaleId no puede ser vacío.");
        }

        if (command.PerformedByUserId.Value == Guid.Empty)
        {
            throw new DomainValidationException("PerformedByUserId no puede ser vacío.");
        }

        if (command.CompletedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("CompletedAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }
    }

    private sealed record PreparedLine(InventoryItem InventoryItem, SaleLine Line);

    private sealed record PreparedMovement(InventoryItem InventoryItem, SaleLine Line, InventoryMovement Movement);
}
