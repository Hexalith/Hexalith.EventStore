---
title: 'Story 3.21 reconciliation with the shared Platform host'
date: '2026-10-20'
status: 'proposed-awaiting-owner-review'
mode: 'batch'
scope: 'moderate'
approach: 'direct-adjustment'
implementation_approved: false
production_authorized: false
selected_broker: null
---

# Sprint Change Proposal: Story 3.21 and the shared Platform host

## 1. Issue summary

Story 3.21 must supply EventStore's Dapr runtime/component qualification to
Hexalith.Platform, which owns the shared host and hosted Kubernetes composition.
It must not grow an independent EventStore deployment system. The retained local
experiments remain useful evidence, but neither they nor Platform's development
composition establish the intended self-hosted Kubernetes profile.

The requested correction is a technical limitation and ownership clarification,
not a reduction of delivery or security guarantees. This proposal, its
[follow-up specification](../implementation-artifacts/spec-3-21-local-broker-failure-diagnosis.md)
and [next build prompt](next-bmad-build-story-3-21.md) are reviewable drafts.
They do not amend frozen intent, approve implementation, ratify AD-26, enroll a
broker, or authorize a cluster change. Existing specifications, tracking,
evidence, workflows and submodules are preserved.

### Evidence and authority order

The EventStore PRD owns requirement intent, its architecture owns EventStore
invariants, epics own slicing, and authenticated owner records own transitions.
Platform's README establishes the shared host's current use and limitations;
its architecture and backlog describe additional intended capabilities, not
delivered Kubernetes support. Cross-repository decisions need the appropriate
owners' acceptance. A README, this proposal, or a tool persona is no substitute.

| Source | Finding used here |
| --- | --- |
| [Retained execution report](../implementation-artifacts/evidence/story-3-21/verification-2026-10-20/report.md) and [summary](../implementation-artifacts/evidence/story-3-21/verification-2026-10-20/verification-summary.json) | Story in progress, four failed/unresolved vectors, latest preservation and full C# capture gate failed; no broker selected; production eligibility FAIL |
| [Initial qualification design](../implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/qualification.md) and [rollout proposal](../implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/rollout-rollback.md) | Read-only cluster observation, candidate identities, pinned-source concerns and exact-bundle approval boundaries; the initial NOT RUN statement remains true for its historical stage |
| [Platform README](../../references/Hexalith.Platform/README.md) | Run/debug shared composition from Platform; technical-module AppHosts serve own-repository tests only; accepted development tool is exactly pinned, with a broader acceptance range |
| [Platform spine](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md), AD-4/9/10/13 and First shared versions | One active-root mapping, runner-owned test lifecycle, declarative subscriptions and a separate Platform-composed image subject; profile/template and issuer reconciliation remain owned work |
| [Platform epics](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/epics.md), Stories 4.0/4.1/4.16/4.17/6.6/6.22 | Existing owners for backups, cluster upgrade, hosted Dapr policy, broker qualification, shared currency and controlled infrastructure changes |
| [EventStore architecture](architecture.md), AD-5/8/9/11/12/24/26/28/31/34/36 | Persisted Dapr-path proof, durable poison handling, runtime floor, actor-state isolation, authority and unchanged fail-closed gates |
| [EventStore epics](epics.md), Stories 3.17/3.19/3.21/5.6–5.9/7.6/7.22 | Qualification, inventory, profile, component, documentation, secrets and restore responsibilities |

The local candidate is runtime/chart `1.18.7`, CLI `1.18.2`, embedded
components-contrib `1.18.6`, RabbitMQ `4.3.6-management`, Kafka `4.3.1`, with
immutable identities already retained. These are recorded experiment inputs,
not a claim about today's newest release or an approved production selection.
The frozen runtime floor remains `1.18.3`.

Platform's README pins module tool `4.33.1`, Aspire CLI/SDK `13.6.1`, runtime
`1.18.2` and CLI `1.18.0`. Its accepted tool range `[4.33.1, 5.0.0-0)` does not
prove equality of another tool's embedded catalog. Its documented catalog
commit is `c6f56c011ce3a83cdf6100c322352daf125c6af9`. EventStore's inspected
Builds gitlink is `6a38cb0f2e74977d7457fc59e6af79457ed8196a`, and its catalog
also declares runtime `1.18.2` and CLI `1.18.0`. Align exact catalog and tool
identities through Builds and Platform; do not treat equal version labels or
an accepted tool range as equivalent bytes. Dapr .NET SDK `1.18.10` is a
separate pin, not the runtime version.

The retained cluster observation is Kubernetes `1.34.9`, Dapr `1.18.1`,
OpenBao `2.6.2`, one node and retained local storage. It is a dated observation,
not the required rehearsal target or a current capacity, backup or security
attestation. Platform already plans an owner-controlled Kubernetes upgrade
after restore and retirement prerequisites. The rehearsal records the eventual
approved target and every deviation; this correction neither pins the target
forever to `1.34.9` nor authorizes that upgrade. OpenBao reuse still needs
dedicated grants and the EventStore profile's security floor, not reuse of the
observed version or another workload's token.

### Four broker checks and preservation blocker

| Check | Retained observation | Required next work and acceptance |
| --- | --- | --- |
| RabbitMQ secret rotation | New generation produced one effect; stale publisher timed out with synthetic status `599` | Record generation publish, overlap, all consumer acknowledgements, controlled startup-only rollout, revocation and session handling. Probe both retained old connections and a fresh sidecar explicitly loading the old lab generation. Require correlated broker/Dapr authentication denial, bounded HTTP failure and fresh Dapr readback beside successful new-generation traffic. Timeout alone stays unknown/FAIL. |
| Kafka secret rotation | New generation produced one effect; stale publisher returned `204` after the attempted old-principal denial | Prove the actual TLS principal, ACL target, policy propagation and existing-session behavior; isolate incorrect fixture identity or policy from runtime behavior. Repeat old-session and cold-old-generation controls beside new-generation success. `204` cannot prove revoked access; a generic failure without correlated denial is insufficient. |
| RabbitMQ cancellation/backpressure | Four `204` IDs absent from the bounded persisted-state reconciliation; cancelled ID later had one effect | Reproduce queue capacity/reject-publish and negative confirms with exact receipts, drain/restart and per-ID Dapr readback. Distinguish pending from confirmed loss; require every acknowledged ID to reconcile within the declared bound and negative confirms to fail. Cancellation bounds waiting, not proof that no effect happened; reconcile unknown outcomes using the same ID, never authorize a second effect. |
| RabbitMQ missing bindings | Bound control had one effect; unbound publication returned `204` | Retain queue/exchange/binding lifecycle diagnostics and bound/unbound controls through Dapr. Verify missing binding independently. Require the existing fail-closed publication guarantee; do not equate publisher confirmation with queue acceptance. Pinned source's `mandatory=false` is a risk hypothesis, not a completed runtime diagnosis. |
| Docker preservation | Two unrelated transient containers disappeared; surviving specifications matched and owned resources were removed | Allocate an exclusive disposable worker/VM with a dedicated local Unix-socket daemon, verify endpoint and daemon identity before mutation, and retain all unrelated before/after specification hashes. A cooperative lock on a shared daemon is insufficient. Preserve the entire existing baseline; deletion/change still fails. Prove sentinel preservation and negative deletion/change fixtures. No ignore list, broad prune, disabling another workload, remote-daemon bypass or global workspace freeze. |

Pinned-source RabbitMQ negative-confirm/cancellation behavior and Kafka
retry-exhaustion behavior remain concerns to falsify. A fixture correction may
produce a new observation; it never rewrites the previous one. If a guarantee
is absent in the pinned runtime, record the reproducible rejected candidate and
an owner/upstream handoff. A different runtime/component requires a separately
authorized immutable candidate and the relevant requalification. Application
provider clients, extra retries, longer timeouts or predeclaring queues alone
cannot silently waive these failure guarantees. Both brokers retain the full
12-scenario matrix, including poison capture before source acknowledgement.

A fresh read-only recheck during this course correction used
`python3 tools/validate-dapr-broker-qualification.py _bmad-output/implementation-artifacts/evidence/story-3-21/verification-2026-10-20/packet --allow-failures`
and exited `1`: `FAIL: artifacts: unlisted or missing retained files`.
The unlisted file is
`packet/inputs/tools/__pycache__/dapr_broker_qualification.cpython-314.pyc`.
It was already present in this task's starting preservation snapshot. No listed
artifact is missing. Leave the file and original packet unchanged; retain this
current integrity failure separately from the report's Docker failure. Future
runs must prevent interpreter caches and verifier output inside closed packet
trees, without filtering the inventory guard or resealing old bytes. This
recheck stops before the container-comparison check and does not establish a
new broker result or reproduce the reported preservation cause.

## 2. Impact analysis

### Responsibility map

| Owner / artifact | Produces | Consumes | Does not confer |
| --- | --- | --- | --- |
| EventStore Story 3.21 | Disposable local Dapr/component experiments, immutable input lock, observed per-broker guarantees/failures, independent verification, candidate inventory and accepted handoff receipts | Existing retained evidence, Story 3.17 schema, Builds-qualified candidate pins and Platform rehearsal receipts | Broker selection, shared-host deployment, canonical production issuance or production promotion |
| Hexalith.Platform | Shared composition, run/environment ownership, resolved components and subscriptions, rendered deployment, disposable self-hosted Kubernetes rehearsal and cross-workload preservation; later exact deployment/restore bundle | EventStore owner declarations, contracts, qualification, Stories 5.7/7.6, Builds catalog/tool and accepted profile contract | EventStore semantic qualification from startup alone, AD-26 ratification by inference, waiver of failed module gates |
| Hexalith.Builds | Runtime/CLI/SDK/control-plane catalog, embedded tool catalog and compatibility checks, generic runner/descriptor support, shared evidence codec/publisher/validator | Qualified candidate evidence and owner-approved catalog changes | Production runtime/broker selection or an EventStore-specific orchestration fork |
| EventStore Story 3.17 | Accepted profile-scoped operation/component inventory and guard | Proposed Story 3.21 rows with exact runtime, components, broker, guarantees, evidence and disposition | Acceptance of a proposed row merely because a JSON handoff exists |
| EventStore Story 5.7, with 5.6/5.8/7.6 | Module-owned component/ACL/health/resiliency/secret contracts and effective parity, actor-invocation qualification | Platform model/rendered/loaded observations, owner-selected broker after AD-26, qualified runtime | A second host, bypass of actor execution contexts, or completion of OpenBao merely from broker rotation tests |
| EventStore Story 3.19 | Publication schema/validator and canonical resolved profile retention at `deploy/dapr/production-profile.yaml`; authenticated role and subject binding | Qualification, Platform composition identity, selected component, ratification, activated catalogs, restore/envelope evidence and shared Builds codec | A substitute Platform profile slot, reuse of an old subject's records for a composed image, or authority from evidence validation |
| Architecture + Platform deployment owners | Authenticated AD-26 ratification or replacement, explicit runtime/broker selection and profile/restore/envelope decisions | Eligible exact-subject evidence | Automatic broker choice from relative pass counts |

Platform implements environment composition and rendering; EventStore owns
module semantics and reusable hosting libraries. In Platform compositions,
tests consume the runner's versioned environment descriptor and never launch a
second AppHost or duplicate lifecycle. EventStore's non-exported local broker
fixture remains an own-repository experiment. Platform source runs use its
active-root Debug mapping; hosted/package evidence uses exact Release/package
and image identities. Dirty source experiments cannot stand in for those
committed candidate identities.

### Epic and artifact impact

- Epic 3: amend Story 3.21's self-hosted scope, staged completion and handoff;
  Story 3.17 accepts proposed rows; Story 3.19 binds the actual composed subject
  and resolves template/binding ownership. Story 3.16 owns approved catalog
  adoption. Story 3.18 classifies any changed released API/manifest/host seam.
- Epic 5: Stories 5.6–5.8 compare EventStore contracts with Platform's model,
  rendering and actual sidecars. Story 5.7 retains per-app physical actor-state
  isolation and actor restriction proof; Story 5.9 removes drift only from
  verified owners. Story 7.6 still owns real OpenBao closure.
- Epic 4: Story 4.15 remains the only route for changing sealed OQ8 workflow
  inputs. Story 4.16 still owns the production-path append envelope and must
  re-prove it after a Dapr minor change. No OQ8 evidence is refreshed here.
- Epic 7: Story 7.22 still owns scheduler-inclusive consistent restore proof;
  Platform integrates it into its environment rehearsal and later bundle.
- Epic 2: Story 2.13 packets and NFR5 stay unchanged. Broker event delivery
  cannot establish notification provenance, multi-replica fan-out or freshness.
- Epic 9: Story 9.1 current-subject authorization, Story 9.2 assurance/sealed
  transitions and Story 9.3 substantive baseline renewal remain prerequisites.
  No hashes, statuses or approvals are refreshed merely to make them match.
- Epics 1, 6 and 8: no new behavior or scope. Existing hosting, evolution,
  payload, package and consumer contracts remain governed by their owners.
- Platform: extend existing Stories 4.16/4.17 and the shared profile/rehearsal
  work; do not create a competing broker-selection story or claim those stories
  delivered. Shared-infrastructure changes remain under its controlled procedure.

The PRD's FR/NFR identities and primary ownership remain unchanged. The
clarification belongs in §8.4, architecture AD-9/11/26, the gate table,
epics and the Story 3.21 frozen specification after approval. No UI layout or
interaction is changed; existing UX `Unknown`/degraded and authoritative
readback rules continue to apply. Whole-document PRD/epics, architecture and
UX were located; affected clauses and cross-epic dependencies were assessed.

## 3. Recommended approach

Use **direct adjustment**, classified **moderate**, with Product Owner,
EventStore/Platform/Builds developers and Architecture/Security owners.
No new product epic or MVP reduction is needed. Local evidence collection and
schema/render preparation can proceed as separately authorized slices; hosted
qualification and all authority transitions wait for their actual prerequisites.

Rollback is not recommended: deleting the runner, reverting evidence or
restoring an older capture would remove diagnostics without fixing the missing
guarantees. MVP review is not recommended: the shared-host boundary is
compatible with current goals; unresolved durability/security still blocks
readiness. Neither option closes a failed gate.

Planning estimate: medium effort for EventStore diagnostics and runner
preservation; medium effort for Builds integration and component contracts;
high uncertainty for runtime defects, Platform hosted qualification and owner
records. Reserve distinct backlog slices and re-estimate after failure diagnosis.
There is no credible production date until an eligible candidate, supported
cluster target, restore proof and authority inputs exist. The critical path is
those dependencies, not documentation or a green evidence-capture test.

## 4. Exact proposed amendments

All OLD excerpts below identify replacement anchors, not permission to edit
sealed or frozen files. NEW text is proposed normative wording. Apply each
inside its owning repository only after the corresponding approval and
authorization. Do not change historical packets or approved evidence bytes.

### A. Story 3.21 frozen Intent and boundaries

Target: [current specification](../implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md),
Intent / Approach.

**OLD:**

> Inventory the cluster read-only, qualify immutable Dapr `1.18.7`/CLI `1.18.2` candidates with RabbitMQ `4.3.6` and Kafka `4.3.1` in disposable infrastructure, and deliver reproducible evidence with a gated rollout/rollback plan.

**NEW:**

> Inventory the target read-only and retain immutable Dapr `1.18.7`/CLI `1.18.2`, RabbitMQ `4.3.6-management` and Kafka `4.3.1` comparison evidence. EventStore owns local Dapr/component qualification and the guarantee/failure handoff. Hexalith.Platform owns the shared composition, disposable self-hosted Kubernetes rehearsal and later exact deployment/restore bundle; Hexalith.Builds owns catalog, CLI and runner pins. EventStore's own AppHost and broker fixture serve repository tests only. Only authenticated owner AD-26 ratification selects a runtime and broker; technical observations, Platform startup and passing capture tests convey no deployment authority.

Append to **Always**:

> Preserve the full 24-vector comparison and its existing pass conditions. Record packet integrity, observed scenario outcomes, candidate qualification, Platform composition/rehearsal and production authority as distinct results. Use an exclusively allocated local Docker daemon for new captures; every pre-existing unrelated container identity/specification remains in the preservation denominator. Platform composition tests consume its runner-owned descriptor and never start an EventStore-owned shared host. All cross-repository evidence binds exact revisions, tool/catalog and artifact identities, loaded configuration and accepted owner handoffs.

Append to **Never**:

> Do not overwrite failed or historical packets, exclude disappearing containers from preservation, treat cancellation or timeout as proof of no effect, convert a rejected guarantee into PASS, or derive broker choice from pass counts. Do not fork Platform composition, edit Builds defaults from EventStore, or substitute the development runtime/catalog for the qualification lock. Preserve Story 2.13, sealed workflows and existing authority records.

Rationale: acknowledge the shared host without discarding local evidence or
changing a safety/authority gate.

### B. Story 3.21 execution, acceptance and completion

Target: the same specification, Tasks & Acceptance.

**OLD:**

> retain outcomes and Story 3.17 inventory-row handoff, guide drift for Story 5.9, and exact proposed production diff/restore bundle after qualification. Bind to Stories 5.7/3.19 only after valid owner records; keep Story 3.21 unfinished until those gates pass.

**NEW:**

> Retain the existing packets unchanged and create fresh diagnostic runs for all 24 broker vectors. Close the capture/preservation task only when the independent verifier accepts the complete executed capture and unrelated-resource preservation; explicitly retain any rejected guarantee. Deliver proposed Story 3.17 rows, Story 5.9 drift and content-bound inputs to Platform's existing hosted qualification work. Platform owns the later exact production deployment/restore bundle. Accept Platform rehearsal receipts into Story 3.21 only when they bind the same qualified immutable runtime/component/broker inputs and the actual composed workload identity. After valid AD-26 records explicitly select a qualified candidate, retain accepted selected-component and runtime/profile-input handoff receipts for Stories 5.7/3.19.

Add acceptance criteria:

1. A complete, integrity-valid failed experiment may complete the separate
   diagnostic slice. It never marks candidate qualification or Story 3.21 done.
   Missing observations, preservation failure or contradictory receipts fail
   even that capture milestone.
2. RabbitMQ rotation, Kafka rotation, RabbitMQ backpressure and RabbitMQ missing
   bindings meet the closure controls in §1 or remain explicit FAIL/unresolved
   with reproducible owner handoffs. Their original failure results remain.
3. Platform's rehearsal binds server/node versions, chart/rendered manifests,
   admission and sidecar injection, mTLS/trust domains, DNS, native TLS,
   OpenBao generations and grants, topic catalog, durable storage restart,
   scheduler/placement and isolated restore. It re-executes applicable Dapr
   delivery/security/fault vectors against the actual shared host and retains
   all target deviations; local SQLite readback is not PostgreSQL actor proof.
4. Story 3.21 stays in progress until qualified selected inputs, valid owner
   AD-26 records and accepted 5.7/3.19 input bindings exist. It does not wait for
   Story 3.19 to issue the downstream publication records that depend on it.
   Story 3.19 issuance and any production execution remain separately blocked.

Rationale: remove the lifecycle/ownership cycle while keeping parent-story
completion and production eligibility gated. A candidate may remain rejected;
this proposal selects none and guarantees neither will qualify.

### C. EventStore epics: Story 3.21

Target: [epics](epics.md), Story 3.21.

**OLD:** `and the cloud-managed pubsub.azure.servicebus.topics`;
`1.18.4 is the current stable release`; `Current reconciliation ... Backlog`.

**NEW:**

> The intended MVP is self-hosted Kubernetes. Compare `pubsub.kafka/v1` and `pubsub.rabbitmq/v1` using the retained immutable candidate lock; Azure/AWS qualification is outside this slice. The runtime floor is `1.18.3`; `1.18.7`/CLI `1.18.2` is the current retained experiment identity, not an approved production pin. Story 3.21 is in progress with four rejected/unresolved checks and a failed latest preservation capture, as recorded in the 2026-10-20 report. Platform owns the hosted composition and rehearsal through its existing Story 4.17 and shared-runtime work. Builds owns runtime/CLI/control-plane catalog and embedded tool alignment. Completion follows amendment B, with no inferred owner selection.

Retain the existing immutable identity, redelivery, quarantine, retention,
CloudEvent identity, scope, credential, guide-drift, reseal and minor-version
re-proof criteria. Propagate amendments A/B into this story's acceptance text;
update Epic 3 context after approved source edits. Do not repin planning input
digests until Story 9.3's substantive reconciliation and owner approval.

### D. Stories 5.6/5.7/5.8 and 7.6: composition and parity

Target: [epics](epics.md), relevant Dependencies and Acceptance sections;
carry the wording into their own specifications before freezing.

**OLD (5.7):**

> Production YAML and AppHost identities are one governed topology.

**NEW:**

> EventStore owns the module topology contract; Platform owns the shared composition, resolved hosted configuration and declarative Subscription rendering. Stories 5.6/5.7/5.8 compare the contract with Platform's model, rendered manifests and loaded sidecars. EventStore's own AppHost proves repository behavior only. Story 5.7 may prepare candidate variants before broker selection, but final production parity binds the owner-selected Story 3.21 runtime/broker, accepted Story 3.17 rows and exact Platform rehearsal. It retains default-deny ACLs, channel-only peer denial, effective actor-invocation tests, app health/resiliency, exact topic and DLQ grants, and per-actor-app physical state targets under AD-5. Other apps read shared operational state through separately scoped components; they receive no actor-store access by broadening scopes.

**OLD (7.6):** `production DAPR runtime is pinned compatibly with the repository's 1.18.0 seed`.

**NEW:**

> Use the qualified immutable runtime/profile inputs from Story 3.21 and the approved Builds catalog. Platform composes the value-free secret-contract instance and singleton logical OpenBao role; EventStore owns the AD-24 retrieval, readiness, scope and rotation semantics. Any physical-name injection into the canonical `openbao` contract requires explicit EventStore/Platform architecture reconciliation before hosted enrollment. Broker-lab secret rotation is supporting evidence and never completes Story 7.6 or a production OpenBao server/security gate.

Preserve production's independent state targets and default-name migration
work already required by AD-5. Do not restore the superseded zero-actor-state
criterion for every domain service, or equate `keyPrefix` with actor isolation.

### E. Story 3.19: canonical profile and composed subject

Target: [epics](epics.md), Story 3.19 Dependencies and Acceptance; coordinate
the EventStore AD-11/26 amendments below with Platform AD-13 owners.

**OLD:**

> issue their records for the unchanged Story 3.15 subject

**NEW:**

> Preserve Story 3.15 and all existing records on their original immutable subject. EventStore issues evidence-validated and release-available records only for its exact qualified server/package subject. A Platform-composed `platform/eventstore` image is a new Platform subject, binding the eligible EventStore server/package, every extension, catalog, runtime, resolved profile and rendered-deployment identity. Only registered, authenticated Platform issuers may issue that composed subject's release-available and production-promoted records. A changed or composed subject starts its own lifecycle at built; it never inherits Story 3.15's container receipts or an unrelated package's authority. Owner approval of this issuer/subject split and the EventStore extension API is required before composed-host availability.

Append:

> Story 3.19 owns EventStore's profile schema and exact-byte validator. `deploy/dapr/production-profile.yaml` remains the sole EventStore canonical inventory slot. Platform generates a complete value-free resolved candidate from its composition and the agreed template/binding contract; the deployment owner publishes the accepted canonical bytes at that slot, and Platform's release/deployment records bind the same digest. A template, partial binding, development manifest or second Platform profile file cannot alone satisfy EventStore AD-26. The retained canonical bytes enumerate actual app/component/physical target/runtime/issuer/catalog/restore/envelope identities; copies are explicitly digest-bound projections, not competing authority.

Keep schema/fixture build work independent from issue-step prerequisites.
Issuance still needs AD-26, selected qualified broker/runtime, component parity,
activated catalogs, scheduler-inclusive restore, append-envelope proof,
ReleaseEvidenceCodec at its pinned Builds identity, valid corrective authority
and required sealed transitions. Platform rollout sequencing adds no exemption.

### F. PRD and architecture

Target: [PRD §8.4](prd.md#84-dapr-infrastructure-boundary).

**OLD:**

> Provider-specific provisioning, Dapr component configuration, and deployment/backup administration remain platform operations.

**NEW:**

> Provider-specific provisioning, Dapr component configuration, and deployment/backup administration remain platform operations. Hexalith.Platform owns shared host composition and hosted self-managed Kubernetes rehearsal; EventStore owns reusable runtime semantics and Dapr operation/component qualification, and Hexalith.Builds owns shared tool/runtime/catalog pins. Own-repository fixtures and AppHosts establish no Platform or production-path authority. Every composed artifact and resolved profile retains its own exact subject, issuer and existing acceptance gates.

No FR/NFR or MVP scope change is proposed. Propagate this clarification to
§11.2.1 supporting ownership if needed; leave all mandatory gate results failed
until their existing evidence and approval rules pass.

Target: [architecture](architecture.md), AD-9.

**OLD:**

> App IDs, service methods, route-catalog fingerprints, sidecar options, component scopes, ACLs, resiliency, placement/scheduler endpoints, topics, topology tests, deploy/operator documentation, and machine-checkable examples change in one slice across AppHost and deployment assets.

**NEW:**

> App IDs, service methods, route-catalog fingerprints, sidecar options, component scopes, ACLs, resiliency, placement/scheduler endpoints, topics, topology tests, deploy/operator documentation, and machine-checkable examples change as one content-bound cross-repository slice: EventStore owns reusable capability and module contracts, Platform owns the shared composition and hosted rendering, and Builds owns catalog/tool pins. Repository AppHosts remain test fixtures; their observations never substitute for Platform composition or hosted qualification. Owner contracts, rendered output and actual loaded sidecars must agree.

Target: AD-11 publication lifecycle, append amendment E's explicit
server/package versus Platform-composed subject/issuer split. Preserve all
existing immutable subjects and require new registered issuer authority.

Target: AD-26 Production proof, append:

> Shared hosted composition and disposable Kubernetes rehearsal are Platform-owned. Story 3.21 supplies EventStore's qualified Dapr/runtime/broker inputs; Stories 5.7 and 7.6 supply module component and secret contracts; Builds supplies qualified catalog/tool and shared evidence-codec identities. The agreed profile template/binding mechanism must produce the complete canonical resolved inventory at `deploy/dapr/production-profile.yaml`, consumed by Story 3.19 and bound by Platform's composed release/deployment records. Rehearsal or candidate publication never selects a broker, replaces per-role ratification or supplies production authority.

Update Structural Seed's AppHost description and the runtime/broker,
profile-identity and production-publication gate rows to this ownership.
Record the AD-26 impact in the architecture memlog: these edits change what
AD-26 requires, including its subject/issuer and rehearsal inputs. Therefore
edit AD-26 itself in the same approved change, recompute its defined subject
digest, and require fresh per-role owner records and the AD-11 separation
window. Do not reuse a previous ratification or perform a hash-only repin.
Do not mark the production target ADOPTED through this proposal.

Target: Story 7.22 Dependencies in [epics](epics.md).

**OLD:** `Story 3.21 (scheduler and placement behavior is runtime-specific)`.

**NEW:**

> Restore design and disposable drills consume Story 3.21's exact candidate runtime/scheduler/placement evidence, without waiting for the parent story to be done. The owner binds the accepted restore-posture digest in the later AD-26 record; Story 7.22 completion and Story 3.19 issuance still require their existing authority and profile gates. Likewise, candidate component/render preparation does not wait for final selection, but selected production parity does. These staged inputs create no production authority and remove no restore invariant.

Rationale: AD-26 names a restore posture, so the candidate restore work must be
possible before selection/completion. This avoids a 3.21 → 7.22 → ratification
→ 3.21 cycle. Story 4.16 still runs its final envelope proof against the
ratified profile; preparation is not G-APPEND acceptance.

### G. Platform and Builds amendments, applied by their owners

Platform [Story 4.17](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/epics.md):

**OLD:**

> Given the broker candidates / When one is selected / Then ... the choice is recorded in the profile inventory

**NEW:**

> Given EventStore Story 3.21's immutable candidate/failure inventory and an owner-approved Builds candidate catalog/tool, Platform composes the non-promoting disposable hosted rehearsal and records loaded identities, target deviations and all required broker/security/storage results. Both candidates' retained results remain available. Selection is made only by the authenticated Architecture and Platform deployment owner AD-26 records after required qualification; Platform records that explicit choice without ranking or automatic fallback. The selected profile then requalifies in the hosted composition. Missing or failed evidence blocks eligibility, even when the shared host starts.

Extend Platform's shared profile work with amendment E's single canonical
resolved profile binding and issuer registration. Story 4.16 supplies hosted
trust/sidecar policy and activation proof; controlled infrastructure and restore
stories retain ownership. Add no EventStore-owned Helm/cluster launcher.

Builds catalog/tool acceptance:

**OLD:** current runtime `1.18.2` / CLI `1.18.0` and accepted tool versions
whose embedded catalogs can differ.

**NEW:**

> An explicitly approved candidate test profile binds exact runtime, CLI, embedded component source, placement, scheduler, Sentry and Aspire/tool catalog identities. Runtime and CLI compatibility are checked independently. Candidate tooling either supports that profile through the shared runner/descriptor or rejects it before resource creation; EventStore supplies no private override. Defaults and the Platform-accepted tool/gitlink move only through Builds' tested catalog/release process and Platform owner acceptance. Range acceptance alone never proves embedded-catalog equality. A candidate pin is not the AD-26 production selection.

The Builds owner also owns generic runner isolation/descriptor capability and
shared ReleaseEvidenceCodec delivery. Neither catalog edits nor changes to the
new manual qualification lane authorize changes to sealed EventStore workflows.

## 5. Ordered changes and implementation handoff

| Order / slice | Owning repository / recipient | Concrete change and exit evidence |
| --- | --- | --- |
| 0 — accept intent and scope | EventStore Product Owner + Architecture; Platform and Builds owners for their portions | Review A–G and the local follow-up spec. Approve exact frozen changes per owner, then validate current-subject Story 9.1 corrective authorization before any gate-closing build handoff. Preserve in-progress/backlog states; record cross-repository agreement. Proposal approval alone is not AD-26 or production approval. |
| 1 — ES-DIAG | EventStore developer/Test | Execute only the draft follow-up spec after approval. Secure exclusive local Docker execution, test preservation without exclusions, diagnose the four checks and rerun all 24 observations. Produce new immutable packets, honest results and proposed 3.17/Platform/Builds handoffs. This independently scoped diagnostic slice can finish while parent 3.21 stays in progress. |
| 2 — B-PINS | Builds maintainer, then Platform tool owner | Use ES-DIAG evidence to approve/reject a candidate profile or proposed runtime remedy. Implement catalog/tool/runner support and compatibility/identity negatives; publish only through separately authorized Builds release work. Platform accepts an exact tool + embedded catalog + gitlink combination. Do not change EventStore sealed workflow pins. |
| 3 — ES-CONTRACT | EventStore 3.17/5.6/5.7/7.6 owners | Accept inventory dispositions, declare logical roles/topics/DLQ/rotation and per-app physical actor-state isolation, and reconcile manifest/SDK defaults. Prepare both candidate variants without selecting one; preserve shared readers through separate state components. Classify any released surface change through 3.18. Contract preparation need not wait for production authority. |
| 4 — P-REHEARSE | Platform developer/operations with EventStore/Test | Amend existing hosted qualification work; use Platform active-root identity and shared runner, accepted Builds pins and actual EventStore contracts. Render and rehearse in an owned disposable Kubernetes cluster; verify injection, policy, Dapr loaded identity, full fault/security/readback matrix, target deviations, storage and scheduler-inclusive restore. Coordinate existing cluster-upgrade/currency backlog without mutating the observed cluster. Failed candidates remain ineligible. |
| 5 — SELECT and BIND | Architecture + Platform deployment roles, EventStore 3.21/5.7/3.19 owners | Issue valid AD-26 records only for an eligible candidate and approved subject; explicitly choose the broker/runtime. Bind accepted selected-component and profile-input handoffs, finalize selected component parity, and retain the complete resolved canonical profile. No automatic fallback if neither candidate qualifies. Close parent 3.21 only on its approved completion criteria. |
| 6 — PROVE and ISSUE | EventStore 4.16/7.22/5.13/3.19 and Epic 9 owners; Builds codec owner; Platform issuers | Complete exact production-profile append/restore/catalog/security and issuer evidence, codec resolution and required sealed transition runs. Issue separate EventStore-package and Platform-composed lifecycle records on the correct subjects. There is no dependency on already-issued Story 3.19 records to collect upstream qualification. |
| 7 — PLAN and DOCUMENT | Platform deployment/operations owner; EventStore 5.9 | Assemble the exact reviewed change/restore bundle with prior release inputs, shared-workload acknowledgements, supported clients, capacity/retention and stop conditions. Reconcile guide drift from verified contracts. Any production mutation, shared Dapr upgrade, traffic switch or migration requires separate explicit exact-bundle approval; none is part of the next build. |

Concrete file ownership for those slices:

- **EventStore:** amend the parent spec, `epics.md`, `architecture.md`, its
  architecture memlog and `epic-3-context.md` only after approval; implement
  ES-DIAG in its listed local tools/fixture/candidate files. Stories 3.17, 5.7,
  7.6 and 3.19 retain their inventory, component, secret-contract and canonical
  profile/validator paths. Future guide work stays in Story 5.9.
- **Builds:** begin with `Props/Directory.Packages.props` and its embedded
  `PlatformVersionCatalog`; validate the `CompositionPrerequisiteProbe`,
  `CompositionToolchainPins`, runner and run-evidence paths against that catalog.
  Implement generic descriptor/isolation support there if needed, then qualify
  and release the exact tool/catalog under Builds' own rules. Shared codec work
  stays in its publisher/validator, not an EventStore copy.
- **Platform:** amend existing planning contracts and Story 4.17, then evolve
  `src/Hexalith.Platform.Composition/` and its focused composition/toolchain tests.
  The exact accepted tool/catalog adoption touches `.config/dotnet-tools.json`,
  `hexalith-platform.json`, `apphost.cs`, its Builds gitlink and README together
  only when required and authorized. Use owner manifests under Platform's direct
  `references/<Owner>/module/` checkouts; never pass the copied declarations as
  additional modules. Hosted render/rehearsal and the later deployment bundle
  belong to Platform's existing environment work, with paths chosen there after
  inspecting its effective guidance. No nested submodule initialization.

Preparation in slices 2–4 may overlap only where it does not depend on an
unproven candidate or missing owner contract. Rejected runtime behavior returns
to its owner for a separately scoped remedy; it does not trigger an automatic
dependency refresh. Moving EventStore, Builds or Platform gitlinks, staging,
commits, pushes, publication and external messages require their own authorized
work; this plan performs none.

Success means a reader can trace an observed guarantee from EventStore's
candidate bytes through Builds pins, Platform's actual composition and
Kubernetes rehearsal, selected component parity and the same canonical profile
digest to valid authority records. Every missing link remains explicit FAIL.
Direct notifications and the current single-replica/no-multi-replica-freshness
claim posture remain until Story 2.13/AD-36 qualify their separate path. Three
replicas on one node establish no node-loss HA; retained PVCs establish no
backup or restore proof.

### Checklist and workflow record

| Checklist items | State | Result |
| --- | --- | --- |
| 1.1–1.3 trigger, problem, evidence | [x] | Story 3.21 report and Platform host ownership establish the trigger and five concrete blockers |
| 2.1–2.5 epic scope, remaining epics, sequencing | [x] | Existing epics retained; cross-repository slices and no-cycle dependencies above |
| 3.1 PRD, 3.2 architecture, 3.4 other artifacts | [x] | Exact proposed amendments A–G; no FR/NFR reduction, authority repin or sealed edit |
| 3.3 UX | [N/A] | No UI changes; support-safe unknown/freshness semantics preserved |
| 4.1 direct adjustment, 4.2 rollback, 4.3 MVP review, 4.4 choice | [x] | Direct adjustment selected; rollback/MVP reduction rejected for stated reasons |
| 5.1–5.5 issue, impact, recommendation, scope, handoff | [x] | Proposal, amendments, ordered repository changes, draft implementation spec and next prompt produced |
| 6.1–6.2 completeness and consistency | [x] | Source/preservation and document checks retained with this handoff |
| 6.3 explicit approval | [!] | Pending owner review; no approval inferred from the request to prepare documents |
| 6.4 sprint status | [N/A] | No added/removed/renumbered stories; existing modified tracker preserved; parent 3.21 remains in progress |
| 6.5 implementation routing | [!] | Ready for PO/DEV coordination; actual handoff requires approved intent and valid corrective authorization |

Workflow activation resolved empty prepend/append/persistent-fact lists.
Configuration is English, expert, user Administrator. Batch mode follows the
request for a complete amendment/handoff package. No review was delegated and
no external messages were sent. Step 4 is complete; Steps 5–6 await explicit
proposal approval. The handoff does not claim the Correct Course workflow's
implementation-approval stage complete.

Preservation and checks are recorded in the
[source snapshot](../implementation-artifacts/evidence/story-3-21-platform-host-reconciliation-2026-10-20/source-snapshot.json)
and [validation record](../implementation-artifacts/evidence/story-3-21-platform-host-reconciliation-2026-10-20/validation.json).
