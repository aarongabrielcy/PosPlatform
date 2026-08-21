using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Enforcement;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales;
using Pos.Application.Users;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Security;
using DomainRegisterSession = Pos.Domain.RegisterSessions.RegisterSession;
using DomainRegisterSessionStatus = Pos.Domain.RegisterSessions.RegisterSessionStatus;

namespace Pos.Application.CashMovements;

// BASIC-CASH-01: entradas/salidas manuales de efectivo sobre una RegisterSession abierta. Sigue el
// mismo esqueleto de guardas que CheckoutService/RegisterSessionService.OpenAsync (enforcement
// primero, luego autenticación/autorización, luego reglas de negocio), y el mismo patrón de
// resultado tipado (CashMovementResult/CashMovementResultStatus) sin excepciones para fallos de
// negocio esperados.
public sealed class CashMovementService : ICashMovementService
{
    private const int MaxReasonLength = 200;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IRegisterSessionRepository _registerSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ICashMovementRepository _cashMovementRepository;
    private readonly IUserRepository _userRepository;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CashMovementService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IRegisterSessionRepository registerSessionRepository,
        ISaleRepository saleRepository,
        ICashMovementRepository cashMovementRepository,
        IUserRepository userRepository,
        IInstallationEnforcementStateService enforcementStateService,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _registerSessionRepository = registerSessionRepository ?? throw new ArgumentNullException(nameof(registerSessionRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _cashMovementRepository = cashMovementRepository ?? throw new ArgumentNullException(nameof(cashMovementRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<CashMovementResult> RecordCashInAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(CashMovementType.CashIn, request, cancellationToken);

    public Task<CashMovementResult> RecordCashOutAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
        RecordAsync(CashMovementType.CashOut, request, cancellationToken);

    public async Task<CashMovementListResult> GetCurrentSessionMovementsAsync(
        CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return CashMovementListResult.Failure(CashMovementResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ViewCashTotals))
        {
            return CashMovementListResult.Failure(CashMovementResultStatus.NotAuthorized);
        }

        var current = _currentRegisterSession.Current;

        if (current is null)
        {
            return CashMovementListResult.Failure(CashMovementResultStatus.SessionNotFound);
        }

        if (current.OrganizationId != user.OrganizationId)
        {
            return CashMovementListResult.Failure(CashMovementResultStatus.SessionBelongsToAnotherOrganization);
        }

        var movements = await _cashMovementRepository.GetByRegisterSessionAsync(
            current.RegisterSessionId, cancellationToken);

        var entries = new List<CashMovementEntry>(movements.Count);

        foreach (var movement in movements.OrderByDescending(m => m.CreatedAtUtc))
        {
            entries.Add(await ToEntryAsync(movement, user, cancellationToken));
        }

        return CashMovementListResult.SuccessResult(entries);
    }

    private async Task<CashMovementResult> RecordAsync(
        CashMovementType type, RecordCashMovementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Guarda de aplicación (igual que CheckoutService/RegisterSessionService.OpenAsync): un
        // movimiento de caja es una mutación comercial nueva, no una contención de estado existente
        // como CloseAsync, así que se bloquea bajo Suspended/CredentialInvalid/Decommissioned.
        if (_enforcementStateService.Current != InstallationEnforcementState.Allowed)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.InstallationRestricted);
        }

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.ManageCashMovements))
        {
            return CashMovementResult.Failure(CashMovementResultStatus.NotAuthorized);
        }

        if (request.Amount <= 0m)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.InvalidAmount);
        }

        var reason = request.Reason?.Trim();

        if (string.IsNullOrEmpty(reason) || reason.Length > MaxReasonLength)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.ReasonRequired);
        }

        var current = _currentRegisterSession.Current;

        if (current is null)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.SessionNotFound);
        }

        if (current.OrganizationId != user.OrganizationId)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.SessionBelongsToAnotherOrganization);
        }

        var session = await _registerSessionRepository.GetByIdAsync(current.RegisterSessionId, cancellationToken);

        if (session is null)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.SessionNotFound);
        }

        if (session.Status != DomainRegisterSessionStatus.Open)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.SessionAlreadyClosed);
        }

        if (type == CashMovementType.CashOut)
        {
            var expectedCash = await ComputeExpectedCashAsync(session, cancellationToken);

            if (request.Amount > expectedCash)
            {
                return CashMovementResult.Failure(CashMovementResultStatus.InsufficientExpectedCash);
            }
        }

        var now = _clock.UtcNow;
        CashMovement movement;

        try
        {
            var amount = new Money(request.Amount, session.OpeningFloat.Currency);
            movement = type == CashMovementType.CashIn
                ? CashMovement.CreateCashIn(CashMovementId.New(), session.Id, user.UserId, amount, reason, now)
                : CashMovement.CreateCashOut(CashMovementId.New(), session.Id, user.UserId, amount, reason, now);
        }
        catch (DomainValidationException)
        {
            return CashMovementResult.Failure(CashMovementResultStatus.InvalidAmount);
        }

        await _cashMovementRepository.AddAsync(movement, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var entry = await ToEntryAsync(movement, user, cancellationToken);

        return CashMovementResult.SuccessResult(entry);
    }

    // Misma fórmula que RegisterSessionService.CloseAsync/GetClosingSummaryAsync (sección 10 de la
    // tarea): OpeningFloat + ventas en efectivo completadas + CashIn - CashOut. Se duplica aquí en
    // vez de extraer una abstracción compartida porque ambos servicios ya duplicaban de forma
    // independiente las tres consultas de ISaleRepository antes de esta tarea (mismo criterio de
    // simplicidad que el resto del código base).
    private async Task<decimal> ComputeExpectedCashAsync(DomainRegisterSession session, CancellationToken cancellationToken)
    {
        var cashSalesTotal = await _saleRepository.GetCompletedCashTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var cashInTotal = await _cashMovementRepository.GetCashInTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var cashOutTotal = await _cashMovementRepository.GetCashOutTotalByRegisterSessionAsync(
            session.Id, cancellationToken);

        return session.OpeningFloat.Amount + cashSalesTotal + cashInTotal - cashOutTotal;
    }

    private async Task<CashMovementEntry> ToEntryAsync(
        CashMovement movement, AuthenticatedUser currentUser, CancellationToken cancellationToken)
    {
        var actorDisplayName = await ResolveDisplayNameAsync(movement.ActorUserId, currentUser, cancellationToken);

        return new CashMovementEntry(
            movement.Id.Value,
            movement.Type,
            movement.Amount.Amount,
            movement.Amount.Currency,
            movement.Reason,
            movement.ActorUserId.Value,
            actorDisplayName,
            movement.CreatedAtUtc);
    }

    private async Task<string> ResolveDisplayNameAsync(
        UserId userId, AuthenticatedUser currentUser, CancellationToken cancellationToken)
    {
        if (userId == currentUser.UserId)
        {
            return currentUser.DisplayName;
        }

        var actor = await _userRepository.GetByIdAsync(userId, cancellationToken);

        return actor?.DisplayName ?? "Usuario desconocido";
    }
}
