---
title: 'EXT-PROTECTION-1 option A erasure feasibility'
type: 'feature'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
baseline_commit: '39b366bca3dbe508b4cf3c5afa69db1ddadb6dd2'
review_loop_iteration: 0
context:
  - /home/administrator/projects/hexalith/platform/docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md
  - /home/administrator/projects/hexalith/platform/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
---

<frozen-after-approval reason="user approved the preceding recommendation with apply recommended">

## Intent

**Problem:** G1 lacks a production key hierarchy with atomic manifest destruction and backup non-revival. OpenBao transactions alone cannot establish erasure.

**Approach:** Apply the approved recommendation: investigate option A through an erasure-first bounded feasibility probe. Use a disposable local instance of the discovered OpenBao version with fresh test keys. If ordinary Raft restore revives a destroyed key and no independently surviving erasure primitive exists, record that failure and stop production engine construction before the transaction proof. This is a negative control, not a provider or qualification.

## Boundaries & Constraints

**Always:** Preserve unrelated Platform changes. Own engineering probe code in Platform. Bind exact image, version, source, input and public result hashes. Use fresh ephemeral test keys; retain no tokens, unseal keys, ciphertext, plaintext or Raft snapshots. Bind listener to local-only isolation and use no production credentials. Capture commands and failures. Preserve public evidence of what was actually observed. Verify the retained control key throughout to distinguish restoration from a service outage.

**Never:** Touch the production OpenBao runtime, copy its data, deploy, edit the Agents register, run Live or cohorts, enable consumers, change dependency status, claim the future engine was tested, select option B without a backend, fabricate a canonical complete target, stage/commit/push or update dependencies. No success result may qualify G1. Do not create a general plugin framework or distributed commit protocol.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Ordinary recovery | Two native transit keys, pre-delete Raft snapshot | Deleted key decrypts before deletion, fails after deletion, decrypts after restore; control stays usable | Report observed non-revival failure and stop option A production work |
| Incomplete observation | Startup, API, snapshot, delete, restore or decrypt cannot be verified | Unknown/blocked, no erasure conclusion | Nonzero exit; retain sanitized stage/status only |
| Version mismatch | Actual native version differs from pinned discovery | No version-bound inference | Refuse before creating test keys |
| Safety and cleanup | New local container and private ephemeral storage | Only owned resources removed; no secret-bearing output or artifact | Cleanup failures reported and prevent a completed result |

</frozen-after-approval>

## Code Map

- Platform `eng/ext-secrets-1/README.md`: existing engineering verification conventions and explicit all-copy gap.
- Platform `eng/ext-secrets-1/install_isolated_registry.py`: production kubeconfig reference; do not call or import its installer for this probe.
- Platform `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleProvider.cs`, `CustodyKeyWrapCore.cs`: interface and wrap primitive only; no production implementation to bind.
- Platform `docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md`: option A/B and exact G1 criteria; append application disposition without rewriting historical evidence.
- EventStore `docs/implementation/ext-protection-1-owner-acceptance.md`: append exact source/test/evidence mappings and remaining blockers.
- Read-only Agents dependency register and 2026-10-18 reconciliation: inputs pinned in baseline.json; no acceptance change.
- Discovery pins OpenBao `quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641`. Docker is local unix socket; do not use cached 2.6.0 as exact-version evidence.

## Tasks & Acceptance

**Execution:**
- [x] Platform `eng/ext-secrets-1/probe_raft_non_revival.py` — implement narrowly scoped disposable real-Raft negative control, bounded operations, sanitized results and guaranteed cleanup. Accept an exact digest pin; no arbitrary runtime URL or token argument. Do not pull implicitly.
- [x] Platform `eng/ext-secrets-1/test_probe_raft_non_revival.py` — meaningful component coverage for incomplete observations, version mismatch, safety boundaries, sanitization and cleanup failures before actual backend execution.
- [x] Run focused component checks, then narrow real-backend probe; retain public evidence under the path in `/tmp/ext-protection-option-a-evidence-path`. Root handles exact image retrieval separately. Report exit 2 for observed revival, exit 1 for blocked/unknown, never qualify production.
- [x] Update owning decision and README with observed scope, stop condition, unexecuted transaction proof, missing erasure primitive and precise next acceptance criteria.
- [x] Update EventStore packet and hash-bind final inputs/source/results; parent integrates final evidence after independent verification.

**Acceptance Criteria:**
- Given the exact local OpenBao image, when the native snapshot/delete/restore sequence runs, then recorded observations distinguish revived key material from refusal or outage without persisting sensitive bytes.
- Given any missing stage or mismatched version, when the probe evaluates results, then it reports blocked rather than concluding erasure or revival.
- Given the observed ordinary-snapshot limitation and absent surviving authority, when reporting feasibility, then production engine construction and transaction qualification remain stopped, and G1 remains open.

## Implementation Notes

User approval applies to the already described feasibility recommendation; no additional provider/deployment approval is inferred. Unrelated dirty Platform files were recorded in baseline.json and must be preserved.

Parent preparation: exact discovered 2.6.2 image pulled successfully by digest. `docker run --rm --network none --cap-drop ALL --entrypoint bao <image> version` reports `OpenBao v2.6.2 (dd9c19c37a878cf4a81b18efb8d6f0599c7da923)`. An initial `/bin/bao` path failed (127); use the PATH executable `bao`. Public image/version commands are retained in the evidence directory. Local EventStore Aspire baseline start/describe/stop exited 0; application resources were Waiting, so this is no healthy-runtime or qualification claim.

Implementation outcome: 16 focused component tests pass. Native attempt 5 observed deleted-key revival after ordinary non-force snapshot restore, with five retained-control checks and verified cleanup. Earlier attempts remain unknown. G1/G3 and production construction remain stopped; no manifest provider was implemented. Parent inspected the actual source/diff and native result, and is completing independent review and final evidence bindings.

Final correction verification: 23 focused tests passed; native attempt 6 again observed revival and verified cleanup with source SHA-256 `1c668b8fa41dd609dc59ece1aeb02aa87b5902900309a7704d1a42dad25f6fbf`. Equality/error/malformed-response mutation checks and delayed-create follow-up reviews confirm the corrections. Parent copied the exact public native result/command into a small Platform documentation bundle with verified SHA256SUMS and byte-preserving attributes. Source mode and both diff checks pass. All eleven review findings were individually triaged; none remains deferred. This spec is complete only for feasibility; G1/G3 and the production owner delivery remain open. No stage, commit, push, Live, cohort, consumer or dependency-status mutation was performed by this work.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
| --- | --- | --- |
| Blind 1: asynchronous restore can be read before it applies | Medium: the restore acknowledgement need not mean the restored target is visible; immediate inventory can return a premature unknown. Existing readiness loops can be reused for bounded restored-state observation. | Patch bounded read-only polling and delayed-restore regression. |
| Blind 2: tmpfs option/empty-Mounts validation | Low: exact `create_args` supplies fixed private uid/gid/mode/size/safety options, with no caller override. HostConfig.Tmpfs is inspected and persistent mounts are rejected. The reproduced unsafe options require a forged daemon response or altered creation code; empty Mounts alone does not negate HostConfig tmpfs. | Reject additional defensive branches as unnecessary for this fixed local probe. |
| Blind 3: inspected resource budgets not checked | Low: fixed creation arguments set memory/swap/process limits and GOMEMLIMIT; operations also have subprocess deadlines. No public argument can disable those limits. There is no observed ignored-limit path on this Docker daemon. Host-memory non-retention claims are corrected separately. | Reject extra infrastructure enforcement checks for this bounded probe. |
| Blind 4: delayed create can outlive absence check | Medium: a timed-out create followed by a single absent inspection cannot establish daemon-side completion. | Patch: retain cleanup as unknown when create is unacknowledged and absent. |
| Blind 5: interpreter death loses ownership recovery metadata | Medium: create-only output is empty until completion; SIGKILL can leave no durable public run identity. A public pre-effect ownership record and documented exact-label recovery are sufficient; no watchdog/service is needed. | Patch initial public metadata and manual owned-container recovery instructions. |
| Blind 6: unconditional secretArtifactsRetained claim | Medium: container removal and omitted evidence do not establish absence of host swap/core copies of Python/subprocess buffers. | Patch wording/field to evidence omission and verified container cleanup; disclose host-memory limitation without changing host settings. |
| Blind 7: nonexportable qualifier lacks metadata readback | Low: native creation requests those flags, but the observation ledger does not read them back. This qualifier is unnecessary to the demonstrated rollback behavior. | Patch by removing the unsupported qualifier rather than expanding provider operations. |
| Blind 8: workstation-only native evidence | Medium: another checkout cannot retrieve the retained native observations through that path alone. The complete negative-control public result and command can be copied with checksums into Platform documentation. | Patch a small shareable public evidence bundle; no production canonical-target claim. |
| Edge 1: unacknowledged create / delayed orphan | Medium: same demonstrated race as Blind 4; current cleanup can say complete before daemon creation finishes. | Patch with Blind 4, retaining this separate finding. |
| Verification 1: wrong-plaintext branch untested | Medium: pre-verified mutation removes equality checking while all 16 tests still pass. | Patch restored-target and retained-control mismatch regressions. |
| Verification 2: unrelated refusal errors untested | Medium: pre-verified mutation accepts arbitrary nonzero target decrypt errors while all 16 tests pass. | Patch wrong status/message/missing-status cases with healthy control and no restore. |

## Verification

- `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_probe_raft_non_revival.py' -v` from Platform.
- Exact isolated probe command and exit classification retained in command records.
- `git diff --check` in Platform and EventStore.
