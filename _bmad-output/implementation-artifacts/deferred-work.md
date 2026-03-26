# Deferred Work

## Deferred from: code review of 19-2-dapr-actor-inspector (2026-03-26)

- Broad exception catch in `GetActorRuntimeInfoAsync` masks programming errors (e.g., NullReferenceException logged as "sidecar unavailable"). Pre-existing pattern from story 19-1.
- `ComposeActorStateKey` relies on DAPR internal key convention (`{appId}||{actorType}||{actorId}||{stateKey}`). If DAPR changes this format, state reads silently fail. Spec documents migration path via DAPR service invocation.
- `ReadActorStateKeyAsync` reads from admin-server's own state store. In multi-state-store production deployments, commandapi's actor state lives in a different physical store. Works in Aspire (shared store).
- `HandleErrorStatus` in API clients discards server `ProblemDetails` response body — diagnostic info lost. Pattern shared across all Admin API clients.
- `AdminActorApiClient` returns null for both HTTP 404 and 500 — callers cannot distinguish "not found" from "server error". Pattern shared across Admin API clients.
