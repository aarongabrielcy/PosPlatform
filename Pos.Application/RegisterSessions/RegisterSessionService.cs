using Pos.Application.Authentication;
using Pos.Application.Branches;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Enforcement;
using Pos.Application.Organizations;
using Pos.Application.Registers;
using Pos.Application.Sales;
using Pos.Application.Users;
using Pos.Domain.Branches;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Registers;
using Pos.Domain.Security;
using DomainRegisterSession = Pos.Domain.RegisterSessions.RegisterSession;
using DomainRegisterSessionStatus = Pos.Domain.RegisterSessions.RegisterSessionStatus;

namespace Pos.Application.RegisterSessions;

public sealed class RegisterSessionService : IRegisterSessionService
{
    // No existe todavía una configuración de moneda por instalación/organización: Money exige
    // una moneda en cada apertura y el modelo actual no ofrece otra fuente. Se fija un valor
    // único hasta que el sistema soporte moneda configurable.
    private const string DefaultCurrency = "MXN";

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IRegisterRepository _registerRepository;
    private readonly IRegisterSessionRepository _registerSessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterSessionService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IOrganizationRepository organizationRepository,
        IBranchRepository branchRepository,
        IRegisterRepository registerRepository,
        IRegisterSessionRepository registerSessionRepository,
        IUserRepository userRepository,
        ISaleRepository saleRepository,
        IInstallationEnforcementStateService enforcementStateService,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _registerSessionRepository = registerSessionRepository ?? throw new ArgumentNullException(nameof(registerSessionRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<RegisterSessionStatusResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return RegisterSessionStatusResult.InvalidState();
        }

        if (!await IsSingleValidOrganizationAsync(user.OrganizationId, cancellationToken))
        {
            return RegisterSessionStatusResult.InvalidState();
        }

        var branches = await _branchRepository.GetByOrganizationAsync(user.OrganizationId, cancellationToken);
        var activeBranches = branches.Where(b => b.IsActive).ToList();

        DomainRegisterSession? openSession = null;
        Register? openRegister = null;

        foreach (var branch in activeBranches)
        {
            var registers = await _registerRepository.GetByBranchAsync(branch.Id, cancellationToken);

            foreach (var register in registers)
            {
                DomainRegisterSession? candidate;

                try
                {
                    candidate = await _registerSessionRepository.GetOpenByRegisterAsync(register.Id, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return RegisterSessionStatusResult.InvalidState();
                }

                if (candidate is null)
                {
                    continue;
                }

                if (candidate.RegisterId != register.Id || openSession is not null)
                {
                    return RegisterSessionStatusResult.InvalidState();
                }

                openSession = candidate;
                openRegister = register;
            }
        }

        if (openSession is null || openRegister is null)
        {
            return RegisterSessionStatusResult.NoneOpen();
        }

        var branchOfRegister = activeBranches.SingleOrDefault(b => b.Id == openRegister.BranchId);

        if (branchOfRegister is null)
        {
            return RegisterSessionStatusResult.InvalidState();
        }

        var openedByDisplayName = await ResolveDisplayNameAsync(openSession.OpenedByUserId, user, cancellationToken);

        var activeSession = new ActiveRegisterSession(
            openSession.Id,
            user.OrganizationId,
            branchOfRegister.Id,
            openRegister.Id,
            openRegister.Name,
            openSession.OpenedByUserId,
            openedByDisplayName,
            openSession.OpenedAtUtc,
            openSession.OpeningFloat.Amount,
            openSession.OpeningFloat.Currency);

        _currentRegisterSession.SetActiveSession(activeSession);

        return RegisterSessionStatusResult.Open(activeSession);
    }

    public async Task<IReadOnlyList<AvailableRegister>> GetAvailableRegistersAsync(
        CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !await IsSingleValidOrganizationAsync(user.OrganizationId, cancellationToken))
        {
            return Array.Empty<AvailableRegister>();
        }

        var activeRegisters = await GetActiveRegistersAsync(user.OrganizationId, cancellationToken);

        return activeRegisters.Select(r => new AvailableRegister(r.Id, r.Name)).ToList();
    }

    public async Task<RegisterSessionResult> OpenAsync(
        OpenRegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_enforcementStateService.Current != InstallationEnforcementState.Allowed)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InstallationRestricted);
        }

        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.OpenRegisterSession))
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.NotAuthorized);
        }

        if (request.OpeningAmount < 0m)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidAmount);
        }

        if (!await IsSingleValidOrganizationAsync(user.OrganizationId, cancellationToken))
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidInstallationState);
        }

        var branches = await _branchRepository.GetByOrganizationAsync(user.OrganizationId, cancellationToken);
        var activeBranchesById = branches.Where(b => b.IsActive).ToDictionary(b => b.Id);

        Register targetRegister;

        if (request.RegisterId is { } requestedRegisterId)
        {
            var requested = await _registerRepository.GetByIdAsync(requestedRegisterId, cancellationToken);

            if (requested is null || !activeBranchesById.ContainsKey(requested.BranchId))
            {
                return RegisterSessionResult.Failure(RegisterSessionResultStatus.RegisterNotFound);
            }

            if (!requested.IsActive)
            {
                return RegisterSessionResult.Failure(RegisterSessionResultStatus.RegisterInactive);
            }

            targetRegister = requested;
        }
        else
        {
            var activeRegisters = await GetActiveRegistersForBranchesAsync(activeBranchesById.Keys, cancellationToken);

            if (activeRegisters.Count == 0)
            {
                return RegisterSessionResult.Failure(RegisterSessionResultStatus.RegisterNotFound);
            }

            if (activeRegisters.Count > 1)
            {
                return RegisterSessionResult.Failure(RegisterSessionResultStatus.RegisterSelectionRequired);
            }

            targetRegister = activeRegisters[0];
        }

        var existingOpenSession = await _registerSessionRepository.GetOpenByRegisterAsync(
            targetRegister.Id, cancellationToken);

        if (existingOpenSession is not null)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.AlreadyOpen);
        }

        var now = _clock.UtcNow;
        DomainRegisterSession session;

        try
        {
            var openingFloat = new Money(request.OpeningAmount, DefaultCurrency);
            session = new DomainRegisterSession(
                RegisterSessionId.New(), targetRegister.Id, user.UserId, openingFloat, now);
        }
        catch (DomainValidationException)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidAmount);
        }

        await _registerSessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var branchOfRegister = activeBranchesById[targetRegister.BranchId];

        var activeSession = new ActiveRegisterSession(
            session.Id,
            user.OrganizationId,
            branchOfRegister.Id,
            targetRegister.Id,
            targetRegister.Name,
            user.UserId,
            user.DisplayName,
            session.OpenedAtUtc,
            session.OpeningFloat.Amount,
            session.OpeningFloat.Currency);

        _currentRegisterSession.SetActiveSession(activeSession);

        return RegisterSessionResult.OpenSuccess(activeSession);
    }

    public async Task<RegisterSessionResult> CloseAsync(
        CloseRegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A diferencia de OpenAsync, CloseAsync no se bloquea por un estado de enforcement
        // restrictivo (Suspended/CredentialInvalid/Decommissioned): cerrar una caja ya abierta es
        // contención/reconciliación de un estado de negocio existente, no una nueva operación
        // comercial, y dejar la caja atrapada exigiría intervención manual fuera de la aplicación
        // (corrección de TAREA de "Close Register" bajo Installation Enforcement). El enforcement
        // sigue vigente para OpenAsync y para el resto de mutaciones nuevas.
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.CloseRegisterSession))
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.NotAuthorized);
        }

        if (request.ClosingAmount < 0m)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidAmount);
        }

        var current = _currentRegisterSession.Current;

        if (current is null)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionNotFound);
        }

        if (current.OrganizationId != user.OrganizationId)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionBelongsToAnotherOrganization);
        }

        var session = await _registerSessionRepository.GetByIdAsync(current.RegisterSessionId, cancellationToken);

        if (session is null)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionNotFound);
        }

        if (session.Status != DomainRegisterSessionStatus.Open)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionAlreadyClosed);
        }

        var now = _clock.UtcNow;

        // ExpectedCash = OpeningFloat + ventas en efectivo completadas durante esta sesión de
        // caja. No existe todavía un ledger de movimientos de caja (entradas/salidas manuales):
        // solo se suman las ventas Cash ya persistidas, tal como exige TAREA 25A sección 23. Las
        // ventas Card (TAREA 25C) nunca se suman: no son efectivo físico en el cajón.
        var cashSalesTotal = await _saleRepository.GetCompletedCashTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var cardSalesTotal = await _saleRepository.GetCompletedCardTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        // GrossSales se calcula desde Sale.Total (vía SaleLine), nunca sumando CashSales + CardSales
        // (TAREA 25C-FIX sección 8-10): Sale ya admite varios Payment y el dominio ya define métodos
        // además de Cash/Card, así que derivarlo de los desgloses por método subcontaría/duplicaría.
        var grossSalesTotal = await _saleRepository.GetCompletedGrossTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var expectedCash = new Money(session.OpeningFloat.Amount + cashSalesTotal, session.OpeningFloat.Currency);

        try
        {
            var countedCash = new Money(request.ClosingAmount, session.OpeningFloat.Currency);
            session.Close(user.UserId, expectedCash, countedCash, now);
        }
        catch (DomainValidationException)
        {
            return RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidAmount);
        }

        await _registerSessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        _currentRegisterSession.Clear();

        var summary = new RegisterSessionSummary(
            current.RegisterName,
            session.OpenedAtUtc,
            session.ClosedAtUtc!.Value,
            current.OpenedByDisplayName,
            user.DisplayName,
            session.OpeningFloat.Amount,
            cashSalesTotal,
            cardSalesTotal,
            grossSalesTotal,
            session.CountedCash!.Amount,
            session.ExpectedCash!.Amount,
            session.CashDifference!.Amount,
            session.OpeningFloat.Currency);

        return RegisterSessionResult.CloseSuccess(summary);
    }

    public async Task<RegisterClosingSummaryResult> GetClosingSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.NotAuthenticated);
        }

        if (!user.HasPermission(Permission.CloseRegisterSession))
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.NotAuthorized);
        }

        var current = _currentRegisterSession.Current;

        if (current is null)
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.SessionNotFound);
        }

        if (current.OrganizationId != user.OrganizationId)
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.SessionBelongsToAnotherOrganization);
        }

        var session = await _registerSessionRepository.GetByIdAsync(current.RegisterSessionId, cancellationToken);

        if (session is null)
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.SessionNotFound);
        }

        if (session.Status != DomainRegisterSessionStatus.Open)
        {
            return RegisterClosingSummaryResult.Failure(RegisterSessionResultStatus.SessionAlreadyClosed);
        }

        var cashSalesTotal = await _saleRepository.GetCompletedCashTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var cardSalesTotal = await _saleRepository.GetCompletedCardTotalByRegisterSessionAsync(
            session.Id, cancellationToken);
        var grossSalesTotal = await _saleRepository.GetCompletedGrossTotalByRegisterSessionAsync(
            session.Id, cancellationToken);

        var summary = new RegisterClosingSummary(
            session.OpeningFloat.Amount,
            cashSalesTotal,
            cardSalesTotal,
            grossSalesTotal,
            session.OpeningFloat.Amount + cashSalesTotal,
            session.OpeningFloat.Currency);

        return RegisterClosingSummaryResult.SuccessResult(summary);
    }

    private async Task<bool> IsSingleValidOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        return organizations.Count == 1 && organizations[0].Id == organizationId;
    }

    private async Task<List<Register>> GetActiveRegistersAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var activeBranchIds = branches.Where(b => b.IsActive).Select(b => b.Id);

        return await GetActiveRegistersForBranchesAsync(activeBranchIds, cancellationToken);
    }

    private async Task<List<Register>> GetActiveRegistersForBranchesAsync(
        IEnumerable<BranchId> branchIds, CancellationToken cancellationToken)
    {
        var result = new List<Register>();

        foreach (var branchId in branchIds)
        {
            var registers = await _registerRepository.GetByBranchAsync(branchId, cancellationToken);
            result.AddRange(registers.Where(r => r.IsActive));
        }

        return result;
    }

    private async Task<string> ResolveDisplayNameAsync(
        UserId userId, AuthenticatedUser currentUser, CancellationToken cancellationToken)
    {
        if (userId == currentUser.UserId)
        {
            return currentUser.DisplayName;
        }

        var openedByUser = await _userRepository.GetByIdAsync(userId, cancellationToken);

        return openedByUser?.DisplayName ?? "Usuario desconocido";
    }
}
