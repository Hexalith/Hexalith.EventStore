# Story 4.16: Feasibility Before Append-Envelope Migration

Date: 2026-11-01

Disposition: User-approved recommendation applied to planning and bounded preparation. Final G-APPEND conformance remains blocked.

## 1. Issue Summary

Story 4.16 needs a mechanically enforced exclusion of second writers. Its first draft named sidecar/network policy files before establishing that such policies could distinguish the legitimate actor writer from a raw actor-state transaction using the same reachable sidecar.

The owner requested a pragmatic comparison, then instructed “apply recommendations.” This approves the bounded reuse check, disposable feasibility experiment and prerequisite ownership correction described below. It does not ratify AD-26, select production inputs, approve fencing implementation or authorize production migration. Batch handling uses the already-discussed recommendation; no repeat intent interview or approval of the same preparatory actions is required.

The tracked PostgreSQL component still admits EventStore and Admin. The root-declared Platform checkout also supplies shared Redis components, not a ratified isolated AD-26 profile. Existing race synchronization, actor tests and Dapr 1.18.7 inputs are reusable. No matching completed production isolation/profile delivery was found in this bounded check.

## 2. Impact Analysis

| Area | Applied correction / retained responsibility |
| --- | --- |
| Epic 4 / 4.16 | Separate candidate feasibility from final conformance; reject a mechanism that also disables the lawful writer. Remove predetermined policy-file tasks. |
| Epic 5 / 5.6 | Own local per-app bindings, shared-reader/data migration and SDK/host default changes; hand off composition to Platform. |
| Epic 5 / 5.7 | Own production physical-target/principal isolation, Platform migration/rollback validation and fresh OQ8 regression on the changed profile. |
| Epic 5 / 5.8 | Compare physical targets, value-free principal references and reader bindings as part of existing drift ownership. |
| Epic 3 / 3.17 | Explicit actor target/writer/reader provenance in the existing inventory contract. 3.18 retains compatibility classification; 3.19 retains the sole canonical production profile. |
| Epic 7 / 7.22 | Restore inventory includes all per-app actor and separately scoped operational targets, retaining coupled state/scheduler invariants. 7.6 retains OpenBao/secret custody. |
| Other epics | No new epic or reopened domain/UI work. Preserve authorized erasure, actor ownership and post-MVP payload scope. |
| PRD and UX | No FR/NFR, SM11, MVP safety or UX change. G-APPEND and all existing production/assurance gates remain unmet. |
| Architecture | Gate rows assign existing requirements and clarify sequencing. AD-5 append and AD-26 normative bytes are unchanged; no approval digest is repinned. |

Shared composition, provisioning, production rehearsal and later deployment remain Platform-owned. This change sends no external message and edits no submodule. Stories 5.9 and 7.8 consume the eventual component mapping in their existing runbook/resiliency work; no new framework is needed.

## 3. Recommended Approach And Observed Result

**Direct adjustment, moderate scope.** Planning edits and reuse checking are small. Reader/data migration and production qualification remain substantive, conditional work; there is no defensible calendar estimate until a viable exclusion mechanism is established. Rollback of completed features and MVP reduction would not satisfy the existing no-loss requirement.

Sequence:

1. Check reusable authority/configuration/work once; retain the existing tests and candidate runtime.
2. Exercise the simplest barrier on a disposable candidate, with a lawful append/readback control.
3. If feasible, complete the separately owned isolation/migration prerequisites and ratify one credible profile before final conformance.
4. If not feasible, stop that mechanism. Request a bounded alternative-mechanism or storage-protection scope proposal; do not accumulate custom proxies, policy engines or control services.

**Executed result:** The coarse actor API-group policy is rejected. With that group allowed, lawful append/readback succeeds but the raw durable contender is overwritten. Removing the group prevents fixture actor warm-up, so it also breaks the lawful path. This does not prove every possible envelope impossible; it prevents treating this simple configuration as the solution. Broader migration and final conformance stop pending the next mechanism/scope decision. Fencing implementation remains unauthorized.

## 4. Detailed Change Proposals Applied

| Artifact / section | Before | After / rationale |
| --- | --- | --- |
| 4.16 draft approach | Enforce the envelope on a ratified profile; predetermined sidecar/network YAML tasks. | One reuse check and disposable feasibility first; choose configuration files only for a demonstrated mechanism. Avoid designing migration around an unsupported barrier. |
| 4.16 dependencies / ACs | Ratification listed as a prerequisite to the story; raw-writer and lawful-control distinction implicit. | Ratification gates final proof. Candidate preparation is allowed earlier; test every applicable writer class, reject failed lawful controls and retain Dapr readback for final acceptance. |
| 5.6 first topology AC | Resolve `statestore`, `pubsub`, ACL and resiliency. | Resolve each app's actor component and separate operational/read-model components, including unique target/principal bindings. Add explicit reader/default migration ownership and 3.18 compatibility handoff. |
| 5.7 state-component AC | Separately scoped actor targets and operational readers required, delivery details implicit. | Bind app/namespace/environment, physical target and principal; prove foreign-principal and cross-environment denial. Platform owns provisioning, 7.6 secret custody. Add migration/rollback and fresh OQ8 proof, preserving historical observations. |
| 5.8 typed mapping | Component names/types/scopes and runtime metadata. | Also compare physical targets, value-free principal references and reader bindings. |
| 7.22 first restore AC | “actor state in `statestore`.” | Every per-app actor target and separately scoped operational/read-model target. Preserve admission/fence/global-position/scheduler coupling. |
| 3.17 inventory | General infrastructure provenance. | Explicit writer, target, principal-reference, reader/default and hosting-app raw-interface provenance for the existing 4.16/5.6/5.7 handoff. |
| Architecture gate rows | “No owning story” for isolation; ratification unblocks all 4.16 work. | Assign 5.6/5.7 and distinguish disposable preparation from final proof. Add a memlog entry confirming no normative AD-26 change. |
| Sprint tracking | 4.16 backlog, no preparatory result. | Keep every lifecycle value unchanged; add a comment linking the rejected coarse mechanism and remaining prerequisites. |

No new committed evidence packet, manifest, validator, fixture or workflow is introduced. The existing evidence-storage simplification supersedes deleted Story 9.1 machinery. Future final conformance and Story 9.2 assurance remain required.

## 5. Implementation Handoff And Verification

**PO / Architect:** Keep the existing no-loss requirement. The next scope proposal must name a demonstrable exclusion or request atomic conflicting-write protection; it must define the boundary, persistence seam, compatibility and acceptance before implementation. The present failed mechanism grants no fencing scope.

**Developer:** Existing tests supply the feasibility result. Do not implement the withdrawn YAML assumption or begin reader migration until the mechanism and execution scope are accepted. Then consume 3.17/3.21, implement 5.6/5.7 and run 5.8/3.18/7.22 obligations before final 4.16 proof.

**Platform / Security / Test:** Own composition and least-privilege provisioning, ratification/secret boundaries and independent scrutiny of final Dapr-path/failover evidence. Passing candidate observations confer no production or G-HIGH-RISK authority. No messages or external handoffs were sent.

### Executed commands

Tested source: `2a1d16d76758c687506c9f375354303430262e7c`. The subsequent `f704401ff9033919b1e9925564d814d7410bfe3d` changed only `DomainModuleAuthoringGuardrailTests.cs`, outside the built/tested graph.

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
```

Result: PASS, zero warnings and errors. The remaining commands invoke the resulting assembly from the repository root:

```bash
EVENTSTORE_TEST_DAPRD_PATH=/home/administrator/.dapr/bin/daprd EVENTSTORE_TEST_DAPR_HOT_RELOAD=false HEXALITH_STORY_4_5_EVIDENCE_DIR=/tmp/story-4-16-feasibility-baseline dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome'
```

Result: 1 passed, 0 failed/skipped. This observational test reported `same-key-overwrite-raw-durable-write-lost`; a green test records loss and does not prove prevention.

An owned scratch wrapper forwards the fixture's original arguments to the same daprd, adding only `--config` for its private policy; `--version` passes through unchanged. The policy has `HotReload: false` and explicit HTTP `v1.0` / gRPC `v1` entries for `state`, `actors`, `invoke`, `publish`, `bindings`, `secrets`, `metadata`, `configuration`, `healthz` and `shutdown` (healthz HTTP only): 19 entries. The negative configuration removes only both `actors` entries, leaving 17. Test mutation and HotReload override variables are unset for both policy runs; configuration is private to these launched sidecars.

```bash
EVENTSTORE_TEST_DAPRD_PATH=/tmp/story-4-16-policy-mmjvtury/actors-allowed/daprd-wrapper HEXALITH_STORY_4_5_EVIDENCE_DIR=/tmp/story-4-16-policy-mmjvtury/actors-allowed/capture dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*AggregateActorIntegrationTests.ProcessCommandAsync_PlumbsAggregateTypeThroughActorPipeline' -method '*AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome' -xml /tmp/story-4-16-policy-mmjvtury/actors-allowed/result.xml

EVENTSTORE_TEST_DAPRD_PATH=/tmp/story-4-16-policy-mmjvtury/actors-removed/daprd-wrapper HEXALITH_STORY_4_5_EVIDENCE_DIR=/tmp/story-4-16-policy-mmjvtury/actors-removed/capture dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*AggregateActorIntegrationTests.ProcessCommandAsync_PlumbsAggregateTypeThroughActorPipeline' -xml /tmp/story-4-16-policy-mmjvtury/actors-removed/result.xml
```

Results:

- Actors allowed: exit 0; 2 passed, 0 failed/skipped. Lawful command plus `GetEventsAsync(0)` readback passes, while the race still reports durable raw-write loss with all eight harness invariants true.
- Actors removed: exit 1; 0 passed, 1 failed, 0 skipped. Fixture actor warm-up times out after 45 seconds with `failed getting app id either from the URL path or the header dapr-app-id`. This is the observed rejection of the coarse mechanism, not a successful conformance negative. No HTTP status was captured.
- `-xml` was accepted with a deprecation notice; future runs should use `-result-xml`.

Runtime: daprd `1.18.7`, SHA-256 `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`, matching the existing candidate lock; CLI `1.18.2`. The existing localhost Redis/placement/scheduler were reused with fixture-owned random app/type identities and ports. The pre-existing five container identities remained running and unchanged; only fixture-owned hosts/sidecars were disposed.

Limitations: Redis is a disposable supporting profile; race readback uses physical Redis diagnostics, while the separate lawful control reads through the actor API. No PostgreSQL least-privilege, Kubernetes/localhost network isolation, placement partition/failover, candidate token-equivalent denial or ratified-profile production proof is claimed. CI run/link: none; final acceptance requires production-path tests and CI. Raw run output remains local scratch, outside tracked documentation.

Planning validation: `git diff --check` and focused structural checks pass. The story draft is 1,574 tokens; links and unique story identities resolve, sprint lifecycle values are unchanged, and the AD-26 ratification subject remains byte-identical at `2ed96f02278d2562a6777a4f33e76cd1bb7eae46fac1d7a072094f6eaa298d90`.

### Change-navigation checklist

| Items | Status | Disposition |
| --- | --- | --- |
| 1.1–1.3 | [x] | Trigger, core problem and executed evidence recorded above. |
| 2.1–2.3, 2.5 | [x] | Existing epic goal retained; prerequisite ownership/sequence corrected. |
| 2.4 | [N/A] | No new or obsolete epic. |
| 3.1–3.2, 3.4 | [x] | PRD goal unchanged; architecture, story, test and deployment impacts mapped. |
| 3.3 | [N/A] | No UX change. |
| 4.1, 4.4 | [x] | Direct adjustment selected; bounded preparation executed. |
| 4.2–4.3 | [N/A] | No rollback or reduced safety/MVP goal. |
| 5.1–5.5, 6.1–6.2 | [x] | Proposal, explicit edits, responsibilities and verification recorded. |
| 6.3 | [x] | User's “apply recommendations” approves these discussed preparatory/planning actions only. |
| 6.4 | [x] | Sprint comment updated; lifecycle values unchanged. |
| 6.5 | [!] | Next mechanism/fencing scope decision, prerequisite implementation and final ratification/proof remain pending. No external handoff sent. |
