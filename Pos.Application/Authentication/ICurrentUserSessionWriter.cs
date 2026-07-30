namespace Pos.Application.Authentication;

// Interfaz separada de ICurrentUserSession para que solo AuthenticationService (u otro código de
// composición explícitamente autorizado) pueda establecer la identidad autenticada; el resto de
// consumidores (ViewModels de UI) solo reciben ICurrentUserSession (lectura + Clear).
public interface ICurrentUserSessionWriter
{
    void SetAuthenticatedUser(AuthenticatedUser authenticatedUser);
}
