using Pos.Domain.Common.Identifiers;

namespace Pos.Application.RegisterSessions;

// Proyección inmutable de una RegisterSession abierta, pensada para vivir dentro de
// ICurrentRegisterSession. No expone la entidad Domain ni Money: solo los datos primitivos que
// la UI necesita mostrar.
public sealed class ActiveRegisterSession
{
    public RegisterSessionId RegisterSessionId { get; }

    public OrganizationId OrganizationId { get; }

    public BranchId BranchId { get; }

    public RegisterId RegisterId { get; }

    public string RegisterName { get; }

    public UserId OpenedByUserId { get; }

    public string OpenedByDisplayName { get; }

    public DateTimeOffset OpenedAtUtc { get; }

    public decimal OpeningAmount { get; }

    public string Currency { get; }

    public bool IsOpen { get; } = true;

    public ActiveRegisterSession(
        RegisterSessionId registerSessionId,
        OrganizationId organizationId,
        BranchId branchId,
        RegisterId registerId,
        string registerName,
        UserId openedByUserId,
        string openedByDisplayName,
        DateTimeOffset openedAtUtc,
        decimal openingAmount,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(openedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        RegisterSessionId = registerSessionId;
        OrganizationId = organizationId;
        BranchId = branchId;
        RegisterId = registerId;
        RegisterName = registerName;
        OpenedByUserId = openedByUserId;
        OpenedByDisplayName = openedByDisplayName;
        OpenedAtUtc = openedAtUtc;
        OpeningAmount = openingAmount;
        Currency = currency;
    }
}
