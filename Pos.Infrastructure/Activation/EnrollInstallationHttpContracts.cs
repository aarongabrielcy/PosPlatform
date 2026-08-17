namespace Pos.Infrastructure.Activation;

// Espejo local del contrato real de pos-cloud (develop):
// POST api/v1/installation-auth/enroll — sin autenticación.
// Request:  { "enrollmentCode": string }
// Response: 201 { "installationId": string, "credential": string }
//           401 { statusCode, code: "ENROLLMENT_FAILED", message, correlationId } (código
//               inválido/expirado/ya usado/desconocido: el backend no distingue el motivo).
internal sealed record EnrollInstallationHttpRequest(string EnrollmentCode);

internal sealed record EnrollInstallationHttpResponse(string InstallationId, string Credential);
