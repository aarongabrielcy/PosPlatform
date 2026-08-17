namespace Pos.Application.Activation;

public sealed record InstallationEnrollmentClientResult(
    InstallationEnrollmentClientStatus Status,
    string? InstallationId,
    string? Credential)
{
    public static InstallationEnrollmentClientResult Success(string installationId, string credential) =>
        new(InstallationEnrollmentClientStatus.Success, installationId, credential);

    public static InstallationEnrollmentClientResult Rejected() =>
        new(InstallationEnrollmentClientStatus.Rejected, null, null);

    public static InstallationEnrollmentClientResult NetworkFailure() =>
        new(InstallationEnrollmentClientStatus.NetworkFailure, null, null);
}
