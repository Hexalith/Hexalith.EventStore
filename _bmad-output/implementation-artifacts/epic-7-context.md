# Epic 7 Context: Operators Can Diagnose, Recover, and Administer Honestly

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give operators a trustworthy way to investigate delivery and projection failures, recover poison events, and administer authorized tenants. Make capability availability and recovery outcomes visible in one accessible Admin dashboard. Align production deployment with the runtime contract and prove operational claims through persisted evidence.

## Stories

- Story 7.1: Delivery Contract And Poison Handling
- Story 7.2: Admin Claims Normalization
- Story 7.3: State-Mutating Admin Audit
- Story 7.4: Honest Deferred Admin Operations
- Story 7.5: Shared Typed Admin Client
- Story 7.6: OpenBao-Backed DAPR Secret Store
- Story 7.7: Readiness And DAPR App-Health
- Story 7.8: DAPR Resiliency
- Story 7.9: Immutable Production Images
- Story 7.10: Integration CI Recovery
- Story 7.11: Persisted-State Evidence And Read-Back Helpers
- Story 7.12: Fake And Integration Test Reclassification
- Story 7.13: Advisory And Performance Workflow Hygiene
- Story 7.14: Admin Shell And Canonical Route Migration
- Story 7.15: GDPR Aggregate Erasure Backlog
- Story 7.16: Admin Interactive OIDC Backlog
- Story 7.17: Aggregate Test-Kit Backlog
- Story 7.18: REST Generator Hardening Backlog
- Story 7.19: Admin Typed-Client And Evidence-State Integration
- Story 7.20: Admin Accessibility Localization And Responsive Conformance
- Story 7.21: Crypto-Shred Boundary Documentation And Evidence
- Story 7.22: Production Restore Posture Including Scheduler State
- Story 7.23: Preserve Stream Identity And Committed Activity Counts

## Requirements & Constraints

- Domain-event pub/sub delivery is at least once and unordered. The CloudEvent `id` is the persisted `MessageId`. External consumers deduplicate on the received `(source, id)` within each subscription. The EventStore processor uses persisted `MessageId` in its subscription scope. Sequence guards apply within the addressed aggregate and projection. Preserve case-sensitive aggregate identity and derive committed activity counts from durable evidence.
- A completed exact duplicate acknowledges without rerunning handlers. An active duplicate retries. Conflicting or ambiguous outcomes require explicit reconciliation. Development markers have bounded retention and capacity with active-owner protection. Exhausted or terminally rejected deliveries acknowledge only after tenant/domain-scoped durable poison capture. Unproven capture remains retryable or goes to a proven durable quarantine. Preserve stable event identity and bounded failure metadata in poison evidence. Restrict evidence and queries to authorized scope, protect continuations, and exclude payloads, credentials, and raw errors.
- Authorize Admin routes through canonical application identity, claims, tenant and permission checks. Mutation audit must be durably admitted before effects and preserve attributable, resumable end-state. Keep restore and import actions hidden. Backup and compaction may show read-only tracking. Retained deferred endpoints return typed 501 after auth, scope, and input validation and perform zero mutations.
- Use OpenBao through Dapr for production secrets with default-deny access and scoped secret references. Restrict Kubernetes credentials to approved bootstrap and dedicated cryptography exceptions. Bind readiness, app health, resiliency targets, and image digests to the actual deployed topology. Preserve the documented limits on MVP erasure claims.
- Run IntegrationTests for each push to main and prove persisted outcomes via the owning Dapr or actor boundary. Keep deterministic release gates distinct from live-sidecar, Aspire, and advisory evidence. Classify tests by the boundaries they exercise and record failed or unavailable proofs honestly. Restore posture must account for scheduler state and preserve admission, fence, event, erasure, and allocator invariants.

## Technical Decisions

- Operations owns poison capture and replay under `eventstore-operations`. Production eligibility requires verified AppHost, subscription and deployment wiring, immutable release identity, app-channel authentication and route-admitted credentials, attributable audit, tenant/target validation, matching catalog identity, and capture-before-ack proof. Subscriber poison handling and producer publication recovery are distinct paths.
- Treat routing catalog, ACLs, app IDs, components, subscriptions, and runtime proof as one topology contract. Probe paths `/health`, `/alive`, and `/ready` are support-safe anonymous endpoints. Interactive-host exceptions are explicitly inventoried data-free assets and auth callbacks. Application authorization remains fail closed.
- Admin UI and CLI consume one typed Admin contract. Consolidate the existing UI host with FrontComposer and Fluent UI V5. SignalR and delivery observations can prompt refresh. Authoritative state establishes projection freshness, command completion, and recovery outcomes.
- Derive deployment authority from the validated immutable OCI index and exact lineage. Local candidate tests and a story status do not grant deployment or release authority.

## UX & Interaction Patterns

- Route-derived navigation retains one owner for every view. Present visible tenant/domain scope, dead-letter count and age, category, retryability, and stable action identity from authoritative reads. Denial omits hidden record existence. Unavailable evidence remains visibly unavailable.
- Accepted operations retain an identity and a usable status path. Show terminal and projection-confirmed outcomes when supported by their required evidence. Retry and archive require current delivery, authorization, audit, readiness, and proof gates. Confirmation preserves scope, actor, effect, and risk at every viewport.
- Use resource-backed complete strings, stable selector hooks, keyboard and screen-reader semantics, scoped live regions, reduced motion, and responsive reflow. Keep triage context visible and associate disabled actions with their reasons and safe next steps.

## Cross-Story Dependencies

- Delivery builds on stable event identity, committed-publication recovery, and protected tenant reads. Bounded subscriber exhaustion supports resiliency and later Admin recovery. The external subscriber guidance shares the publisher’s CloudEvents identity contract.
- Claims normalization enables attributable audit. Deferred behavior and shared clients feed the shell. The shell and delivery/audit evidence feed typed operator journeys, which supply the surfaces for accessibility, localization, and responsive verification.
- Topology parity underpins production secret, health, resiliency, and runtime test proof. A conforming corrective release and exact lineage support immutable deployment. Integration-lane ownership precedes shared readback, tier reclassification, and advisory hygiene. Production restore posture uses the exact candidate runtime and requires the profile owner’s acceptance.
- GDPR aggregate erasure, interactive OIDC, aggregate test kit, and REST generator hardening are planning work. Runtime delivery for any of these requires separate authorization.
