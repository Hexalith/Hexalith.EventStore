# Glossary

| Term | Meaning |
| --- | --- |
| Admin attribution | AD-29 identity chain preserving either the authenticated operator or an issuer-validated bounded delegation across one resumable mutation/audit unit. |
| Admin UI | `src/Hexalith.EventStore.Admin.UI`, evolved in place as the single consolidated EventStore UI under resource `eventstore-admin-ui` and FrontComposer module `event-store-admin`; it must not present unavailable operations as functional. |
| Aggregate identity | EventStore identity made from canonical tenant, domain, and aggregate ID; EventStore envelope IDs use ULID-safe handling where required. |
| Architecture artifact | Finalized `_bmad-output/planning-artifacts/architecture.md`; owns Phase 4 component, integration, topology, and stable AD-1 through AD-33 decision-record gates. Its memlog is the decision authority for this spec derivation. |
| Canonical tenant | Exactly one explicit lowercase tenant satisfying the 1-64-character `AggregateIdentity` grammar at every boundary; request and `eventstore:tenant` grant values must agree. |
| Consumer-removal receipt | Signature- or immutable-identity-verified Consumer-owner approval over the unchanged consumer identity, parity packet, capability catalog, applicable-mode matrix, and exact removal-subject digests, with outcome `consumer-removal-authorized`, timestamp, and validity; EventStore-side approvals cannot substitute for it. |
| Correlation identity | `X-Correlation-ID` value accepted or minted only at the first public boundary, constrained to 1-128 ASCII alphanumeric or hyphen characters, and propagated unchanged downstream; it is diagnostic, not command-status or trace identity. |
| DAPR boundary | State, pub/sub, service invocation, actors, configuration, access control, and resiliency infrastructure boundary. |
| DAPR app channel | Non-Development sidecar-to-app channel authenticated by `dapr-api-token` against startup `APP_API_TOKEN`; the token does not authenticate a claimed caller app ID. |
| Dead-letter capture | AD-31 durable tenant/domain poison record keyed by `MessageId`; broker acknowledgement is forbidden until capture succeeds or a separately proven durable quarantine accepts the message. |
| Domain module | EventStore-backed domain code package containing aggregates, commands, events, projections, query handlers, validators, and contracts, without reusable platform boilerplate. |
| Domain-service SDK | EventStore SDK surface that supplies host composition, canonical DAPR endpoints, discovery, telemetry, health checks, projection dispatch, query routing, event consumers, read-model store, and cursor codec. |
| Enabler story | A specification or readiness story that authorizes later implementation but does not count as runtime implementation progress; Epic 6 uses this classification for Stories 6.1, 6.3, and 6.5. |
| Evidence ledger | A non-executable planning record that preserves scope, evidence, status, and migration links while focused child stories own implementation; Story 4.8 is the durable-admission ledger for Stories 4.9-4.15. |
| External API host | Dedicated host for generated REST controllers; separate from interactive UI hosts. |
| Interactive UI host | Blazor or similar user-facing host that consumes EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers. |
| Erasure workflow | AD-30 post-MVP domain-policy-owned workflow that coordinates fenced EventStore mechanics and reports logical, projection, cryptographic, broker, backup, restore, cache, export, replica, and legal-hold facets separately. |
| OpenBao operational secret profile | AD-24 production contract in which DAPR component `openbao` uses `secretstores.hashicorp.vault` v1 and the value-free canonical contract drives logical names, access grants, lifecycle, readiness, and rotation evidence; it is one facet of AD-26. |
| Parity packet | Content-addressed proof that generic projection/query replacements work through production paths. Story 1.20 records approved source/package identity; Story 3.13 records rejected `v3.94.1` evidence; Story 3.15 may add positive deployed identity for the separately authorized Story 3.14 release. |
| Payload-protection KEK custody | Separate AD-23 and draft payload-protection concern for production `pdenc-v2` DEK wrap/unwrap; it is not supplied or approved by the AD-24 DAPR secret store. |
| Payload-protection sequence | The post-MVP chain `8.2 -> 8.3 -> (8.4 and 8.5) -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10 -> 8.11`, initially authorized only by Story 8.1 and thereafter by predecessor evidence. |
| Production profile | AD-26 content-bound self-managed Kubernetes posture using independent DAPR sidecars, PostgreSQL v1 actor state, an approved durable broker, resiliency, OpenBao, activated catalogs, restore posture, and required production evidence. |
| Projection dispatch carrier | `ProjectionDispatchResponse` v2 with `ProjectionDispatchOutcome`, the sole cross-service projection carrier; the server persists its normalized per-route result and checkpoint decision matrix. |
| Projection-confirmed success | User-visible success state backed by read-model/projection evidence, not command acceptance or SignalR notification alone. |
| Query provenance | Route-stamped `ProjectionBacked`, `HandlerComputed`, or `Unknown` classification that controls whether lifecycle, version, freshness, and ETag evidence may be treated as projection-backed. |
| Release evidence codec | AD-11 platform-owned versioned codec that emits the retained canonical UTF-8 bytes hashed by release-identity producers and verifiers without reserialization. |
| Release identity | Canonical AD-11 record binding repository, version/tag, source, workflow and Builds revisions, one-use authority, package inventory and digests, registry, OCI graph, smoke evidence, and codec/verifier identity into one lineage. |
| Readiness recovery | Planning correction that reconciles PRD, architecture, UX, epics, implementation specs, evidence, and tracking before a fresh readiness verdict. Current reconciliation is blocked on the AD-26 through AD-33 epic handoff and existing PRD lifecycle/exit gates. |
| Reserved system tenant | The normalized value `system`, rejected at every public tenant boundary. Internal platform operations use a distinct cataloged namespace and never synthesize tenant or global-administrator authority from a `system:*` subject. |
| Routing/idempotency catalog | AD-33 canonical retained-byte `eventstore-routing-catalog.json` envelope joining routes and AD-25 admission facets through stable route-entry IDs, one root digest, and one activated generation. |
| Support-safe state | UI, logs, diagnostics, and errors that do not expose tokens, decoded JWT payloads, raw metadata, raw payloads, cursor internals, ETag internals, stack traces, or secrets. |
| UX artifact | `_bmad-output/planning-artifacts/ux.md`; must own Phase 4 UI governance, user-flow evidence, and support-safe interaction rules. |
