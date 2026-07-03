# Integration Architecture — Hexalith.EventStore

> How the deployable parts of this monorepo communicate. All inter-service communication goes through
> **DAPR** (service invocation, pub/sub, actors) — never direct HTTP between services.

## Parts & their runtime relationships

```mermaid
flowchart LR
    subgraph Clients
      ExternalApp[External app]
      UIc[Sample Blazor UI]
      AdminUI[Admin.UI Blazor]
      CLI[Admin.Cli]
      MCP[Admin.Mcp]
    end

    ExternalApp -->|typed REST| DomainApi[Generated external API host]
    DomainApi -->|IEventStoreGatewayClient<br/>DAPR service invocation| ES[eventstore gateway]
    UIc -->|EventStore Client libraries<br/>DAPR service invocation| ES
    AdminUI -->|REST| AdminSrv[eventstore-admin]
    CLI -->|REST| AdminSrv
    MCP -->|REST| AdminSrv
    AdminSrv -->|DAPR service invocation + state reads| ES
    ES -->|DAPR service invocation| SAMPLE[sample domain svc]
    ES -->|DAPR service invocation| TENANTS[tenants domain svc]
    ES <-->|state store + pub/sub| DAPR[(DAPR sidecars)]
    AdminSrv -->|state reads, metadata| DAPR
    ES -.->|SignalR projection-changed| UIc
    ES -.->|SignalR| AdminUI
    KC[Keycloak] -->|OIDC tokens| Clients
```

## Integration points

| From | To | Transport | Details |
|------|----|-----------|---------|
| External clients | Dedicated generated API hosts (`sample-api`, `tenants-api`, custom) | REST (JWT) | Call typed per-domain REST endpoints generated from `ICommandContract`/`IQueryContract` messages |
| Dedicated generated API hosts | `eventstore` gateway | DAPR service invocation through `IEventStoreGatewayClient` | Generated controllers submit command/query gateway requests; no MediatR/domain-service/actor/state-store bypass |
| Interactive UI hosts | `eventstore` gateway | EventStore Client libraries over DAPR service invocation | Submit commands/queries; receive SignalR projection-changed; no generated or hand-written per-message MVC command/query controllers |
| `eventstore` | Domain services (`sample`, `tenants`, custom) | **DAPR service invocation** | `DaprDomainServiceInvoker` resolves (AppId, MethodName) via `IDomainServiceResolver` from `EventStore:DomainServices`; version from command extensions (`v{n}`) |
| `eventstore` | State store | DAPR state (actor-scoped) | Events (write-once), metadata, snapshots, ETags, projection state, command status/archive |
| `eventstore` | Pub/Sub | DAPR pub/sub | Events as CloudEvents 1.0; topic `{tenant}.{domain}.events`; dead-letter `deadletter.*` |
| Pub/Sub | `eventstore` `POST /projections/changed` | DAPR subscription `*.*.projection-changed` | Regenerate ETag + SignalR broadcast |
| `eventstore` | Clients | SignalR (`ProjectionChangedHub`) + Redis backplane | Real-time read-model refresh per `{projectionType}:{tenantId}` group |
| `eventstore-admin` | `eventstore` | DAPR service invocation + state reads | Admin **writes are delegated** to the gateway (ADR-P4); reads go direct to state store (`keyPrefix=none`) |
| Admin.UI / Cli / Mcp | `eventstore-admin` | REST (JWT) | All admin operations via the Admin.Server REST API |
| All services | Keycloak | OIDC (HTTP) | JWT issuance/validation (or symmetric-key fallback when `EnableKeycloak=false`) |

## DAPR sidecar wiring (from `HexalithEventStoreExtensions`)

| Service | DAPR AppId | State store | Pub/Sub | Notes |
|---------|-----------|-------------|---------|-------|
| `eventstore` | `eventstore` | ✅ | ✅ | Fixed `DaprHttpPort=3501` for admin metadata queries |
| `eventstore-admin` | `eventstore-admin` | ✅ (reads) | ❌ | No pub/sub; reads only; resiliency path injected (run-mode) |
| `eventstore-admin-ui` | `eventstore-admin-ui` | ❌ | ❌ | Service invocation to admin only |
| `sample-api` / `tenants-api` | `sample-api` / `tenants-api` | ❌ | ❌ | Dedicated generated REST facades; invoke `eventstore` through gateway client |
| `sample` / `tenants` | `sample` / `tenants` | ❌ | ❌ | **Zero infrastructure access** (D4); invoked by eventstore |

## Domain modules are domain-centric

Domain services (`sample`, `tenants`, and any custom domain) own **only domain logic** — aggregates,
commands, events, projections, validators, queries, contracts. Everything needed to run on Hexalith.EventStore
is supplied by the **`Hexalith.EventStore.DomainService` SDK** (which builds on the client libraries):
hosting, the DAPR-invoked domain-service endpoints (`/process`, `/replay-state`,
`/admin/operational-index-metadata` today; `/project` generalization is Epic A3), convention
discovery/registration, ServiceDefaults, and — as the platform last-mile lands — telemetry, health checks,
and event-subscription/projection-consumer plumbing.

A conforming domain module therefore does **not** ship its own `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults`
projects, and does **not** re-implement projection/query actors or DAPR sidecar wiring. The reference shape is
`samples/Hexalith.EventStore.Sample` (≈ domain code + a 2-line host). Tenants uses the same split: domain
service stays headless/domain-centric, and public typed REST is hosted by a dedicated external API host.

Generated public REST is outside the domain service and outside interactive UI hosts. External API hosts
reference `Hexalith.EventStore.RestApi.Generators` as an analyzer and expose typed REST routes, but generated
controllers still call the EventStore gateway through `IEventStoreGatewayClient`. They do not call MediatR,
domain services, DAPR actors, state stores, projection actors, or query dispatchers directly.

## Access control (D4 / FR34)

Each receiving service has its own DAPR Configuration CRD (`accesscontrol*.yaml`):

- `eventstore` allows `eventstore-admin` (delegation), interactive UI hosts that use EventStore Client
  libraries, and generated external API hosts such as `sample-api` / `tenants-api`.
- `eventstore-admin` allows `eventstore-admin-ui` (D13).
- `sample` / `tenants` allow `eventstore` POST-only (command invocation).
- Self-hosted/dev: allow-by-default + public trust domain. **Production: deny-by-default + mTLS.**

## Topic & resource naming conventions (`NamingConventionEngine`)

- Domain name: kebab-case from aggregate/projection type (suffixes `Aggregate`/`Projection`/`Processor`
  stripped), or `[EventStoreDomain]` override.
- State store name: `{domain}-eventstore`. Events topic: `{domain}.events`.
- Projection-changed topic: `{tenantId}.{projectionType}.projection-changed`.

## Failure isolation

- Advisory paths (status write, archive, snapshot, projection notify, SignalR broadcast) **fail open**.
- Auth/tenant/RBAC/validation **fail closed**.
- Publish failures → drain recovery (reminder-based retry); step 3–5 infra failures → dead-letter.
- Pub/sub circuit breaker (>5 failures) drives `AggregateActor` into `PublishFailed`/drain.
