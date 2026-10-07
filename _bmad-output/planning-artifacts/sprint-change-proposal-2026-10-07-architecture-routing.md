# Sprint Change Proposal: Owners For The 2026-10-07 Architecture Routed Items

Date: 2026-10-07
Project: Hexalith.EventStore
Requested by: Administrator
Trigger: [Phase 4 architecture handoff, 2026-10-07](architecture/architecture-eventstore-2026-07-05/reviews/phase-4-architecture-handoff-2026-10-07.md), "Routed items" (spine `architecture.md`, SHA-256 `1ff06c5ecc94003765a2fa6fd39491d9ccdb6d5b09419fcf7c8755c30d2d31e5`, `status: draft`)
Builds on: [sprint-change-proposal-2026-10-07.md](sprint-change-proposal-2026-10-07.md) (approved and applied). Nothing in it is reverted or reworded.
Review mode: Incremental, grouped; each group is approvable on its own
Status: Proposed. Awaiting owner approval per group.
Scope: Moderate. Six new backlog stories, extensions to Stories 2.14, 2.15, 3.19, 4.16, 5.7, and 9.3, one staged AD-ID propagation, and five PRD items routed to the product owner

## 1. Issue Summary

The 2026-10-07 architecture update left routed items that no story owns. Without owners:

- Stories 2.14 and 2.15 have no catalog to build on.
- The Story 3.19 issue step lacks four inputs: a durable broker, a DAPR runtime pin, a restore posture, and `ReleaseEvidenceCodec`.
- The AD-16 fallback and the Dapr framework routes stay open on every host except the DomainService SDK.
- G-TENANT would leave the gateway's own tenant validator and the existing `system` tenant behind.

**Problem statement.** This is an ownership gap from the architecture update, not a new requirement. Every item is an adopted rule or an explicit spine routing ("no owning story yet … routes to correct-course"). This proposal assigns owners only. No gate result changes, and readiness stays FAIL.

**Baseline correction.** The request describes the 2026-10-07 correct-course and the spine as uncommitted. They are not. Commit `48ef7171` ("test: add BoundedV1DomainResultProducerScratchTests…", 2026-10-07 09:34 +02:00) committed them, and it is pushed: `HEAD` = `origin/main` = `48ef7171`, with a clean worktree. Its subject does not describe these files. This proposal works from that commit.

### Evidence, re-verified at `48ef7171`

| Item | Observation |
| --- | --- |
| AD-33 catalog | No catalog schema or codec type exists in `Hexalith.EventStore.Contracts`. `deploy/dapr/eventstore-routing-catalog.json` and `deploy/dapr/production-profile.yaml` are absent. |
| Broker | `deploy/dapr` holds `pubsub-kafka.yaml`, `pubsub-rabbitmq.yaml`, and `pubsub-servicebus.yaml`. Nothing selects one. |
| Runtime pin | `.github/workflows/integration.yml` pins runtime `1.18.2` and CLI `1.18.0`. `deploy/README.md` pins `daprd` `1.18.0`, and `docs/guides/deployment-kubernetes.md` pins `1.14.4`. `integration.yml` and `ci.yml` are hashed OQ8 v3 gate inputs (`tools/validate-oq8-platform-evidence.py:521-522`). |
| Restore posture | No restore-posture artifact exists in `docs/` or `deploy/`. |
| AD-16 fallback | `FallbackPolicy` appears only in the DomainService SDK (`EventStoreDomainServiceSecurityExtensions.cs`). The gateway (`src/Hexalith.EventStore/Program.cs:47-48`) and Operations (`src/Hexalith.EventStore.Operations/Program.cs:43-44`) map `MapSubscribeHandler()` and `MapActorsHandlers()` with no policy. Story 5.5 review deferrals D-1 and D-2 in `deferred-work.md` record both gaps. |
| Peer ACLs | `/**` grants: `deploy/dapr/accesscontrol.yaml:42`, AppHost `accesscontrol.yaml:43,52`, `accesscontrol.tenants.yaml:23`, `accesscontrol.eventstore-admin.yaml:32`, `accesscontrol.sample.yaml:23`. |
| Operations | `DeadLetterOperationsEndpointExtensions.IsAuthorized` (`:227-234`) admits a request when `dapr-caller-app-id` equals the configured admin app ID and any non-empty `Bearer` value is present. The token is never validated. |
| UI hosts | Admin UI (`AdminUIServiceExtensions.cs:183-188`) and Sample Blazor UI (`Program.cs:77-78`) map `MapStaticAssets()` and `MapRazorComponents()` with no policy. Admin interactive login is backlog Story 7.16 and out of MVP (PRD §9.2). |
| Gateway tenant validator | `ClaimsTenantValidator.cs:44-45` compares tenants with `StringComparison.Ordinal` and does not normalize. `:22` lets a global administrator reach `system`. Gateway controllers, `AuthorizationBehavior`, and `ProjectionChangedHub` use it. |
| `system` tenant | `AggregateIdentity.cs:92` and `NamingConventionEngine.cs:92` give `system` its own topic shape. `RestTenantSource.System` is a public generator option. Tenants persists platform aggregates under `TenantIdentity.DefaultTenantId = "system"`, and its UI gateway sends commands as `system`. |
| `ReleaseEvidenceCodec` | Absent from EventStore and from `references/Hexalith.Builds` at its gitlink `397c94a4`. |
| AD-ID propagation | `epics.md` pins architecture digest `7e3dbc7b…`, the spine at `528a4720`, which contains AD-1 through AD-33. No epics constraint line cites AD-33, AD-34, AD-35, or AD-36. |

## 2. Impact Analysis

### Epic impact

| Epic | Impact |
| --- | --- |
| 2 | Story 2.14's scope is extended (Group 4). Stories 2.14 and 2.15 gain the catalog prerequisite (Group 1). |
| 3 | New Story 3.21: runtime pin and broker qualification. Story 3.19's issue step gains named prerequisites (Groups 1, 2, 6). |
| 4 | Story 4.16 gains the runtime-pin dependency (Group 2). |
| 5 | New Stories 5.12 and 5.13 (catalog) and 5.14 (fallback and framework routes). Story 5.7 gains the peer ACL deny rules. |
| 7 | New Story 7.22: restore posture including scheduler state. |
| 9 | Story 9.3 gains the AD-ID propagation obligation for its repin change (Group 5). |
| 1, 6, 8 | None. |

No epic becomes obsolete, and no new epic is needed.

### Artifact conflicts

- **PRD.** §11.2 ownership mapping for the new stories' declarations, plus `source_artifacts`. Group 7 routes five content decisions to the product owner without applying them.
- **Architecture.** Not edited. Six Implementation Status rows still read "no owning story yet". `bmad-architecture` replaces that text on its next Update, which also changes the digest.
- **UX.** None.
- **Tracker.** Six new `backlog` rows. Guarded comment blocks are untouched.
- **Digests.** None refreshed. AD-34 through AD-36 are staged for the Story 9.3 repin change, not cited now (Group 5).

### Propagation discipline (applies to every group)

New and extended stories cite only AD IDs that exist in the spine digest `epics.md` pins today, AD-1 through AD-33. AD-34, AD-35, and AD-36 are added in the same change that repins that digest, which only Story 9.3 performs. AC text describes the required behavior without those labels. AD-26 is cited as "production proof mechanics; the target stays `[ASSUMPTION]`". No story here adopts any of the 11 inline `[ASSUMPTION]` clauses.

## 3. Recommended Approach

**Direct adjustment.** Add focused backlog stories and extend existing ones within the current epics.

- **Rollback.** Not viable; nothing completed is wrong.
- **MVP review.** Not needed. Each item is a prerequisite of an existing MVP gate (G-TENANT, G-STATUS-ID, G-AUTH-HOSTS, G-PUBLICATION-AUTH) or of the AD-26 production proof.
- **Effort.** Planning effort is low. Delivery effort is high: two multi-host proofs (5.13, 7.22) and one cross-repository migration (2.14).
- **Risk.** Planning risk is low. The main delivery risk is that 5.12's field set depends on three unresolved assumptions (#5, #8, #10).

**Sequencing consequence for the owner's next architecture run.** Story 5.12 is the structural prerequisite of 2.14 and 2.15. Its optional fields depend on handoff assumptions #5 (AD-17 MessageId-version carrier), #8 (AD-27 platform-operation namespace), and #10 (AD-33 credential kind and operation). Resolve those three first in `/bmad-architecture` (Update), before the 5.12 spec freezes.

## 4. Detailed Change Proposals

Each group is applied only if approved. Story numbers are as proposed. If a group is skipped, later new stories in the same epic are renumbered at application time.

### Group 1: AD-33 route catalog, sequenced before Stories 2.14 and 2.15 (request item 1)

Recommended split: schema, codec, and envelope come first, because 2.14 and 2.15 need them. Activation is a separate multi-host proof that only 3.19 needs. A single-story alternative would gate 2.14 and 2.15 on activation for no benefit.

**1.1 New Story 5.12**, inserted after Story 5.11.

```markdown
### Story 5.12: Route And Idempotency Catalog Schema, Codec, And Envelope

As a platform maintainer,
I want one versioned `Contracts` schema and canonical codec for the deployable route and idempotency catalog envelope,
So that the gateway, admission, dispatchers, and deployment ACLs resolve every message to exactly one app ID, method, and contract version from the same bytes.

**Requirements coverage:** Supporting FR12, FR15, FR27, FR32, NFR2, and NFR12. No primary FR or NFR claim. Owns the schema, codec, and envelope of the architecture gate row "Canonical routing/idempotency catalog envelope"; Story 5.13 owns its activation.

**Architecture constraints:** AD-33 (`Contracts` owns the schema and codec), AD-25 (idempotency facet), AD-9, AD-17, and AD-27.

**Dependencies:** A Story 9.1 authorization record for gates G-TENANT and G-STATUS-ID, whose owning Stories 2.14 and 2.15 depend on this story. The Platform deployment owner owns the signed or content-bound production instance; this story ships the schema, codec, validator, and the Development/test instance. **Assumption boundary:** the spine tags three catalog fields `[ASSUMPTION]`: the MessageId-version digest (AD-17), the platform-operation namespace (AD-27), and the admitted credential kind with its required operation (AD-33). This story adds none of them as a field until the owner resolves the matching assumption through `bmad-architecture`.

**Acceptance Criteria:**

**Given** `Hexalith.EventStore.Contracts`
**When** the catalog schema and codec are added
**Then** the schema is versioned, and the codec produces canonical UTF-8 bytes whose SHA-256 is the root digest, with stable route-entry IDs joining the route facet and the AD-25 idempotency facet under one root digest and generation
**And** decoding and re-encoding any valid envelope reproduces its bytes exactly, while unknown fields, unsupported versions, and non-canonical bytes are rejected.

**Given** an envelope
**When** the validator bound by this story runs
**Then** every command and query maps by `(Domain, MessageType)`, and every projection by `(Domain, ProjectionType)`, to exactly one app ID, method, and contract version, and every idempotency entry binds the AD-25 fields
**And** a duplicate, missing, or ambiguous key, an undeclared fallback, a runtime override outside Development, or a route/facet digest mismatch fails with the entry ID and field.

**Given** the repository's `Contracts` route declarations
**When** `deploy/dapr/eventstore-routing-catalog.json` is generated for the Development/test profile
**Then** its routes equal what Development resolves from those declarations, and a guard fails when a declaration and the committed envelope diverge
**And** production readiness still fails until Story 5.13 activates an instance.

**Given** the validator's rejection paths
**When** its tests run
**Then** each rejection is proven by a checked-in negative fixture observed failing beside a positive control
**And** no guard is green by construction.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 1). The schema, codec, and envelope file are absent.
```

**1.2 New Story 5.13**, inserted after Story 5.12.

```markdown
### Story 5.13: Route Catalog Activation And Topology Binding

As a deployment owner,
I want every required host to load and validate one catalog generation before it is committed, and to roll back as a unit,
So that the gateway, admission, dispatchers, AppHost, and deployment ACLs can never route the same message differently.

**Requirements coverage:** Supporting FR27, FR32, and NFR17. No primary FR or NFR claim. Owns activation for the architecture gate row "Canonical routing/idempotency catalog envelope".

**Architecture constraints:** AD-33 (activation), AD-9, AD-12, AD-25, and AD-26 (production proof mechanics; the target stays `[ASSUMPTION]`).

**Dependencies:** Story 5.12; Stories 5.6 and 5.7 for the AppHost and production ACL models it compares; a Story 9.1 authorization record for gate G-PUBLICATION-AUTH. Signing the production instance belongs to the Platform deployment owner and the Story 3.19 issue step.

**Acceptance Criteria:**

**Given** a new catalog generation
**When** it is activated
**Then** activation runs prepare, ready, and commit: every required host loads and validates the same root and facet digests before the deployment owner commits, and any failure rolls back to the prior complete generation
**And** a duplicate, missing, or ambiguous entry, unsupported override, partial generation, signature or trust failure, or fingerprint mismatch fails readiness on every host.

**Given** the AppHost, deployment ACLs, gateway, admission, and domain and projection dispatchers
**When** the topology check runs
**Then** each reports the root digest it activated, and all of them match
**And** an ACL operation or app ID that the activated catalog does not declare fails the check.

**Given** a multi-host drill on the production-equivalent profile
**When** a host fails during prepare, during ready, and after commit
**Then** the generation each host serves is read back through its Dapr path, and no host serves a mixed or partial generation
**And** the evidence binds the root digest, the host set, and the drill results.

**Given** no activated production instance
**When** production readiness is evaluated
**Then** it fails
**And** the activated root digest is the only catalog identity the Story 3.19 profile may bind.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 1).
```

**1.3 Story 2.14, Dependencies.** Add the catalog prerequisite. The full Group 4 text below includes this fragment.

- OLD: "**Dependencies:** Story 2.12; a Story 9.1 authorization record for gate G-TENANT."
- NEW: "**Dependencies:** Story 2.12; Story 5.12 (catalog schema and codec for the cataloged platform-operation scope); a Story 9.1 authorization record for gate G-TENANT."

**1.4 Story 2.15, Dependencies.**

- OLD: "**Dependencies:** A Story 9.1 authorization record for gate G-STATUS-ID."
- NEW: "**Dependencies:** Story 5.12 (catalog schema and codec); a Story 9.1 authorization record for gate G-STATUS-ID. Until the owner resolves the AD-17 carrier assumption through `bmad-architecture`, each contract's `Contracts` declaration stays the sole source of its MessageId version, and the manifest this story validates is derived from those declarations and fails on drift."

**1.5 Story 3.19.** Adds the issue-step prerequisite "Story 5.13". The combined text is in Group 6.

**1.6 Index and tracker.**

- Epic List, Epic 5 story set: "5.1–5.11" becomes "5.1–5.13" (or "5.1–5.14" with Group 3). Append: "Backlog 5.12 and 5.13 own the AD-33 route and idempotency catalog: schema, codec, and envelope ahead of Stories 2.14 and 2.15, then activation (`sprint-change-proposal-2026-10-07-architecture-routing.md`)."
- Tracker rows after `5-11`, under one dated comment:
  - `5-12-route-and-idempotency-catalog-schema-codec-and-envelope: backlog`
  - `5-13-route-catalog-activation-and-topology-binding: backlog`
- PRD §11.2 supporting declarations: NFR2 adds 5.12; NFR12 adds 5.12; NFR17 adds 5.13.

### Group 2: Durable broker, DAPR runtime pin, and restore posture before the Story 3.19 issue step (request item 2)

Two stories, not three. The broker must be qualified on the exact runtime it will run on, so the pin and the broker share one story. Restore posture is a separate operational proof.

Neither story selects anything. Under AD-26, the owner names the broker, the pin, and the restore posture only in the AD-26 ratification record or an approved replacement. Both stories therefore follow the build-then-issue pattern: the build ends on a truthful FAIL, and `done` waits for that owner record. Their evidence is also what the "Keep withholding" outcome asks the owner to name.

**2.1 New Story 3.21**, inserted after Story 3.20.

```markdown
### Story 3.21: Production DAPR Runtime Pin And Durable Broker Qualification

As a deployment owner,
I want one tested DAPR runtime pin and qualified durable-broker candidates on the production path,
So that the AD-26 decision names a broker and runtime from evidence, and the production profile binds exactly what was proven.

**Requirements coverage:** Supporting FR32, FR34, NFR6, and NFR17. No primary FR or NFR claim. Owns the durable-broker and DAPR-runtime-pin inputs that AD-26 requires before any `production-promoted` record.

**Architecture constraints:** AD-26 (production proof mechanics; the target stays `[ASSUMPTION]`), AD-5 (envelope re-proof on a runtime minor-version change), AD-8, AD-9, AD-12, and AD-31.

**Dependencies:** Story 3.17 (inventory rows); a Story 9.1 authorization record for gate G-PUBLICATION-AUTH. **Selection boundary:** the owner names the broker and runtime pin only in an AD-26 ratification record or an approved replacement; this story builds evidence and never selects. The DAPR CLI pin and catalog alignment belong to the Hexalith.Builds owner. `.github/workflows/integration.yml` and `.github/workflows/ci.yml` are sealed OQ8 v3 inputs: their runtime and CLI pins change only inside a planned Story 4.15 reseal, and otherwise the qualification lane is a new workflow file.

**Acceptance Criteria:**

**Given** a candidate runtime of at least `1.18.3` (`1.18.4` is the current stable release) and its CLI
**When** every runtime, CLI, placement, and scheduler pin in CI, the AppHost, and the production-profile inputs is inventoried
**Then** each location is listed with its value, and the candidate passes the live-sidecar suite in the qualification lane
**And** a production-profile input pinned below the floor or to a mutable tag fails a guard; drift in the deployment guides is reported to Story 5.9.

**Given** the broker candidates `pubsub.kafka` and `pubsub.rabbitmq` (templates in `deploy/dapr`) and the cloud-managed `pubsub.azure.servicebus.topics`
**When** each is qualified on the candidate runtime
**Then** at-least-once redelivery after subscriber failure, dead-letter routing, retention, a CloudEvent `id` equal to `MessageId`, publishing and subscription scopes, and `secretKeyRef` credentials are observed and recorded as Story 3.17 inventory rows with exact versions
**And** an unobserved guarantee stays unresolved, and no candidate is recorded as selected.

**Given** the owner's AD-26 record naming the broker and runtime pin
**When** the story binds them
**Then** the selected broker component goes to Story 5.7 for production parity, and the runtime image identity goes to the Story 3.19 profile
**And** any later runtime minor-version change requires broker requalification and the Story 4.16 envelope re-proof.

**Given** the build steps are complete and no AD-26 record names a broker and pin
**When** the validator runs
**Then** it reports a truthful FAIL under the Epic 9 truthful-FAIL CI rule
**And** the story is `done` only after that record exists and the binding above passes.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 2). CI pins runtime `1.18.2` and CLI `1.18.0`, below the AD-26 floor.
```

**2.2 New Story 7.22**, inserted after Story 7.21.

```markdown
### Story 7.22: Production Restore Posture Including Scheduler State

As a platform operator,
I want a written restore posture for production state, proven by a restore drill,
So that a restore never re-admits a consumed idempotency key, repeats a global position, or silently loses reminders.

**Requirements coverage:** Supporting FR34, NFR7, and NFR16. No primary FR or NFR claim. Owns the restore-posture input that AD-26 requires before any `production-promoted` record. Numeric RTO and RPO targets, retention, and environment promotion stay with Platform Operations and are not inferred here.

**Architecture constraints:** AD-26 (production proof mechanics; the target stays `[ASSUMPTION]`), AD-5, AD-6, AD-12, and AD-25.

**Dependencies:** Story 3.21 (scheduler and placement behavior is runtime-specific); Story 3.17; a Story 9.1 authorization record for gate G-PUBLICATION-AUTH. The owner names the restore posture only in an AD-26 ratification record or an approved replacement.

**Acceptance Criteria:**

**Given** the production-profile state: actor state in `statestore`, admission and fence state, the global-position allocator, read models and checkpoints, DAPR scheduler state for reminders and jobs, and placement
**When** the restore posture is written
**Then** it states, for each, the backup method (a platform operation, never application code), the consistency point, and the restore order
**And** it names the states that may never be restored on their own.

**Given** a restore drill on the production-equivalent profile to a point before later commands
**When** the restored system serves traffic
**Then** no consumed idempotency key is re-admitted, no fence is reissued, no `GlobalPosition` repeats, and reminders and jobs are restored or re-registered as declared
**And** any invariant the posture cannot guarantee is declared as a restore prohibition, and end state is read back through Dapr paths.

**Given** the owner's AD-26 record naming the restore posture
**When** the story binds it
**Then** the posture digest goes to the Story 3.19 profile
**And** until that record exists, the validator reports a truthful FAIL and the story is not `done`.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 2). No restore-posture artifact exists.
```

**2.3 Story 4.16, Dependencies.** This follows from adopted AD-5: the envelope is re-proven on every runtime minor-version change.

- OLD: "**Dependencies:** Story 3.17; owner ratification of AD-26; a Story 9.1 authorization record for gate G-APPEND."
- NEW: "**Dependencies:** Story 3.17; Story 3.21 (the runtime the envelope is proven on; a later runtime minor-version change requires re-proof); owner ratification of AD-26; a Story 9.1 authorization record for gate G-APPEND."

**2.4 Story 3.19.** Adds the issue-step prerequisites "Stories 3.21 and 7.22". The combined text is in Group 6.

**2.5 Index and tracker.**

- Epic List, Epic 3 story set "3.1–3.20" becomes "3.1–3.21". Append: "Backlog 3.21 qualifies the DAPR runtime pin and durable-broker candidates for the AD-26 profile (`sprint-change-proposal-2026-10-07-architecture-routing.md`)."
- Epic List, Epic 7 story set "7.1–7.21" becomes "7.1–7.22". Append: "Backlog 7.22 owns the AD-26 restore-posture input (`sprint-change-proposal-2026-10-07-architecture-routing.md`)."
- Tracker rows:
  - `3-21-production-dapr-runtime-pin-and-durable-broker-qualification: backlog`, after `3-20`
  - `7-22-production-restore-posture-including-scheduler-state: backlog`, after `7-21`
- PRD §11.2 supporting declarations: NFR6 adds 3.21; NFR7 adds 7.22; NFR16 adds 7.22; NFR17 adds 3.21.

### Group 3: Authenticated fallback, Dapr framework routes, and peer ACLs (request item 3)

The application-side controls go to new Story 5.14. The peer ACL deny rules go to Story 5.7, the candidate the spine names.

**3.1 New Story 5.14**, inserted after Story 5.13 (or after 5.11 if Group 1 is skipped).

```markdown
### Story 5.14: Authenticated Fallback And Dapr Framework-Route Protection On Every HTTP Host

As a security owner,
I want every HTTP host to deny any endpoint that names no policy, and the Dapr framework and Operations routes to require the right credential,
So that endpoint mapping order, peer invocation, or a forged caller header can never expose a host.

**Requirements coverage:** Primary NFR1's AD-16 authenticated-fallback and Dapr framework-route slice. Supporting FR26, FR28, and FR34.

**Architecture constraints:** AD-16, AD-28, AD-10, AD-29, and AD-31.

**Dependencies:** Story 5.3 (probe anonymity); Story 5.5 (sidecar-channel policy and workload assertions); a Story 9.1 authorization record for gate G-AUTH-HOSTS. Tenants hosts land in the Tenants repository under its owner, as in Story 5.11. Interactive UI hosts wait for the product-owner decision on static assets and login callbacks (`sprint-change-proposal-2026-10-07-architecture-routing.md`, item 7.2). Story 7.1 keeps Operations delivery, capture, and acknowledgement semantics; this story owns only the authentication and authorization of the Operations endpoints.

**Acceptance Criteria:**

**Given** the EventStore gateway, Admin Server Host, Sample API and generated-host fixtures, Operations, DomainService SDK hosts, and the Tenants hosts
**When** endpoint metadata is enumerated
**Then** each host configures an authenticated fallback that denies any endpoint without a named policy, every endpoint names its policy and authentication scheme, and only `/health`, `/alive`, and `/ready` carry `AllowAnonymous`
**And** the sidecar-channel scheme never satisfies the fallback, and an endpoint-metadata test fails on any unexpected anonymous or unnamed endpoint, including endpoints added later.

**Given** the gateway and Operations map `MapSubscribeHandler` and `MapActorsHandlers`
**When** a request reaches those routes without the receiver's app-channel token
**Then** it is denied before actor activation or subscription handling, and no actor state change is observed
**And** those routes carry the explicit sidecar-channel policy the DomainService SDK already applies (closes Story 5.5 review deferral D-1).

**Given** the Operations dead-letter list and action endpoints
**When** a request arrives
**Then** authorization validates the credential the route admits (the human bearer relayed by Admin Server, validated through the shared JWT contract, or a workload assertion) and preserves AD-29 attribution
**And** the caller app ID can only deny, and a missing, unvalidated, or mismatched credential is rejected before any list, replay, or skip effect.

**Given** the interactive UI hosts (Admin UI, Sample Blazor UI)
**When** the product-owner decision on static assets and login callbacks is still pending
**Then** they are reported as failing conformance, with the conflict named
**And** this story adds no exemption.

**Given** completion is requested
**When** forged-header, peer-invocation, missing-credential, and wrong-credential suites run against real host pipelines
**Then** every denial shows zero downstream execution and unchanged persisted state
**And** valid sidecar deliveries, actor callbacks, and probes still work.

**Current reconciliation (2026-10-07):** Backlog. Added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 3). Only the DomainService SDK configures a fallback policy. The gateway (`Program.cs:47-48`) and Operations (`Program.cs:43-44`) map framework routes without a policy, and Operations authorizes dead-letter calls by caller app ID plus an unvalidated bearer (`DeadLetterOperationsEndpointExtensions.cs:227-234`).
```

**3.2 Story 5.7 extension.**

- Architecture constraints. OLD: "**Architecture constraints:** AD-9, AD-10, and AD-12." NEW: "**Architecture constraints:** AD-9, AD-10, AD-12, and AD-28." The rest of the line is unchanged.
- Dependencies. Append: "Story 5.14 owns the application-side route policies these ACL deny rules back up. The actor-invocation qualification runs on the Story 3.21 candidate runtime."
- Current reconciliation. Append: "**Scope extension (2026-10-07):** peer ACL deny rules for channel-only routes and actor methods, and qualification of actor-invocation restriction, were added by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 3). `/**` grants exist today in `deploy/dapr/accesscontrol.yaml` and four AppHost access-control files."
- Two new ACs, inserted before "**Given** Story 5.7 completion is requested":

```markdown
**Given** every production and AppHost DAPR `Configuration`, including the `eventstore`, `eventstore-admin`, `tenants`, and `sample` access-control files
**When** a peer app ID invokes a receiver's subscription routes, `/dapr/subscribe`, `/dapr/config`, actor routes, or actor methods through service invocation
**Then** the receiving policy denies it: no `/**` or other wildcard grant covers a channel-only route or actor method, and each allowed operation is an exact path and verb
**And** a regression test proves a denied peer cannot reach those routes (the ACL half of Story 5.5 review deferral D-2).

**Given** the question whether the profile restricts actor invocation and channel-only routes to their hosting app ID
**When** it is qualified on the Story 3.21 candidate runtime
**Then** the result is recorded as a Story 3.17 inventory row, with exact runtime and component versions and observed evidence
**And** the result neither adopts nor rejects the AD-28 `[ASSUMPTION]` about actor-method execution contexts, which the owner resolves through `bmad-architecture`.
```

**3.3 Index and tracker.**

- Epic List, Epic 5: the story set ends at 5.14. Append: "5.14 owns NFR1's AD-16 authenticated-fallback and Dapr framework-route slice (`sprint-change-proposal-2026-10-07-architecture-routing.md`)."
- Tracker: `5-14-authenticated-fallback-and-dapr-framework-route-protection: backlog`.
- PRD §11.2 NFR1: primary declarations add 5.14, and the caveat gains: "Story 5.14 owns the AD-16 authenticated-fallback and Dapr framework-route slice (assigned 2026-10-07); blocking until its evidence passes, and interactive UI hosts wait for the product-owner decision routed with it."

### Group 4: Story 2.14 extended to the gateway validator and the existing `system` tenant (request item 4)

The title and tracker key are unchanged. The edits in order:

**4.1 "I want" line.**

- OLD: "I want every generated controller and the Tenants API host to canonicalize and validate tenants through one shared contract before routing,"
- NEW: "I want every generated controller, the Tenants API host, and the EventStore gateway's tenant validator to canonicalize and validate tenants through one shared contract before routing,"

**4.2 Architecture constraints.**

- OLD: "**Architecture constraints:** AD-27 (`Contracts` owns the canonicalizer and grammar), AD-10, and AD-28."
- NEW: "**Architecture constraints:** AD-27 (`Contracts` owns the canonicalizer and grammar), AD-10, AD-28, and AD-11 (NFR12 classification of the `system` migration)."

**4.3 Dependencies boundary sentence.** The Group 1 fragment is applied separately.

- OLD: "this story owns request-boundary rejection and the distinct platform-operation scope."
- NEW: "this story owns request-boundary rejection, the distinct platform-operation scope, and the migration of existing `system` usage."

**4.4 Two new ACs**, inserted after the "platform-wide operation" AC:

```markdown
**Given** the EventStore gateway's `ClaimsTenantValidator`, which today compares request tenants and `eventstore:tenant` grants with `StringComparison.Ordinal`, normalizes nothing, and lets a global administrator reach `system`
**When** a command, query, stream, admin-storage, or SignalR hub request is authorized through it
**Then** it uses the same `Contracts` canonicalizer as the generated controllers and the Tenants host, so every unit accepts or rejects the same request identically
**And** no principal, including a global administrator, reaches `system` through a public tenant boundary.

**Given** the existing `system` tenant: Tenants' platform-owned aggregates and the paths that address them (such as `TenantIdentity.DefaultTenantId` and the Tenants UI command gateway), the `system` actor-ID and pub/sub topic shape in `AggregateIdentity` and `NamingConventionEngine`, and the public `RestTenantSource.System` generator option
**When** the story's migration plan is approved by the owner before implementation
**Then** each moves to the distinct platform-operation scope without editing or deleting persisted events, and persisted `system` streams and topics stay readable and attributable
**And** the plan classifies every changed public surface under NFR12: removing `RestTenantSource.System` or changing its behavior incompatibly requires an approved SemVer-major proposal, and a plan that changes AD-27 returns to `bmad-architecture` first.
```

**4.5 CI-lane AC.**

- OLD: "**Given** compiled generated-controller tests and Tenants runtime tests"
- NEW: "**Given** compiled generated-controller tests, gateway tests, and Tenants runtime tests"

**4.6 Current reconciliation.** Append: "Scope extended 2026-10-07 by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 4) to the gateway `ClaimsTenantValidator` and the migration of existing `system` usage."

**Owner option 4b (outside the request, recommended).** Admin Server has its own tenant checks: `AdminTenantAuthorizationFilter`, plus a stream-query filter in `DaprStreamQueryService.cs:160` that compares with `OrdinalIgnoreCase`. That is a third, different comparison rule at a public boundary. AD-27 says "each boundary", and Story 5.2 (in review) owns Admin tenant filters but not the canonicalizer. If 4b is approved, the gateway AC's **Given** also names "Admin Server's `AdminTenantAuthorizationFilter` and stream-query tenant filter". If it is not approved, the gap is recorded as unowned.

### Group 5: Staged AD-34, AD-35, and AD-36 propagation through the Story 9.3 repin (request item 5)

Only Story 9.3 refreshes the digest, and propagation must land in the same change. So this group applies nothing to constraint lists now. It extends Story 9.3 and stages the table that the repin change applies.

**5.1 Story 9.3, new AC**, inserted after its first AC:

```markdown
**Given** a manifest that binds an architecture digest different from the one `epics.md` records
**When** that digest is repinned in `epics.md`, the PRD §11.3 register, and the manifest
**Then** the same change adds every AD ID the newly pinned spine introduces to the **Architecture constraints** line of each affected story, starting from the staged table in `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 5)
**And** the validator rejects a repin that changes only digests, a constraint line that cites an AD ID absent from the pinned spine, and a staged story that omits its assigned AD ID.
```

**5.2 Story 9.3, Current reconciliation.** Append: "Extended 2026-10-07 by `sprint-change-proposal-2026-10-07-architecture-routing.md` (Group 5): the repin change carries the staged AD-ID propagation."

**5.3 Staged propagation table.** This is not applied now. It covers the `1ff06c5e…` spine. Rows for new stories apply only if their group is approved. If the repinned spine differs, the generic rule in 5.1 governs.

| Story | Add | Reason |
| --- | --- | --- |
| 2.13 | AD-34, AD-36 | The retained Redis backplane is a named AD-34 non-conformance; `PubSub` fan-out needs AD-36 resource-bound provenance |
| 2.14 | AD-36 | The platform-operation scope admits the credential kind its route declares (AD-27) |
| 3.17 | AD-34 | Owns the AD-34 inventory and guard |
| 3.19 | AD-34, AD-36 | The profile digest binds the AD-34 inventory projection and the production AD-36 issuer *(added; not in the request list)* |
| 4.16 | AD-34 | Envelope-falsification writers are AD-34 fault-injection rows |
| 5.4 | AD-35 | Admin CLI security evidence is McpCli migration compatibility |
| 5.5 | AD-36 | Implements the workload-assertion contract and resource binding |
| 5.7 | AD-34, AD-36 | Profile component and ACL qualification; receiver audience from the activated catalog app ID |
| 5.11 | AD-36 | The workload-assertion validation profile in the shared JWT contract |
| 7.4 | AD-35 | Honest unavailable operations include the Admin.Cli inventory |
| 7.5 | AD-35, AD-36 | Typed admin client consumers include the CLI; the Admin relay is a delegated-user call |
| 8.6 | AD-34 | Dapr key-operation qualification and exception path |
| 3.21 (new) | AD-34 | Runtime and broker qualification rows |
| 5.12 (new) | AD-36 | Receiver workload audience is the catalog app ID |
| 5.13 (new) | AD-36 | Same, at activation |
| 5.14 (new) | AD-36 | Operations endpoints admit a declared credential kind |
| 7.22 (new) | AD-34 | Backup and restore are platform operations classified by AD-34 |

The Admin/McpCli set is taken to be Stories 5.4, 7.4, and 7.5. Not staged; the generic rule decides them at repin: 5.2, 7.3, and 7.19 (delegated-user call class), and 6.6 (its AD-13 amendment cites AD-34-qualified operations).

**5.4 Not done here.** No constraint line, digest, or spine text changes. The PRD §11.3 stale architecture digest (handoff product-owner item 5) is refreshed by the same Story 9.3 repin.

### Group 6: `ReleaseEvidenceCodec` as an explicit Story 3.19 prerequisite (request item 6)

**6.1 Story 3.19, Dependencies.** This text combines the fragments from Groups 1, 2, and 6. A fragment drops out if its group is skipped.

- OLD: "**Dependencies:** Owner ratification of AD-26 or an approved replacement; Story 5.7 for the production component contents; Story 3.15 (`done` for FR36-C2); Story 9.2 for the Assurance Control; a Story 9.1 authorization record for gate G-PUBLICATION-AUTH."
- NEW: OLD text, followed by: "**Issue-step prerequisites** (`sprint-change-proposal-2026-10-07-architecture-routing.md`): the build step does not wait for them, but no `release-available` or `production-promoted` record is issued until each is met. They are Story 3.21 (DAPR runtime pin and selected broker) [G2]; Story 7.22 (restore posture including scheduler state) [G2]; Story 5.13 (activated route and idempotency catalog digests) [G1]; and `ReleaseEvidenceCodec` [G6], which the Hexalith.Builds owner delivers in the SHA-pinned shared Builds publisher/validator and EventStore consumes at a pinned gitlink, and which was absent everywhere on 2026-10-07."

The `[Gn]` markers are for this proposal only and are not applied.

**6.2 Story 3.19, new AC**, inserted before the "build steps are complete" AC:

```markdown
**Given** the pinned Hexalith.Builds gitlink
**When** the publication-authority validator resolves `ReleaseEvidenceCodec`
**Then** a missing codec, or one whose identity differs from the pinned Builds publisher/validator, fails every record beyond `evidence-validated` with a named cause
**And** EventStore never re-implements the codec.
```

**6.3 Story 3.19, Current reconciliation.** Append: "Issue-step prerequisites named 2026-10-07 by `sprint-change-proposal-2026-10-07-architecture-routing.md`."

**6.4 Routed to the Builds owner** (the same person, in the Builds repository): deliver `ReleaseEvidenceCodec` and publish a gitlink candidate. The EventStore gitlink bump stays a separate, later change; this proposal touches no submodule.

### Group 7: Product-owner decisions routed to `bmad-prd` (request item 7). Not applied.

Approving this group approves the routing and the proposed text as input to a `bmad-prd` Update. `prd.md` is not edited for these items here.

**7.1 Assurance Control seal versus the truthful-FAIL rule.**

- *Conflict.* The glossary's Assurance Control (1) defines a seal as the validator result "for a required, blocking run". The Epic 9 truthful-FAIL rule makes live gate evaluations non-blocking, and only fixture suites block. No live result can be sealed, so G-HIGH-RISK can never pass.
- *Option A (recommended).* Replace (1) with: "(1) sealed CI validation, meaning the gate validator's live result is retrieved from the CI platform for a required run of the guarded transition's seal workflow (Story 9.2), on the exact head SHA and workflow-file digest, under the platform's authenticated CI identity, never supplied by the author as a file; that run blocks only its transition (readiness, `release-available`, `production-promoted`, or consumer removal) and never `main`;". This is the PRD half of spine assumption #4 (AD-12). Decide it together with #4 in `bmad-architecture`.
- *Option B.* Make live evaluations blocking on `main`. This contradicts the 2026-10-07 owner decision, and `main` would stay red.

**7.2 Anonymous endpoints versus UI static assets and login callbacks.** The handoff cites §9.2. The three-probe rule itself is in NFR1, SM10, and AD-16. §9.2 is where Admin interactive login is kept out of MVP.

- *Conflict.* NFR1 allows only `/health`, `/alive`, and `/ready` anonymously. Interactive UI hosts must serve static assets and authentication callbacks before a user is signed in. Today the Admin UI and Sample Blazor UI also map their pages with no policy, while human login (Story 7.16) is post-MVP.
- *Option A (recommended).* Amend NFR1 to admit one closed class on interactive UI hosts only: "an enumerated set of static framework assets and authentication-protocol callback endpoints that carry no tenant, operational, or user data, each explicitly pinned `AllowAnonymous`, support-safe, and enumerated by endpoint-metadata tests (AD-16)". Separately, decide whether human login for UI hosts enters MVP (pull Story 7.16 forward). If it does not, UI hosts are excluded from the MVP production profile until it exists, and their NFR1 conformance moves with them.
- *Option B.* Keep NFR1 as written. UI hosts fail NFR1 and SM10, and MVP stays blocked until post-MVP login.
- Either outcome then updates AD-16 through `bmad-architecture`, as AD-16 itself requires.

**7.3 NFR18 stale ownership text.** This is editorial.

- OLD: "the document does not yet exist and no story currently owns it (see §12)."
- NEW: "the document does not yet exist; Story 6.7 is its primary owner (assigned 2026-10-07; see §12 OR6)."

**7.4 Glossary "DAPR Boundary" narrower than §8.4.** This is editorial.

- OLD: "The state, pub/sub, service invocation, actor, config, access-control, and resiliency infrastructure abstraction boundary."
- NEW: "The infrastructure boundary defined in §8.4. Application and shared runtime code reach persistence and actors, messaging, service invocation, configuration, secrets, bindings, scheduling and workflows, cryptographic provider operations, access control, and resiliency only through a suitable Dapr building block or component, unless an accepted architecture exception exists."

**7.5 MediatR `14.2.0` license posture before the next release.** Add a §12 row:

> OR30 | **Blocking for the next release.** Decide the MediatR `14.2.0` license posture. The released `Server` and `Gateway` packages reference it under RPL-1.5 or a commercial license, while the host suppresses its license log. Record a commercial license, accept the RPL-1.5 obligations after review, or replace or re-pin the dependency through the Hexalith.Builds catalog (NFR12 classification applies to any public-surface change). | Product owner with the release owner; Builds owner for the catalog | Before the next release

## 5. Implementation Handoff

**Scope classification: Moderate.** It reorganizes the backlog: six stories, six extensions, and one staged propagation. Group 7 and the assumption ordering go to the product owner and architect, who are the same person.

| Recipient | Responsibility |
| --- | --- |
| Developer agent, now | Apply the approved groups to `epics.md`, `prd.md` (§11.2 mapping and `source_artifacts` only), and `sprint-status.yaml`. Do not touch `architecture.md`, submodules, or `src/`. Run the Contracts.Tests guard classes. |
| Owner, `/bmad-architecture` (Update) | Work through the 11 `[ASSUMPTION]` clauses, starting with #5, #8, and #10 (5.12 fields), #4 (with item 7.1), and #9 (with the 5.7 qualification). On the next spine edit, replace the "no owning story yet" text in the six affected Implementation Status rows with these stories. |
| Owner, AD-26 | Issue the per-role records, Architecture and Platform deployment, under `single-maintainer-attested`, no earlier than 2026-10-08 09:34 +02:00. That is 24 h after commit `48ef7171` captured the current AD-26 bytes, and any further AD-26 edit restarts the window. A ratification record names the broker, runtime pin, restore posture, and envelope path. Stories 3.21 and 7.22 supply evidence for those choices. |
| Owner, `/bmad-prd` (Update) | Group 7 items 7.1–7.5. |
| Builds owner | `ReleaseEvidenceCodec` (Group 6); the DAPR CLI `1.18.2` alignment already routed in the handoff. |
| `bmad-build` | Waves below. Each story needs its Story 9.1 authorization record before handoff. |

### Sequencing (interleaves with the 2026-10-07 waves)

| Wave | Work | Unblocked by |
| --- | --- | --- |
| 0 | Apply the approved groups | Approval |
| 1 (owner) | Assumptions #5, #8, #10, #4, #9; AD-26 records after the 24 h window; Group 7 PRD Update | Wave 0 |
| 2 | Story 9.1, then its records for G-TENANT/G-STATUS-ID, G-AUTH-HOSTS, and G-PUBLICATION-AUTH | Existing 2026-10-07 Wave 1 |
| 3 | 5.12, then 2.14 and 2.15; 5.14; 3.21 build (candidate qualification) | 9.1 records |
| 3b | 5.13 (after 5.6 and 5.7); 7.22 (after 3.21) | Wave 3 |
| 4 | 3.19 issue step | AD-26 records naming broker, pin, and restore; 3.21; 7.22; 5.13; `ReleaseEvidenceCodec` |
| 5 | 9.3 repin, carrying the Group 5 propagation | OR14 completion |

### Success criteria

1. Every correct-course item in the handoff's "Routed items" names an owning story or extension in `epics.md`.
2. No `epics.md` constraint line cites AD-34, AD-35, or AD-36, and Story 9.3 carries the repin obligation and the staged table.
3. `architecture.md`, AD-26, the 11 `[ASSUMPTION]` clauses, every digest, and every submodule are unchanged.
4. `sprint-status.yaml` has one new `backlog` row per approved new story, and its guarded blocks are intact.
5. The Contracts.Tests guard classes that read `prd.md` and `sprint-status.yaml` stay green.
6. Every PRD §11.4 gate stays FAIL/BLOCKED, and readiness stays FAIL.

### Observed, outside the requested scope (not edited)

- The `epics.md` Story 5.5 narrative still says "remains backlog". The tracker says `review`. This is the OR15 lifecycle drift that Story 9.3 guards.
- PRD §11.2 NFR7 does not list Story 4.16, although 4.16 declares primary NFR7 class (c). That gap came from the 2026-10-07 application, and it is left for the 9.3 drift guard or an explicit owner request.
- The spine Stack line records the Builds gitlink as `ba4ca78c`. `HEAD` pins `397c94a4`. This is for `bmad-architecture`.

## Checklist Record

| Item | Status | Note |
| --- | --- | --- |
| 1.1 Trigger | [x] | 2026-10-07 architecture handoff, "Routed items"; no single triggering story |
| 1.2 Problem type | [x] | Ownership gap left by an architecture update |
| 1.3 Evidence | [x] | Re-verified at `48ef7171` (Section 1) |
| 2.1 Current epic | [x] | Epics 2, 3, and 5 cannot meet G-TENANT, G-STATUS-ID, G-AUTH-HOSTS, or G-PUBLICATION-AUTH without these owners |
| 2.2 Epic-level changes | [x] | New stories in Epics 3, 5, and 7; extensions in 2, 3, 4, 5, and 9 |
| 2.3 Remaining epics | [x] | Epics 1, 6, and 8 unaffected |
| 2.4 Obsolete or new epics | [x] | None |
| 2.5 Order and priority | [x] | 5.12 before 2.14 and 2.15; four issue-step prerequisites before 3.19 issues |
| 3.1 PRD | [!] | §11.2 mapping applied on approval; five content items routed (Group 7) |
| 3.2 Architecture | [!] | Not edited; assumption ordering and gate-row text routed |
| 3.3 UX | [N/A] | Item 7.2 may later touch UI-host flows through `bmad-prd` |
| 3.4 Other artifacts | [x] | Sealed OQ8 inputs (`integration.yml`, `ci.yml`) are named as constraints; no CI edits here |
| 4.1 Direct adjustment | Viable | Selected |
| 4.2 Rollback | Not viable | Nothing to revert |
| 4.3 MVP review | Not needed | Every item gates an existing MVP gate |
| 4.4 Recommended path | [x] | Direct adjustment |
| 5.1–5.5 Proposal components | [x] | Sections 1–5 |
| 6.1–6.2 Review | [x] | Groups 1–7 drafted |
| 6.3 Final approval | [ ] | Pending, per group |
| 6.4 Sprint-status update | [ ] | On approval |
| 6.5 Handoff confirmation | [ ] | On approval |
