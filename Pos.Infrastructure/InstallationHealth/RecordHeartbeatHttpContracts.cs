namespace Pos.Infrastructure.InstallationHealth;

// Espejo local del contrato real de pos-cloud (develop):
// POST api/v1/installation-health/heartbeat — Authorization: Bearer <Installation Credential>
// Request:  { "appVersion": string (1-50 chars), "clientReportedAt"?: string (ISO 8601 UTC) }
// Response: 204 No Content
//           401 { statusCode, code: "INSTALLATION_CREDENTIAL_INVALID", message, correlationId }
//           403 { statusCode, code: "INSTALLATION_SUSPENDED" | "INSTALLATION_DECOMMISSIONED", ... }
// clientReportedAt es únicamente diagnóstico (detección de desfase de reloj del cliente); el
// backend nunca lo usa como autoritativo para lastSeenAt/health (ver
// docs/architecture/installation-health.md#server-time-authority en pos-cloud).
internal sealed record RecordHeartbeatHttpRequest(string AppVersion, string ClientReportedAt);

internal sealed record HeartbeatErrorHttpResponse(string? Code);
