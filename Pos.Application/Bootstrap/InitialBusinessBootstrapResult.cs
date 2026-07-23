using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Bootstrap;

public sealed class InitialBusinessBootstrapResult
{
    public InitialBusinessBootstrapStatus Status { get; }

    public OrganizationId? OrganizationId { get; }

    public BranchId? BranchId { get; }

    public RegisterId? RegisterId { get; }

    public RoleId? RoleId { get; }

    public UserId? UserId { get; }

    private InitialBusinessBootstrapResult(
        InitialBusinessBootstrapStatus status,
        OrganizationId? organizationId,
        BranchId? branchId,
        RegisterId? registerId,
        RoleId? roleId,
        UserId? userId)
    {
        Status = status;
        OrganizationId = organizationId;
        BranchId = branchId;
        RegisterId = registerId;
        RoleId = roleId;
        UserId = userId;
    }

    public static InitialBusinessBootstrapResult Created(
        OrganizationId organizationId,
        BranchId branchId,
        RegisterId registerId,
        RoleId roleId,
        UserId userId) =>
        new(InitialBusinessBootstrapStatus.Created, organizationId, branchId, registerId, roleId, userId);

    public static InitialBusinessBootstrapResult AlreadyInitialized() =>
        new(InitialBusinessBootstrapStatus.AlreadyInitialized, null, null, null, null, null);

    // No expone password ni hash: solo identificadores tipados o su ausencia.
    public override string ToString() => $"InitialBusinessBootstrapResult[Status={Status}]";
}
