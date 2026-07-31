using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.Tests.Bootstrap;
using Pos.Application.Tests.Common.Time;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Organizations;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Registers;
using Pos.Domain.Security;
using Pos.Domain.Users;
using RegisterSessionStatus = Pos.Application.RegisterSessions.RegisterSessionStatus;

namespace Pos.Application.Tests.RegisterSessions;

public class RegisterSessionServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    // ---------- GetCurrentAsync (sección 22) ----------

    [Fact]
    public async Task GetCurrentUnauthenticatedUserReturnsInvalidState()
    {
        var fixture = new Fixture();
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.InvalidState, result.Status);
        Assert.Null(result.ActiveSession);
    }

    [Fact]
    public async Task GetCurrentInvalidInstallationStateReturnsInvalidState()
    {
        var fixture = new Fixture();
        fixture.OrganizationRepository = new FakeOrganizationRepository();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task GetCurrentReturnsNoneOpenWhenNoSessionIsOpen()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.NoneOpen, result.Status);
        Assert.Null(result.ActiveSession);
    }

    [Fact]
    public async Task GetCurrentReturnsOpenSessionAndLoadsCurrentRegisterSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.Open, result.Status);
        Assert.NotNull(result.ActiveSession);
        Assert.Equal(session.Id, result.ActiveSession!.RegisterSessionId);
        Assert.Equal(fixture.Register.Id, result.ActiveSession.RegisterId);
        Assert.Equal(fixture.Branch.Id, result.ActiveSession.BranchId);
        Assert.Equal(fixture.Organization.Id, result.ActiveSession.OrganizationId);
        Assert.Equal(100m, result.ActiveSession.OpeningAmount);
        Assert.Equal("MXN", result.ActiveSession.Currency);
        Assert.Equal(1, fixture.CurrentRegisterSession.SetCallCount);
        Assert.Same(result.ActiveSession, fixture.CurrentRegisterSession.Current);
    }

    [Fact]
    public async Task GetCurrentResolvesDisplayNameOfADifferentOpeningUserDuringRecovery()
    {
        var fixture = new Fixture();
        var otherUser = new User(
            UserId.New(), fixture.Organization.Id, fixture.Role.Id, "OTRO", "Otro Usuario",
            new PasswordHash(SyntheticPasswordHash), FixedNow);
        fixture.UserRepository = new FakeUserRepository([fixture.User, otherUser]);
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, otherUser.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.Open, result.Status);
        Assert.Equal("Otro Usuario", result.ActiveSession!.OpenedByDisplayName);
    }

    [Fact]
    public async Task GetCurrentReturnsInvalidStateWhenMoreThanOneRegisterHasAnOpenSession()
    {
        var fixture = new Fixture();
        var secondRegister = new Register(RegisterId.New(), fixture.Branch.Id, "Caja 2", "CAJA-2", FixedNow);
        fixture.RegisterRepository = new FakeRegisterRepository([fixture.Register, secondRegister]);
        fixture.AuthenticateAs(fixture.User);
        var sessionOne = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        var sessionTwo = fixture.OpenSessionOn(secondRegister, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([sessionOne, sessionTwo]);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task GetCurrentReturnsInvalidStateWhenTheSameRegisterHasMoreThanOneOpenSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var sessionOne = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        var sessionTwo = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([sessionOne, sessionTwo]);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task GetCurrentIgnoresClosedSessionsAndReturnsNoneOpen()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        session.Close(fixture.User.Id, session.OpeningFloat, session.OpeningFloat, FixedNow.AddHours(8));
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        var service = fixture.BuildService();

        var result = await service.GetCurrentAsync();

        Assert.Equal(RegisterSessionStatus.NoneOpen, result.Status);
    }

    [Fact]
    public async Task GetCurrentDoesNotWriteOrCommit()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        var service = fixture.BuildService();

        await service.GetCurrentAsync();

        Assert.Equal(0, fixture.RegisterSessionRepository.AddCallCount);
        Assert.Equal(0, fixture.RegisterSessionRepository.UpdateCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- OpenAsync (sección 23) ----------

    [Fact]
    public async Task OpenSucceedsWithASingleActiveRegisterAutoSelected()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.True(result.Success);
        Assert.Equal(fixture.Register.Id, result.ActiveSession!.RegisterId);
        Assert.Equal(100m, result.ActiveSession.OpeningAmount);
    }

    [Fact]
    public async Task OpenSucceedsWithExplicitRegisterIdWhenMultipleActiveRegistersExist()
    {
        var fixture = new Fixture();
        var secondRegister = new Register(RegisterId.New(), fixture.Branch.Id, "Caja 2", "CAJA-2", FixedNow);
        fixture.RegisterRepository = new FakeRegisterRepository([fixture.Register, secondRegister]);
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(secondRegister.Id, 50m));

        Assert.True(result.Success);
        Assert.Equal(secondRegister.Id, result.ActiveSession!.RegisterId);
    }

    [Fact]
    public async Task OpenAllowsZeroOpeningAmount()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 0m));

        Assert.True(result.Success);
        Assert.Equal(0m, result.ActiveSession!.OpeningAmount);
    }

    [Fact]
    public async Task OpenAllowsPositiveOpeningAmount()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 250.75m));

        Assert.True(result.Success);
        Assert.Equal(250.75m, result.ActiveSession!.OpeningAmount);
    }

    [Fact]
    public async Task OpenRejectsNegativeOpeningAmount()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, -1m));

        Assert.False(result.Success);
        Assert.Equal(RegisterSessionResultStatus.InvalidAmount, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.AddCallCount);
    }

    [Fact]
    public async Task OpenReturnsNotAuthenticatedWhenNoUserIsLoggedIn()
    {
        var fixture = new Fixture();
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(RegisterSessionResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task OpenReturnsNotAuthorizedWhenUserLacksPermission()
    {
        var fixture = new Fixture();
        var roleWithoutPermission = new Role(RoleId.New(), fixture.Organization.Id, "Cajero", FixedNow, []);
        var userWithoutPermission = new User(
            UserId.New(), fixture.Organization.Id, roleWithoutPermission.Id, "SINPERMISO", "Sin Permiso",
            new PasswordHash(SyntheticPasswordHash), FixedNow);
        fixture.AuthenticateAs(userWithoutPermission, roleWithoutPermission);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(RegisterSessionResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.AddCallCount);
    }

    [Fact]
    public async Task OpenReturnsInvalidInstallationStateWhenThereIsNoOrganization()
    {
        var fixture = new Fixture();
        fixture.OrganizationRepository = new FakeOrganizationRepository();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(RegisterSessionResultStatus.InvalidInstallationState, result.Status);
    }

    [Fact]
    public async Task OpenReturnsRegisterNotFoundWhenExplicitRegisterIdDoesNotExist()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(RegisterId.New(), 100m));

        Assert.Equal(RegisterSessionResultStatus.RegisterNotFound, result.Status);
    }

    [Fact]
    public async Task OpenReturnsRegisterInactiveWhenTheRegisterIsInactive()
    {
        var fixture = new Fixture();
        var inactiveRegister = Register.Rehydrate(
            RegisterId.New(), fixture.Branch.Id, "Caja 2", "CAJA-2", isActive: false, FixedNow);
        fixture.RegisterRepository = new FakeRegisterRepository([fixture.Register, inactiveRegister]);
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(inactiveRegister.Id, 100m));

        Assert.Equal(RegisterSessionResultStatus.RegisterInactive, result.Status);
    }

    [Fact]
    public async Task OpenReturnsAlreadyOpenWhenTheRegisterAlreadyHasAnOpenSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var existingSession = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([existingSession]);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(RegisterSessionResultStatus.AlreadyOpen, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.AddCallCount);
    }

    [Fact]
    public async Task OpenReturnsRegisterSelectionRequiredWhenMultipleActiveRegistersExistAndNoneIsSelected()
    {
        var fixture = new Fixture();
        var secondRegister = new Register(RegisterId.New(), fixture.Branch.Id, "Caja 2", "CAJA-2", FixedNow);
        fixture.RegisterRepository = new FakeRegisterRepository([fixture.Register, secondRegister]);
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(RegisterSessionResultStatus.RegisterSelectionRequired, result.Status);
    }

    [Fact]
    public async Task OpenCallsAddExactlyOnce()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(1, fixture.RegisterSessionRepository.AddCallCount);
    }

    [Fact]
    public async Task OpenCallsCommitExactlyOnce()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task OpenLoadsTheCurrentRegisterSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(1, fixture.CurrentRegisterSession.SetCallCount);
        Assert.Same(result.ActiveSession, fixture.CurrentRegisterSession.Current);
    }

    [Fact]
    public async Task OpenRegisterSessionRequestDoesNotExposeAUserIdProperty()
    {
        var properties = typeof(OpenRegisterSessionRequest).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("UserId", properties);
    }

    [Fact]
    public async Task OpenDoesNotModifyTheUserSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

        Assert.Equal(0, fixture.CurrentUserSession.ClearCallCount);
    }

    // ---------- CloseAsync (sección 24) ----------

    [Fact]
    public async Task CloseSucceedsWithZeroDifferenceWhenCountedMatchesExpected()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.True(result.Success);
        Assert.Equal(100m, result.Summary!.ExpectedAmount);
        Assert.Equal(100m, result.Summary.ClosingAmount);
        Assert.Equal(0m, result.Summary.Difference);
    }

    [Fact]
    public async Task ClosePositiveDifferenceWhenCountedExceedsExpected()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(120m));

        Assert.True(result.Success);
        Assert.Equal(20m, result.Summary!.Difference);
    }

    [Fact]
    public async Task CloseNegativeDifferenceWhenCountedIsBelowExpected()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(90m));

        Assert.True(result.Success);
        Assert.Equal(-10m, result.Summary!.Difference);
    }

    [Fact]
    public async Task CloseReturnsNotAuthenticatedWhenNoUserIsLoggedIn()
    {
        var fixture = new Fixture();
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task CloseReturnsNotAuthorizedWhenUserLacksPermission()
    {
        var fixture = new Fixture();
        var roleWithoutPermission = new Role(RoleId.New(), fixture.Organization.Id, "Cajero", FixedNow, []);
        var userWithoutPermission = new User(
            UserId.New(), fixture.Organization.Id, roleWithoutPermission.Id, "SINPERMISO", "Sin Permiso",
            new PasswordHash(SyntheticPasswordHash), FixedNow);
        fixture.AuthenticateAs(userWithoutPermission, roleWithoutPermission);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.UpdateCallCount);
    }

    [Fact]
    public async Task CloseReturnsSessionNotFoundWhenThereIsNoCurrentRegisterSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.SessionNotFound, result.Status);
    }

    [Fact]
    public async Task CloseReturnsSessionNotFoundWhenTheRepositoryNoLongerHasIt()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.CurrentRegisterSession.SetActiveSession(fixture.ToActiveRegisterSession(session));
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository();
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.SessionNotFound, result.Status);
    }

    [Fact]
    public async Task CloseReturnsSessionAlreadyClosedWhenItWasClosedElsewhere()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.CurrentRegisterSession.SetActiveSession(fixture.ToActiveRegisterSession(session));
        session.Close(fixture.User.Id, session.OpeningFloat, session.OpeningFloat, FixedNow.AddHours(8));
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.SessionAlreadyClosed, result.Status);
    }

    [Fact]
    public async Task CloseReturnsSessionBelongsToAnotherOrganizationWhenOrganizationMismatches()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        var session = fixture.OpenSessionOn(fixture.Register, fixture.User.Id);
        fixture.RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
        fixture.CurrentRegisterSession.SetActiveSession(new ActiveRegisterSession(
            session.Id, OrganizationId.New(), fixture.Branch.Id, fixture.Register.Id, fixture.Register.Name,
            fixture.User.Id, fixture.User.DisplayName, session.OpenedAtUtc, 100m, "MXN"));
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(RegisterSessionResultStatus.SessionBelongsToAnotherOrganization, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.UpdateCallCount);
    }

    [Fact]
    public async Task CloseRejectsNegativeClosingAmount()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        var result = await service.CloseAsync(new CloseRegisterSessionRequest(-1m));

        Assert.Equal(RegisterSessionResultStatus.InvalidAmount, result.Status);
        Assert.Equal(0, fixture.RegisterSessionRepository.UpdateCallCount);
    }

    [Fact]
    public async Task CloseCallsUpdateExactlyOnce()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(1, fixture.RegisterSessionRepository.UpdateCallCount);
    }

    [Fact]
    public async Task CloseCallsCommitExactlyOnce()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task CloseClearsTheCurrentRegisterSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(1, fixture.CurrentRegisterSession.ClearCallCount);
        Assert.Null(fixture.CurrentRegisterSession.Current);
    }

    [Fact]
    public async Task CloseRegisterSessionRequestDoesNotExposeARegisterSessionIdProperty()
    {
        var properties = typeof(CloseRegisterSessionRequest).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("RegisterSessionId", properties);
    }

    [Fact]
    public async Task CloseDoesNotModifyTheUserSession()
    {
        var fixture = new Fixture();
        fixture.AuthenticateAs(fixture.User);
        fixture.SeedOpenCurrentSession(openingAmount: 100m);
        var service = fixture.BuildService();

        await service.CloseAsync(new CloseRegisterSessionRequest(100m));

        Assert.Equal(0, fixture.CurrentUserSession.ClearCallCount);
    }

    // ---------- Fixture ----------

    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
            Branch = new Branch(BranchId.New(), Organization.Id, "Sucursal Centro", "SUC-1", FixedNow);
            Register = new Register(RegisterId.New(), Branch.Id, "Caja 1", "CAJA-1", FixedNow);
            Role = new Role(
                RoleId.New(), Organization.Id, "Cajero", FixedNow,
                [Permission.OpenRegisterSession, Permission.CloseRegisterSession]);
            User = new User(
                UserId.New(), Organization.Id, Role.Id, "CAJERO", "Cajero Uno",
                new PasswordHash(SyntheticPasswordHash), FixedNow);

            OrganizationRepository = new FakeOrganizationRepository([Organization]);
            BranchRepository = new FakeBranchRepository([Branch]);
            RegisterRepository = new FakeRegisterRepository([Register]);
            UserRepository = new FakeUserRepository([User]);
            RegisterSessionRepository = new FakeRegisterSessionRepository();
            CurrentUserSession = new FakeCurrentUserSession();
            CurrentRegisterSession = new FakeCurrentRegisterSession();
            UnitOfWork = new FakeUnitOfWork();
            Clock = new FakeClock(FixedNow.AddHours(1));
        }

        public Organization Organization { get; }

        public Branch Branch { get; }

        public Register Register { get; }

        public Role Role { get; }

        public User User { get; }

        public FakeOrganizationRepository OrganizationRepository { get; set; }

        public FakeBranchRepository BranchRepository { get; set; }

        public FakeRegisterRepository RegisterRepository { get; set; }

        public FakeUserRepository UserRepository { get; set; }

        public FakeRegisterSessionRepository RegisterSessionRepository { get; set; }

        public FakeCurrentUserSession CurrentUserSession { get; }

        public FakeCurrentRegisterSession CurrentRegisterSession { get; }

        public FakeUnitOfWork UnitOfWork { get; }

        public FakeClock Clock { get; }

        public void AuthenticateAs(User user, Role? role = null)
        {
            var effectiveRole = role ?? Role;

            CurrentUserSession.CurrentUser = new AuthenticatedUser(
                user.Id, user.OrganizationId, effectiveRole.Id, user.Username, user.DisplayName,
                effectiveRole.Name, effectiveRole.Permissions);
        }

        public RegisterSession OpenSessionOn(Register register, UserId openedByUserId) =>
            new(RegisterSessionId.New(), register.Id, openedByUserId, new Money(100m, "MXN"), Clock.UtcNow);

        public ActiveRegisterSession ToActiveRegisterSession(RegisterSession session) =>
            new(
                session.Id, Organization.Id, Branch.Id, session.RegisterId, Register.Name,
                session.OpenedByUserId, User.DisplayName, session.OpenedAtUtc,
                session.OpeningFloat.Amount, session.OpeningFloat.Currency);

        public void SeedOpenCurrentSession(decimal openingAmount)
        {
            var session = new RegisterSession(
                RegisterSessionId.New(), Register.Id, User.Id, new Money(openingAmount, "MXN"), FixedNow);
            RegisterSessionRepository = new FakeRegisterSessionRepository([session]);
            CurrentRegisterSession.SetActiveSession(ToActiveRegisterSession(session));
        }

        public RegisterSessionService BuildService() =>
            new(
                CurrentUserSession,
                CurrentRegisterSession,
                OrganizationRepository,
                BranchRepository,
                RegisterRepository,
                RegisterSessionRepository,
                UserRepository,
                UnitOfWork,
                Clock);
    }
}
