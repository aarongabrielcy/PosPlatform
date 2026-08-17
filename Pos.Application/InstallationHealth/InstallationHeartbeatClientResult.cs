namespace Pos.Application.InstallationHealth;

public sealed record InstallationHeartbeatClientResult(InstallationHeartbeatClientStatus Status)
{
    public static InstallationHeartbeatClientResult Success() =>
        new(InstallationHeartbeatClientStatus.Success);

    public static InstallationHeartbeatClientResult CredentialInvalid() =>
        new(InstallationHeartbeatClientStatus.CredentialInvalid);

    public static InstallationHeartbeatClientResult Suspended() =>
        new(InstallationHeartbeatClientStatus.Suspended);

    public static InstallationHeartbeatClientResult Decommissioned() =>
        new(InstallationHeartbeatClientStatus.Decommissioned);

    public static InstallationHeartbeatClientResult NetworkFailure() =>
        new(InstallationHeartbeatClientStatus.NetworkFailure);
}
