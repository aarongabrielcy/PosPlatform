using Pos.Application.Branches;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Installation;
using Pos.Application.Organizations;
using Pos.Application.Registers;
using Pos.Application.Security;
using Pos.Application.Users;
using Pos.Domain.Branches;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Registers;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Bootstrap;

public sealed class InitialBusinessBootstrapService : IInitialBusinessBootstrapService
{
    private const string AdministratorRoleName = "Administrator";
    private const string InitialBranchCode = "MAIN";
    private const string InitialRegisterCode = "MAIN";

    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 256;

    // Protege el bootstrap dentro del proceso: dos llamadas concurrentes desde scopes
    // distintos comparten este semáforo estático para serializar la evaluación de estado y
    // la escritura, evitando duplicar la instalación inicial.
    private static readonly SemaphoreSlim InstallationLock = new(1, 1);

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IRegisterRepository _registerRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IPasswordHasher _passwordHasher;
    private readonly InstallationStructureInspector _structureInspector;

    public InitialBusinessBootstrapService(
        IOrganizationRepository organizationRepository,
        IBranchRepository branchRepository,
        IRegisterRepository registerRepository,
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IPasswordHasher passwordHasher)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _structureInspector = new InstallationStructureInspector(
            _branchRepository, _registerRepository, _roleRepository, _userRepository);
    }

    public async Task<InitialBusinessBootstrapResult> BootstrapAsync(
        InitialBusinessBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        EnsureValidRequest(request);

        await InstallationLock.WaitAsync(cancellationToken);
        try
        {
            var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

            if (organizations.Count > 1)
            {
                throw new InitialBusinessBootstrapStateException(
                    "Existen múltiples registros de Organization; el estado de la instalación es inconsistente.");
            }

            if (organizations.Count == 1)
            {
                return await EvaluateExistingInstallationAsync(organizations[0], cancellationToken);
            }

            return await CreateInstallationAsync(request, cancellationToken);
        }
        finally
        {
            InstallationLock.Release();
        }
    }

    private async Task<InitialBusinessBootstrapResult> EvaluateExistingInstallationAsync(
        Organization organization,
        CancellationToken cancellationToken)
    {
        var status = await _structureInspector.EvaluateAsync(organization.Id, cancellationToken);

        return status switch
        {
            OrganizationStructureStatus.Complete => InitialBusinessBootstrapResult.AlreadyInitialized(),
            OrganizationStructureStatus.MissingBranch => throw new InitialBusinessBootstrapStateException(
                $"Organization '{organization.Id}' no tiene ninguna Branch asociada."),
            OrganizationStructureStatus.MissingRegisterInAnyBranch => throw new InitialBusinessBootstrapStateException(
                $"Organization '{organization.Id}' no tiene ninguna Branch con al menos un Register."),
            OrganizationStructureStatus.MissingAdministrativeRole => throw new InitialBusinessBootstrapStateException(
                $"Organization '{organization.Id}' no tiene ningún Role administrativo."),
            OrganizationStructureStatus.MissingAdministratorUser => throw new InitialBusinessBootstrapStateException(
                $"Organization '{organization.Id}' tiene Role administrativo pero ningún User asignado."),
            _ => throw new InvalidOperationException($"Estado estructural desconocido: {status}."),
        };
    }

    private async Task<InitialBusinessBootstrapResult> CreateInstallationAsync(
        InitialBusinessBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        OrganizationId organizationId;
        Organization organization;
        BranchId branchId;
        Branch branch;
        RegisterId registerId;
        Register register;
        RoleId roleId;
        Role role;
        UserId userId;
        User user;

        try
        {
            organizationId = OrganizationId.New();
            organization = new Organization(organizationId, request.OrganizationName, now);

            branchId = BranchId.New();
            branch = new Branch(branchId, organizationId, request.BranchName, InitialBranchCode, now);

            registerId = RegisterId.New();
            register = new Register(registerId, branchId, request.RegisterName, InitialRegisterCode, now);

            roleId = RoleId.New();
            role = new Role(roleId, organizationId, AdministratorRoleName, now, AdministrativePermissionSet.All());

            var passwordHash = new PasswordHash(_passwordHasher.Hash(request.AdministratorPassword));

            userId = UserId.New();
            user = new User(
                userId,
                organizationId,
                roleId,
                request.AdministratorUsername,
                request.AdministratorDisplayName,
                passwordHash,
                now);
        }
        catch (DomainValidationException ex)
        {
            throw new InitialBusinessBootstrapValidationException(ex.Message, ex);
        }

        await _organizationRepository.AddAsync(organization, cancellationToken);
        await _branchRepository.AddAsync(branch, cancellationToken);
        await _registerRepository.AddAsync(register, cancellationToken);
        await _roleRepository.AddAsync(role, cancellationToken);
        await _userRepository.AddAsync(user, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return InitialBusinessBootstrapResult.Created(organizationId, branchId, registerId, roleId, userId);
    }

    private static void EnsureValidRequest(InitialBusinessBootstrapRequest request)
    {
        if (request is null)
        {
            throw new DomainValidationException("request no puede ser nulo.");
        }

        EnsureNotWhitespace(request.OrganizationName, nameof(request.OrganizationName));
        EnsureNotWhitespace(request.BranchName, nameof(request.BranchName));
        EnsureNotWhitespace(request.RegisterName, nameof(request.RegisterName));
        EnsureNotWhitespace(request.AdministratorUsername, nameof(request.AdministratorUsername));
        EnsureNotWhitespace(request.AdministratorDisplayName, nameof(request.AdministratorDisplayName));

        if (string.IsNullOrWhiteSpace(request.AdministratorPassword))
        {
            throw new DomainValidationException("AdministratorPassword es obligatorio.");
        }

        if (request.AdministratorPassword.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            throw new DomainValidationException(
                $"AdministratorPassword debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.");
        }
    }

    private static void EnsureNotWhitespace(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{fieldName} es obligatorio.");
        }
    }
}
