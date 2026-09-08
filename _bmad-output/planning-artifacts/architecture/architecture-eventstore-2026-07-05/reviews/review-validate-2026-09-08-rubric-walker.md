# Rubric Walker review - VALIDATE gate 2026-09-08

Subject: `_bmad-output/planning-artifacts/architecture.md` (609 lines, 25 ADs, frontmatter `status: final`, `updated: 2026-08-29`, committed at `f44b5800`).
Lens: good-spine checklist (8 items) supplied by the gate. Read-only; nothing edited.

## Verdict

PASS-WITH-FINDINGS - 0 critical, 2 high, 7 medium, 5 low; the requirement inventory is fully bound and every Epic 5/6/7 story has a home except three shared-invariant dimensions (production runtime envelope, poison/dead-letter delivery contract, tenant/claims identity guards) that are neither decided, deferred, nor asked.

## Method

- Read the whole spine in three pages (1-231, 232-432, 433-609) and indexed every `### AD-n`, `**Binds:**`, section start, and story mention.
- Grepped the spine for every `FR1..FR37` / `NFR1..NFR19` literal, resolved range bindings (`FR23-FR32`, `NFR12-NFR14`, ...) by hand, and compared with the PRD inventory (`prd.md` lines 154-288; ids FR1-FR37, NFR1-NFR19, plus `SM-C2` at line 369).
- Listed `## Epic 5/6/7` and their story headings in `epics.md` (lines 3690-6003), read each story's user-story lead and `Requirements coverage`, and mapped each to an AD, Convention row, Deferred row, or nothing.
- Checked internal consistency: Structural Seed (515-583) vs ADs and the Capability map (584-596); both mermaid diagrams (74-89, 553-583) vs AD-3/AD-10 text; frontmatter `binds`/`sources`/`companions` (11-53) vs the memlog tail and the `epics.md` input-digest.
- Checked the operational envelope against repository reality supplied by the gate brief plus `deploy/README.md` (backend matrix lines 24-48), `deploy/dapr/*` (both `statestore-postgresql.yaml` and `statestore-cosmosdb.yaml` exist), `.github/workflows/integration.yml` (Redis local sidecar; PostgreSQL only for the Story 4.14 OQ8 test), `sprint-status.yaml` (epic-6 and epic-7 `backlog`), and the OQ8 evidence packet (`profile: oq8-postgresql-v1`).
- Measured altitude fit: word count per AD (AD-22 = 1095 words, AD-11 = 844, AD-24 = 821, AD-25 = 565, AD-19 = 432; the other 20 ADs average ~150).
- Did not re-verify package currentness (another lens) or sweep the code (another lens); did not run builds or tests.

## Findings

### H1 - Production runtime envelope is undecided: state-store provider, pub/sub broker, deployment target, environment matrix

- Severity: high
- Evidence: `architecture.md:453-457` names `oq8-postgresql-v1` (`state.postgresql`, `actorStateStore: true`, two hosts) but only as the "approved production-equivalent **evidence profile**" for OQ8, not as the production state store. `architecture.md:606` (Deferred, append fencing) states "no behavior is inferred for another state-store provider" and records that the only durability evidence (Story 4.5) was on `state.redis`/Redis 6. `architecture.md:396` disqualifies Azure Container Apps managed DAPR but names no conforming target. The Stack table (`architecture.md:486-513`) has no state-store, pub/sub, container-orchestrator, or environment row. The only environment vocabulary is "Development"/"non-development" scattered in AD-16 (`:249`), AD-24 (`:388, :394`) and NFR3. Repository reality: `deploy/README.md:24-31` offers Redis (local), PostgreSQL and **Azure Cosmos DB** as production state stores with shipped component files for both; `deploy/README.md:34-46` offers RabbitMQ, Kafka and Azure Service Bus, each "configured" with ordering "not proven" for Kafka/Service Bus; `deploy/README.md:153,176,343` documents both Kubernetes (`kubectl`) and Container Apps paths.
- Why it lets two units diverge: Story 5.7 (production component/ACL parity), 5.9 (operator docs), 7.8 (resiliency targets), 7.9 (immutable images), 4.14/4.15 (OQ8 on PostgreSQL) and the deferred append-fence story each choose a backend and a target. The Deferred row itself makes the append-fence design provider-specific ("provider-portable ETag/first-write ... proven"), so a fence proven on PostgreSQL and a Cosmos DB deployment template can both be "compliant". The PRD is equally silent (no FR/NFR names a provider or target), so this is inherited, but the feature-altitude spine is where the shared choice must be fixed or explicitly parked.
- Disposition: discuss (owner decision). Suggested wording if the owner confirms PostgreSQL/Kubernetes: add to AD-9 or a new AD-26 "Production actor state store is DAPR `state.postgresql` (`deploy/dapr/statestore-postgresql.yaml`); `statestore-cosmosdb.yaml` and every other provider are unapproved until a provider-portable fence and OQ8-equivalent evidence pass on that provider. Production pub/sub is one approved broker component per environment; EventStore emits no broker ordering key, so no slice may claim broker ordering." If the owner will not decide now, add an `## Open Questions` section (the spine has none; every undecided item is currently forced into Deferred with a "why it can wait" it does not have) listing production state store, broker, target, and environment matrix.

### H2 - Poison/dead-letter delivery contract (FR34, NFR6, Story 7.1) is silent

- Severity: high
- Evidence: zero hits in the spine for `poison`, `dead-letter`, `deadLetter`, and one hit for `dedup` (`architecture.md:139`, MessageId dedup only). AD-8 (`:135-140`) fixes at-least-once/unordered semantics and MessageId/sequence-guard dedup but says nothing about what happens to a message that keeps failing: retry bound, dead-letter topic naming/tenant scoping, who consumes the DLQ, whether a checkpoint advances past a poisoned event, or bounded in-memory dedup windows. Not in Conventions (`:462-485`) or Deferred (`:598-609`). `deploy/README.md:39-46` shows each broker with a different dead-letter mechanism and all three production subscription files plus `samples/dapr-components/*/pubsub.yaml` already set `enableDeadLetter`.
- Why it lets two units diverge: every subscriber built today (Sample counter subscription, `subscription-projection-changed.yaml`, Tenants consumers via Story 1.10, future domain consumers via AD-2 seams) must pick a retry/DLQ behavior now; Story 7.1 is `backlog`, so nothing fixes the shape they must converge on. NFR2 tenant isolation across topics (`architecture.md` State keys row `:467`) does not extend to DLQ topics.
- Disposition: discuss. Minimal autofix if the owner wants only a parking slot: add a Deferred row "Poison/dead-letter contract: retry bound, tenant/domain-scoped `deadLetterTopic` naming, DLQ consumer ownership, checkpoint behaviour on poison, bounded in-memory dedup window - spec-first under Story 7.1; until then every subscription declares `enableDeadLetter`/`deadLetterTopic` under the same `{tenant}-{domain}` scoping rule as its source topic and no checkpoint advances past an undelivered event (AD-8, AD-19)."

### M1 - Several AD Rules are story scheduling, dated exceptions, or evidence procedure rather than checkable invariants

- Severity: medium
- Evidence: Design Paradigm `architecture.md:60-70` ("active Stories 4.9-4.15", "Epic 3 remains open for ... Story 3.16"); AD-11 `:161-166` (Story 3.14 ownership sentence; the `ReleaseIdentity` field enumeration and codec procedure); AD-13 `:179` (Stories 1.18/1.19/6.3/6.4 scheduling); AD-22 `:335-366` (Story 1.20/3.13/3.14/3.15 ownership paragraph; the 2026-07-27 Story 2.12 scoped exception including an attribution-correction erratum; the 2026-08-16 Story 3.13 amendment with a review-subject digest and the literal malformed `https` facts); AD-23 `:376-378` (Story 8.2 blocked until a spec records approvals); AD-25 `:456-460` (Stories 4.14-4.15 assemble a packet at a named path); Conventions Release row `:484` ("remains 14 packages until Story 8.8 ... from 14 to 16"); Stack preamble `:488` (Story 3.11/3.16). Story mentions total 45 across 27 distinct stories.
- Why it fails the checklist: a builder or test cannot tell "compliant" from "non-compliant" against "Story 3.14 owns adding this label emission" or "Story 8.2 is blocked until"; those belong in the memlog and the story specs. The dated exceptions are content-bound to one story and one consumer and confer no shared invariant. The practical cost is real: the 2026-08-29 proposal existed only to strip a stale "handoff is blocked" paragraph from this spine, and `:60` still calls Stories 4.9-4.15 "active" while 4.15 is now `review` and 4.9-4.14 `done`. This is also what makes the 609-line length: AD-22 (1095 words), AD-11 (844) and AD-24 (821) are ~45% of the AD text, and most of the excess is procedure.
- Disposition: discuss (structural), with a safe autofix subset: (a) move `:337` and `:339-366` to the memlog/story specs and leave in AD-22 one sentence - "Dated scoped exceptions (Story 2.12 2026-07-27; Story 3.13 2026-08-16) are recorded in the memlog and confer no authority beyond their named story and consumer."; (b) replace `:60` "active Stories 4.9-4.15" with "Stories 4.9-4.15"; (c) move the AD-11 `:161` "Story 3.14 owns ..." sentence and the AD-23 `:378` "Story 8.2 implementation is blocked until ..." sentence to the memlog; (d) cut the Release row's package-count schedule to "release inventory is exactly `tools/release-packages.json`" (the count is testable from the file).

### M2 - Structural Seed does not match the ADs and the Capability map

- Severity: medium
- Evidence: seed text block `architecture.md:517-533` lists Contracts, Client, Server, EventStore, Gateway, DomainService, RestApi.Generators, Aspire, ServiceDefaults, Admin.* and four samples. It omits `Hexalith.EventStore.AppHost` (the owner named in AD-9 `:145` and in the topology diagram `:555`), `Hexalith.EventStore.SignalR` (AD-8 `:139`), `Hexalith.EventStore.Testing` (cited as a "Lives in" location in map rows `:586` and `:596`), `Hexalith.EventStore.Operations`, `Hexalith.EventStore.Admin.Server.Host`, `Hexalith.EventStore.Testing.Integration`, and `samples/dapr-components/` (the local half of AD-9's "DAPR component/configuration YAML" unit), `samples/Sample.Tests`, `samples/deploy`. Only `deploy/dapr/` is listed for topology YAML.
- Why it matters: a builder placing a new SignalR notification, an AppHost resource, or a test helper has no seed location, and AD-9's "change together" unit is under-described (only the production half is in the seed).
- Disposition: autofix. Add rows: `Hexalith.EventStore.AppHost/ # local orchestration; AD-9 topology owner with samples/dapr-components/ and deploy/dapr/`, `Hexalith.EventStore.SignalR/ # projection-changed notification transport (AD-8)`, `Hexalith.EventStore.Testing/ and .Testing.Integration/ # shared fakes, read-back helpers, live-sidecar fixtures (AD-12)`, `Hexalith.EventStore.Operations/`, `Hexalith.EventStore.Admin.Server.Host/`, and `samples/dapr-components/ # local DAPR component YAML (AD-9)`.

### M3 - Diagrams grant Admin.Server direct state-store reads that AD-3 forbids

- Severity: medium
- Evidence: topology diagram `architecture.md:576` `AdminServer -->|support-safe operational reads only| StateStore` and `:577` `AdminServer -->|state-mutating actions| EventStore`; paradigm diagram `:78` `Admin -->|delegated writes and safe reads| Gateway`. AD-3 Rule `:109`: "External command/query entry points delegate to the EventStore gateway. They do not call ... state stores ... directly", and its Prevents `:108` explicitly names "Admin code". AD-10 `:151` says admin mutations are attributable but grants no read carve-out. The memlog records the decision ("Admin surfaces are operational facades ... state-mutating admin actions delegate to gateway-owned write paths") but it never became AD text; `deploy/dapr/statestore-cosmosdb.yaml:7-8` already scopes `eventstore-admin` to read admin indexes directly.
- Why it lets two units diverge: Story 7.3 (audit), 7.5 (typed admin client), 5.2 (admin tenant filters) and the Admin CLI/MCP can each decide whether admin reads go through the gateway or hit scoped state components; the two diagrams themselves disagree on whether reads are "delegated" (gateway) or "direct" (state store).
- Disposition: autofix. Append to AD-3 Rule: "Exception: `Admin.Server` may perform support-safe, read-only operational reads against explicitly scoped state-store components (topology diagram); every state mutation and every tenant-data disclosure still passes the gateway's authorization and audit path (AD-10)." Align `:78` to "delegated writes; direct scoped operational reads".

### M4 - Frontmatter/companion hygiene: self-referential `companions`, memlog stops at 2026-08-16, `epics.md` architecture digest stale again

- Severity: medium
- Evidence: `architecture.md:53` lists `_bmad-output/planning-artifacts/architecture.md` under `companions` - the spine itself (the folder's `ARCHITECTURE-SPINE.md` is a symlink to it). `.memlog.md` header `updated: 2026-08-16T12:55` and its final entry is "(event) spine finalized" for the 2026-08-16 run, yet the spine says `updated: 2026-08-29` and cites the 2026-08-20 and 2026-08-29 proposals (`:34-35`); the 08-29 edits (handoff block removed, a Deferred row removed, Stack-authority wording, SDK seed 10.0.400) are unrecorded. `epics.md:15` pins `architecture.md: 623bc23e...` while the committed spine hashes to `2b96a810f34f3cc40ee2ef5fc0f9b1cfaf01a395c73d1321953a946162fe945b` - the exact drift the 2026-08-29 proposal (section 4.5) was raised to repair has recurred.
- Why it fails the checklist: `sources`/`companions` are the spine's provenance contract; a downstream readiness check that recomputes the epics input digest will fail, and the memlog cannot explain the 08-29 changes.
- Disposition: autofix for `:53` (replace with the symlink path `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md` or drop the line) and for the memlog (append the 08-20/08-29 decisions as `(decision)`/`(event)` lines); discuss for the `epics.md` digest because it is owned by the epics artifact, and because the spine will move again if any finding here is applied - reconcile the digest last.

### M5 - Observability, retention/backup, scale-out and multi-region are silent

- Severity: medium
- Evidence: "telemetry" appears only in AD-2's Prevents (`architecture.md:102`) and a seed comment (`:527`); OpenTelemetry only as Stack rows (`:505-506`); no rule for trace/log correlation (MessageId/CorrelationId/CausationId onto spans), exporter posture per environment, metric naming (AD-23 `:378` defers metric names to the payload-protection spec only), or support-safe logging beyond cursors/ETags (`:475`) and NFR5 metadata. "backup" appears once (`:609`, GDPR out of MVP); no event/snapshot retention or backup/restore posture (NFR15 only says Admin must not fake them); scale-out appears only as "at least two EventStore hosts" (`:455`) and the single global allocator (AD-6 `:127`); "region" never appears while `deploy/README.md:31` sells Cosmos DB "global distribution".
- Why it matters: Story 1.7 (done) and 7.11/7.13 (backlog) shape telemetry and evidence tooling; Story 7.4 (backup/restore honesty) and 7.15 (GDPR/backup erasure) depend on a retention stance; a second EventStore host is required by AD-25 but nothing says which components are safe to scale (gateway, projection consumers, global allocator).
- Disposition: discuss; minimal autofix is to add Deferred rows naming each dimension so the silence is deliberate: "Observability contract (correlation propagation, exporter posture, metric naming)", "Event/snapshot retention and backup/restore posture", "Scale-out and multi-region posture (single global allocator; provider-specific)".

### M6 - Conventions table duplicates AD text; the copies already drift

- Severity: medium
- Evidence: `architecture.md:462-485` rows restate AD-7/AD-15 (Cursors and ETags `:475`, Projection lifecycle `:476`), AD-16 (Health probes `:473`), AD-17 (Command status `:472`), AD-18 (Sidecar headers `:481`), AD-21 (UI `:482`), AD-24 (Secrets `:480`), AD-25 (Idempotency admission `:469`), AD-23 (Payload protection `:479`), AD-9/AD-22 (Runtime topology `:483`), and AD-11 (Release `:484`, which repeats the OCI index/platform/validator sentence verbatim and adds a package-count schedule AD-11 lacks).
- Why it matters: two statements of one rule are two places to edit; the Release row already carries a fact (14-to-16 inventory schedule) that AD-11 does not, so a test author reading only AD-11 gets a different rule than one reading the table.
- Disposition: autofix. Keep only rows that add a convention absent from any AD (Identity, Domain naming, State keys, Mutation, Errors, Serialization) and reduce each duplicating row to one line: "See AD-n." (or delete it).

### M7 - Tenant/claims identity guards for Epic 5/7 have no home: reserved `system` tenant and single claims-normalization authority

- Severity: medium
- Evidence: NFR2 (`prd.md:271`) "Tenant provisioning must reject the reserved `system` tenant name" - the spine has zero hits for a system/reserved tenant (`system` appears only in `:97` "The system remains" and `:337`); Story 5.10 (`epics.md:4178`, backlog) owns it. Story 7.2 (`epics.md:4754`, backlog) requires "Admin claims normalized exactly like gateway claims"; the spine has zero hits for `claims`; AD-10 `:151` requires tenant authorization but names no single normalization seam.
- Why it lets two units diverge: tenant provisioning lives in the Tenants domain (another repository) while the reserved-name check could equally be argued into the gateway's tenant validation or the admission adapter; the Convention "State keys" row (`:467`) does not reserve any identity. Claims normalization is a textbook shared seam that Admin.Server, the gateway and the SignalR hub can each reimplement.
- Disposition: autofix. Add to the Identity convention row: "The tenant id `system` is reserved for platform-owned scope; every provisioning path rejects it and every EventStore tenant filter treats it as non-managed." Add to AD-10 Rule: "Tenant and permission claims are normalized by one platform-owned claims-normalization seam shared by the gateway, Admin, and SignalR hosts; hosts do not parse claims locally."

### L1 - Editing artifact in the Deferred table

- Severity: low
- Evidence: `architecture.md:606` begins "**ADD, deferred to a separately approved implementation story.**" - "ADD," reads as a leftover edit instruction, not prose.
- Disposition: autofix - "**Deferred to a separately approved implementation story.**"

### L2 - Erratum narrative inside an AD

- Severity: low
- Evidence: `architecture.md:337` parenthetical "(Attribution corrected 2026-07-28 by code review; the original wording named a `/pushall` merge ... )".
- Why: it is memlog content; it changes nothing a builder must do.
- Disposition: autofix - delete the parenthetical (record it in the memlog if not already there).

### L3 - FR17 and FR18 are bound only by AD-1's blanket range

- Severity: low
- Evidence: no AD `Binds` names FR17 or FR18 except AD-1 `FR1-FR37` (`architecture.md:95`); the Capability map `:588` routes "FR17-FR22, FR25" to AD-9/AD-11/AD-12, but AD-12 binds NFR10 (`:169`), not FR17, and nothing binds FR18. Both stories (3.1, 3.2) are done, so risk is nil.
- Disposition: autofix - add FR17 to AD-12 Binds; add FR18 to AD-9 or accept the blanket and say so in the map row.

### L4 - `status: final` while the body carries live story status

- Severity: low
- Evidence: `architecture.md:9` `status: final`; sprint status has epics 2,3,4,5,8 `in-progress` and 6,7 `backlog`. "final" is appropriate for a build substrate whose decisions are settled, but `:60` ("active Stories 4.9-4.15"), `:70` ("Epic 3 remains open"), `:484` ("remains 14 packages until Story 8.8"), `:378` ("Story 8.2 ... is blocked until") are statements that expire as stories close, which is why the spine had to be reopened on 08-29.
- Disposition: discuss - either keep `final` and apply M1 so nothing in the body expires, or accept that `updated` will keep moving and record each such move in the memlog (M4).

### L5 - AD-21 points readers to the Stack table for a value the spine declares non-authoritative

- Severity: low
- Evidence: `architecture.md:488` correctly makes the Stack table "a dated rendering" and the Builds catalog "live version authority [that] always wins" - the seed-vs-binding treatment is right and stale rows are harmless as authority. But AD-21 `:321` says "(current value in the Stack table)" and the UI row `:482` cites "the Builds catalog's single `HexalithFrontComposerVersion`", while the table `:503` still says `4.1.1` against a committed catalog of 4.3.0 (worktree 4.4.0). Under the spine's own rule the table is not where the current value lives, so the pointer is misleading rather than harmful.
- Disposition: autofix - `:321` "(current value in the Builds catalog; the Stack table is a dated rendering)". The version number itself is the currentness lens's call.

## Passes

1. Divergence coverage for the level below: Epic 5 stories 5.1-5.9 map to AD-10/AD-16 (5.1-5.5), AD-9 (5.6-5.8), AD-9 Convention Runtime topology (5.9); Epic 6 stories 6.1-6.6 are fully governed by AD-13 spec-first plus the Deferred rows at `:604` and `:607`; Epic 7 stories 7.3-7.20 map to AD-10 (7.3, 7.4), AD-21 (7.5, 7.14, 7.19, 7.20), AD-24 (7.6), AD-16 (7.7), AD-9/AD-25 resiliency (7.8), AD-11/AD-22 + Runtime topology row (7.9), AD-12/NFR10 (7.10-7.13), Deferred `:609` (7.15-7.18). Gaps are H2 (7.1), M7 (5.10, 7.2) only.
2. Every FR1-FR37 and NFR1-NFR19 appears in at least one AD `Binds` (FR30 via AD-3 `FR23-FR32` and AD-5 `FR29-FR31`; NFR13 via AD-4/AD-17 `NFR12-NFR14`) and every FR appears in a Capability map row; the frontmatter `binds` (`FR1-FR37`, `NFR1-NFR19`) equals the PRD inventory exactly; no PRD id is missing from the spine and the spine cites no id the PRD lacks (`SM-C2` is a PRD success-metric counterbalance, `prd.md:369`, legitimately bound by AD-12).
3. Deferred rows cannot open a divergence: the append-fence row forbids any slice selecting a local fence; the OpenBao-values row assigns exact values to one overlay; sharding is spec-gated by AD-6; snapshots/guards/upcasting are spec-first under AD-13; UX and GDPR rows are out of this altitude/MVP with an owner named.
4. Stack table is treated as seed, not binding (`:488`), with a defined refresh path (Story 3.11 contract, Story 3.16 follow-ups); AD-11 pins the SDK seed `10.0.400`/`rollForward: latestPatch` and the `10.0.11` security floor consistently with the table and `global.json`.
5. ADs 1-10, 12, 14-20 are enforceable invariants with concrete guardrail evidence (AD-15 item 5, AD-16 Evidence, AD-17 item 5, AD-18 item 5-6, AD-19 normalization matrix); AD-14's metadata flow and AD-15's provenance rule are mutually consistent and explicitly layered (`:239`); AD-16 refines AD-10 and AD-18 refines AD-3/AD-10 without contradiction.
6. Internal consistency held for: AD-25 vs the OQ8 packet (`profile: oq8-postgresql-v1` at the path AD-25 names); `tools/release-packages.json` has exactly 14 packages as AD-11/Release row state; AD-9's "change together" unit matches the topology diagram; the dependency diagram (`:206-226`) agrees with AD-2/AD-4/AD-21 edges; `sources` lists every proposal the body cites, including 2026-08-20 and 2026-08-29.
7. No template comments, `[ASSUMPTION]` tags, `TODO`/`TBD` placeholders; deterministic lint 0 findings (per brief). Line-length/altitude: 20 of 25 ADs are at spine altitude; the heavy five (AD-11, AD-19, AD-22, AD-24, AD-25) are identified in M1 with the procedure text that can move to companions without losing an invariant.
