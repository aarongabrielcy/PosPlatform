namespace Pos.Application.Installation;

public interface IInstallationStateService
{
    Task<InstallationState> GetInstallationStateAsync(CancellationToken cancellationToken);
}
