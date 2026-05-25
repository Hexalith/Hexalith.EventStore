[← Back to Hexalith.EventStore](../../README.md)

# Troubleshooting: DAPR Actor Placement Outage

This guide helps service operators diagnose the failure mode where EventStore queries and commands intermittently time out under `aspire run` even though every service and sidecar reports healthy. The cause is almost always the **DAPR actor placement service** being unreachable from the sidecars, which makes every actor invocation hang to its timeout. Use this page when you see `TaskCanceledException` on the query or command path and want to confirm placement is the culprit before suspecting application code.

> **Prerequisites:** [Prerequisites](../getting-started/prerequisites.md) — DAPR CLI, Docker Desktop, Aspire CLI

## Symptom

- A UI page (e.g. the Sample BlazorUI counter page) or an API call hangs for ~10–30 s, then surfaces a `TaskCanceledException` (inner `IOException`/`SocketException` "operation aborted").
- The Aspire **trace** shows the request reaching EventStore and routing to an actor, with the actor span (e.g. `CallActor/ProjectionActor/QueryAsync`, or a command's aggregate actor) consuming the entire duration before failing.
- The startup `BootstrapGlobalAdmin` command may also fail with `TaskCanceledException … HttpClient.Timeout of 100 seconds elapsing` — a second, unrelated actor failing the same way is a strong tell.
- Crucially, the sidecar's own `/healthz` and the existing `dapr-sidecar` health check still report **healthy** — only actors are affected.

## Diagnosis

1. **Check the EventStore sidecar console** (Aspire dashboard → `eventstore` resource → its DAPR sidecar, or `docker`/CLI logs). The signature is a continuous loop:

   ```text
   level=error msg="Failed to connect to placement service: failed to open stream to placement
   service: rpc error: code = Unavailable desc = connection error: desc = \"error reading server
   preface: EOF\". Retrying..." scope=dapr.runtime.actors.placement.loops.placement
   ```

   The same `error reading server preface: EOF` typically appears for the scheduler host too.

2. **Query the sidecar metadata** to confirm the actor host never joined placement. Use the EventStore sidecar's DAPR HTTP port (shown in the Aspire dashboard or `DAPR_HTTP_PORT`):

   ```bash
   $ curl -s http://localhost:<DAPR_HTTP_PORT>/v1.0/metadata | jq .actorRuntime
   ```

   A broken host shows `"hostReady": false` and `"placement": "placement: disconnected"`. A healthy host shows `"hostReady": true` and `"placement": "placement: connected"`.

3. **The built-in guardrail:** the `dapr-actor-placement` health check (tag `ready`) reports **Unhealthy** with the message *"DAPR actor host not ready … Actor invocations … will hang until timeout. Check the DAPR placement service."* whenever `hostReady` is `false`. Watch for it on the `eventstore` resource health in the Aspire dashboard or `/health`.

## Fix

1. **Restart the DAPR control-plane containers.** This re-establishes the gRPC stream (the most common cause is a stale Docker Desktop port-forward after long host uptime or sleep/resume):

   ```bash
   $ docker restart dapr_placement dapr_scheduler
   ```

   The sidecars reconnect automatically within seconds (their placement loop retries continuously); actor calls then succeed immediately.

2. **If the loop persists,** re-initialise the DAPR self-hosted control plane:

   ```bash
   $ dapr uninstall --all
   $ dapr init
   ```

   Then restart `aspire run`.

3. **Verify** the fix: re-run the failing query — it should return in well under a second (a hanging query takes ~10–30 s), and the `dapr-actor-placement` health check should report Healthy (`placement: connected`).

> **Note:** This is an environment/control-plane condition, not an application defect. The actors, the DAPR service-invocation transport, and the resilience handler all behaved correctly given an unreachable placement service — they simply have nothing to route to until placement is restored.

## Next Steps

- **Next:** [Deployment Progression Guide](deployment-progression.md) — how the topology runs across environments
- **Related:** [service-unavailable](../reference/problems/service-unavailable.md), [internal-server-error](../reference/problems/internal-server-error.md)
