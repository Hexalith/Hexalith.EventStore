# Aspire Log Excerpts

Date: 2026-05-10
AppHost: `D:\Hexalith.EventStore\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj`
Dashboard: `https://localhost:17017/login?t=82ab2cc4cacc0bc99f863ef765ce2cb9`

## Resource Status

Aspire MCP `list_resources` showed the no-Keycloak dev topology running and healthy:

- `eventstore-admin` project: Running, Healthy, HTTP `http://localhost:8090`, HTTPS `https://localhost:8091`
- `eventstore-admin-ui` project: Running, Healthy, HTTP `http://localhost:8092`, HTTPS `https://localhost:8093`
- `eventstore` project: Running, Healthy, HTTP `http://localhost:8080`, HTTPS `https://localhost:7141`
- Dapr sidecars for `eventstore-admin`, `eventstore`, `sample`, and `tenants`: Running, Healthy
- `statestore` and `pubsub` Dapr components: Running, Healthy

## Admin.Server

Aspire MCP `list_structured_logs` for `eventstore-admin` returned:

- `Hosting environment: Development`
- `EventStoreDaprHttpEndpoint=http://localhost:3501`
- Dapr sidecar metadata probes returned HTTP 200
- Tenant mismatch probe logged: `Tenant access denied: requested=tenant-b, authorized=[tenant-a]`

The tenant mismatch log maps to `tenant-mismatch-compaction.json`, which returned HTTP 403 with `Tenant Access Denied`.

## EventStore

Aspire MCP `list_structured_logs` for `eventstore` showed startup/bootstrap, projection discovery, and existing bootstrap global-admin activity. No Issue #15 upstream admin route invocation entries were observed for:

- `api/v1/admin/storage/snapshot-policy`
- `api/v1/admin/storage/snapshot`
- `api/v1/admin/storage/compact`
- `api/v1/admin/backups/*`

This matches the `honest-defer` implementation: Admin.Server returns typed deferred operation results without invoking absent EventStore upstream routes.

## Build Note

The no-Keycloak Aspire restart rebuilt successfully with 0 errors. MSBuild reported three transient copy retry warnings for `Hexalith.EventStore.Admin.Server.dll` while the previous Admin.Server host process released its file lock; the retry completed and the AppHost started.
