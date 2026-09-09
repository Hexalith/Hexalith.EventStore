# Architecture Reconciliation

## Authority

`_bmad-output/planning-artifacts/architecture.md` is the governing architecture at SHA-256
`2678116099e3d1c1f68ee38ef344b9bef5a58a82062a800e5a89b8b0f5774395`; its decision memlog is
`5b6fa6ec60261de4be496a8350b048e8cf5381d6cd0a8681cc475e69cbd4f793`. Exactly AD-1 through
AD-33 govern. Later wording within an existing AD supersedes earlier wording without renumbering
the decision, capability, story, or implementation-spec identity.

Existing implementation specifications remain historical records. Where a prior spec conflicts
with this table, its identity and evidence stay intact but the conflicting rule cannot authorize new
implementation, completion, production readiness, deployment, or consumer migration.

## Decision Dispositions

| Decision | Reconciled implementation-spec consequence | Upstream disposition |
| --- | --- | --- |
| AD-1 | CQRS, DDD, event sourcing, and DAPR remain the platform paradigm; Aspire is a local seed and AD-26 alone governs production. | Compatible; narrow any spec that treats AppHost or local DAPR success as production proof. |
| AD-2 | Domain modules keep behavior and contracts only; reusable hosting, routing, persistence, health, and telemetry stay in EventStore packages. | Compatible with Epic 1. |
| AD-3 | The gateway remains the command/query policy edge; only named tenant-authorized support-safe read adapters may inspect platform operational state. | Supersedes absolute “no direct state reads” wording; mutations and generic-key access remain forbidden. |
| AD-4 | Generated REST remains confined to dedicated external API hosts using `IEventStoreGatewayClient`. | Compatible with Epic 2. |
| AD-5 | Every production aggregate mutation or side effect requires a nonempty idempotency key, successful AD-25 admission, and the current nonzero fence. | Epic 4 must cover all protected paths; optional or null-key execution is Development compatibility only. |
| AD-6 | Aggregate sequence, nonzero global position, persisted `MessageId`, and exact duplicate reply remain stable. | Compatible with Epic 4; provider write-once remains unproven. |
| AD-7 | MVP erasure is only typed, scoped, idempotent, read-back-proven projection model/checkpoint removal. | Story 1.14 keeps its identity and evidence but must never claim AD-30 or GDPR completion. |
| AD-8 | Delivery is at-least-once/unordered, projection success is evidence-backed, and terminal rejection is acknowledged only after AD-31 durable capture. | Story 7.1 requires an AD-31 sink and explicit unknown/no-handler policy. |
| AD-9 | AppHost, YAML, catalogs, app IDs, scopes, ACLs, resiliency, topics, tests, and operator docs change together. | Stories 5.6-5.9 must add AD-26 and AD-33 fingerprints and reject stale deployment prose. |
| AD-10 | Authorization stays application-layer and current; telemetry and support output exclude protected data; the shared exact JWT contract governs every binding host. | PRD NFR1/NFR3 align; Story 5.3 must use the final NFR3 contract. |
| AD-11 | Packages and the sole current `eventstore` image remain manifest/release-identity governed; candidate identity does not grant deployment. | `eventstore-operations` needs its own immutable release identity before AD-31 production use. |
| AD-12 | High-risk closure requires persisted, topology, security, restart, and immutable release evidence. | Compatible; environment failure remains unproven rather than passed. |
| AD-13 | Snapshot, projection-cost/sequence, and upcaster changes remain spec-first. | Compatible with Epic 6; an approved spec is authority to start, not delivery evidence. |
| AD-14 | Query payload and platform metadata remain separate; only projection-backed routes may assert authoritative freshness. | Compatible with query/provenance specs. |
| AD-15 | `ProjectionVersion` is an optional persisted, bounded, route-scoped opaque equality token, never ordered progress. | Supersedes specs that derive progress from the token or ETag. |
| AD-16 | Every HTTP host installs an authenticated `FallbackPolicy`; only `/health`, `/alive`, and `/ready` are explicitly anonymous. | Direct conflict: Story 5.3 says it does not introduce a fallback policy; epic and implementation spec must be amended before closure. |
| AD-17 | Generated `202` responses emit an absolute gateway-authoritative status location or none; `MessageId` alone selects status. | Compatible with Story 2.9; correlation remains diagnostic only. |
| AD-18 | One outbound handler replaces DAPR control-plane headers from trusted configuration. | Story 2.10 remains valid but does not satisfy inbound AD-28 authentication. |
| AD-19 | `ProjectionDispatchResponse` v2 and `ProjectionDispatchOutcome` are the cross-service carrier; normalized route/checkpoint state is server-owned and persisted. | Supersedes the Phase 4 SPEC’s former public `ProjectionDispatchResult` Version 1 wording; Story 1.17’s identity stays stable. |
| AD-20 | Rebuild pages are transport units; staged output must equal canonical replay and failure keeps the last complete live model. | Focused rebuild SPEC remains valid with AD-26/27/28/32/33 production constraints added. |
| AD-21 | The existing Admin UI remains the sole EventStore UI; dependency versions come from the Builds catalog. | Supersedes the stale FrontComposer `4.1.1` literal; do not freeze the current `4.4.0` rendering as authority either. |
| AD-22 | Consumer removal requires an unchanged content-bound parity packet and separate authenticated Consumer-owner receipt. | Compatible; story status or EventStore approval never grants removal. |
| AD-23 | EventStore owns the optional shared payload engine; domains own legal policy and operators own key custody. | Compatible with Epic 8; AD-30 owns full erasure workflow semantics. |
| AD-24 | OpenBao is the production DAPR secret store; app-token rollout and digest-key retirement are coupled to cataloged consumer acknowledgement and zero references. | Story 7.6 must validate the shared AD-25/AD-33 catalog generation before retiring keys. |
| AD-25 | Admission retains one tenant/digest authority and now consumes the idempotency facet of the AD-33 catalog envelope. | Stories 4.9-4.15 keep their identities; any separate idempotency catalog is superseded. |
| AD-26 | Only the content-bound self-managed Kubernetes/PostgreSQL v1/durable-broker/OpenBao profile can authorize production; Redis is Development/test only and Cosmos is an alternative, not evidence. | Epic update required. Story 4.14 remains OQ8 correctness evidence; Stories 5.7-5.9 cannot offer multiple provider variants as equivalent production authorities. |
| AD-27 | `Contracts` owns one lowercase tenant grammar; every boundary requires one explicit request tenant and matching `eventstore:tenant` grant; public `system` and inferred wildcard scope are rejected. | Epic update required for Stories 5.2, 5.10, 7.2, and every tenant-facing boundary. Story 5.10’s internal `system`/global-admin wording is superseded by the distinct cataloged platform namespace. |
| AD-28 | Every non-Development DAPR app endpoint validates `dapr-api-token` against startup `APP_API_TOKEN`; this authenticates the channel, not caller claims. | Direct conflict: Story 5.5 permits alternate workload credentials as substitutes. Amend Story 5.5 and affected domain/projection endpoint specs. |
| AD-29 | Admin mutations preserve authenticated operator identity or exact bounded delegation and use one resumable mutation/audit state machine. | Epic update required for Story 7.3 and every Admin mutation consumer; generic actor strings or separate best-effort audit are insufficient. |
| AD-30 | Domain legal policy owns a stable full-erasure workflow; EventStore executes fenced phases and reports every storage/copy/legal-hold facet separately. | Story 7.15 must be amended or followed by post-MVP implementation stories. No PRD change is needed unless full erasure moves into the MVP. |
| AD-31 | `Hexalith.EventStore.Operations` owns durable poison capture/replay under `eventstore-operations` but is non-production until all topology, release, auth, audit, tenant, target, catalog, and capture gates pass. | Epic update required for Story 7.1; split Operations wiring/release from subscriber semantics if one story would become oversized. |
| AD-32 | `Contracts` owns `X-Correlation-ID` with the exact 1-128 ASCII alphanumeric/hyphen grammar; only the first public boundary mints and downstream hops never remint. | Epic update required for Story 5.4 and downstream integration/Admin stories; reject invalid internal replacements and never use correlation for status. |
| AD-33 | One retained-byte routing/idempotency catalog binds gateway, admission, domain, projection, AppHost, and ACL topology through prepare/ready/commit activation. | Epic update required. Story 1.17’s projection catalog is only one facet and cannot claim unified production routing. |

## Conflicts Requiring Upstream Updates

### PRD

No new product capability or scope decision is required: FR1-FR37 and NFR1-NFR19 can contain all
AD-1 through AD-33 consequences, NFR1/NFR3 already match amended AD-16/AD-10, and full erasure
remains post-MVP. A PRD maintenance update is nevertheless required before readiness can pass:

- close OR14 by binding the new architecture identity and then regenerating the epic input digests;
- define “production-path” evidence in NFR7/NFR16 through AD-26 so Redis and isolated
  `oq8-postgresql-v1` evidence cannot be mistaken for full production-profile authority;
- include the new AD-26 through AD-33 ownership rows in the FR/NFR-to-epic drift guard required by
  OR8 and the clause-level exit accounting required by OR7/OR17.

If AD-30 full erasure is promoted from the existing post-MVP backlog into Phase 4 MVP, FR35, the
MVP scope, NFR ownership, success metrics, and legal-policy authority require a substantive PRD
change first. This reconciliation does not make that promotion.

### Epics and implementation specs

An epic update is required before the next readiness pass. `epics.md` records zero references to
AD-26 through AD-33 and its stored PRD and architecture digests are stale. Preserve every existing
story and spec identity; add focused successor stories when amending a completed story would imply
new delivery evidence.

Minimum corrections:

1. Amend Story 5.3 for mandatory `FallbackPolicy` and remove any Production symmetric-key
   break-glass allowance; amend Story 5.5 to require the exact AD-28 app-channel protocol.
2. Give AD-26 one production-profile owner spanning Stories 5.6-5.9 and 7.6-7.9, or add a focused
   successor; narrow Story 4.14 and every Redis lane to the claim actually proven.
3. Give AD-27 one Contracts implementation and migration owner, then update Stories 5.2, 5.10,
   7.2, REST, SignalR, admission, and Admin boundaries to consume it.
4. Amend Story 7.3 for AD-29 delegation and resumable audit phases; amend Story 7.1 for the AD-31
   Operations sink, capture-before-ack, release/topology gates, and cataloged skip outcomes.
5. Reconcile Story 1.14 and Story 7.15 with the AD-7/AD-30 phase boundary without changing their
   identities or retroactively claiming full erasure.
6. Amend Story 5.4 and downstream client/host stories for AD-32 first-boundary versus downstream
   behavior.
7. Add an AD-33 unified-catalog slice spanning Contracts, gateway, admission, projection, AppHost,
   deployment ACLs, activation, rollback, and drift evidence; do not relabel Story 1.17’s
   projection-only catalog as complete coverage.

The owning PRD/epic workflows must perform those upstream edits. This spec update reports and
contains the conflicts but does not mutate the PRD, epics, story files, sprint tracker, code,
deployment assets, or external systems.
