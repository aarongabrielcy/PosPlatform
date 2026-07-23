namespace Pos.Application.Bootstrap;

public interface IInitialBusinessBootstrapService
{
    Task<InitialBusinessBootstrapResult> BootstrapAsync(
        InitialBusinessBootstrapRequest request,
        CancellationToken cancellationToken);
}
