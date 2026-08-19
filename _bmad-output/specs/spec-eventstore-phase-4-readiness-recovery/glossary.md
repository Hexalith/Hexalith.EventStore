# Glossary

| Term | Meaning |
| --- | --- |
| Admin UI | `src/Hexalith.EventStore.Admin.UI`, evolved in place as the single consolidated EventStore UI under resource `eventstore-admin-ui` and FrontComposer module `event-store-admin`; it must not present unavailable operations as functional. |
| Aggregate identity | EventStore identity made from tenant, domain, and aggregate ID; EventStore envelope IDs use ULID-safe handling where required. |
| Architecture artifact | Finalized `_bmad-output/planning-artifacts/architecture.md`; owns Phase 4 component, integration, topology, and stable AD-1 through AD-25 decision-record gates. Its memlog is the decision authority for this spec derivation. |
| Consumer-removal receipt | Signature- or immutable-identity-verified Consumer-owner approval over the unchanged consumer identity, parity packet, capability catalog, applicable-mode matrix, and exact removal-subject digests, with outcome `consumer-removal-authorized`, timestamp, and validity; EventStore-side approvals cannot substitute for it. |
| DAPR boundary | State, pub/sub, service invocation, actors, configuration, access control, and resiliency infrastructure boundary. |
| Domain module | EventStore-backed domain code package containing aggregates, commands, events, projections, query handlers, validators, and contracts, without reusable platform boilerplate. |
| Domain-service SDK | EventStore SDK surface that supplies host composition, canonical DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec. |
| Enabler story | A specification or readiness story that authorizes later implementation but does not count as runtime implementation progress; Epic 6 uses this classification for Stories 6.1, 6.3, and 6.5. |
| Evidence ledger | A non-executable planning record that preserves scope, evidence, status, and migration links while focused child stories own implementation; Story 4.8 is the durable-admission ledger for Stories 4.9-4.15. |
| External API host | Dedicated host for generated REST controllers; separate from interactive UI hosts. |
| Interactive UI host | Blazor or similar user-facing host that consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers. |
| OpenBao operational secret profile | AD-24 production profile in which DAPR component `openbao` uses `secretstores.hashicorp.vault` v1 and the value-free canonical contract drives logical names, access grants, lifecycle, readiness, and rotation evidence. |
| Parity packet | Content-addressed proof that generic projection/query replacements work through production paths. Story 1.20 records approved source/package identity; Story 3.13 records rejected `v3.94.1` evidence; Story 3.15 may add positive deployed identity for the separately authorized Story 3.14 release. |
| Payload-protection KEK custody | Separate AD-23 and draft payload-protection concern for production `pdenc-v2` DEK wrap/unwrap; it is not supplied or approved by the AD-24 DAPR secret store. |
| Payload-protection sequence | The post-MVP chain `8.2 -> 8.3 -> (8.4 and 8.5) -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10 -> 8.11`, initially authorized only by Story 8.1 and thereafter by predecessor evidence. |
| Projection dispatch result | Server-owned Version 1 normalized result with bounded ordinal entries, stable status codes, and explicit checkpoint-advance state; distinct from the frozen `/project/v2` wire response. |
| Projection-confirmed success | User-visible success state backed by read-model/projection evidence, not command acceptance or SignalR notification alone. |
| Query provenance | Route-stamped `ProjectionBacked`, `HandlerComputed`, or `Unknown` classification that controls whether lifecycle, version, freshness, and ETag evidence may be treated as projection-backed. |
| Release evidence codec | AD-11 platform-owned versioned codec that emits the retained canonical UTF-8 bytes hashed by release-identity producers and verifiers without reserialization. |
| Release identity | Canonical AD-11 record binding repository, version/tag, source, workflow and Builds revisions, one-use authority, package inventory and digests, registry, OCI graph, smoke evidence, and codec/verifier identity into one lineage. |
| Readiness recovery | Planning correction that reconciles PRD, architecture, and UX, removes forward prerequisites, replaces oversized stories with focused children, migrates evidence auditably across the eight-epic plan, and re-runs readiness. The 2026-08-16 deployed-parity handoff stays blocked until one content-addressed planning set proves that epics, Story 3.13-3.15 specifications, and sprint tracking changed together. |
| Reserved system tenant | The normalized tenant identity `system`, which Story 5.10 rejects at the provisioning boundary before state access or side effects. |
| Support-safe state | UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets. |
| UX artifact | `_bmad-output/planning-artifacts/ux.md`; must own Phase 4 UI governance, user-flow evidence, and support-safe interaction rules. |
