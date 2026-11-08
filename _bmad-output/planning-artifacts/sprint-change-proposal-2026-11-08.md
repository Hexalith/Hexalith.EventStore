---
title: 'Use Dapr Cryptography with Kubernetes Secrets and refresh Dapr to latest stable'
date: '2026-11-08'
status: 'approved-planning-applied'
approval_date: '2026-11-08'
approval_source: 'Interactive project owner: continue and approve'
approved_proposal_sha256: '4b618adbca7c6802e41b6da40dac6b8d085ab81875921d6bb1293d9ec3dccd68'
mode: 'batch'
scope_classification: 'moderate-backlog-change-with-security-architecture-amendment'
trigger_story: '8-7-server-persistence-and-snapshot-integration'
reviewed_head: 'b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce'
reviewed_worktree: 'dirty; existing story verification and submodule changes preserved'
selected_component: 'crypto.dapr.kubernetes.secrets/v1'
target_runtime: '1.19.1'
target_cli: '1.19.1'
target_dotnet_sdk: '1.19.0'
implementation_authorized: false
planning_application_authorized: true
---

# Dapr Cryptography and latest-stable runtime course correction

## 1. Issue Summary

Story 8.7's first phase implemented a neutral `payload-key-v1` Dapr binding contract. Its remaining Server integration depends on a separately scoped Platform RSA/OpenBao component, real-service qualification and capacity work. The Platform component does not implement that payload-key contract. Deploying the ordinary OpenBao secret store in Platform Story 4.15 would not implement it.

The owner requested `$bmad-correct-course use DAPR Cryptography component`, explicitly selected **Kubernetes Secrets with private keys available to Dapr sidecars**, selected **batch** review, and then requested **latest Dapr**. These decisions authorize preparation of this concrete proposal. They do not approve unseen normative schema/authority bytes or install a new cluster runtime.

The proposed on-premises path uses the built-in `crypto.dapr.kubernetes.secrets/v1` component. EventStore calls Dapr; Platform provisions versioned Kubernetes key Secrets and owns their custody and recovery. The custom OpenBao payload-key binding ceases to be a prerequisite of this path. Ordinary operational/application secrets continue to use OpenBao under their existing requirements.

### Verified release targets and API limitation

Latest stable releases were checked on 2026-11-08 against official GitHub release APIs and NuGet flat-container version indexes:

| Item | Target | Observed source identity / disposition |
| --- | --- | --- |
| Dapr runtime and matching control-plane family | `1.19.1` | `cfb5ee090dda7cfc26145c7703605d831350aa2a`; no prerelease selected |
| Dapr CLI | `1.19.1` | `e6263a6e5e6aa63b73ae24ea76a685d074c21d96` |
| Embedded components-contrib | `1.19.0` | `1d331c389f11baaa1d664022b6b6c2f59811ce11`; read from runtime `go.mod`, not independently upgraded |
| Dapr .NET SDK family | `1.19.0` | Already pinned in the checked-out Builds catalog; `Client`, `Actors`, `Actors.AspNetCore`, `AspNetCore` and `Workflow` match latest stable NuGet indexes |

The downloaded official standard Linux amd64 runtime archive matched its published SHA-256, `93217800d5072cf1735ed89ec8a9cdeedfb9d235895fb9e0d864088bc5d9466c`. The extracted executable SHA-256 is `b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2`; `--version` returned `1.19.1` and `--build-info` returned the full runtime commit above. The CLI archive's published checksum is `6e9a339d261e430e5914909cc4443572573c197c174c3e14504c34116831de3b`; the CLI archive was not downloaded or executed.

The runtime release is stable; the cryptography APIs still use `Alpha1` and the documented Kubernetes cryptography component is certified Alpha. The 8.12 profile must explicitly accept and qualify that support posture; the runtime version alone cannot upgrade its certification. Use the standard component-inclusive release artifact in qualification rather than assuming a stable-components-only binary contains this component.

An exclusively owned loopback sidecar with an empty resource directory returned **HTTP 501 / `ERR_API_UNIMPLEMENTED`** for all three POST requests to `/v1.0-alpha1/subtlecrypto/qualification/{getkey,wrapkey,unwrapkey}`. Its process was stopped and reaped. The default 1.19.1 source still excludes the implementation without the `subtlecrypto` build tag. Proto declarations alone do not establish usable APIs; the earlier conversational suggestion that the declared low-level operations supplied a straightforward replacement was incomplete.

A second disposable loopback probe used the same executable, the stock `crypto.dapr.localstorage/v1` component, a synthetic RSA-3072 PEM key, `RSA-OAEP-256`, AES-GCM, and a fixed synthetic 32-byte input. HTTP PUT Encrypt/Decrypt both returned 200, and the round trip matched exactly. The encrypted Dapr envelope was **689 bytes**, exceeding the existing **512-byte** wrapped-key bound. This was an HTTP diagnostic using local storage; it proves neither the production gRPC path nor Kubernetes component custody. The temporary private-key file was removed after stopping the sidecar.

Local scratch results: `/tmp/eventstore-dapr1191-7k_odgkm/surface-result.json` and `/tmp/eventstore-dapr1191-stream-cwfzdxou/streaming-result.json`. These are local observations, not CI or production evidence. The installed Dapr executable, active runtime lock, shared catalog, cluster and released artifacts were not changed.

### Input preservation

The workspace advanced concurrently from `e1980560934736688a05a53a0aadc9397277afae` to the reviewed HEAD above. The existing 8.7 verification edit and dirty Builds/Platform gitlinks remain outside this proposal's change set. Checked-out Builds is `beb85e497957fedc66854013efd2cc4bd216ecbc`; checked-out Platform is `019f593544ad157036b7d12173ff89a16464f3cc`. These are observed checkouts, not newly selected or deployed identities.

| Input | Observed SHA-256 |
| --- | --- |
| [PRD](prd.md) | `d837dd08258f0d385f164dbc9f337da22d1aeaba722fbfdd7d6be605a3c8d176` |
| [Architecture](architecture.md) | `af9dbf6b30daad508b19c3c4771a8d0a4ec43f5a5adcbd4fced7b14345932a37` |
| [Epics](epics.md) | `0da79c9446a0ac9ad7648760de955374ee589793c2b5c2751ef74e67785ce5db` |
| [UX handoff](ux.md) | `a135c9690bbd1dd157f00fdb389e7a5c8bec691e32826c5fdb12b97d07da95c1` |
| [Base security specification](../implementation-artifacts/spec-shared-payload-protection-engine.md), full file | `542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82` |
| [Approved OpenBao supplement](../implementation-artifacts/spec-8-7-on-premises-backend-amendment.md), full file | `f00108e144d946b12b599cff211c170f36806de776d669a6f6832aa2058b9aba` |
| [Current 8.7 spec](../implementation-artifacts/spec-8-7-server-persistence-and-snapshot-integration.md), dirty full file | `08871ef5a8557bbb706bfbc85c66a3483887c5072b28a0acee8290254336adcf` |
| [Current runtime lock](../../deploy/dapr/qualification/runtime-lock.json) | `326a24228313b4f76428f3091bfa449ba00c6a37fc4bdca55fb78409ab433238` |

## 2. Impact Analysis

### Epic and story impact

| Area | Required change |
| --- | --- |
| Epic 3 / Story 3.21 | Qualify the 1.19.1 runtime/CLI/control-plane family, refresh active tool/profile inputs and rerun affected broker, transport, actor and restore checks. Keep old evidence tied to its original runtime. |
| Stories 3.16–3.18 | Builds retains shared version ownership; 3.17 maps the new crypto operations; 3.18 assesses the new durable key-record reader capability and future package changes. No general dependency refresh is included. |
| Epic 8 / Stories 8.1–8.5 | Preserve completed original work and its approvals. A new supplement must describe the changed custody, backend, wrapped-key format and authority. Original proof does not qualify the new profile. |
| New Story 8.12 | Own Dapr/Kubernetes cryptography profile, versioned wrapped-key records and real component qualification before 8.7 host wiring. Numerical order does not imply execution order. |
| Story 8.6 | Retain Azure implementation, exception, history and incomplete/deferred status. It is outside this selected on-premises dependency chain. |
| Story 8.7 | Replace the OpenBao binding prerequisite with qualified 8.12 plus runtime/capacity/state/fleet prerequisites. Preserve all Server event/snapshot/read/recovery criteria. |
| Story 8.8 | Replace mandatory engine-plus-Azure publication with a separately qualified Dapr-path release of the engine package. Proposed manifest transition is 14 to 15; Azure companion remains non-packable. |
| Stories 8.9–8.11 | Bind Parties parity, post-v2 rollback and G5 to the selected Dapr/Kubernetes profile, exact historical key versions and new key-record reader capability. Retain their separate approvals. |
| Epics 1, 2, 4, 5 and 7 | No functional scope expansion. Runtime consumers need relevant regression checks. Story 4.16 requires runtime-minor re-proof; 5.6/5.7 retain topology/migration ownership; 7.6 remains the ordinary OpenBao secret-store story; 7.22 includes key-recovery compatibility. |
| Epic 6 | No snapshot or event-evolution format change is proposed. Existing payload-protection index and erasure-memory capacity prerequisites remain separate, unfinished work. |
| Epic 9 | Apply existing baseline/assurance mechanisms when accepting new normative and runtime identities. This proposal grants no gate closure or automatic digest repin. |

No epic becomes obsolete, and no new epic is required. Epic 8 remains optional, disabled by default and post-MVP. The user selected on-premises technology; no instruction rescheduled this capability into MVP.

### Artifact conflicts

- **PRD FR34 and NFR17:** Kubernetes Secrets are currently limited to bootstrap material. A narrow payload-KEK exception is necessary; choosing a Dapr component cannot silently waive those clauses.
- **PRD FR37 and NFR19:** Dapr fits the required boundary. Durable-format compatibility, buffer cleanup, typed failures, lifecycle and rollback remain required. Sidecar memory and Kubernetes backup copies need an explicit custody/limitation profile.
- **Architecture AD-23/AD-24:** Preserve the event `pdenc-v2` envelope and AAD, distinguish a Dapr envelope used only for a DEK, and document the narrow Kubernetes key-store exception. No general application-secret migration is proposed.
- **Frozen security and 8.7 documents:** The base and approved OpenBao supplement retain their bytes and approvals. Add an explicitly scoped successor supplement and new 8.12 spec; reapprove revised 8.7 intent before implementation.
- **Runtime and CI:** Active pins span multiple versions and source-bound checks. A mechanical replacement would invalidate evidence. `.github/workflows/integration.yml` and `.github/workflows/ci.yml` include retained OQ8/original-runtime subjects; changing them requires their planned reseal, never relabelling old proof.
- **UX:** No screen, flow, route, localization or accessibility change. Operational documentation continues to use bounded typed status and must not expose key paths, material or backend errors.
- **Platform:** Replace the proposed RSA/OpenBao binding handoff with Kubernetes crypto-key provisioning, RBAC, encryption-at-rest, rotation, backup/restore and rollout qualification. Platform Stories 4.15/6.7 retain ordinary-secret deployment scope; no Platform story is falsely marked complete.

## 3. Recommended Approach

**Direct adjustment**, delivered in separate runtime-refresh, cryptography-profile and Server integration steps. Retain functioning core/reader/lifecycle work. Rollback of completed stories is unjustified; it would discard tested contracts and still require new custody and compatibility decisions. MVP reduction is unnecessary because Epic 8 is already post-MVP.

### Proposed stock-Dapr integration

Use the released streaming **Encrypt/Decrypt APIs over the supported .NET/gRPC client** to encrypt and recover the EventStore-generated **32-byte DEK**. Existing event/snapshot encryption remains the approved local AES-256-GCM `pdenc-v2` implementation. Dapr Crypto Scheme v1 is confined to the opaque wrapped-DEK field of a new versioned key record. No private runtime build, provider HTTP call, application Kubernetes client, exported KEK or custom OpenBao component is selected.

This is a proposed integration design supported by the local feasibility result, not qualified production behavior. It requires these explicit decisions and proofs in 8.12:

1. Add a closed wrapped-key schema/version and algorithm identifier for Dapr-enveloped DEKs. Propose a **2,048-byte decoded wrapped-DEK ceiling**, retaining the 16 KiB whole-record ceiling and existing transaction budgets. Validate it against maximal approved key-identity lengths, all supported framing and malformed/truncated inputs before freezing the new spec. V1's 512-byte bound and literal RSA record interpretation stay unchanged. Never truncate or strip a Dapr envelope to fit V1.
2. Persist exact immutable key-version addresses, such as the operator-approved `namespace/secretName/dataKey` form. Use a distinct Secret for every KEK version. Never replace a version's key bytes or let ciphertext-supplied names select another namespace/key. Explicit decrypt options use the recorded trusted identity; qualify envelope/algorithm mismatch rejection and retained-version reads.
3. Platform publishes a closed value-free inventory/current-write-version generation; a controlled configuration/rollout changes the write version. Native Dapr does not provide the former OpenBao Profile/Lookup operation contract. Current-version authorization comes from the approved deployment/fleet generation, with exact rollback/retirement rules and no mutable `latest` alias.
4. Bind a new authority profile to the approved supplement and key-record reader capability. A process that reads event v2 but cannot read the new wrapped-key schema is ineligible for that generation. Existing roots are never silently stamped with the new authority or converted. Preserve legacy/V1 readers and explicit migration/rollback authorization.
5. Preserve reservation-before-payload, actor-owned atomic append and completion only after durable persistence. Crypto and state have no distributed transaction. Reconcile persistence from exact existing durable evidence; unknown crypto completion stays Ambiguous/operator-required unless exact-original evidence resolves it. Missing Secrets and cache misses never prove an operation absent.
6. Qualify bounded gRPC inputs/outputs, caller cancellation, all client/sidecar/component retries under one engine budget, safe structural error classification and no-leak channels. Stock generic errors may require conservative Unavailable/ConsistencyMismatch/Ambiguous outcomes; never infer deletion or exact absence from text or a generic status.
7. Accept software KEKs in Kubernetes and Dapr memory for this profile only. Platform owns least-privilege per-workload access to the exact Secrets, separate creation/rotation identity, component scopes/API authentication, encryption at rest, backup membership, restore, old-key retention and controlled rollout. Application code reads no private KEK. Application-owned DEK buffers retain existing clearing obligations; sidecar/copy limitations are recorded and tested, never described as guaranteed all-copy erasure.

If the real Kubernetes/gRPC lane cannot satisfy these contracts, 8.12 remains blocked and the design returns to review. Its failure must not select a custom runtime, direct provider fallback, different payload format or weaker lifecycle behavior.

### Effort, risk and sequence

Planning/application effort is **medium**; runtime and cryptography delivery effort is **high** because the upgrade changes a runtime minor and the wrapped-key storage contract. Security/compatibility risk is **high until qualification**, especially historical readers, key-version reuse, custody/backup copies, sidecar diagnostics and rollback. The prototype demonstrates feasibility only; no delivery date is inferred from it.

Sequence: approve these planning deltas; qualify/adopt the latest stable runtime family under 3.21/Builds ownership; draft and approve 8.12's exact supplement/spec; implement and qualify its real Kubernetes/gRPC path; complete the separate capacity work; resume 8.7; then 8.8, 8.9, 8.10 and 8.11. Runtime and capacity investigation can progress independently, but Server wiring waits for all its prerequisites. No schedule commitment or production rollout follows from proposal approval.

## 4. Detailed Change Proposals

The following P1–P7 text is the approved review candidate. The application record below identifies the applied planning changes; future runtime/spec/qualification work retains its stated gates.

### P1 — PRD: narrow Kubernetes payload-key exception

**File:** [prd.md](prd.md), FR34/NFR17 and §6.9.

**OLD (FR34 excerpt):** `restrict Kubernetes Secrets to documented bootstrap credentials only when no approved mounted or projected credential mechanism is available`.

**NEW:** `restrict Kubernetes Secrets to documented bootstrap credentials when no approved mounted or projected credential mechanism is available, plus the separately approved FR37 Dapr/Kubernetes payload-KEK profile; the latter permits only operator-owned versioned crypto-key Secrets consumed by the scoped Dapr cryptography component and grants no application-secret retrieval exception`.

**OLD (NFR17):** `OpenBao bootstrap credentials are platform inputs and may use Kubernetes Secrets only when no approved mounted or projected mechanism is available.`

**NEW:** Retain that sentence and append: `A separately approved FR37 payload-protection profile may use dedicated, versioned Kubernetes Secrets through crypto.dapr.kubernetes.secrets/v1 for software KEK custody. Application code accesses cryptographic operations through Dapr and never retrieves those private keys. This exception does not move ordinary operational/application secrets, tokens or unrelated internal-proof keys out of OpenBao.`

**ADD under §6.9:** `The selected on-premises target is Dapr Cryptography with Kubernetes-managed software KEKs. Qualify the exact runtime/component and a versioned wrapped-DEK schema while preserving pdenc-v2 event/snapshot bytes, AAD, historical readers, lifecycle and rollback. Dapr memory, Kubernetes/etcd backups and recovery copies are explicit custody boundaries; deployment and all-copy erasure require separate evidence.`

**Rationale:** Make the selected custody change explicit without weakening ordinary secret handling or MVP security. FR/NFR IDs, NFR19's application-buffer requirements and post-MVP scope remain stable. Add 8.12 as supporting FR37/NFR19 coverage in §11 and record the successor gate in §12; do not close readiness.

### P2 — Architecture: component boundary and custody

**File:** [architecture.md](architecture.md), AD-23, AD-24, implementation-status row and existing topology.

**OLD (AD-23):** `Dapr Crypto Scheme v1 never replaces pdenc-v2 or its AAD bytes, and a Dapr secret store is not a cryptographic key-operation component.`

**NEW:** Retain that sentence and append: `The proposed on-premises successor uses crypto.dapr.kubernetes.secrets/v1 and supported streaming Encrypt/Decrypt only for DEKs in a newly versioned key record. Activation requires the separately approved custody/schema/authority supplement and real Kubernetes/gRPC qualification. Event/snapshot pdenc-v2 and AAD bytes remain unchanged; the existing V1 wrapped-key contract is not reinterpreted.`

**OLD (AD-24):** `Kubernetes Secrets may contain only documented bootstrap material when no approved mounted or projected mechanism exists; they are not an application-secret backend.`

**NEW:** `Kubernetes Secrets may contain documented bootstrap material when no approved mounted or projected mechanism exists, plus dedicated payload KEKs under the separately approved AD-23 Dapr/Kubernetes profile. They are not a general application-secret backend. Platform owns key provisioning, least-privilege grants, at-rest protection, distinct version addresses, retirement, backups and restore; Dapr sidecars hold private material for this software-key profile.`

**Status-row change:** Replace the stale production-key-operations statement that no provider is selected with the truthful split: Azure 8.6 retained/deferred under its existing exception; OpenBao binding first phase retained; Dapr/Kubernetes selected as the proposed on-premises successor, pending 8.12 approval/conformance.

**Diagram change:** Existing payload engine → Dapr edge gains `Encrypt/Decrypt DEK`; add Dapr → Kubernetes versioned KEK Secrets and Platform → key provisioning/rotation ownership. Preserve event-store/actor transaction edges; add no custody server.

**Rationale:** Dapr owns provider operations and private-key access; EventStore retains event format and lifecycle. Update the existing architecture companion change log. Story 9.3 owns any approved architecture-baseline propagation; never refresh stale digests solely to silence a guard. Runtime changes still consume AD-26's existing subject/ratification and runtime-minor re-proof rules.

### P3 — Story 3.21: latest stable Dapr refresh

**File:** [epics.md](epics.md), Story 3.21 runtime inventory AC.

**OLD comparison:** `Dapr 1.18.7, CLI 1.18.2, components-contrib 1.18.6`.

**NEW target:** `Dapr runtime/control-plane 1.19.1, independently released CLI 1.19.1 and embedded components-contrib 1.19.0. Retain Dapr .NET SDK 1.19.0 while latest stable NuGet evidence agrees. Keep the previous profile as historical comparison, not current-version conformance.`

**ADD acceptance criteria:**

- **Given** these release targets, **when** active locks/tool declarations/images/charts/fixtures are refreshed, **then** exact source commits, release checksums, Linux amd64/arm64 images and chart identities match the downloaded artifacts, all active owners are inventoried, and prior-run identities are retained with their original evidence.
- **Given** a runtime minor upgrade, **when** acceptance runs, **then** affected broker, routing/app-token/access-control, actor persistence/reminder, scheduler/placement, snapshot/restore and compatibility lanes execute on the new subject; a pin change or old green result alone passes none of them.
- **Given** sealed OQ8 and original-runtime lanes, **when** updating current CI, **then** their historical subjects stay intact until the exact planned reseal; a new qualification lane may run the latest runtime independently and does not establish sealed-lane equivalence.

**Owning paths:** `deploy/dapr/qualification/runtime-lock.json`, its exact public-identity inputs, `tools/dapr_broker_qualification.py`, its validators/tests, `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogTransport*.cs`, active non-sealed workflows, current runtime documentation and corresponding fixtures. Shared Builds tool pins are changed only from the Builds repository; existing latest SDK pins need no edit. Preserve the current working-tree gitlinks. Do not mutate Platform's cluster as part of a repository dependency update.

### P4 — New Story 8.12: Dapr Kubernetes Cryptography And Wrapped-Key Conformance

**Placement:** Add after 8.11, with an explicit prerequisite edge **8.12 → 8.7**. It is a new story, not a renaming of Azure 8.6 or closure of 8.7.

**Proposed story text:**

> As an EventStore maintainer, I want a qualified stock-Dapr Kubernetes cryptography backend and versioned wrapped-key storage, so that on-premises payload protection uses the built-in component with exact historical reads and lifecycle safety.
>
> Requirements: supporting FR37, NFR12, NFR16, NFR17 and NFR19. Dependencies: compatible 8.1–8.5 evidence; accepted P1/P2 custody policy; an approved exact successor supplement; and the qualified latest-runtime input. Platform owns provisioning/target rehearsal; EventStore owns semantics and tests. Capacity remains a separate 8.7 prerequisite.
>
> Given the frozen base, when the successor is specified, then its exact key-record schema, wrapper bound/algorithm, logical key inventory, failure mapping, options, authority/generation transition and downgrade rules are approved without rewriting predecessor approvals.
>
> Given crypto.dapr.kubernetes.secrets/v1 and the approved source graph, when production gRPC encrypt/decrypt protects a fresh 32-byte DEK, then exact versioned key selection, bounded complete output and restart/rotation historical recovery pass; unsupported or malformed output produces no plaintext/fallback.
>
> Given V1 records, new key records and mixed event history, when readers, reservation/completion, rewrap, erasure and restore run, then old records retain their exact interpretation, new records require the new capability, and persisted key/lifecycle/fence/index end-state proves each transition. New-schema roots reject older readers/writers.
>
> Given cancellation, outage, missing/denied keys, corruption, loss of response or authority change, when a real operation fails, then bounded typed outcomes, current authorization, ownership/clearing and monotonic state remain correct; uncertainty is retained without fabricated absence or deletion.
>
> Given real Kubernetes qualification, when app/component scopes, workload RBAC, sidecar/API authentication, immutable version discipline, key rotation, old-key retention, backup/restore and diagnostics are inspected, then the selected component/profile and remaining custody limitations are proven or explicitly blocked; localstorage/mocks cannot substitute.
>
> Given completion is requested, when affected unit/golden/compatibility and real-Dapr/Kubernetes tests execute, then every selected case passes with zero live skips, evidence is retained in the owning spec plus CI, and only the bounded 8.7 handoff can be authorized. No release, deployment, Azure completion or G5 follows.

**Planned files:** `../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md` and `../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md`; implementation after those approvals extends the existing core under `src/Hexalith.EventStore.PayloadProtection/`, its strict state codecs/coordinators, `tests/Hexalith.EventStore.PayloadProtection.Tests/` and `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`. Use one type per C# file. No third adapter package or new evidence subsystem.

**Tracker addition after approval:** `8-12-dapr-kubernetes-cryptography-and-wrapped-key-conformance: backlog`. Keep 8.1–8.5 at their existing statuses and 8.6/8.7 in progress. Do not invent a new approval digest before exact normative bytes exist.

### P5 — Story 8.7: change prerequisite and revise intent through a successor

**Files:** Story 8.7 in [epics.md](epics.md) and the [owning spec](../implementation-artifacts/spec-8-7-server-persistence-and-snapshot-integration.md).

**OLD dependency:** `a separately scoped Platform RSA/OpenBao component, real OpenBao + Dapr conformance`.

**NEW dependency:** `completed Story 8.12 with the approved Dapr/Kubernetes software-key and wrapped-record profile, real Kubernetes/gRPC conformance, the accepted latest-runtime input, completed index-capacity/erasure-memory work, and current shared-state/fleet/fence/restore admission`.

**OLD approach:** `Qualify a Platform-owned OpenBao software-key backend, then connect shared-engine integration through existing actor hooks.`

**NEW proposed intent for reapproval:** `Consume the qualified Dapr Kubernetes cryptography backend and versioned key records from Story 8.12, then connect shared-engine integration through existing actor hooks. Preserve actor-owned atomic staging, trusted occurrence identity, durable reservation/completion, every typed read route and the capacity/state/fleet gates.`

**Application rule:** Retain the current frozen block/digest and original baseline as history. Record the owner-approved supersession and bind a separately identified revised intent/supplement before implementation. Never update the old digest field to make changed bytes appear previously approved. Retain first-phase Contracts/binding code and tests; make no automatic removal of public DTOs or existing interaction-specific authority.

Keep every event, snapshot, every-read-path, lifecycle, authorization, fault, rollback and no-op matrix row. Add new-key-record/mixed-reader capability checks to startup and rollback. Remove only the custom OpenBao payload-binding prerequisite for this selected successor path.

### P6 — Stories 8.8–8.11: coherent release and successor evidence

| Story / section | OLD | NEW and rationale |
| --- | --- | --- |
| 8.8 intent/package-set AC | Engine and Azure adapter always released together; 14 → 16 | Selected Dapr path releases the engine using its existing Dapr dependency; 14 → 15 only under 8.8's exact approved release spec. Azure companion stays non-packable and deferred. No third Dapr adapter package. |
| 8.8 package-only proof | Mandatory real packaged Azure path | Real packaged Dapr/Kubernetes path plus preserved engine-only omitted/Disabled behavior and API/dependency/inventory/SBOM/provenance checks. Future Azure publication requires separate conformance and release approval. |
| 8.9 backend identity | Azure is the shared production backend | Selected qualified Dapr/Kubernetes profile; dual-provider parity still means retained Parties legacy provider versus the shared engine. Preserve separate Parties owner authority and legal/erasure/report ownership. |
| 8.10 rollback profile | Retained Azure backend/identity | Retain Dapr component, exact historical key Secrets and a last-known-good reader that supports every written key-record schema. Event V2 support alone is insufficient. No reader/key removal or history rewrite. |
| 8.11 evidence/custody | Azure-specific production evidence denominator | Selected Dapr/Kubernetes runtime/component/Secrets/backup/sidecar custody denominator, real persisted Server and packaged consumer evidence, original/new goldens, rollback and all existing role/assurance decisions. No all-copy-erasure or legal-compliance inference. |

At this planning stage both future projects remain non-packable and `tools/release-packages.json` remains at 14. The 14 → 15 decision is a proposed 8.8 scope correction, not an instruction to publish or edit the inventory now. Epic 8 track 8C becomes qualified selected-backend integration (8.12 before 8.7; Azure 8.6 deferred); track 8D names the selected approved package set instead of mandatory Azure pair publication.

### P7 — Platform, operations, documentation and unchanged UX

**OLD Platform handoff:** Extend `Hexalith.Platform.Custody.Dapr` with the custom RSA/OpenBao `payload-key-v1` operations.

**NEW handoff:** Platform creates a separately owned task for the stock Kubernetes cryptography component, dedicated versioned KEK Secrets, least-privilege runtime/provisioner identities, custody/backup/restore, rotation/retirement and the shared Kubernetes rehearsal. Choose exact target identities and resource definitions in that owning repository. This proposal does not create or execute that Platform task.

Keep ordinary OpenBao secret retrieval and Platform 4.15/6.7 scope. Preserve default no-op behavior, deferred-work history and both pending capacity issues. Under 8.12/8.8 delivery, update payload-protection/deployment docs and the 3.17 operation inventory to distinguish localstorage test fixtures, sidecar-held Kubernetes KEKs, retained Azure exception and actual qualified states. UX files remain unchanged; no new success or protection label is inferred from component installation.

## 5. Implementation Handoff

**Classification: Moderate backlog reorganization with a required security/architecture amendment.** The product goal and event format remain stable. The custody and durable key-record changes require Architect/Security participation before the Developer handoff; this is not a mechanical backend swap.

| Recipient | Concrete responsibility |
| --- | --- |
| Product owner / Developer, after proposal approval | Apply P1–P7's planning edits; add 8.12 backlog entry and dependency edges; preserve statuses, original approvals, worktree changes and submodule boundaries. |
| Architect / Security / EventStore owner | Draft and approve the exact 8.12 normative supplement and 8.7 successor intent, including key schema/bounds, custody limitations, authority, mixed-reader and failure semantics. No earlier profile digest is reused. |
| Builds maintainer and 3.21 Developer | Refresh the active runtime/CLI family to 1.19.1, retain current latest SDK 1.19.0, verify images/charts/checksums, qualify affected consumers, and route any sealed-lane change through its owned reseal. No unrelated catalog or submodule refresh. |
| Platform owner | Own the scoped Kubernetes key-provisioning/custody/recovery task and target rehearsal; retain ordinary OpenBao services. Infrastructure mutations require their own exact bounded operation authority. |
| 8.12 Developer / Test owner | Implement only from the approved supplement/spec; prove actual Kubernetes component plus gRPC behavior and persisted key/lifecycle/fault/rotation/restore end-state with zero selected live skips. |
| Capacity owner / 8.7 Developer | Complete index/erasure-memory prerequisites under their existing authority, then finish Server's full event/snapshot/read/recovery/mode matrix. |
| Release / Parties / G5 owners | Reapprove the revised 8.8 package set and 8.9–8.11 subjects; preserve separate release, consumer, deployment and final-availability decisions. |

### Success criteria

1. Current planning consistently selects stock Dapr Cryptography with Kubernetes Secrets for the on-premises payload profile and latest stable runtime/CLI 1.19.1; SDK 1.19.0 remains correctly distinguished.
2. No implementation depends on unimplemented subtle APIs or silently selects a custom runtime. Production uses the qualified gRPC API, and localstorage/HTTP feasibility remains labelled diagnostic.
3. Existing event/snapshot envelope/AAD and goldens remain byte-identical; V1 wrapped records retain their schema/bounds. New records have approved independent version/bounds/authority and demonstrated mixed-history/rollback behavior.
4. No successful Dapr call bypasses lifecycle, tenant, reservation, actor transaction, capacity, current authorization or restore gates. Old readers cannot join a generation containing unsupported key records.
5. Kubernetes custody and the narrow PRD/AD exception are explicit. Application code receives no private KEK, provider endpoint or Kubernetes client; memory/backup limitations are disclosed accurately.
6. The runtime upgrade has newly bound checksums/source/image/chart identities and relevant regression evidence. Historical/sealed evidence retains its original subject until authorized reseal.
7. 8.7 stays in progress until all prerequisites and persisted Server tests pass; current 14-package inventory, disabled defaults, G5 and production/readiness status remain unchanged during planning.

### Checklist execution record

| Checklist item | Status | Disposition |
| --- | --- | --- |
| 1.1 Trigger story | [x] | 8.7 blocked on custom OpenBao payload-key conformance |
| 1.2 Core problem | [x] | Owner-selected technology/custody change and latest-stable update |
| 1.3 Evidence | [x] | Repository source, release/NuGet evidence and two native 1.19.1 probes above |
| 2.1 Current epic | [x] | Epic 8 goal retained; backend/key-record/release prerequisites change |
| 2.2 Epic changes | [x] | New 8.12; extend 3.21; revise 8.7–8.11 |
| 2.3 Remaining epics | [x] | Runtime consumers and existing ownership reviewed in impact table |
| 2.4 Obsolete/new epics | [x] | None |
| 2.5 Ordering | [x] | Latest runtime and approved 8.12 plus capacity precede 8.7 |
| 3.1 PRD | [x] | P1 explicit scoped policy exception, stable IDs/MVP scope |
| 3.2 Architecture | [x] | P2 changed custody/key records, retained event format and owners |
| 3.3 UX | [N/A] | No screen/flow/component changes |
| 3.4 Other artifacts | [x] | CI, locks, package set, Platform, docs and tests covered |
| 4.1 Direct adjustment | [x] | Selected; medium planning/high delivery effort |
| 4.2 Rollback | [N/A] | Discarding completed core/legacy work offers no benefit |
| 4.3 MVP review | [N/A] | Optional post-MVP scope retained |
| 4.4 Recommendation | [x] | Stock Encrypt/Decrypt for DEKs, new wrapped-record profile |
| 5.1–5.5 Proposal | [x] | Sections 1–5 include concrete old/new edits and owners |
| 6.1–6.2 Completion/accuracy | [x] | All analysis items disposed; technical delivery gates remain explicit |
| 6.3 User approval | [x] | Owner “continue and approve” accepted all P1–P7 on 2026-11-08 at the recorded pre-application digest |
| 6.4 Planning/tracker application | [x] | P1–P7 planning deltas applied; 8.12 added backlog and existing statuses preserved |
| 6.5 Handoff confirmation | [x] | Written PO/DEV/Architect/Builds/Platform/Test handoff retained in §5 and story dependencies; no external messages or runtime implementation dispatched |

### Review checkpoint and authority

The interactive project owner answered **“continue and approve”** on 2026-11-08, accepting all P1–P7 at pre-application SHA-256 `4b618adbca7c6802e41b6da40dac6b8d085ab81875921d6bb1293d9ec3dccd68`. Both the review continuation and final proposal approval are satisfied. Planning application is authorized and recorded below. This is a conversational owner decision, not an authenticated per-role production record; future unseen normative/schema bytes, runtime conformance, cluster mutations and release/G5 retain their stated separate authority.

### Primary sources

- [Latest runtime release 1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1), [CLI release 1.19.1](https://github.com/dapr/cli/releases/tag/v1.19.1) and [.NET SDK release 1.19.0](https://github.com/dapr/dotnet-sdk/releases/tag/v1.19.0).
- [1.19.1 default subtle API stubs](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/pkg/api/universal/subtlecrypto.go), [public API schema](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/dapr/proto/runtime/v1/crypto.proto), and [runtime embedded dependency versions](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/go.mod).
- [Kubernetes crypto component documentation](https://docs.dapr.io/reference/components-reference/supported-cryptography/kubernetes-secrets/) and [pinned component key retrieval](https://github.com/dapr/components-contrib/blob/1d331c389f11baaa1d664022b6b6c2f59811ce11/crypto/kubernetes/secrets/component.go).
- [Dapr cryptography overview](https://docs.dapr.io/developing-applications/building-blocks/cryptography/cryptography-overview/) and [usage guidance](https://docs.dapr.io/developing-applications/building-blocks/cryptography/howto-cryptography/): native Dapr envelope and production gRPC recommendation.
- [Prior exact-runtime qualification](../implementation-artifacts/story-8-6-dapr-qualification.md), [approved Azure exception](../implementation-artifacts/spec-8-6-crypto-boundary-amendment.md), [approved OpenBao supplement](../implementation-artifacts/spec-8-7-on-premises-backend-amendment.md), and [8.7 progress/gates](../implementation-artifacts/spec-8-7-server-persistence-and-snapshot-integration.md).

### Planning application record — 2026-11-08

Applied P1–P7 at EventStore HEAD `b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce` with the existing dirty 8.7 verification and Builds/Platform pointers preserved. The accepted pre-application proposal digest remains above; its approval does not transfer to unseen normative schema/authority bytes.

- PRD: scoped FR34/NFR17 Kubernetes payload-KEK exception, FR37 direction/supporting 8.12 coverage and post-MVP OR31; all existing readiness/production failures remain.
- Architecture: AD-23/24 custody/DEK direction, selected-path AD-11 package plan, gated topology and production-key status. The defined AD-26 subject remains byte-identical at SHA-256 `b9e899ecd6bb456537358b23cb69a564e09bc2c7b199dd657075900b2d17503b`; the companion memlog records the impact decision. No baseline digest is refreshed.
- Epics/tracker: 3.21 latest-stable target; 8.12 added as backlog before 8.7 in dependency order; 8.7–8.11 use the selected backend/release/rollback chain. Existing lifecycle values are unchanged. The existing all-stories completion rule keeps Epic 8 open while retained Azure 8.6 is in progress, even if the selected on-premises G5 path later passes; Azure completion is never inferred.
- 8.7/deferred work: new separately identified approved planning intent and precedence record, preserving the old frozen block, baseline, supplements and all prior evidence. Backend qualification, capacity and host gates remain open.
- Handoff: next runtime work belongs to 3.21/Builds; next payload work is 8.12's exact spec and normative supplement, followed by real Kubernetes/gRPC qualification. Platform owns its separately scoped key-provisioning/custody/rehearsal task. No external message was sent.

Runtime/CLI installations, active locks, dependency catalogs, C# code, workflows/sealed inputs, release inventory, submodules and cluster resources were not modified. Both future projects remain non-packable; the manifest remains 14. Planning verification passed on 2026-11-08:

```bash
python3 /tmp/validate-dapr-course-planning.py
git diff --check
```

The one-off integrity checker returned exit 0 for all seven groups: eight-document UTF-8/LF/whitespace/frontmatter and added-link checks; unique tracker keys with only 8.12 added backlog and every prior lifecycle preserved; story/dependency/15-package successor/rollback consistency; scoped policy and post-MVP/readiness/AD-26-subject preservation; frozen 8.7 intent/approval/baseline, prior verification and append-only deferred history; exact approved proposal subject plus immutable base/supplement/UX/active-lock/sealed-input/current-inventory preservation; and changed-path boundaries. `git diff --check` returned exit 0. This temporary checker is a local planning check, not a new repository test framework, CI receipt or runtime conformance result. No C# implementation changed and no .NET, cluster or live-provider suite ran during this planning application.

The correct-course workflow is complete for approved planning application and written handoff. Runtime upgrade, the exact 8.12 normative design, real backend qualification, capacity completion and Server integration remain with their assigned stories; they are not marked delivered by this approval record.
