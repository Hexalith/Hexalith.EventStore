# Route catalog Development fixture

This separate fixture qualifies shared catalog state and local host agreement. Its
Redis component uses `keyPrefix: none` inside a uniquely owned disposable Redis
container, so every enrolled sidecar addresses the same CAS cell without touching
the machine's existing Redis. The owned live tests assign unique app IDs/scopes and state keys, bind
sidecars to loopback, and retain/hash the exact generated inputs. The checked-in
files describe that restricted shape; they are not production components.

`RouteCatalogDevelopmentLiveTests` exercises concurrent empty-key first-write CAS,
the actual metadata registry/cache, durable dispatch leases, failure phases and
restart readback through three sidecars. `RouteCatalogHostedLiveTests` adds the
gateway query controller/decorator, SDK domain host, generated API host and
authenticated generation reads through each receiver's own Dapr sidecar. The slim
case proves the generated API's own-sidecar cross-application call is refused. An
owned, pinned Sentry 1.18.7 with temporary credentials qualifies the matching mTLS
positive through that same caller-sidecar path and one exact API-to-gateway ACL
grant. ACLs remain deny by default. This does not qualify the deployed profiles
owned by Stories 5.6/5.7.

Activation is explicit: prepare retained canonical bytes, attest each independently
observed host after prior leases drain, then commit. A failed prepare/ready retains
the previous catalog. A failed host's outstanding lease never expires into an
unsafe commit. Ambiguous RPC completion also retains a fence. Recovery uses the
protected reconciliation endpoint with an owner verifier and exact fence/root/epoch
proof of actual remote quiescence; no verifier is registered by default. Elapsed
time, caller cancellation and remote eventual completion alone never clear it. The
hosted fixture's test owner signs only independently observed completed receiver
execution. Rollback uses the retained complete predecessor and advances the epoch.

Host observations include installed admission adapters and available key generations,
named projection fingerprints/handlers, and effective auxiliary endpoint policies.
Consumer hosts can capture admission facts with `RouteCatalogAdmissionObservation.CaptureAsync`;
SDK domain hosts use `RouteCatalogDomainObservation.Capabilities` and `Auxiliary`
after mapping their endpoints. These facts must come from the installed services,
not the activation proposal. Missing or incompatible facts refuse readiness.

The observation also binds the effective authority component, state key and normalized
local Dapr HTTP endpoint. `DaprRouteCatalogStateStore.Coordinates` captures the same
runtime configuration used for its requests; the common observation registration
reads those coordinates from that store. Custom stores must provide explicit,
independently observed coordinates. The loaded component's `metadata.name` must
match the selected component. Moving a host to another cell or endpoint requires a
new matching host identity, even when its retained topology documents are unchanged.

The receiver keeps its lease through endpoint work and deferred `IResult` execution.
SDK read-model state RPCs enroll the active leases through the Contracts-owned
`RouteCatalogRemoteExecution` boundary after local validation. Transport uncertainty
retains the durable fence even when a dispatcher returns a normal Retryable or
Indeterminate response. Custom handlers performing other remote work must use this
same explicit boundary; arbitrary handler work cannot be assumed quiescent. Local
validation/cancellation before an RPC drains normally. General HTTP invocation
disables automatic redirects and refuses raw actor URLs; supported typed actor
paths retain their declared router scopes. Ordinary HTTP retains its configured
redirect behavior. Guarded invocation uses a separately owned, redirect-disabled
primary transport beneath the same final SDK header owner. Native transports copy
their effective public TLS/certificate, credential, proxy and connection settings.
Custom primary transports must configure `RouteCatalogHostOptions.InvocationTransportFactory`
to create a separate redirect-disabled primary with the same effective settings and
no duplicate application delegating handlers. Missing or failed custom configuration
refuses invocation while ordinary HTTP remains available. Dapr's Basic-auth invocation
form is unsupported and refuses before activation, during preparation and after commit;
ordinary unrelated Basic authentication remains available.

Projection delivery captures the tenant's actual committed named and legacy methods
at one app and contract version. Named-only plans never call legacy `project`;
legacy-only plans never enter named dispatch. A mixed plan executes both declared
paths and keeps their model/checkpoint effects separate. Rebuild output selection
joins its declared tenant, app/version and actual method, including non-callable
legacy outputs; unsupported metadata never authorizes a different method.

Configure `RouteCatalogHostOptions.ControlPlaneAppIds` independently with the exact
Dapr recipients allowed for catalog coordination. The standard HTTP pipeline permits
only the canonical control methods and verbs before activation or while preparation
fences data requests. When the SDK owns the `dapr-app-id` header, use header-routed
`/internal/routing/...` paths rather than combining that header with an explicit
`/v1.0/invoke/...` URL. Workload and application-channel authorization still apply.

Common hosting refuses readiness and dispatch without an activated catalog,
independent host observations, and verified trust. The Development trust fixture
is accepted only in the Development environment with explicit fixture enrollment.
Production requires its owner's profile, trust validator, host observer and
qualified topology; this fixture supplies none of that authority.

The catalog host fixture uses controlled gateway access validators and a projection
actor invoker. The separate older command/named-projection live witnesses isolate
the catalog layer to preserve their original storage/protocol assertions. They are
not counted as activated catalog host or real actor drain proof. The focused
catalog tests and owned hosted cancellation drill supply those enforcement controls;
no production or performance qualification is inferred.

Local prerequisites are Linux, a local Unix-socket Docker daemon, the pinned Redis
image below, and the checksum-matching `daprd` from `deploy/dapr/qualification/runtime-lock.json`.
The fixture resolves `DOCKER_CONTEXT` before `DOCKER_HOST`, refuses conflicting
selections, and pins the validated Unix endpoint for every owned Docker operation,
including Redis and Sentry extraction and exact-ID/owner-label cleanup. Infrastructure
tooling has a 30-second owned-process deadline with process-tree cleanup; no global
Docker context/configuration or process is changed.
The fixture uses `EVENTSTORE_ROUTE_CATALOG_DAPRD`, then `DAPR_BIN`, then
`~/.dapr/bin/daprd`; it rejects a binary whose SHA-256 differs from the lock. Pull
the existing Sentry image before running; the fixture intentionally uses
`docker create --pull never`:

```sh
docker pull ghcr.io/dapr/sentry@sha256:b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256
docker pull redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf
```

The integration lane prepares this exact Sentry pin and an isolated checksum-verified
runtime from the existing lock. It sets only the catalog fixture's binary path and
keeps the lane's existing Dapr installation, primary workflow, job names and pins.

Each catalog case binds its own Redis container to a random loopback port and checks
the exact container ID/owner label before cleanup. It removes only that container;
there is no shared-key cleanup. The checked-in component endpoint is a placeholder
that the fixture materializes from its owned backend.
