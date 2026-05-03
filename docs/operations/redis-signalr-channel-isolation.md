# Redis SignalR Channel Isolation Policy

This policy covers only the ASP.NET Core SignalR Redis backplane used by
Hexalith.EventStore projection-change notifications. It does not cover DAPR
state store keys, DAPR pub/sub topics, actor state, cache keys, tenant
authorization, query authorization, or the public SignalR hub protocol.

## Decision Record

| Field | Value |
| --- | --- |
| Date | 2026-05-03 |
| Decision | `separate-redis-per-isolation-boundary` |
| Decision owner/role | Platform architect / production DevOps owner |
| Boundaries covered (where prefix discipline applies) | CI/test lanes, staging, production, and any multi-instance runtime using the SignalR Redis backplane. Local single-app development is exempt (see [Local single-app development](#local-single-app-development-defined)). |
| Evidence reviewed | Current EventStore SignalR wiring, AppHost topology, R10-A2 runtime proof, R10-A6 evidence pattern, Microsoft SignalR Redis backplane guidance, and StackExchange.Redis configuration support |
| Revisit trigger | Revisit if production deployments repeatedly require shared Redis, if prefix collisions are observed, or if operators need first-class EventStore validation outside the raw Redis connection string |

Primary production policy: every isolation boundary MUST use a separate Redis
deployment for the SignalR backplane. Shared Redis is an exception path, not the
default. When a shared Redis exception is approved, every non-local EventStore
deployment on that Redis server MUST set a non-empty `channelPrefix` in the
existing SignalR Redis connection string.

## Current Implementation Posture

EventStore enables SignalR from `EventStore:SignalR:Enabled`. When enabled, the
server maps `/hubs/projection-changes` and broadcasts the public payload
`ProjectionChanged(projectionType, tenantId)` to the hub group
`{projectionType}:{tenantId}`.

The Redis backplane is optional. `SignalRServiceCollectionExtensions.ConfigureBackplane`
reads `options.BackplaneRedisConnectionString` and falls back to the
`EVENTSTORE_SIGNALR_REDIS` process environment variable only when that option is
`null`. The resolved string is parsed with
`StackExchange.Redis.ConfigurationOptions.Parse(...)`, and
`AbortOnConnectFail = false` is set before calling `AddStackExchangeRedis`. The
full StackExchange.Redis connection string parse path already supports
`channelPrefix=...`.

The actual configuration cascade is:

1. `EventStore:SignalR:BackplaneRedisConnectionString` is bound from any
   registered `IConfiguration` provider, including environment variables of the
   form `EventStore__SignalR__BackplaneRedisConnectionString`,
   `appsettings.json`, command-line, or user-secrets. Standard ASP.NET Core
   provider precedence applies.
2. If the bound value is `null`, the code falls back to the
   `EVENTSTORE_SIGNALR_REDIS` process environment variable read directly via
   `Environment.GetEnvironmentVariable`.
3. `ValidateSignalROptions` rejects an empty or whitespace-only
   `BackplaneRedisConnectionString` at startup. To leave the option unset, omit
   the key entirely; do not bind it to `""`.

There is no EventStore-specific `BackplaneRedisChannelPrefix` option. This is
intentional for now: the selected policy prefers separate Redis per boundary,
and the approved shared-Redis exception path can already express the required
prefix through the raw Redis connection string.

The AppHost unconditionally enables SignalR for the local EventStore resource
(`eventStore.WithEnvironment("EventStore__SignalR__Enabled", "true")` in
`src/Hexalith.EventStore.AppHost/Program.cs`). The setting is wired alongside
the Blazor UI sample but is not gated on its presence. The AppHost does not
provision a SignalR Redis backplane or set a channel prefix for ordinary local
single-instance development.

## Isolation Boundary

An isolation boundary is the tuple of:

- Environment, such as local, CI, test, staging, or production.
- Deployed application or product instance.
- Cluster, namespace, slot, or equivalent platform partition when relevant.
- Tenant or tenant lane only when Redis is intentionally shared across
  tenant-isolated EventStore runtimes.
- Test or CI lane when Redis is shared by parallel runs.

Tenant separation is required at the application authorization level through JWT
claims, `ProjectionChangedHub.JoinGroup` authorization, query authorization, and
DAPR access-control policies. Redis SignalR channel separation is transport
isolation for pub/sub fan-out only. It is not tenant authorization and does not
replace any application-level authorization control.

## Channel Prefix Requirements

Use a channel prefix only for the SignalR Redis backplane. Do not reuse it as a
general Redis key namespace, tenant security boundary, cache prefix, DAPR pub/sub
topic pattern, or deployment secret.

### Local single-app development (defined)

"Local single-app development" means exactly one EventStore process pointed at a
local Redis that no other SignalR application or parallel EventStore instance
shares. Two AppHost runs on the same developer box that use the same local
Redis are NOT local single-app development; they are a local shared-Redis
scenario and MUST follow the shared-prefix rules below (or use distinct local
Redis instances).

### Required prefix shape (non-local shared Redis)

When Redis is shared outside local single-app development, the prefix MUST:

- Be non-empty after trimming.
- Be stable across restarts and replicas for the same isolation boundary.
- Be unique for every shared isolation boundary.
- Be lowercase and limited to `a-z`, `0-9`, `.`, and `-`. Redis pub/sub channel
  names are byte-exact: `hesr.PROD.eventstore` and `hesr.prod.eventstore`
  identify different channels. Lowercase is a correctness requirement, not a
  style preference.
- Stay short enough to be safe across Redis client versions and operator
  tooling. The repository convention is ≤96 characters; this is a project
  convention for readability and tool compatibility, not a Redis protocol
  limit. Treat it as advisory upper bound.
- Cover at least environment and application identity.
- Include tenant- or lane-scoped segments only when Redis is intentionally
  shared across those scopes.
- Contain no secrets, connection strings, access tokens, raw internal
  hostnames, raw production topology names, or sensitive tenant identifiers.
  Tenant-lane aliases MUST be non-reversible labels (`tenant-lane-a`,
  `lane-blue`); they MUST NOT be the real tenant identifier, customer name,
  or any value that doubles as authorization input.

There is no startup validator that enforces these rules on the parsed
StackExchange.Redis `channelPrefix` value. `ConfigurationOptions.Parse` accepts
empty `channelPrefix=` (silently treats it as no prefix), accepts mixed-case
values, and accepts collisions with other applications' prefixes. Compliance is
deploy-time discipline, not runtime enforcement. A future story may add a
first-class option with validation; see [Source Change Policy](#source-change-policy).

### Recommended format

```text
hesr.<environment-alias>.<application-alias>[.<cluster-or-namespace-alias-or-lane-alias>]
```

Position 4 is a single optional alias slot. Use it for whichever distinguishing
dimension is needed for the boundary (cluster, namespace, deployment slot,
test/CI lane, or tenant-lane label). When two distinguishing dimensions are
needed for the same boundary, encode both in that slot using `-` as a
sub-separator (for example `hesr.prod.eventstore.region-a-lane-blue`). Do not
use additional `.` segments; they are reserved for future extension.

### Examples

| Scenario | Example prefix | Notes |
| --- | --- | --- |
| Local single-app development | none | No Redis backplane prefix required. |
| Local shared Redis smoke test | `hesr.local.eventstore.devbox` | Use a safe developer or machine alias, not a hostname. |
| CI parallel lane | `hesr.ci.eventstore.lane-07` | Lane id MUST come from a stable per-run source (e.g. `BUILD_ID`, runner index). Two runs MUST never compute the same lane id; see [Lane id derivation](#lane-id-derivation). |
| Test environment | `hesr.test.eventstore.blue` | Use a stable environment alias. |
| Production dedicated Redis | none | Dedicated Redis is the primary policy; no prefix is required for isolation. |
| Production shared Redis exception | `hesr.prod.eventstore.region-a` | Requires owner approval and evidence. Use sanitized aliases. |
| Tenant-isolated runtime sharing Redis | `hesr.prod.eventstore.tenant-lane-a` | Use only non-reversible tenant-lane aliases. Real tenant identifiers, customer names, or any tenant-identifying values MUST NOT appear in committed examples or operational evidence. |

### Lane id derivation

For CI/parallel-test lane prefixes, derive the lane segment from a source the
platform guarantees to be unique per concurrent run. Acceptable derivations:

- A CI-provided per-run identifier (`GITHUB_RUN_ID`, `BUILD_BUILDID`, the
  runner-pool worker index, etc.).
- A short stable hash of such an identifier when the raw value is too long or
  leaks topology.

Hard-coded lane labels (`lane-07`) are acceptable only when the platform
schedules at most one run per label at a time. If two concurrent runs can share
the same label, SignalR pub/sub will silently merge.

### Configuration examples

```bash
EventStore__SignalR__Enabled=true
EventStore__SignalR__BackplaneRedisConnectionString="redis-shared:6379,channelPrefix=hesr.test.eventstore.blue"
```

```bash
# Substitute the real password at deploy time; never commit it.
EVENTSTORE_SIGNALR_REDIS="redis-shared:6379,ssl=true,password=${REDIS_PASSWORD},channelPrefix=hesr.prod.eventstore.region-a"
```

The prefix value is configuration metadata, not a credential. It may appear in
redacted operational evidence, but examples committed to the repository MUST
use sanitized aliases and MUST use placeholder syntax (`${REDIS_PASSWORD}`,
`<replace-with-redis-password>`) for any password component, never a literal
`password=<redacted>` token that an operator might copy verbatim into a secret
store.

## Operator Decision Table

| Policy | Use when | Required configuration | Required evidence | Production suitability |
| --- | --- | --- | --- | --- |
| Dedicated Redis per boundary | Normal production, staging, and isolated test environments | `EventStore:SignalR:BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS` points to a Redis deployment dedicated to the isolation boundary | Redacted Redis endpoint identity, database/index if used, owner, fail-open behavior on connection loss, and confirmation that no shared-prefix exception is in use | Preferred production policy |
| Shared Redis with required prefix | Temporary or platform-approved shared Redis where boundaries must stay separated | Existing Redis connection string MUST include `channelPrefix=...` whose value satisfies every rule in [Required prefix shape](#required-prefix-shape-non-local-shared-redis): non-empty, lowercase, restricted character set, stable, unique per boundary, environment+application scoped, no sensitive identifiers | Same-prefix positive delivery proof, different-prefix negative/control proof when practical, deploy-time owner, collision check (see below), redaction review, and fail-open behavior on connection loss | Production exception only |
| Shared Redis with accepted risk | Emergency exception where no channel-isolation guarantee is claimed | Existing Redis connection string may omit `channelPrefix=...`; exception MUST name owner, expiry or revisit date, affected boundary, and explicit no-guarantee statement | Owner-approved exception record stored under `_bmad-output/operational-evidence/redis-signalr-shared-accepted-risk/<env>-<app>-<yyyy-mm-dd>.md` (or the equivalent platform record store named in the project's evidence index) with the fields below; recorded BEFORE deployment | Not suitable as a standing production policy |

The selected primary policy is the first row. The other rows exist so operators
can classify exceptions explicitly instead of relying on implicit Redis reuse.

### Accepted-risk exception record schema

Every accepted-risk exception MUST produce a record with these fields:

- `decision: shared-redis-with-accepted-risk`
- `date`
- `owner` (role and individual)
- `affected-boundary` (environment, application, cluster/namespace, lane)
- `redis-endpoint-redacted`
- `expiry-or-revisit-date`
- `no-guarantee-statement` (verbatim acknowledgement that no Redis channel-isolation guarantee is provided for this boundary)
- `link-to-deploy-change` (PR, runbook entry, or change-request id)

Records MUST live under `_bmad-output/operational-evidence/redis-signalr-shared-accepted-risk/` (or the equivalent platform path the operator-onboarding doc points to) with sanitized aliases, not under user-local files. A revisit before the recorded date closes the record by either upgrading to dedicated-Redis or shared-with-required-prefix.

### Collision-check procedure (shared Redis with required prefix)

Before approving a shared-Redis exception, perform AT LEAST one of the following
and capture the result in the deployment evidence:

1. **Prefix registry check.** Compare the proposed prefix against the
   organization's prefix registry (or the `redis-signalr-shared-accepted-risk`
   evidence directory) for an exact-string conflict. If no registry exists,
   create one as part of approving the first shared exception.
2. **Live channel inspection.** Against the target Redis (or a representative
   non-production replica), connect with `redis-cli` and run
   `PUBSUB CHANNELS '<proposed-prefix>*'` while a representative SignalR
   workload is publishing. Any subscribed channel matching the prefix that is
   not owned by the deploying application is a conflict.
3. **MONITOR sample.** For short-lived test/CI lanes, run `MONITOR` for at
   least the duration of one publish round-trip and confirm that only the
   deploying application's connection ids publish under the proposed prefix.

Record which check was performed, who performed it, and the result (no
collision / collision found and resolved by changing prefix to `<new>`).

## Runtime Evidence

Future runtime proofs involving the SignalR Redis backplane MUST record:

- Redacted Redis endpoint identity and whether the endpoint is dedicated or
  shared.
- Redis database/index when a non-default database is used.
- Effective channel-prefix decision: none because Redis is dedicated, configured
  prefix value, or accepted-risk no-prefix exception.
- Deploy-time owner for the Redis endpoint and prefix value.
- Same-prefix positive delivery case when proving shared Redis delivery.
- Different-prefix negative/control case when practical.
- Expected fail-open behavior when Redis becomes unavailable AT RUNTIME
  (`AbortOnConnectFail = false` keeps the host running on connection loss).
  Fail-open does NOT cover startup-time misconfiguration: a malformed connection
  string causes `StackExchange.Redis.ConfigurationOptions.Parse` to throw
  synchronously and the host fails to start. Evidence MUST distinguish the two
  failure modes.
- Confirmation that no JWT, bearer token, connection string password, access
  key, unsafe hostname, raw production topology detail, or sensitive tenant
  identifier was committed.

### Redaction-specific rule for `ConfigurationOptions`

The R10-A2 runtime proof endpoints emit a redacted form of the resolved
StackExchange.Redis configuration. Evidence artifacts MUST NOT include the raw
output of `StackExchange.Redis.ConfigurationOptions.ToString()` unless the
emitting code path has been proven to scrub `password=`, `user=`, and any
`auth` token. The published output of `ConfigurationOptions.ToString()` is
known to round-trip credentials present in the parsed connection string;
operators copying it into evidence files leak secrets even when the surrounding
prose appears redacted. When in doubt, copy only the redacted endpoint host,
the boolean `AbortOnConnectFail` value, the resolved channel-prefix string, and
the explicit "redaction confirmed" line.

If a proof cannot run the different-prefix negative/control case, record why the
configuration/unit evidence is the selected gate and classify the missing proof
as an evidence limitation, not as isolation proof.

## Source Change Policy

A first-class EventStore channel-prefix option MUST NOT be added unless one of
these conditions becomes true:

- Production requires shared Redis frequently enough that deploy-time policy
  cannot be managed through the existing Redis connection string.
- Operators need startup validation that a prefix is present for non-local
  shared Redis deployments.
- The raw connection string path cannot express the selected prefix safely.
- A future story chooses to fail startup on conflicting raw prefix and
  EventStore-owned prefix values.

Any future first-class option MUST be scoped only to the SignalR Redis backplane
channel prefix. It MUST NOT become a general Redis naming, tenancy, cache,
pub/sub, or security abstraction.

## References

- [Microsoft Learn: Redis backplane for ASP.NET Core SignalR scale-out](https://learn.microsoft.com/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0)
- [StackExchange.Redis configuration options](https://stackexchange.github.io/StackExchange.Redis/Configuration.html)
- [SignalR operational evidence pattern](signalr-operational-evidence.md)
