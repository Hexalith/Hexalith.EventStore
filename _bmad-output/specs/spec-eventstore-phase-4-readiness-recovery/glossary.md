# Glossary

| Term | Meaning |
| --- | --- |
| Admin UI | `src/Hexalith.EventStore.Admin.UI`, evolved in place as the single consolidated EventStore UI under resource `eventstore-admin-ui` and FrontComposer module `event-store-admin`; it must not present unavailable operations as functional. |
| Aggregate identity | EventStore identity made from tenant, domain, and aggregate id; EventStore envelope ids use ULID-safe handling where required. |
| Architecture artifact | `_bmad-output/planning-artifacts/architecture.md`; owns Phase 4 component, integration, topology, and decision-record gates. |
| DAPR boundary | State, pub/sub, service invocation, actors, configuration, access control, and resiliency infrastructure boundary. |
| Domain module | EventStore-backed domain code package containing aggregates, commands, events, projections, query handlers, validators, and contracts, without reusable platform boilerplate. |
| Domain-service SDK | EventStore SDK surface that supplies host composition, canonical DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec. |
| External API host | Dedicated host for generated REST controllers; separate from interactive UI hosts. |
| Interactive UI host | Blazor or similar user-facing host that consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers. |
| OpenBao operational secret profile | AD-24 production profile in which DAPR component `openbao` uses `secretstores.hashicorp.vault` v1 and the value-free canonical contract drives logical names, access grants, lifecycle, readiness, and rotation evidence. |
| Parity packet | EventStore-owner-reviewed proof that generic projection/query replacements work through production paths and map an approved EventStore source SHA to exact consumed package hashes or deployed image identity. |
| Payload-protection KEK custody | Separate AD-23 and draft payload-protection concern for production `pdenc-v2` DEK wrap/unwrap; it is not supplied or approved by the AD-24 DAPR secret store. |
| Projection dispatch result | Server-owned Version 1 normalized result with bounded ordinal entries, stable status codes, and explicit checkpoint-advance state; distinct from the frozen `/project/v2` wire response. |
| Projection-confirmed success | User-visible success state backed by read-model/projection evidence, not command acceptance or SignalR notification alone. |
| Query provenance | Route-stamped `ProjectionBacked`, `HandlerComputed`, or `Unknown` classification that controls whether lifecycle, version, freshness, and ETag evidence may be treated as projection-backed. |
| Readiness recovery | Planning correction that reconciles PRD, architecture, and UX, removes later-epic prerequisites, replaces oversized stories with focused children, migrates evidence audibly, and re-runs readiness. |
| Support-safe state | UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets. |
| UX artifact | `_bmad-output/planning-artifacts/ux.md`; must own Phase 4 UI governance, user-flow evidence, and support-safe interaction rules. |
