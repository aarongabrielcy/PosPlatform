using Pos.Application.Organizations;
using Pos.Application.Security;
using Pos.Application.Users;

namespace Pos.Application.Authentication;

// ILogger es opcional según el contrato de la tarea; se omite aquí porque Pos.Application no
// referencia Microsoft.Extensions.Logging.Abstractions y agregar el paquete requiere
// autorización explícita del usuario.
public sealed class AuthenticationService : IAuthenticationService
{
    private const int MaxPasswordLength = 256;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserSessionWriter _sessionWriter;

    public AuthenticationService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        ICurrentUserSessionWriter sessionWriter)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _sessionWriter = sessionWriter ?? throw new ArgumentNullException(nameof(sessionWriter));
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        EnsureValidRequest(request);

        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        if (organizations.Count != 1)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InvalidInstallationState);
        }

        var organizationId = organizations[0].Id;
        var normalizedUsername = NormalizeUsername(request.Username);

        var user = await _userRepository.GetByUsernameAsync(organizationId, normalizedUsername, cancellationToken);

        if (user is null)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials);
        }

        var passwordIsValid = _passwordHasher.Verify(request.Password, user.PasswordHash.Value);

        if (!passwordIsValid)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InactiveUser);
        }

        var role = await _roleRepository.GetByIdAsync(user.RoleId, cancellationToken);

        if (role is null)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InvalidInstallationState);
        }

        if (!role.IsActive)
        {
            return AuthenticationResult.Failure(AuthenticationStatus.InactiveRole);
        }

        var authenticatedUser = new AuthenticatedUser(
            user.Id,
            user.OrganizationId,
            role.Id,
            user.Username,
            user.DisplayName,
            role.Name,
            role.Permissions);

        _sessionWriter.SetAuthenticatedUser(authenticatedUser);

        return AuthenticationResult.Success(authenticatedUser);
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private static void EnsureValidRequest(AuthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password es obligatorio.", nameof(request));
        }

        if (request.Password.Length > MaxPasswordLength)
        {
            throw new ArgumentException($"Password no puede exceder {MaxPasswordLength} caracteres.", nameof(request));
        }
    }
}
