# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Support long-lived streams with bounded snapshot/projection cost, sequence-safe delivery, stable event versions, deterministic upcasting, validated identity, and cancellation-aware processing. Specification approval authorizes only its paired implementation; delivered runtime value requires Stories 6.2, 6.4, and 6.6 together.

## Stories

- Story 6.1: Folded Snapshot Frozen Spec
- Story 6.2: Folded Snapshot Implementation
- Story 6.3: Projection Delivery Cost And Sequence Guard Spec
- Story 6.4: Projection Cost And Sequence Guard Implementation
- Story 6.5: Event Versioning And Upcasting Spec
- Story 6.5a: Event Contract, Writer, and Migration Evidence Spec
- Story 6.5b: Verified Read, Replay, and Projection Spec
- Story 6.5c: Publication, Subscription, and Rollout Spec
- Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec
- Story 6.6: Event Versioning And Upcasting Implementation
- Story 6.7: AOT And Trimming Posture Reference

## Requirements & Constraints

- Snapshots contain only folded state at one exact sequence and minimal versioned/protection metadata. Equivalent state has identical bytes regardless of history length, within an approved overhead bound. Automatic/manual snapshot-plus-tail recovery equals canonical replay, preserving legacy/protected readability. Byte bounds, migration, and protection decisions must close before implementation; historical approval alone supplies no authority.
- Current projections read zero events; incremental delivery reads a contiguous bounded tail through the admitted head, with cost following lag/page size. Unsupported handlers use canonical replay. Duplicate/gap/race/cancellation/rebuild failures preserve checkpoints and the complete live model. Paged rebuild equals canonical replay.
- New events carry canonical kebab-case type and positive payload version. Addressed tenant/domain/aggregate/type, message identity, and contiguous sequence must agree before dispatch. Legacy aliases are allow-listed; ambiguous/malformed identity fails closed.
- All consumers share bounded contiguous deterministic upcasting after readability/unprotection and before deserialization. Only a private in-memory view changes. Stored bytes, identity, sequence, and protection evidence stay immutable. Typed failures commit no partial state, checkpoint, publication marker, or handler effect.
- Cancellation propagates through processing/query/projection/read/upcast seams until the durable boundary. Pre-commit cancellation stops mutation; post-commit cancellation preserves committed truth/reconciliation. Cancellation stays distinct from rejection/failure.
- Source/binary/wire/package compatibility requires inventory-backed baselines and mixed-version/rollback evidence. Breaking changes need approved migration and SemVer-major release. AOT/trimming remains unsupported while reflection is load-bearing; release packages must reject declarations that claim either capability.

## Technical Decisions

- `AggregateActor` alone mutates admitted aggregate/event/snapshot state through `IActorStateManager` under a current execution fence. Events, metadata, snapshots, results, and outbox stage together and save once. Ambiguous saves require fresh addressed Dapr actor readback before retry/acknowledgement; unavailable readback proves no absence and permits no repeat effect.
- Story 6.6 stays strictly within Dapr: no application SQL, database driver/credential/schema, private actor-state key, provider fork, or direct-database product evidence. Control state uses qualified Dapr ETag/transactions or a dedicated actor. Separate actors/components, broker delivery, and external effects use durable intent, idempotency, and reconciliation.
- Digests and Dapr readback prove logical returned values, not physical bytes, provider attestation, or historical generations. Withdrawn SQL/provider designs grant no authority; operations needing unavailable provider proof remain disabled with typed unavailable/hold outcomes.
- Deterministic allow-listed registries reject malformed metadata, invalid chains, unsupported versions, corrupt digests, unreadable protection, and incomplete prefixes before effects. One evolution service serves every replay/projection/subscription/reconstruction/inspection consumer.
- Catalogs/dependencies are trusted deployer-reviewed code; tenants supply no code, paths, or CLR selection. Admission binds complete immutable manifests/options/artifacts to gateway/serving-peer pins; execution uses those artifacts. Missing/changed/ambiguous artifacts refuse admission; undeclared loads are prohibited. Observed violations remove capability and refuse uncommitted success, without hostile-code confinement or universal prevention of earlier effects.
- Qualification checks persisted end state through Dapr, live-sidecar crash/retry, consumer equivalence, cancellation, and compatibility. Environment blockers remain separate from product failures. Local checks grant no fleet readiness, activation, release, or promotion.

## UX & Interaction Patterns

Expose support-safe snapshot size/sequence/readability, projection head/checkpoint/lag/lifecycle, and event type/version/legacy/upcast/hop/outcome evidence. Hide payloads, CLR assembly details, secrets, provider internals, and traces. Persisted authoritative evidence establishes completion/freshness; acknowledgement or notification does not. Only projection-backed query provenance supplies lifecycle authority. Open capabilities stay unavailable; missing provenance is `Unknown`.

## Cross-Story Dependencies

- Story 6.1 is done for the specification gate after Jérôme Piquot (`jpiquot`) approved normative SHA-256 `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`, the 4096-byte overhead bound, explicit Story 6.2 authorization, and completion-record reconciliation on 2026-10-08. The inventory baseline is `7d76df4981fb070c4d84d817bf6fc800f27d0adb`. Story 6.2 stays backlog with authorization to implement the approved sections; normative byte/design drift voids that authorization. No runtime bounded-cost outcome is delivered. Story 6.3's approved specification is absent; Story 6.4 stays unauthorized and preserves Stories 1.18/1.19 correctness.
- Stories 6.5a–6.5d feed the owner-approved integrated Story 6.5 design. Story 6.6 follows compatible rules under the controlling Dapr-only and trusted-code amendments; the earlier digest remains historical approval evidence.
- Story 6.6 remains in progress. Production consumer wiring, authoritative catalogs/serving peers, immutable execution, Dapr recovery, compatibility, and fleet qualification remain open. V2 admission stays fenced until writer and all serving readers/consumers qualify; amendments grant no migration/publication/deployment/promotion authority.
- Corrective stories require Story 9.1 authorization within their bounded lane; general readiness remains blocked. Story 6.7 supplies the AOT/trimming posture and package guard. The optional Epic 8 protection engine is not a prerequisite. Event evolution does not authorize snapshot or projection-cost redesign.
