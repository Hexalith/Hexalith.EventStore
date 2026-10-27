[← Back to Hexalith.EventStore](../../README.md)

# Dapr Infrastructure Boundary

Use this guide when adding or changing an EventStore infrastructure operation. It records the project-wide policy, a preliminary classification of known paths, and the evidence required before selecting a backend or accepting an exception. The final evaluated inventory and automated guard belong to backlog Story 3.17.

> **Prerequisites:** [Architecture Overview](architecture-overview.md), [DAPR Component Reference](../guides/dapr-component-reference.md)

## Required Boundary

EventStore application and shared runtime code must use suitable Dapr building blocks and components for persistence and actors, messaging, service invocation, configuration, secrets, bindings, scheduling/workflows, and cryptographic provider operations where applicable. This covers production libraries, hosts, samples, linked source, and generated-host inputs. Domain modules continue to consume platform seams and receive no direct persistence authority.

For an operation supported by a suitable Dapr interface, application code must not add a database driver, broker client, cloud SDK, provider HTTP call, provider connection string, or provider schema dependency. A missing SDK helper does not justify bypassing a supported Dapr API. A generic binding carrying application-owned SQL or provider protocols does not establish backend portability or override actor ownership.

Suitability means that the operation meets its required correctness, security, compatibility, and operational guarantees on an identified runtime/client/component profile. Record the guarantees and observed evidence. Convenience, familiarity, or an unmeasured performance preference is not a capability gap. Unknown suitability remains unresolved; a Dapr failure must never trigger silent direct-provider fallback.

Backend selection is expressed through component configuration. A change still requires profile-specific tests and data migration where necessary. Qualify ETags, transaction scope, TTL, ordering, cancellation, retries, failure classification, identity, and isolation as applicable to the operation. Sharing a physical backend does not create transactions across actors, state components, pub/sub, or external systems.

The planning authority is [PRD §8.4](../../_bmad-output/planning-artifacts/prd.md#84-dapr-infrastructure-boundary) and architecture AD-1/AD-3/AD-23/AD-26. This policy does not establish production readiness or replace the existing readiness, release, security, and consumer-authority gates.

## Preliminary Classification

The following rows are source observations and owned follow-ups, not a complete dependency inventory or conformance result. Package-catalog entries alone do not prove runtime use. Story 3.17 must inspect evaluated and transitive project/package graphs, compiled/linked/generated inputs, configuration, credentials, and provider HTTP call sites.

| Operation / edge | Observed or planned paths | Classification and evidence gate | Owner / follow-up |
| --- | --- | --- | --- |
| Aggregate event/snapshot and drain-registration state | `src/Hexalith.EventStore.Server/Actors/`, `src/Hexalith.EventStore.Server/Events/` | Actor-owned mutations remain solely `IActorStateManager`-owned. Logical access uses the owning actor boundary; no private actor keys/tables or application SQL. Component/profile guarantees remain evidence-gated. | EventStore persistence owner; 3.17 inventory, existing 6.6 amendment and AD-26 gates |
| Read models, checkpoints, catalogs, and non-actor control state | `src/Hexalith.EventStore.Client/Projections/`, Server and Gateway inputs | Platform Dapr state/actor APIs, with explicit transaction/ETag/TTL and ownership boundaries. Same-backend deployment is not proof of cross-component atomicity. | Platform state/topology owners; 3.17 and existing state/profile stories |
| Event distribution and domain calls | Server publishing/dispatch and `src/Hexalith.EventStore.DomainService/` | Dapr publish/subscription and service invocation paths; ordering, duplicates, poison capture, authorization, and persisted end-state need their existing acceptance evidence. | Delivery/domain-service owners; 3.17 and AD-8/AD-9/AD-12 |
| Application secrets and configuration | `src/Hexalith.EventStore.ServiceDefaults/`, `deploy/dapr/`, AppHost component inputs | Dapr Secrets/Configuration APIs where configured; production OpenBao, scopes, readiness, and app-channel authentication remain separately gated. Secret retrieval is not cryptographic key-operation qualification. | Security/Operations; 3.17 and existing 5.6–5.9/7.6 profile work |
| SignalR replica distribution | [registration source](../../src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs), [options](../../src/Hexalith.EventStore/SignalRHub/SignalROptions.cs), [EventStore host project](../../src/Hexalith.EventStore/Hexalith.EventStore.csproj), [Gateway project](../../src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj) | **Accepted scoped exception** `signalr-replica-distribution` (owner decision 2026-10-25, single-maintainer-attested). `ConfigureBackplane` calls `AddStackExchangeRedis` only when `BackplaneRedisConnectionString` / `EVENTSTORE_SIGNALR_REDIS` is set; Gateway compiles this source through linked inputs. The exception covers cross-replica hub messages at these paths only and grants no production eligibility. | SignalR transport owner; [Story 2.13](../../_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md); 3.17 guard |
| Production payload key operations | Frozen shared spec §§5/11/16; planned `src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/` | **Unresolved planned integration.** `crypto.azure.keyvault` is a candidate, not a selected/conformant adapter. No direct Azure SDK is selected by this reconciliation; the core already exists and stays provider-neutral/non-packable. | Security/Operations and payload owner; 8.6 and [draft amendment](../../_bmad-output/implementation-artifacts/spec-shared-payload-protection-dapr-amendment-2026-10-05.md) |
| External client transport | Gateway/public API hosts, browser SignalR clients, Admin clients | Public HTTP and browser SignalR are transport boundaries, with application authorization, compatibility, scoped groups, and freshness obligations. They are not provider-persistence adapters. Inventory them explicitly; do not exempt unrelated provider calls. | API/SignalR/Security owners; 3.17 classification and 2.13 compatibility |
| Telemetry export | [ServiceDefaults project](../../src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj) and `Extensions.cs` | OpenTelemetry/OTLP is a distinct governed diagnostic transport. Redaction, scopes, exporter policy, cardinality, and readiness gates remain required. This classification grants no business-infrastructure bypass. | Observability owner; 3.17 final classification and AD-10 production telemetry gate |
| Resource provisioning and deployment/backup administration | `src/Hexalith.EventStore.AppHost/`, `src/Hexalith.EventStore.Aspire/`, `deploy/` | Orchestration/component/IaC inputs, separated from shipped business-operation adapters. Provider packages or credentials here cannot leak into runtime graphs or application configuration. | Platform deployment owner; 3.17 classification and AD-9/AD-26 |
| Test setup, fault injection, and physical diagnostics | [Redis backplane proof](../../tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs), test/integration tooling | Explicit-purpose test tooling only. Acceptance enters through Dapr and reads logical persisted values through the appropriate Dapr state/actor boundary. Backend tools do not supply application contracts or replace Dapr-path correctness evidence. No blanket `tests/` exemption. | Test owner; 3.17 and 7.11; 6.6 keeps its stricter direct-database product-evidence prohibition |

## Notification And Crypto Qualification

Story 2.13 compared Dapr pub/sub fan-out to locally connected hubs and the Azure SignalR output binding with the self-hosted Redis backplane. Local runs with two hosts and independent sidecars delivered the signal and the scoped detail to both hosts with matching persisted readback. Tenant denials, the metadata ceilings (16 entries, 2,048 serialized UTF-8 bytes), connection-reset and silent-discard outages, and fresh-endpoint replica restart also passed. Three gaps blocked a Dapr replacement:

1. **Tenant enrollment.** `pubsub.redis` treats the shipped wildcard subscription `*.*.projection-changed` as one literal stream, so concrete tenant topics are not delivered. Fan-out worked only with exact per-tenant subscriptions and an operator sidecar reload.
2. **Replica restart.** With stock daprd 1.18.x, the surviving host's remote actor call fails after a peer restarts on the same internal endpoint. Only a disposable patched runtime passed.
3. **Production profile.** The self-hosted Kubernetes target runs Dapr 1.18.1, below the AD-26 floor, with no durable broker or binding-capable issuer selected. Azure is outside MVP.

On 2026-10-25 the owner accepted a scoped exception for the retained backplane instead. Its paths, configuration keys, alternatives and removal trigger are in the [register](../architecture/dapr-infrastructure-exceptions.yaml). Results, commands and run identities are in the [Story 2.13 story](../../_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md) per architecture AD-12. Earlier qualification write-ups remain in Git history. Story 3.17 inventories the exception paths and enforces them in its guard.

Story 8.6 must qualify the exact Dapr runtime/client/crypto component for current-version discovery, exact-version wrap/unwrap, RSA-HSM-3072 and RSA-OAEP-256 validation, deterministic identity and least privilege, private connectivity, cancellation, combined retry budgets, typed failures, key custody, buffer ownership/zeroing, historical decryptability, and rollback. Component existence or an encrypt/decrypt happy path is insufficient. Dapr Crypto Scheme v1 must not replace frozen `pdenc-v2`/AAD bytes. The detached amendment is draft/unapproved and preserves the current shared authority and approval packets.

## Exception Decisions

The [accepted-exception register](../architecture/dapr-infrastructure-exceptions.yaml) holds one entry: the Story 2.13 SignalR Redis backplane (`signalr-replica-distribution`), accepted 2026-10-25. Missing qualification is not evidence of a permanent capability gap.

Before introducing an operation-specific bypass, retain:

1. The operation, required guarantee, exact Dapr runtime/client/component profile, reproducible gap evidence, and alternatives considered.
2. The isolated adapter, exact allowed source/project/configuration paths, runtime credential boundary, owner, and review/removal trigger.
3. The architecture-owner decision and evidence reference, plus Security/Operations/compatibility decisions where applicable.
4. Guard validation proving the exception cannot authorize unrelated dependencies, linked/generated assets, raw HTTP, or credentials.

Unknown, missing, invalid, stale, or overbroad decisions stay unresolved and cannot supply conformance. Story 3.17 owns the final machine-checked schema and guard implementation after graph inspection. Static analysis must state its limits; dependency names or source scans cannot prove complete network enforcement.

## Next Steps

- **Next:** [Story 3.17 inventory and enforcement](../../_bmad-output/implementation-artifacts/3-17-dapr-boundary-inventory-and-enforcement.md) — close evaluated graph coverage and guard negatives before claiming conformance.
- **Related:** [Architecture Overview](architecture-overview.md), [DAPR Component Reference](../guides/dapr-component-reference.md), [Story 2.13 qualification](../../_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md)
