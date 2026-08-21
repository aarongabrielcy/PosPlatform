using Pos.Application.Authentication;
using Pos.Application.CashMovements;
using Pos.Application.Enforcement;
using Pos.Application.RegisterSessions;
using Pos.Application.Tests.Bootstrap;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.Enforcement;
using Pos.Application.Tests.RegisterSessions;
using FakeSaleRepository = Pos.Application.Tests.Sales.CompleteSale.FakeSaleRepository;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Organizations;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Tests.CashMovements;

public class CashMovementServiceTests
{
    private static readonly DateTimeOffset SessionOpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    // ---------- RecordCashInAsync: caso válido (sección 35) ----------

    [Fact]
    public async Task RecordCashInSucceedsForAnAuthorizedUserOnAnOpenSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(100m, "Reposición de efectivo"));

        Assert.True(result.Success);
        Assert.Equal(CashMovementType.CashIn, result.Movement!.Type);
        Assert.Equal(100m, result.Movement.Amount);
        Assert.Equal("Reposición de efectivo", result.Movement.Reason);
        Assert.Equal(1, fixture.CashMovementRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task RecordCashInDoesNotIncreaseGrossOrCashSalesTotals()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.SaleRepository.CompletedCashTotalToReturn = 200m;
        fixture.SaleRepository.CompletedGrossTotalToReturn = 200m;
        var service = fixture.BuildService();

        await service.RecordCashInAsync(new RecordCashMovementRequest(100m, "Reposición de efectivo"));

        // CashIn nunca escribe en ISaleRepository: el servicio solo lo consulta para el límite de
        // CashOut, nunca lo actualiza. GrossSales/CashSales siguen siendo responsabilidad exclusiva
        // de CheckoutService.
        Assert.Equal(0, fixture.SaleRepository.AddCallCount);
        Assert.Equal(0, fixture.SaleRepository.UpdateCallCount);
    }

    // ---------- RecordCashOutAsync: caso válido (sección 36) ----------

    [Fact]
    public async Task RecordCashOutSucceedsWhenWithinExpectedCash()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.SaleRepository.CompletedCashTotalToReturn = 300m;
        var service = fixture.BuildService();

        // ExpectedCash = OpeningFloat(100) + CashSales(300) = 400.
        var result = await service.RecordCashOutAsync(new RecordCashMovementRequest(200m, "Pago de mensajería"));

        Assert.True(result.Success);
        Assert.Equal(CashMovementType.CashOut, result.Movement!.Type);
        Assert.Equal(1, fixture.CashMovementRepository.AddCallCount);
    }

    // ---------- Monto inválido ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task RecordCashInRejectsZeroOrNegativeAmount(decimal amount)
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(amount, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.InvalidAmount, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- Reason en blanco rechazado ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordCashOutRejectsBlankReason(string reason)
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashOutAsync(new RecordCashMovementRequest(10m, reason));

        Assert.Equal(CashMovementResultStatus.ReasonRequired, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
    }

    [Fact]
    public async Task RecordCashInRejectsReasonLongerThan200Characters()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, new string('a', 201)));

        Assert.Equal(CashMovementResultStatus.ReasonRequired, result.Status);
    }

    // ---------- Requiere sesión abierta (sección 40) ----------

    [Fact]
    public async Task RecordCashInFailsWhenThereIsNoOpenSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.CurrentRegisterSession.Clear();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.SessionNotFound, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
    }

    [Fact]
    public async Task RecordCashOutFailsWhenThereIsNoOpenSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.CurrentRegisterSession.Clear();
        var service = fixture.BuildService();

        var result = await service.RecordCashOutAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.SessionNotFound, result.Status);
    }

    [Fact]
    public async Task RecordCashInFailsWhenTheCurrentSessionIsAlreadyClosed()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.Session.Close(fixture.User.Id, fixture.Session.OpeningFloat, fixture.Session.OpeningFloat, Now);
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.SessionAlreadyClosed, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
    }

    // ---------- Autorización (sección 41) ----------

    [Fact]
    public async Task AdminCanRecordCashInAndCashOut()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsAdmin();
        var service = fixture.BuildService();

        var cashIn = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));
        var cashOut = await service.RecordCashOutAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.True(cashIn.Success);
        Assert.True(cashOut.Success);
    }

    [Fact]
    public async Task ManagerCanRecordCashInAndCashOut()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var cashIn = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));
        var cashOut = await service.RecordCashOutAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.True(cashIn.Success);
        Assert.True(cashOut.Success);
    }

    [Fact]
    public async Task CashierCannotRecordCashInOrCashOut()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsCashier();
        var service = fixture.BuildService();

        var cashIn = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));
        var cashOut = await service.RecordCashOutAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.NotAuthorized, cashIn.Status);
        Assert.Equal(CashMovementResultStatus.NotAuthorized, cashOut.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
    }

    [Fact]
    public async Task UnauthenticatedUserCannotRecordCashIn()
    {
        var fixture = new Fixture();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.NotAuthenticated, result.Status);
    }

    // ---------- Installation Enforcement (sección 42) ----------

    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public async Task RecordCashInIsBlockedUnderRestrictiveEnforcementStates(InstallationEnforcementState state)
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.EnforcementStateService.SetCurrentForTest(state);
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(CashMovementResultStatus.InstallationRestricted, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task RecordCashInIsNotBlockedWhenInstallationIsAllowed()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        fixture.EnforcementStateService.SetCurrentForTest(InstallationEnforcementState.Allowed);
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.True(result.Success);
    }

    // ---------- Límite de CashOut (sección 39) ----------

    [Fact]
    public async Task CashOutEqualToExpectedCashIsAllowed()
    {
        var fixture = new Fixture(); // OpeningFloat = 100m, sin ventas ni movimientos previos.
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashOutAsync(new RecordCashMovementRequest(100m, "Retiro total"));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CashOutGreaterThanExpectedCashIsRejectedAndNothingIsPersisted()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashOutAsync(new RecordCashMovementRequest(100.01m, "Retiro excesivo"));

        Assert.Equal(CashMovementResultStatus.InsufficientExpectedCash, result.Status);
        Assert.Equal(0, fixture.CashMovementRepository.AddCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task CashOutLimitAccountsForPriorCashInAndCashOutMovements()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        // ExpectedCash = 100 (OpeningFloat) + 50 (CashIn previo) - 30 (CashOut previo) = 120.
        fixture.CashMovementRepository = new FakeCashMovementRepository(
        [
            CashMovement.CreateCashIn(
                CashMovementId.New(), fixture.Session.Id, fixture.User.Id, new Money(50m, "MXN"), "Motivo", SessionOpenedAtUtc),
            CashMovement.CreateCashOut(
                CashMovementId.New(), fixture.Session.Id, fixture.User.Id, new Money(30m, "MXN"), "Motivo", SessionOpenedAtUtc),
        ]);
        var service = fixture.BuildService();

        var allowed = await service.RecordCashOutAsync(new RecordCashMovementRequest(120m, "Retiro al límite"));

        Assert.True(allowed.Success);
    }

    // ---------- Atribución (sección 43) ----------

    [Fact]
    public async Task RecordedMovementStoresTheStableActorUserId()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        Assert.Equal(fixture.User.Id.Value, result.Movement!.ActorUserId);
        Assert.Equal(fixture.User.DisplayName, result.Movement.ActorDisplayName);
    }

    [Fact]
    public async Task GetCurrentSessionMovementsResolvesDisplayNameOfADifferentActorAndFallsBackWhenMissing()
    {
        var fixture = new Fixture();
        var otherUser = new User(
            UserId.New(), fixture.Organization.Id, fixture.ManagerRole.Id, "OTRO", "Otro Usuario",
            new PasswordHash(SyntheticPasswordHash), SessionOpenedAtUtc);
        var missingUserMovement = CashMovement.CreateCashOut(
            CashMovementId.New(), fixture.Session.Id, UserId.New(), new Money(5m, "MXN"), "Motivo", SessionOpenedAtUtc);
        var otherUserMovement = CashMovement.CreateCashIn(
            CashMovementId.New(), fixture.Session.Id, otherUser.Id, new Money(5m, "MXN"), "Motivo", SessionOpenedAtUtc);
        fixture.UserRepository = new FakeUserRepository([fixture.User, otherUser]);
        fixture.CashMovementRepository = new FakeCashMovementRepository([missingUserMovement, otherUserMovement]);
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();

        var result = await service.GetCurrentSessionMovementsAsync();

        Assert.True(result.Success);
        Assert.Equal("Otro Usuario", result.Movements!.Single(m => m.ActorUserId == otherUser.Id.Value).ActorDisplayName);
        Assert.Equal(
            "Usuario desconocido",
            result.Movements!.Single(m => m.ActorUserId != otherUser.Id.Value).ActorDisplayName);
    }

    // ---------- GetCurrentSessionMovementsAsync: acceso de lectura (sección 17) ----------

    [Fact]
    public async Task ManagerCanViewCurrentSessionMovements()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsManager();
        var service = fixture.BuildService();
        await service.RecordCashInAsync(new RecordCashMovementRequest(10m, "Motivo válido"));

        var result = await service.GetCurrentSessionMovementsAsync();

        Assert.True(result.Success);
        Assert.Single(result.Movements!);
    }

    [Fact]
    public async Task CashierCannotViewCurrentSessionMovements()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAsCashier();
        var service = fixture.BuildService();

        var result = await service.GetCurrentSessionMovementsAsync();

        Assert.Equal(CashMovementResultStatus.NotAuthorized, result.Status);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = new Organization(OrganizationId.New(), "Acme Retail", SessionOpenedAtUtc);
            ManagerRole = new Role(
                RoleId.New(), Organization.Id, "Manager", SessionOpenedAtUtc,
                [Permission.ManageCashMovements, Permission.ViewCashTotals]);
            AdminRole = new Role(
                RoleId.New(), Organization.Id, "Administrator", SessionOpenedAtUtc, Enum.GetValues<Permission>());
            CashierRole = new Role(
                RoleId.New(), Organization.Id, "Cashier", SessionOpenedAtUtc,
                [Permission.ProcessSale, Permission.CloseRegisterSession]);
            User = new User(
                UserId.New(), Organization.Id, ManagerRole.Id, "GERENTE", "Gerente Uno",
                new PasswordHash(SyntheticPasswordHash), SessionOpenedAtUtc);

            Session = new RegisterSession(
                RegisterSessionId.New(), RegisterId.New(), User.Id, new Money(100m, "MXN"), SessionOpenedAtUtc);

            CurrentUserSession = new FakeCurrentUserSession();
            CurrentRegisterSession = new FakeCurrentRegisterSession();
            CurrentRegisterSession.SetActiveSession(new ActiveRegisterSession(
                Session.Id, Organization.Id, BranchId.New(), Session.RegisterId, "Caja 1",
                User.Id, User.DisplayName, Session.OpenedAtUtc, Session.OpeningFloat.Amount, Session.OpeningFloat.Currency));

            RegisterSessionRepository = new FakeRegisterSessionRepository([Session]);
            SaleRepository = new FakeSaleRepository(null);
            CashMovementRepository = new FakeCashMovementRepository();
            UserRepository = new FakeUserRepository([User]);
            EnforcementStateService = new FakeInstallationEnforcementStateService();
            UnitOfWork = new FakeUnitOfWork();
            Clock = new FakeClock(Now);
        }

        public Organization Organization { get; }

        public Role ManagerRole { get; }

        public Role AdminRole { get; }

        public Role CashierRole { get; }

        public User User { get; }

        public RegisterSession Session { get; }

        public FakeCurrentUserSession CurrentUserSession { get; }

        public FakeCurrentRegisterSession CurrentRegisterSession { get; }

        public FakeRegisterSessionRepository RegisterSessionRepository { get; set; }

        public FakeSaleRepository SaleRepository { get; set; }

        public FakeCashMovementRepository CashMovementRepository { get; set; }

        public FakeUserRepository UserRepository { get; set; }

        public FakeInstallationEnforcementStateService EnforcementStateService { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public FakeClock Clock { get; }

        public void AuthenticateAsManager() => Authenticate(ManagerRole);

        public void AuthenticateAsAdmin() => Authenticate(AdminRole);

        public void AuthenticateAsCashier() => Authenticate(CashierRole);

        private void Authenticate(Role role) =>
            CurrentUserSession.CurrentUser = new AuthenticatedUser(
                User.Id, User.OrganizationId, role.Id, User.Username, User.DisplayName, role.Name, role.Permissions);

        public CashMovementService BuildService() =>
            new(
                CurrentUserSession,
                CurrentRegisterSession,
                RegisterSessionRepository,
                SaleRepository,
                CashMovementRepository,
                UserRepository,
                EnforcementStateService,
                UnitOfWork,
                Clock);
    }
}
