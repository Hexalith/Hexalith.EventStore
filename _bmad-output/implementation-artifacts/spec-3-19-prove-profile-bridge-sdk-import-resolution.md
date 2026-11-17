---
title: '3.19 Prove profile-bridge SDK and import resolution'
type: 'feature'
created: '2026-11-17'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3b2350e7c1fff640131f8101596c78a59e31a03f'
planning_baseline: '3b2350e7c1fff640131f8101596c78a59e31a03f'
bridge_completion: '0c2c09ed4279473570d3243d5c01e48301c8e46e'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Bridge isolation closes application-source membership, but authenticated publication still lacks SDK/import-resolution proof.

**Approach:** Prove each invocation's execution-input closure within the existing trusted-toolchain boundary. Discharge only the build-input prerequisite.

## Boundaries & Constraints

**Always:** Work in EventStore; paths are owner-relative. Reuse the isolated snapshot and 38 unchanged owner sources, codecs and digest domains. Trust installed SDK/runtime/OS as before; hashes authenticate nothing. Honor unchanged `global.json`, including `latestPatch`; record resolved SDK. Keep SDK generators, bounded children, filtered environment, private outputs/caches, disabled fallback and Debug locally/Release in CI.

Before authenticated preflight, compare captured/control bytes with authenticated head bindings and exclude uncontrolled resolver/import channels. Trusted SDK preflight may then observe resolution; each later stage requires verified input closure before launch. Incomplete proof retains `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE`. Keep every unrelated qualification, ratification, issuer and Builds `ReleaseEvidenceCodec` refusal; authority evaluation still fails without a seal.

**Never:** Accept caller commands/properties, saved JSON or prior success as proof. Add binary attestation, packages, infrastructure selections, authority, production files, pushes or unpublished gitlink commits. Change approvals or parent acceptance/status.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Complete bound invocation | Prove closure; validate v1/v2; authority remains unavailable |
| Unknown SDK/import/task/reference/analyzer or incomplete observations | Safe refusal before the affected stage |
| Changed snapshot, selection or generated input | Invalidate proof; never reuse stale results |
| Missing SDK/pack, fallback/network attempt, no-op child | Refuse; exit zero alone cannot pass |
| Forged proof or unbound control | Retain build-input refusal before preflight |

</frozen-after-approval>

## Code Map

- `tools/profile_bridge_build.py` — capture/configuration and `execute`; retain descriptor-safe reads.
- `tools/publication_authority.py` — `transition_inputs`, `evaluate_transition`, `profile_check`, `missing_prerequisites` own authenticated bindings, results and refusals.
- `tools/phase_4_transition.py` — reuse `Control.bounded_process` unchanged.
- Publication-validator BH2/BH3/EH2 define toolchain trust.

## Tasks & Acceptance

**Execution:**
- [x] `tools/ProductionProfileBridge/BuildProof.targets`, `tools/ProductionProfileBridge/ProductionProfileBridge.csproj` — add fixed observation targets and their sole explicit import; keep other imports/items closed. Observe task/compiler inputs and generated outputs.
- [x] `tools/profile_bridge_build.py` — capture the target; implement bounded preflight/stage checks. Resolve SDK/pack/runtime roots; combine evaluated/preprocessed imports with task/compiler observations covering sources, references, analyzers, additional files/configuration, response files and generated inputs. Classify inputs as verified snapshot, trusted toolchain or private output with verified producer/identity. Reject unknown paths, symlinks/escapes, incomplete observations and stage mutations. Bind proof to capture, installation, arguments/environment and invocation; preprocessing alone is insufficient.
- [x] `tools/profile_bridge_build.py` — suppress workload advertising using the SDK's supported opt-out; verify exact-environment process/network behavior including first use. Preserve offline restore controls.
- [x] `tools/publication_authority.py`, `tools/phase-4-transition.schema.json` — bind new controls/subjects, compare capture with authenticated head, recompute binding capacity. Replace only the unconditional build-input barrier with fixed verification; condition reporting on proof. Retain other barriers, head checks and owner-result validation.
- [x] `tools/tests/test_profile_bridge_build.py`, `tools/tests/test_publication_authority.py`, `tools/tests/test_phase_4_transition.py` — cover matrix, binding omissions, forged/stale/incomplete observations, stage mutations, unavailable tracing, bounded failures and no-op output; assert launch order/no seals.
- [x] `tools/tests/check_production_profile_bridge.py` — require genuine v1/v2 golden results and stage observations; test actual ancestor/user/resolver/response poisoning, pack absence, generated-input tampering and workload process/network behavior. Unavailable tracing cannot pass.
- [x] `docs/architecture/publication-authority-v1.md` — document proved closure, supported resolution envelope and remaining barriers. Preserve workflow/publisher/SDK pins.

**Acceptance Criteria:**
- Given authenticated exact-head inputs and a supported installed toolchain, when the fixed verifier succeeds, then only this invocation's build-input prerequisite is discharged and unchanged owner results match both digest domains.
- Given incomplete or invalid stage proof, when authenticated evaluation runs, then it retains the build-input refusal before that stage or any successor launches and emits no seal.
- Given a proved bridge but absent release-codec or qualification prerequisites, when evaluation continues, then those specific refusals remain and no authority is issued.

## Implementation Notes

- 2026-11-17: User approved this specification and requested continued implementation ("approve and continue"). Approved draft SHA-256: `29593a041d9543758382f110e863f7c118a6ca024e3adaee21dc6a7c9dc79e6c`. Frozen intent is unchanged; approval covers this engineering prerequisite only. Preserve existing preparation records, working changes and parent-story acceptance/status. No remote operation or unpublished submodule-pointer commit is authorized.

- Implemented fixed observation targets and invocation-bound preflight, task/compiler input checks, private output membership/seals and Linux ptrace/seccomp observation. SDK/pack/runtime selection directory identities invalidate drift between stages. Installed SDK/runtime/OS remain trusted; no binary attestation or package/tool installation was added. All other authority prerequisites remain mandatory.
- Parent audit read the complete tracked/untracked diff and every later source delta, checked all seven tasks/three acceptance criteria, and matched every matrix row to executed passing controls. All 38 owner sources, bridge body, pins, approvals, parent frozen/acceptance blocks and sprint statuses are preserved. Exact Git-blob control bytes remain required for authenticated preflight; normal CRLF-filtered checkout bytes are not silently normalized.

## Spec Change Log

## Review Triage Log

Review pass 1: all three independent layers completed before triage. Parent probes confirmed restored-mtime identity evasion, source mutation reaching csc before the later refusal, missing runtime/dependency metadata in a genuine proof, and transferable registered proof payloads. Verification-gap findings include executed mutation evidence. No frozen-intent change is required.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1: ordinary checkout EOL mismatch | medium | defer | `global.json` is already in baseline publication controls and unchanged `Control.prepare` already compares local bytes to authenticated raw blobs. Standard CRLF checkout therefore has a pre-existing source-binding prerequisite. Preserve the refusal; exact-byte verification-workspace preparation belongs to a separate caller integration. |
| BH2: restored-mtime installation rewrite | medium | patch | A parent `run-with-temp` probe rewrote an installed-file fixture at equal length and restored mtime; `fingerprint` stayed equal. Include change time in identities and cover this demonstrated mutation. |
| BH3: scalar values versus documented contract | low | patch | Isolated parser probes accept listed scalar names with varying values. Fixed project/CLI and trusted SDK remain the source of scalar configuration; unsupported target/language values fail compilation. Clarify the documentation's option-name/input contract rather than adding a second compiler policy. |
| BH4: output destination/presence policy | low | reject | Parser-only acceptance is real, but omitted or redirected assembly output cannot supply the required fresh assembly; overwriting a snapshot member fails post-child hash verification. The files are disposable and no proof is issued for those outcomes. A separate output-policy layer adds guards for a state not reached by the fixed SDK producer in everyday use. |
| BH5: source/identity change at csc handoff | medium | patch | Parent genuine probe changed captured Program.cs immediately before the parser call. csc ran and produced an intermediate assembly before the controller refused. Recheck the existing capture and installation seals while csc is stopped. |
| BH6: runtime/dependency/resolver metadata omitted | medium | patch | A genuine proof's identity table omitted dotnet/MSBuild/csc runtimeconfig and deps files. Existing-file edits do not change parent directory identities. Bind the supported execution metadata and refuse changes before dependent launches. |
| BH7: descendant environment not observed | medium | patch | The syscall observer reads argv but not envp; installed Microsoft.CSharp.Core.targets passes CscEnvironment. Starting-tracer equality does not bind the actual compiler environment. Observe and validate permitted descendant environments. |
| BH8: transferred capability payload/context | medium | patch | Registered-proof transfer to different OwnerOutput bytes is accepted by `proved`. Bind acceptance to the actual verifier result and invocation/capture context so consumers cannot confuse a transferred/prior capability with the current result. |
| BH9: AF_PACKET envelope | maybe-false | defer | The observer checks Internet families 2/10. Raw-packet transmission requires an additional socket envelope/capability and has not been demonstrated through any supported fixed SDK/bridge command. If reachable, the consequence is medium; a supported-toolchain trace or controlled reachable fixture, without replacing trusted binaries, would settle it. No generic hostile-toolchain sandbox is claimed. |
| BH10: genuine authenticated positive path absent | medium | patch | Mocked authenticated success and separate genuine bridge checks do not exercise their handoff together. Add a genuine exact-byte synthetic authenticated invocation reaching the retained unrelated authority barrier; keep the real checkout/issuance prerequisites separate. |
| EH1: nested late SDK import | medium | patch | The review's staged fixture adds a nested SDK wildcard import after preflight and still receives a proof. Only roots/immediate children are sealed. Close nested import-directory membership before dependent stages. |
| EH2: compiler environment claim | medium | patch | Same envp omission as BH7, corroborated by the installed CscEnvironment task parameter and the starting-only environment check. |
| VG1: capability handoff coverage | medium | patch | Pre-verified: removing `proofs.append(output)` left all 91 snapshot/publication/lifecycle tests passing. Add a real profile_check-to-evaluate_transition success boundary using the genuine verifier. |
| VG2: actual csc response boundary coverage | medium | patch | Direct parser tests and ambient csc.rsp poisoning never reject a changed generated response at the real stopped exec. Add that integration control and make parser-call removal fail it. |

Survivors group only by shared root: BH7/EH2 (actual descendant environment), and BH10/VG1 (genuine authenticated capability handoff). The nine patch entries are local corrections/tests/documentation within the approved execution-input proof, with no new public surface. BH1 and unverified BH9 are recorded separately in the Platform deferred-work ledger.

- Review pass 1 resolution: all nine accepted patch groups are implemented and verified. File/directory identities include ctime; selected SDK import directories and execution metadata are sealed; actual descendant environments and stopped-compiler source/identity/output seals are checked. Capabilities bind the minted result object/bytes and current capture. Genuine authenticated handoff and stopped-response controls catch their removal mutants. Compiler scalar-setting ownership is stated precisely. BH1 and unverified BH9 remain deferred; no authority or parent acceptance is issued.

## Verification

Use `run-with-temp`; await children and retain logs outside scratch. Run snapshot/publication/lifecycle unittest suites individually and `python3 -B tools/tests/check_production_profile_bridge.py`. Require genuine SDK/process/network observations and guard-removal failures. Check source hashes, golden bytes, approvals, parent acceptance/status, absent production slots and `git diff --check`. Fixtures complete no parent story.

Executed verification: **21 snapshot tests, 27 publication tests, 43 lifecycle tests and 58 genuine integration controls pass**. Both golden versions selected SDK 11.0.100. Final SDK-selection directory and path-guard deltas also passed the narrow unit checks and a genuine seven-stage run with poisoning/network/no-op controls. The authentication-argument removal mutant fails its regression as required. Commands, source identities, matrix coverage, log digests and supported limitations are recorded in the [verification summary](evidence/profile-bridge-sdk-proof-20261117/verification-summary.json). No live authenticated build-input proof, parent acceptance or production authority is claimed.

Post-review parent verification on unchanged source: **24 snapshot/SDK tests, 27 publication tests, all 43 lifecycle tests and 60 genuine integration control lines passed**. The full genuine suite includes both explicit profile versions, real synthetic authenticated handoff reaching the retained Builds-codec refusal with no seal, stopped-source/metadata/envp/response tampering, both removal mutants, first-use network controls, no-op refusal and actual missing-pack NETSDK1226. Whitespace checks passed. Commands run sequentially from the EventStore root beneath `run-with-temp`:

- `python3 -B -m unittest discover -s tools/tests -p test_profile_bridge_build.py -v`
- `python3 -B -m unittest discover -s tools/tests -p test_publication_authority.py -v`
- `python3 -B -m unittest discover -s tools/tests -p test_phase_4_transition.py -v`
- `python3 -B tools/tests/check_production_profile_bridge.py`
- `git diff --check`

Exact source/log identities and timings remain in the local ignored verification summary. Contributor policy keeps evidence directories out of commits; this spec records the commands and results. No remote CI was run and no live authenticated publication is claimed. Parent Story 3.19 remains in-progress and Platform 4.9/9.2 remain draft/backlog. Local commit identity is recorded by Git history and the Platform completion observation; no unpublished submodule pointer is committed.
