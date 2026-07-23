namespace Pos.Application.Bootstrap;

public sealed record InitialBusinessBootstrapRequest(
    string OrganizationName,
    string BranchName,
    string RegisterName,
    string AdministratorUsername,
    string AdministratorDisplayName,
    string AdministratorPassword);
