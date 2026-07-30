namespace Pos.Application.Installation;

internal enum OrganizationStructureStatus
{
    Complete,
    MissingBranch,
    MissingRegisterInAnyBranch,
    MissingAdministrativeRole,
    MissingAdministratorUser,
}
