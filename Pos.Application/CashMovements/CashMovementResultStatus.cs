namespace Pos.Application.CashMovements;

public enum CashMovementResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InstallationRestricted,
    SessionNotFound,
    SessionAlreadyClosed,
    SessionBelongsToAnotherOrganization,
    InvalidAmount,

    // Cubre tanto Reason vacío/blanco como Reason que excede la longitud máxima permitida
    // (sección 8 de la tarea: "Reason required / invalid reason" es un único caso de error, sin
    // distinguir subtipos).
    ReasonRequired,

    InsufficientExpectedCash,
}
