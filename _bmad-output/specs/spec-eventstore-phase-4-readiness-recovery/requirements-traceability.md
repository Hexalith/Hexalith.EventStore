# Requirements Traceability

## Capability To FR Coverage

| Capability | Functional requirements |
| --- | --- |
| CAP-1 Domain author self-service platform | FR1-FR10 |
| CAP-2 External integration surfaces | FR11-FR16 |
| CAP-3 Release and repository reliability | FR17-FR22, FR25 |
| CAP-4 Event correctness and recovery | FR23-FR24, FR27, FR29-FR31 |
| CAP-5 Security and tenant isolation | FR26, FR28, FR32 |
| CAP-6 Bounded cost and event evolution | FR33 |
| CAP-7 Operator trust, admin honesty, and future backlog | FR34-FR35 |
| CAP-8 Readiness recovery package | Readiness gates and success metrics |

## Functional Requirements

| FR | Requirement |
| --- | --- |
| FR1 | Domain modules built on Hexalith.EventStore must be domain-centric and contain domain code while EventStore libraries supply platform boilerplate. |
| FR2 | The platform must provide `AddEventStoreDomainService`, `UseEventStoreDomainService`, and `MapEventStoreDomainService`. |
| FR3 | The domain-service SDK must expose `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`. |
| FR4 | The platform must provide `IDomainQueryHandler` discovery, dispatch, operational metadata, gateway query-type capture, and handler-aware `/query` routing. |
| FR5 | The platform must provide a persisted read-model store and write policy with optimistic-concurrency merge-on-write, multi-key/index support, DAPR implementation, and in-memory testing. |
| FR6 | The platform must provide a DataProtection-backed query cursor codec with scope validation, payload limits, tamper/key-rotation handling, and caller-supplied purpose isolation. |
| FR7 | The platform must provide `/project` projection dispatch plus generic domain-event subscription/consumer plumbing with deduplication and endpoint mapping. |
| FR8 | The platform must provide Aspire, telemetry, and health-check extensions for domain modules. |
| FR9 | Sample and Tenants must adopt platform SDK seams so duplicated routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, and per-domain Aspire wiring are removed or reduced to domain logic. |
| FR10 | The EventStore package set must include domain-service and service-default packages and publish only the manifest-governed EventStore package set. |
| FR11 | The platform must provide REST API generator contracts with `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, and assembly-level `RestApiAttribute`. |
| FR12 | The REST API generator must emit typed OpenAPI-visible controllers that delegate to `IEventStoreGatewayClient`, with tests for discovery, routing, diagnostics, and output. |
| FR13 | Generated REST controllers must live in dedicated external API hosts, not interactive UI hosts. |
| FR14 | Sample must introduce a contracts-only library and external API host that proves generated query and command controllers. |
| FR15 | Tenants must move generated controllers to an external API host while Tenants UI consumes client libraries. |
| FR16 | Projection-changed transport must add metadata-rich detail with optional group scope, bounded metadata, scoped SignalR groups, optional DAPR notification support, and signal-only compatibility. |
| FR17 | Live DAPR sidecar tests must move out of the per-push release gate into a dedicated integration workflow with warm-up and readiness retry. |
| FR18 | `DaprETagService` must allow an overridable actor request timeout while preserving the production default. |
| FR19 | Root-declared Git submodules must live under `references/`, and solution, project, docs, Aspire metadata, and LLM paths must resolve through that layout. |
| FR20 | The Aspire Keycloak resource must be named `security` while preserving Keycloak implementation behavior. |
| FR21 | Cross-repo Hexalith dependencies must use Debug source project references only when explicitly enabled and Release package references by default. |
| FR22 | Release restore, build, test, pack, and semantic-release commands must assert package-reference mode and avoid packaging submodule projects. |
| FR23 | Persisted events must receive non-zero actor-allocated global positions; CloudEvent ids must use event `MessageId`; duplicate command replies must preserve original result fields. |
| FR24 | Global-position allocation must be renegotiated toward tenant/domain sharding, and the frozen global-ordering spec must be updated before implementation. |
| FR25 | EventStore workflows must use shared Hexalith.Builds security gates through `@main`, keep third-party actions SHA-pinned through shared workflows, and define NuGet publish scope in `tools/release-packages.json`. |
| FR26 | Phase 0 remediation must clear staged state on infrastructure failure, protect anonymous admin endpoints, strip committed admin secrets, enforce production auth guards, add tenant-filter parity, gate admin Swagger, require destructive CLI confirmation, use ULID-safe admin correlation middleware, and correct stale test-baseline docs. |
| FR27 | Pipeline remediation must use `MessageId`, `CausationId`, and `CommandType` for resume/idempotency matching; key command status/archive by message id; preserve transient retryability; validate tenant access before idempotency reads. |
| FR28 | Trust-boundary remediation must require app-layer credentials for internal, domain-service, projection-notification, and admin-computation endpoints and remove wire-asserted administrator trust. |
| FR29 | Replay and dispatch remediation must make apply-method resolution boundary-safe and ambiguity-detecting and use one shared `JsonSerializerOptions` path for command, rehydrate, project, and pub/sub payloads. |
| FR30 | Crash recovery must detect committed-but-unpublished events and complete publication or drain/recover them without resubmission using the same correlation id. |
| FR31 | Append durability remediation must start with a live-sidecar two-writer race test and DAPR conflict-exception spike before choosing optimistic-concurrency fencing. |
| FR32 | Runtime topology remediation must align AppHost-loaded DAPR pub/sub, ACL, and key-prefix posture with tests and production templates. |
| FR33 | Cost/evolution remediation must introduce folded snapshots, lower projection replay cost, projection sequence guards, event versioning/upcasting, event metadata identity validation, and cancellation-token seams. |
| FR34 | Delivery/admin/deployment remediation must document at-least-once unordered delivery, add poison/dead-letter handling, bound in-memory deduplication, normalize admin claims, audit state-mutating admin actions, hide deferred admin operations, add secret-store-backed config, add readiness/app-health checks, and restore meaningful IntegrationTests coverage. |
| FR35 | Backlog capabilities must be tracked for GDPR aggregate erasure/tombstoning, Admin interactive OIDC login, aggregate test kit, and REST generator hardening. |

## Non-Functional Requirements

| NFR | Requirement |
| --- | --- |
| NFR1 | Security must fail closed for public, internal, domain-service, projection-notification, and admin surfaces; no endpoint may rely only on network posture or caller-supplied admin flags. |
| NFR2 | Tenant isolation must be preserved across state keys, actor IDs, topics, admin queries, generated REST APIs, SignalR groups, and deployment configuration. |
| NFR3 | Production authentication must reject insecure symmetric-key mode unless explicitly break-glassed, require HTTPS metadata where appropriate, and pin accepted JWT algorithms. |
| NFR4 | Committed configuration must not contain forgeable administrator signing keys, credentials, bearer tokens, decoded JWT payloads, or operational secrets. |
| NFR5 | SignalR detail metadata must remain bounded and metadata-only; framework logs must not expose metadata values above Debug level. |
| NFR6 | Event delivery semantics are at-least-once and unordered; subscribers must deduplicate by `MessageId` and order only where domain semantics make `SequenceNumber` meaningful. |
| NFR7 | Event persistence and command processing must avoid silent data loss from staged-state flushes, stale pipeline records, append races, and committed-but-unpublished events. |
| NFR8 | Snapshot and projection behavior must have a bounded cost model as streams grow and must avoid unnecessary full-stream replay when already current. |
| NFR9 | Release behavior must be reproducible and independent of local submodule checkout state. |
| NFR10 | CI/CD must separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane. |
| NFR11 | Package publishing must be manifest-driven and must not publish submodule packages or packages outside EventStore release inventory. |
| NFR12 | Backward compatibility must be preserved for additive framework changes such as SignalR signal-only notifications and existing generic gateway APIs. |
| NFR13 | Generated code and source-generator packages must build cleanly under warnings-as-errors and follow EventStore style, nullable, ULID, and `ConfigureAwait(false)` rules. |
| NFR14 | Interactive UI hosts must not expose generated or hand-written per-message MVC command/query controllers; UI flows consume client libraries. |
| NFR15 | Admin UX must not present deferred backup, restore, import, compaction, or other unavailable operations as functional. |
| NFR16 | Integration and higher-tier tests must assert persisted state-store/read-model/end-state evidence, not only HTTP status codes or mock call counts. |
| NFR17 | Operational hardening must support secret stores, DAPR app health checks, readiness-tagged health checks, resiliency targets, immutable image tags, and documented crypto-shred boundaries. |
| NFR18 | AOT/trimming is explicitly not a target while reflection conventions remain load-bearing. |
