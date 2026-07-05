# Glossary

| Term | Meaning |
| --- | --- |
| Admin UI | EventStore administrative Blazor surface. It must not present unavailable backup, restore, import, compaction, or deferred operations as functional. |
| Aggregate identity | EventStore identity made from tenant, domain, and aggregate id; EventStore envelope ids use ULID-safe handling where required. |
| Architecture artifact | `_bmad-output/planning-artifacts/architecture.md`; owns Phase 4 component, integration, topology, and decision-record gates. |
| DAPR boundary | State, pub/sub, service invocation, actors, configuration, access control, and resiliency infrastructure boundary. |
| Domain module | EventStore-backed domain code package containing aggregates, commands, events, projections, query handlers, validators, and contracts, without reusable platform boilerplate. |
| Domain-service SDK | EventStore SDK surface that supplies host composition, canonical DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec. |
| External API host | Dedicated host for generated REST controllers; separate from interactive UI hosts. |
| Interactive UI host | Blazor or similar user-facing host that consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers. |
| Projection-confirmed success | User-visible success state backed by read-model/projection evidence, not command acceptance or SignalR notification alone. |
| Readiness recovery | Planning correction that creates PRD, architecture, and UX artifacts, splits oversized stories, maps high-risk NFRs, and re-runs readiness before Phase 4 execution. |
| Support-safe state | UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets. |
| UX artifact | `_bmad-output/planning-artifacts/ux.md`; must own Phase 4 UI governance, user-flow evidence, and support-safe interaction rules. |
