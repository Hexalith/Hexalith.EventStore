---
title: 'Story 3.15 Corrected Deployed Runtime Parity Closure'
type: 'feature'
created: '2026-08-21'
status: 'done'
baseline_commit: '94591f3539ce30372db58e5fdd3ba017ea8c07b8'
review_loop_iteration: 6
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Corrective release `v3.96.2` has a valid Story 3.14 handoff, but it does not independently prove Production runtime parity or provide the content-bound acceptances required to select a deployment-grade identity.

**Approach:** Revalidate the immutable Story 3.14 lineage, independently retain public package, raw OCI, and two-platform Production-smoke evidence, then issue one canonical positive-parity subject whose exact bytes are accepted by the three required authenticated roles.

## Boundaries & Constraints

**Always:** Treat the Story 3.14 packet and Stories 3.13/1.20 evidence as read-only; derive every selected edge from trusted sources and retained raw bytes; distinguish GitHub release-asset and NuGet-signed package byte domains; use exact canonical UTF-8 bytes and a trusted versioned verifier; fail closed with support-safe reason and rerun trigger; select only OCI index digest `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` after all checks and receipts pass.

**Ask First:** Any external write, credential use beyond read-only pulls, creation or collection of owner receipts, deployment, consumer mutation, or change to the approved identity, role registry, or frozen predecessor artifacts.

**Never:** Execute packet-supplied code; splice `v3.94.1`, quarantine, or another release lineage; trust labels, tags, observations, pass flags, self-declared roles, or current-time authority validity alone; fabricate approvals; rewrite published artifacts; authorize deployment, consumer removal, publication, or registry mutation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Positive closure | Exact `v3.96.2` handoff, 14 public packages, raw two-platform OCI graph, bounded Production smokes, unchanged subject, three valid receipts | `deployed_runtime_parity: available`; selected identity is the bound OCI index digest | None |
| Mutable or mixed evidence | Missing/extra bytes, noncanonical encoding, changed package/OCI/smoke fact, tag-only fact, or foreign lineage | No identity is selected | Record deterministic blocker and rerun trigger |
| Invalid acceptance | Missing, duplicate, stale, wrong-role, unverifiable, or subject-mismatched receipt | Technical evidence remains non-authorizing and parity unavailable | Reject all receipts after any subject change |
| Downstream citation | Deployment or consumer-removal request cites the completed packet | Packet supplies immutable evidence only | Require separate deployment or Consumer-owner authority |

</frozen-after-approval>

## Code Map

- `tools/validate-corrective-release-evidence.py:12-73` and `tools/release_evidence_handlers/v3.py:58-404,863-974` -- trusted Story 3.14 dispatcher/canonical-byte gate; preserve v3 behavior and require predecessor digest `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- `tools/validate-corrected-deployed-runtime-parity.py` and `tools/deployed_runtime_parity_handlers/v1.py` -- new allowlisted closure dispatcher/handler for independent package, OCI, Production-smoke, subject, registry, receipt, and non-authority validation.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235` -- predecessor mutation and canonicalization patterns; do not extend its frozen candidate contract.
- `tools/assemble-corrected-deployed-runtime-parity.py` -- deterministic packet producer: re-mints the subject, derives the package count and parity verdict from retained evidence rather than asserting them, and runs the pinned verifier over its own output before exiting.
- `tools/capture-corrected-deployed-runtime-parity-smokes.py` -- bounded two-platform Production smoke capture.
- `_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances/` -- complete
  receipt/source trees bound to superseded subjects `bb58d691...` and `dab64f5f...`, retained unbound
  for audit. They must never be moved back into the packet; the `bb58d691...` owner sources are
  anchored on issue `#346` and are rejected on lineage as well as on subject.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs` -- new positive-closure and fail-closed mutation suite.
- `_bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` -- immutable predecessor packet; only the successful `v3.96.2` subgraph is selectable.
- `_bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` -- new hash-closed technical evidence, subject-addressed acceptances, and final verdict.
- `.gitattributes`, `docs/ci.md`, and `_bmad-output/implementation-artifacts/{3-15-corrected-deployed-runtime-parity-closure.md,3-15-corrected-deployed-runtime-parity-closure-proof-packet.md}` -- byte stability and operator handoff.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-corrected-deployed-runtime-parity.py` and `tools/deployed_runtime_parity_handlers/v1.py` -- implement a closed-schema, allowlisted verifier that revalidates the predecessor, recomputes every retained edge and canonical subject, validates exactly three packet-bound receipts, and never executes retained code.
- [x] `_bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` and `.gitattributes` -- retain LF-stable workflow/archive facts, all 14 independently downloaded NuGet packages, raw OCI graph, bounded Production smoke logs/results for both immutable children, owner-role registry, closed inventory, canonical subject, and subject-addressed receipts without hash cycles.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs` -- cover every matrix row and mutation-prove identity bytes, package domains, OCI chain, both smokes, inventory, registry, subject, each receipt field/role, and non-authority flags.
- [x] `_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure*.md` and `docs/ci.md` -- record exact lineage, commands/results, blockers, rerun triggers, positive identity, receipt sources, and evidence-only operator boundary.

**Acceptance Criteria:**
- Given the frozen Story 3.14 handoff, when Story 3.15 validation runs, then it first reproduces the exact predecessor identity digest and independently maps the source/workflow/authority, 14 package identities in both byte domains, raw OCI index/children/configs, required provenance, and two Production smokes into one lineage.
- Given the hash-closed technical packet, when the canonical subject is recomputed, then it binds every decision input, explicit positive outcome, selected index, authority and registry digests, and verifier identity; any transitive change invalidates all receipts.
- Given exactly the authenticated EventStore owner, Release owner, and Test Architect accept the unchanged subject, when the final verifier runs, then parity becomes available and the exact index is selected while every deployment and consumer-removal authority remains false.
- Given any matrix mutation, when focused tests execute, then the candidate fails closed with no skipped case and the frozen Story 3.14 packet remains byte-for-byte unchanged.

### Review Findings (2026-08-22, full review — 4 layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; none failed)

Scope: uncommitted working-tree diff at baseline `94591f35` (this spec, the story record, proof
packet, `.gitattributes`, `docs/ci.md`, `sprint-status.yaml`, `deferred-work.md` additions, the new
`CorrectedDeployedRuntimeParityClosureTests.cs`, and the four new `tools/` files). Evidence blobs
under `evidence/story-3-15/` and the unrelated leftover `review-child-prompt-3-13-edge-case-hunter.md`
were out of scope. 32 raw findings, merged to 19, 5 dismissed (docs/ci.md arm64-emulation disclosure
refuted by a pre-existing section elsewhere in the same doc; tracker-status nuance is subjective
process opinion; the two cross-module `# noqa: SLF001` reaches are an already-acknowledged trade-off;
one Verification-Gap note the layer itself declined to grade; the Acceptance Auditor's own passing
re-verifications are confirmations, not findings).

**decision (human input required — the correct fix is ambiguous):**

- [x] [Review][Decision→Patch] RESOLVED 2026-08-22 (owner: add explicit scope check). Owner-role registry authority is reused from a comment explicitly scoped to Story 3.13 — `_validate_registry` (`tools/deployed_runtime_parity_handlers/v1.py:591-618`) accepts `evidence/story-3-15/.../registry/role-registry-source.json` as authority, but that comment's own body reads "I ratify the exact reviewer-role mappings for ... Story 3.13" and "authorizes no ... Story 3.13 done status" — it never mentions Story 3.15, and `epic-3-context.md` states Story 3.15 "cannot splice in 3.13 evidence." The validator checks only comment id/url/user plus substring presence of the three role lines, never the comment's own declared scope. **Decision: this is a real gap, not acceptable reuse — add an explicit check/acknowledgment rather than silently reading a Story-3.13-scoped comment as Story-3.15 evidence.** Follow-up patch below.
- [x] [Review][Decision→Patch] RESOLVED 2026-08-22 (owner: rename the field, keep the shallow check). `repository_signed: True` is asserted from a zip-entry-count check, not real cryptographic verification — `_validate_packages` (`v1.py:424-436`) only checks that exactly one `.signature.p7s` zip entry exists and the nuspec identity fields match; it never verifies the PKCS#7 signature bytes against a trusted NuGet.org signing certificate. This is genuinely new code (Story 3.14's predecessor handler has no such check at all). **Decision: real PKCS#7 verification is out of scope; rename the field so the schema doesn't overclaim what was actually checked.** Follow-up patch below.

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] Owner-role registry authority comment is Story-3.13-scoped, not Story-3.15-scoped, and nothing checks or records that — `_validate_registry` must either reject an `authority_source` comment body that names a different story than the one being validated, or (if repo-wide role rosters are intentionally reusable across stories) the spec/registry file must carry an explicit note stating the comment is general identity/authority-holder fact, not release-lineage evidence, so it is not later misread as spliced Story 3.13 evidence. [tools/deployed_runtime_parity_handlers/v1.py:591-618]
- [x] [Review][Patch] Rename `repository_signed` to avoid overclaiming cryptographic verification — the field is set from a `.signature.p7s` zip-entry-presence + nuspec-identity check only, with no PKCS#7 chain verification. Rename to something like `repository_signature_entry_present` (and update `closure.json`'s schema/consumers and the test fixtures accordingly) so the field name matches what was actually verified. [tools/deployed_runtime_parity_handlers/v1.py:424-436]

- [x] [Review][Patch] Mutation tests can't prove 5 downstream semantic checks actually work — `MutableOrMixedEvidenceNeverSelectsIdentity` and the `"unverifiable"` case of `InvalidAcceptanceNeverAuthorizesParity` mutate files by appending one byte and assert only the generic `"retained file binding mismatch"` string from `_verify_file`, which always fires first. Verified by deletion: the nuspec/signature check, OCI label/platform check, smoke-log equality check, registry authority-source check, and receipt GitHub-source check can each be deleted with all 36 tests still green. Add cases that edit content while correcting the SHA-256/size binding so the semantic check is what must fire. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:145-173, :265; tools/deployed_runtime_parity_handlers/v1.py:429, :480-486, :587, :609-618, :701-720]
- [x] [Review][Patch] Test Architect receipt branch and nested `durable_source` fields are never exercised by any negative case — `EveryReceiptFieldIsRequired` and `InvalidAcceptanceNeverAuthorizesParity` both index only `receipts[0]`/`bindings[0]` (always `eventstore-owner`); the structurally distinct `bmad-test-architect-record` branch is exercised only by the all-green positive-closure test, and no case removes a single nested `durable_source` field (`file`/`kind`/`sha256`/`size`). [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:230, :268; tools/deployed_runtime_parity_handlers/v1.py:721-729]
- [x] [Review][Patch] No closed-inventory guard across the bulk of the packet — stray files silently accepted (demonstrated) — `_validate_inventory` hashes only the expected/declared file set and never walks `packet_root` to detect unreferenced files, unlike `_validate_receipts` which does close-list `acceptances/<subject>/`. Verified: a synthetic fully-accepted packet with an added `packages/stray-not-listed.bin`, `stray-at-root.txt`, and `oci/extra/stray.raw` still validates and selects the identity, exit 0. [tools/deployed_runtime_parity_handlers/v1.py:621-638, :641-654]
- [x] [Review][Patch] Receipts' `durable_source` isn't independently verifiable, and this isn't acknowledged — `_validate_receipts` treats a JSON file inside the same packet the receipt author controls as authenticity proof, never fetching it live from the GitHub API — reproducing, unacknowledged, the exact "proves consistency, not independence" gap this diff's own new `deferred-work.md` chunk-1 entry just recorded for the analogous Story 3.13 mechanism. Add the same acknowledgment here. [tools/deployed_runtime_parity_handlers/v1.py:641-731]
- [x] [Review][Patch] Two `.get("user", {}).get(...)` chains crash uncaught instead of failing closed — if `user` is present but not a dict, `.get("login")`/`.get("id")` raises `AttributeError`, which `validate-corrected-deployed-runtime-parity.py`'s `except (OSError, DispatchError, ValueError, JSONDecodeError)` does not cover. Violates the frozen spec's "Always: ... fail closed with support-safe reason." [tools/deployed_runtime_parity_handlers/v1.py:614, :702-703; tools/validate-corrected-deployed-runtime-parity.py:86]
- [x] [Review][Patch] Capture script isn't robust to genuine infrastructure failure — `docker pull` sits outside the `try` block entirely, so a pull failure propagates an uncaught `CalledProcessError` and `smoke-results.json` is never written; `port_output.rsplit(":", 1)[1]` also raises an uncaught `IndexError` if `docker port` returns no mapping. [tools/capture-corrected-deployed-runtime-parity-smokes.py:43, :51-106, :79]
- [x] [Review][Patch] `attempts` accepts JSON boolean `true` as a valid positive count — the check omits the `isinstance(..., bool)` exclusion the repo's own `_positive_integer` helper already applies three functions above. [tools/deployed_runtime_parity_handlers/v1.py:556-557, :127-130]
- [x] [Review][Patch] Per-platform smoke timestamps aren't bounded to the aggregate window — `_validate_smokes` bounds each platform's own duration but never checks `item["started_at"]`/`["ended_at"]` fall within `[overall_start, overall_end]`, so a log from an unrelated run could be substituted if its own internal duration is short enough. [tools/deployed_runtime_parity_handlers/v1.py:525-567]
- [x] [Review][Patch] Registry authority-source body check is substring-only — `any(line not in body for line in expected_lines)` lets arbitrary extra or contradictory text coexist in the same comment undetected. [tools/deployed_runtime_parity_handlers/v1.py:606-618]
- [x] [Review][Patch] `RunProcess` has no subprocess timeout — `WaitForExit()`/`ReadToEnd()` have no timeout/kill path; reproduces the pattern already flagged LOW for the sibling `CorrectiveOciProvenanceReleaseTests.cs:53` earlier in this same diff. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:491-513]
- [x] [Review][Patch] `V1_HANDLER_SHA256` bare literal has no self-check test — the live-file check in `v1.py` independently re-verifies the declared handler hash against the actual file on disk (fails closed on drift), but nothing regression-tests that the literal stays in sync. [tools/validate-corrected-deployed-runtime-parity.py:13; tools/deployed_runtime_parity_handlers/v1.py:268-271]
- [x] [Review][Patch] Dead variable in assembler — `predecessor_root` is assigned and never read again. [tools/assemble-corrected-deployed-runtime-parity.py:36]
- [x] [Review][Patch] Uncaught `KeyError` in assembler on manifest/predecessor drift — `predecessor_packages[package_id]` has no guard or diagnostic. [tools/assemble-corrected-deployed-runtime-parity.py:58]
- [x] [Review][Patch] Registry `created_at` is a hardcoded literal, unconstrained by validation — the assembler hardcodes `"2026-08-14T07:08:46Z"`; `v1.py` only checks it parses, never that it matches anything. [tools/assemble-corrected-deployed-runtime-parity.py:46; tools/deployed_runtime_parity_handlers/v1.py:603]
- [x] [Review][Patch] No drift guard binding `docs/ci.md`'s new digests to actual values — the new §3.15 section states specific subject/index digests in prose; no test in the new suite references `docs/ci.md` at all (grep-confirmed zero matches), the same drift class the Story 3.14 chunk of this diff explicitly requests a guard for. [docs/ci.md:376-397]
- [x] [Review][Patch] `deferred-work.md` "chunk 2" section precedes "chunk 1", both dated the same day — breaks the ledger's otherwise chronological append order. [_bmad-output/implementation-artifacts/deferred-work.md:22, :39]
- [x] [Review][Patch] This spec's own Verification section documents a command outcome that contradicts actual behavior — running the checked-in `closure.json` command directly returns `fail: exactly three packet-bound receipts are required`, exit 1 (verified), not the "expected: ... pass" the Verification section states; contradicts the companion story record, which correctly documents the fail-closed expectation. [_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md:81]

### Review Findings (2026-08-25, full review -- 3 layers x 5 diff chunks, 15 reviewers; none failed)

Scope: the complete baseline diff at `94591f35` (639,621 bytes, 78 files), chunked by byte-exact
partition into tooling/CI, the 3.15 suite plus evidence payloads, the 3.13 suite, the 3.14 and
governance suites, and docs/specs/ledgers. Every load-bearing claim below was reproduced locally
before triage; several plausible findings were refuted by running them and are not listed.

**blocking (fixed in this pass):**

- [x] [Review][HIGH] **Fail-open in the trusted-handler pin.** `_load_handler` hashed only the leaf
  module, but `importlib.util.find_spec` imports the parent package first, so
  `release_evidence_handlers/__init__.py` executed unhashed. Reproduced: injected code printed *and
  the validator still returned `pass`, exit 0* -- not merely un-pinned but fail-open, while the
  comment added in the same diff claimed "an unreviewed edit never executes". Fixed by pinning every
  file on the import path and resolving paths from the script instead of `find_spec` (which itself
  triggers the parent import). [tools/validate-corrective-release-evidence.py:22-31,72-96]
- [x] [Review][HIGH] **The transitive verifier was unbound, so the rerun trigger did not hold.**
  `v1.py` delegates predecessor validation, nuspec identity parsing, and the release-manifest check
  to `release_evidence_handlers.v3`, whose digest appeared nowhere in the packet. Reproduced: a
  tampered `v3.py` yielded `pass` with the *identical* subject `bb58d691...` and identical selected
  identity, leaving all three receipts valid -- contradicting frozen AC2 ("binds ... verifier
  identity; any transitive change invalidates all receipts") and the closure's own `rerun_trigger`.
  `docs/ci.md` had recorded this as owed "when its in-review packet is re-frozen"; the re-freeze had
  happened without paying it. Fixed by binding `v3.py` and its package initializer in `dispatch`,
  and pinning all four import-path files before the first import.
  [tools/deployed_runtime_parity_handlers/v1.py:37-45,268-300; tools/validate-corrected-deployed-runtime-parity.py:17-30,62-70]
- [x] [Review][HIGH] **Guard covering two path strings, one of them fictional.**
  `DigestBearingRawOciEvidenceIsBinary` asserted
  `evidence/story-3-15/oci/index.raw`, which does not exist -- `git check-attr` answers for any path
  string, so that case passed vacuously. Confirmed 14 of 24 tracked `.raw` files were `text: auto`,
  all under `story-3-13` and all digest-bearing via `identity-crosswalk.json`. Fixed by enumerating
  `git ls-files '*.raw'` with an existence check and a coverage control, and by adding the missing
  `story-3-13/**` rules. All 24 are now `text: unset` with zero byte churn.
  [tests/.../ContainerPublishingGovernanceTests.cs:772-800; .gitattributes:15-19]

**patch (fixed in this pass):**

- [x] [Review][Patch] Registry role lines were `dict(findall(...))`, i.e. last-wins, so a *prepended*
  contradicting `- eventstore-owner: github:mallory` was silently discarded and the mapping compared
  equal. The existing negative test only appended, which loses. Duplicate role keys now reject.
- [x] [Review][Patch] The disclaimer gate matched `"authorizes no"` and `"deployment"` anywhere in
  the body, so `"authorizes nothing; this deployment is fully authorized"` satisfied it. Both markers
  must now fall inside one sentence.
- [x] [Review][Patch] The roster comment was authenticated far more weakly than an acceptance
  receipt in the same file (login only). Now also binds `user.id`, `updated_at == created_at`, and
  `performed_via_github_app is None`.
- [x] [Review][Patch] Stale or foreign acceptance trees were invisible: `_validate_inventory` skipped
  the whole `acceptances/` prefix. Only the bound subject's directory is exempt now; anything else
  under `acceptances/` is rejected.
- [x] [Review][Patch] Neither tamper test had a positive control, so a broken temp-tree harness was
  indistinguishable from the guard firing. Both now assert the untampered copy validates first.
- [x] [Review][Patch] `docs/ci.md`'s digest assertion was presence-only and could not notice a
  superseded digest left beside the current one; the Story 3.15 section's 64-hex token set is now
  exact.
- [x] [Review][Patch] `sprint-status.yaml`'s comment above the Story 3.13 row still read "Acceptance
  is exactly 0/3 ... can never reach done" directly above `done`, and recorded nothing about issue
  #351 or the self-attestation caveat.
- [x] [Review][Patch] This spec's Design Notes asserted Story 3.14's spec was `done` and its tracker
  `review`; the same changeset set both to `in-progress`.
- [x] [Review][Patch] `docs/brownfield/deployment-guide.md` prose named two mandatory provenance
  properties while its own samples pass three and `Directory.Build.targets` hard-fails without the
  third.

**deferred (recorded in deferred-work.md, not fixed here):**

- [ ] [Review][Defer] `ValidateAcceptances` (`DeployedRuntimeParityClosureTests.cs:7267`) still
  enforces the `.../commit/<sha>#story-3-13-<hash>-<role>` anchor, `acceptance-source/v1`, and
  `retained-immutable-external-record` -- the unmintable shape Story 3.13 was reopened to remove,
  surviving on the sibling closure-packet path while the disposition path moved to `/v2` and
  `github-issue-comment`. Live at two call sites including the `story_may_be_done` gate. Only a
  fixture can satisfy it; a genuine GitHub receipt would be rejected.
- [x] [Review][Defer→Resolved 2026-08-25] `author_association` asymmetry is closed: the new
  Story-3.15-scoped roster comment is MEMBER-authenticated, and both registry and receipt paths now
  require MEMBER/OWNER/COLLABORATOR. The CONTRIBUTOR exception was removed.
- [ ] [Review][Defer] `created` provenance labels are self-comparing
  (`expected ??= ExpectedLabels(observedCreated)`) and `v3.py`'s `_expected_labels` omits `created`
  entirely, so the publisher-supplied instant can stop reaching the image undetected. The retained
  child configs both carry the malformed `2026-08-20T11`, truncated at the first colon.
- [ ] [Review][Defer] `redirect_count` cannot fail: the capture invokes `curl` without `--location`,
  so `num_redirects` is structurally zero. Likewise `observed_runtime_platform` is read from the
  image metadata `--platform` already selected, so the verifier's mismatch check cannot fire.
- [ ] [Review][Defer] `smokes/*.log` are canonical JSON restatements of `smoke-results.json`, not
  transcripts, so the log-versus-summary check compares two hand-written documents.
- [x] [Review][Defer->Resolved 2026-08-25 (loop 6 landing)] `FrozenStory314PacketRemainsByteForByteUnchanged` now pins a manifest digest over the whole 66-file frozen packet, not one file.
- [ ] [Review][Defer] `_bmad-output/test-artifacts/` gate artifacts: the matrix names a test method
  that does not exist with every line number off by two, `pct: 100` is reported on zero totals, and
  `evaluator` is `Administrator` while the matrix signs off as `bmad:murat`.
- [ ] [Review][Defer] The Builds gitlink was moved to the tip of `origin/main` (`22a578b5`) while
  `release.yml` still pins `a07078ad`, so the Builds-side preflight change is not in the executed
  release path. Rotation is supposed to happen from the pin, never from main.

### Review Findings (2026-08-25, full review -- 3 layers x 9 diff chunks, 27 reviewers; none failed)

Every claim below was reproduced locally before being actioned; two plausible reviewer claims were
refuted and are recorded as such.

**[Review][Decision] [HIGH] Release fails at container publish under the current Builds pin.**
Removing the `ContainerProvenanceCreated` fallback and adding a mandatory `<Error>` shipped without
rotating the pin that supplies the value; the pinned `a07078ad` publisher never passes it, and the
gitlink bump to `22a578b5` does not change what CI executes. Reproduced with a direct
`dotnet msbuild -t:ValidateContainerProvenanceInputs` run. Owner decision, 2026-08-25: **record
only, do not touch CI** -- rotating a release pin is outward-facing and belongs to the 3.14 lane.
Filed in `deferred-work.md` as a blocking owner decision.

**[Review][Patch] [HIGH] Both Story 3.15 records asserted a superseded subject and 3/3 receipts.**
`3-15-...-closure.md` and `...-proof-packet.md` still claimed "parity is available", subject
`bb58d691`, and three passing receipts, while the packet was at `1dee194f` with zero receipts and
exited 1. Only `docs/ci.md` is drift-bound by a test, so nothing caught it. Both records rewritten
to state the fail-closed verdict, the blocking owner action, and a reproduction command.

**[Review][Patch] [HIGH] The SHA-pinned Python verifiers had no EOL protection.**
`.gitattributes` carried only `* text=auto` for `*.py` while `.editorconfig` sets
`end_of_line = crlf`, so an EditorConfig-honouring editor silently invalidates the canonical subject
with a clean `git status` -- the Story 3.3 trap, now applied to four hash-pinned files. Added
`*.py text eol=lf`; `git check-attr` now reports `eol: lf` for all four.

**[Review][Patch] [HIGH] The registry disclaimer gate accepted a body asserting the opposite.**
Substring markers `("authorizes no", "deployment")` are satisfied by *"authorizes nothing beyond
deployment role identity"*. Replaced with a word-bounded, single-sentence regex; verified the bypass
is now rejected and the genuine retained disclaimer still accepted.

**[Review][Patch] [HIGH] Acceptance sources were prefix-matched, permitting a cross-lineage splice.**
`id`, `url`, `html_url`, and `issue_url` were each checked independently by prefix, so a receipt
could carry a comment id from one thread, an anchor from another, and an issue_url from a third --
the exact defect Story 3.13 was reopened for. All four must now resolve to one comment on one issue,
and issues `#324`/`#346` are rejected by number. The superseded receipts were themselves anchored on
`#346`.

**[Review][Patch] [HIGH] A date-only timestamp crashed the verifier instead of failing closed.**
`datetime.fromisoformat("2026-08-25" + "+00:00")` yields a naive datetime, which raises an uncaught
`TypeError` on comparison. Timestamps now require the full second-precision UTC shape.

**[Review][Patch] [HIGH] The assembler always exited 0 and never validated its own output.**
It printed a success-shaped line for a packet the pinned verifier rejects, imported the trusted
handler unpinned, hardcoded `"count": 14` and `deployed_runtime_parity: "available"`, and carried a
stale `created_at` forward across content changes. It now derives the count, refuses to assemble
over failed smokes, re-stamps `created_at` when content changes, runs the pinned verifier over its
own output, and propagates a non-zero exit.

**[Review][Patch] [MEDIUM]** An unknown package id raised a bare `KeyError` outside the entry
point's catch tuple; now an `EvidenceError` naming the id. Each rostered role is bound to exactly
one source kind, so an owner receipt can no longer present a self-attested record. CRLF comment
bodies are normalized before role-line and disclaimer matching (a genuine GitHub body previously
matched zero role lines). The dispatcher gained a table-consistency guard, a post-import
`__file__` assertion, single-read manifest hashing, `sys.dont_write_bytecode`, and per-file naming
in its pin-mismatch message.

**[Review][Patch] [MEDIUM]** The stale `_bmad-output/test-artifacts/` gate reported `PASS` over the
superseded subject with a vacuous `p1_status: MET` on an empty set. Withdrawn and banner-marked
SUPERSEDED rather than regenerated; regeneration is filed.

**[Review][Refuted]** "The Builds pin `a07078ad` is not reachable on the Builds remote" -- it is
contained in `origin/main`, as is `22a578b5`. The *missing reachability guard* is real and filed;
the claimed live break is not. **[Review][Refuted]** "`git diff --check` was recorded with no
result" applied to a prior loop's wording, not to a defect in this packet.

**[Review][Defer]** Eight items filed in `deferred-work.md`: the blocking pin decision, the missing
dedicated Story 3.15 acceptance issue, the absent remote-reachability guard, the `.raw`-only
normalization guard, the self-comparing `created` label assertion, the Story 3.13 closure path still
requiring the unmintable commit anchor, and gate-artifact regeneration.

### Review Findings (2026-08-25, full review -- loop 4, chunk 1 of 5: closure verifier core; 4 layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; none failed)

Scope: `tools/deployed_runtime_parity_handlers/{__init__,v1}.py`, `tools/validate-corrected-deployed-runtime-parity.py`,
`tools/validate-corrective-release-evidence.py`, `tools/release_evidence_handlers/v3.py` (+1113/-10 vs baseline `94591f35`).
Remaining chunks not yet reviewed this loop: packet producers; `CorrectedDeployedRuntimeParityClosureTests.cs`;
retained evidence packet + story/proof-packet/spec/docs; build/release plumbing.

**Landing window (superseded -- kept for the record):** when loop 4 was written the packet was at 0/3
receipts, so a re-mint was free. That is no longer the situation this paragraph described: receipts were
subsequently collected twice, and the loop-6 batch landing re-minted the subject again and rejected all three
`a8cc777e...` receipts. The packet is back at 0/3 by that landing, not by the condition recorded here.

- [x] [Review][Decision→Patch] RESOLVED 2026-08-25 (loop 4): ACCEPT + BIND LIMITATION -- keep the `bmad-test-architect-record` shape and add the self-attestation caveat to `REQUIRED_LIMITATIONS`, so every receipt must repeat it verbatim. `REQUIRED_LIMITATIONS` is subject-bound, so this re-mints; free at 0/3. Original finding: Test Architect acceptance source is unauthenticated by construction -- `_validate_receipts`'s `bmad-test-architect-record` branch builds `expected_source` purely from the receipt itself (`{k: v for k, v in receipt.items() if k != "durable_source"}` plus three constants), so the "durable source" carries no information the receipt did not already assert: no external identity, no independent timestamp, no signature, no URL anchor. Confirmed byte-for-byte against the retained superseded pair at `evidence/story-3-15/superseded-acceptances/bb58d691.../{test-architect.json,sources/test-architect.json}`. The check cannot fail for any producer-generated pair. Combined with `EXPECTED_IDENTITIES` mapping both `eventstore-owner` and `release-owner` to `github:jpiquot`, "three authenticated roles" resolves to one authenticated account plus a self-authored file. Spec AC3 requires "exactly the authenticated ... Test Architect"; the frozen Never forbids trusting "self-declared roles". The `_validate_receipts` docstring concedes only the GitHub trade-off, which does not describe this branch. DECISION: require an external anchor for the test-architect role, or formally accept and add the limitation to `REQUIRED_LIMITATIONS` (which is itself subject-bound). [tools/deployed_runtime_parity_handlers/v1.py:846-855]
- [x] [Review][Decision→Patch] RESOLVED 2026-08-25 (authorized completion): FULL ALLOWLIST -- dedicated Story 3.15 issue `#352` is now pinned as `STORY_3_15_ISSUE`; `FOREIGN_LINEAGE_ISSUES` was deleted, so 324/346/351 and every future sibling issue fail closed automatically and both owner receipts must resolve to the same thread. Four mutation cases plus the positive closure prove the allowlist. Original finding: Anti-splice protection was a two-element denylist that accepted Story 3.13 issue `#351`, arbitrary fresh issues, and two owner receipts from different threads. [tools/deployed_runtime_parity_handlers/v1.py]
- [x] [Review][Decision→Patch] RESOLVED 2026-08-25 (loop 4): KEEP DECORATIVE, REMOVE THE DUPLICATION HAZARD -- introduce one named constant for the `(login, id)` pair consumed by both `_validate_registry` and `_validate_receipts`, plus an assertion that the two paths agree, so re-rostering fails a check instead of silently splitting them. Deriving identities from the registry was rejected: the registry carries only `github:jpiquot` strings, not the numeric id, so deriving it would force a registry format change and a new roster comment for a latent-only risk. Original finding: The owner-role registry is validated but carries no authority -- `_validate_registry` asserts `registry["roles"] == {role: [EXPECTED_IDENTITIES[role]]}` and `role_lines != EXPECTED_IDENTITIES`, both comparisons against a module constant, so the registry is only ever proven to equal the hardcoded roster. `_validate_receipts` then independently re-hardcodes `login != "jpiquot"` and `id != 6775094` rather than deriving them from the validated registry. Re-rostering a role would leave the registry check accepting the new roster while the receipt check silently kept demanding the old account. The numeric id is a bare literal in two places with no named constant. DECISION: make the registry load-bearing (derive expected identities from it -- its digest is already subject-bound via `authority.owner_role_registry_sha256`), or keep it decorative and say so. [tools/deployed_runtime_parity_handlers/v1.py:673-679,710,839]
- [x] [Review][Decision→Patch] RESOLVED 2026-08-25 (authorized completion): COLLECT A 3.15-SCOPED ROSTER -- retained MEMBER-authenticated issue comment `5407975180` from dedicated issue `#352`, repointed `registry.authority_source`, required its `issue_url`, exact Story 3.15 body, and full owner-grade association, and removed the `CONTRIBUTOR` exception and Story-3.13 hardcodes. The retained source semantically matches the live GitHub API document. Original finding: the registry authority source was a Story-3.13-scoped CONTRIBUTOR comment from foreign issue `#324`. [tools/deployed_runtime_parity_handlers/v1.py]
- [x] [Review][Patch] Pinned-source verification is fail-open: tampered bytecode executes while every SHA-256 pin passes, turning the 0/3 packet into `pass` [tools/validate-corrected-deployed-runtime-parity.py:32-37]
- [x] [Review][Patch] Every recomputed-content guard is unreachable by its own test suite -- the 13-case theory pins `_verify_file`'s pre-check message, proving the branch is never entered [tools/deployed_runtime_parity_handlers/v1.py:506-551,575-593,673-679,743-745]
- [x] [Review][Patch] The `dispatch` live-file binding loop has no negative test; restricting it to `handler_binding` alone leaves the suite green [tools/deployed_runtime_parity_handlers/v1.py:319-327]
- [x] [Review][Patch] The "subject binds every decision input" equality has no negative case; weakening it to compare only `decision` keeps every test green [tools/deployed_runtime_parity_handlers/v1.py:895-903]
- [x] [Review][Patch] The byte-domain conflation guard is untested, and its second disjunct is green by construction -- `content` is already verified to hash to `nuget["sha256"]`, so `content == <predecessor bytes>` implies the first disjunct; it re-reads 14 `.nupkg` files to establish nothing [tools/deployed_runtime_parity_handlers/v1.py:485-489]
- [x] [Review][Patch] `_load_dispatch_metadata` raises an uncaught `TypeError` instead of a support-safe reason when `dispatch.handler.sha256` is unhashable; the sibling dispatcher had this exact bug fixed in the same diff by moving the check inside the `try` [tools/validate-corrected-deployed-runtime-parity.py:71]
- [x] [Review][Patch] The registry disclaimer regex still admits sentences asserting the opposite of what it requires -- `\bauthorizes no\b[^.;\n]*\bdeployment\b` matches "authorizes no changes, and authorizes deployment of any image" and "authorizes no obstacle to deployment"; one-sentence confinement does not put `deployment` in the scope of the negation. Every other limitation in the file is an exact-string match [tools/deployed_runtime_parity_handlers/v1.py:71]
- [x] [Review][Patch] Import-provenance is checked for 1 of the 4 executing modules -- `_verify_imported_file` covers only `deployed_runtime_parity_handlers.v1`; `release_evidence_handlers.v3` (which `v1.py:38-45` says "decide[s] most of the closure verdict") and both package initializers are hash-checked by path but never confirmed to be the modules importlib resolved. `_verify_dispatch_table` also omits the package-initializer coverage its sibling enforces, and the new dispatcher drops the sibling's post-import `EXPECTED_PACKET_CODEC_SHA256` cross-check with no replacement [tools/validate-corrected-deployed-runtime-parity.py:44-52,93-114]
- [x] [Review][Patch] The verifier never emits the rerun trigger on failure, though the frozen Always requires "fail closed with support-safe reason and rerun trigger"; `RERUN_TRIGGER` exists but is used only as a packet-field equality check [tools/validate-corrected-deployed-runtime-parity.py:148]
- [x] [Review][Patch] Dead allowlist branch plus a test that is green for the wrong reason -- `else: raise EvidenceError("acceptance source kind is not allowlisted")` is unreachable because `v1.py:825-826` already rejects any kind that is not `EXPECTED_SOURCE_KINDS[role]`, and `ReceiptSourceKindOutsideTheAllowlistFailsClosed` asserts only `ShouldContain("acceptance source kind")`, which the earlier message also satisfies [tools/deployed_runtime_parity_handlers/v1.py:857-858]
- [x] [Review][Patch] Symlinked entries evade the closed-inventory sweep -- files under a symlinked directory are missed by the `rglob` walk, and a dangling symlink is neither `is_file()` nor `is_dir()`, so it rides along inside the acceptance tree without being hashed [tools/deployed_runtime_parity_handlers/v1.py:751-763,779-785]
- [x] [Review][Patch] `xml.etree.ElementTree.fromstring` expands internal entities, so the billion-laughs shape is reachable through `predecessor_handler._nuspec_identity` on packet-supplied `.nuspec` bytes (verified: a nested-entity document parsed and expanded). Also a private cross-module call marked `# noqa: SLF001`, whose parameter is named `package_bytes` while every caller passes a path [tools/deployed_runtime_parity_handlers/v1.py:437; tools/release_evidence_handlers/v3.py:429]
- [x] [Review][Patch] `MANIFEST_FILE` is dead -- the same literal is re-hardcoded in the verifier's `validate()`; the new verifier also silently drops the `--manifest` override the sibling exposes, undocumented [tools/deployed_runtime_parity_handlers/v1.py:36]
- [x] [Review][Defer] v3's timestamp parser is looser than v1's, so frozen-predecessor timestamps are checked by the weaker rule [tools/release_evidence_handlers/v3.py:456-465] -- deferred, pre-existing
- [x] [Review][Defer] `size` has no upper bound and every retained/discovered file is read whole into memory [tools/deployed_runtime_parity_handlers/v1.py:161-185] -- deferred, pre-existing
- [x] [Review][Defer] `_verify_dispatch_table` and `_load_handler`'s consistency checks cannot fire with single-entry constant tables [tools/validate-corrected-deployed-runtime-parity.py:44-52] -- deferred, pre-existing
- [x] [Review][Defer] Smoke results bytes are never checked for canonical form [tools/deployed_runtime_parity_handlers/v1.py:554-560] -- deferred, pre-existing
- [x] [Review][Defer] All failures collapse to exit 1, so a tampered verifier is indistinguishable from invalid evidence [tools/validate-corrected-deployed-runtime-parity.py:145-150] -- deferred, pre-existing
- [x] [Review][Defer] `"closure.json"` is hardcoded into the closed inventory while the CLI accepts an arbitrary evidence path and `--packet-root` [tools/deployed_runtime_parity_handlers/v1.py:746] -- deferred, pre-existing
- [x] [Review][Defer] The `summary_bindings` deletion reduces `validate_packet_files`' standalone behavior inside a spec-frozen line range, leaving a vestigial `summaries` dict [tools/release_evidence_handlers/v3.py:944-952] -- deferred, pre-existing

### Review Findings (2026-08-25, trusted-verifier hardening pass)

- [x] [Review][Patch] Execute all four Story 3.15 trust-path modules only from their verified source
  bytes under sanitized import resolution; evict stale/preloaded module names and reject
  repository-local dependency shadows.
- [x] [Review][Patch] Make the Story 3.14 dispatcher source-only for its exact verified package
  initializer and v3 handler, with path/origin consistency and shadow/preload mutation coverage.
- [x] [Review][Patch] Strictly admit only UTF-8 nuspec XML and reject DTD/entity declarations before
  ElementTree parsing, including a UTF-16 bypass regression.
- [x] [Review][Patch] Require exact JSON integers for dispatch version and every aggregate/platform
  Production-smoke numeric fact; booleans and equal-valued floats fail closed.
- [x] [Review][Patch] Use one monotonic per-platform smoke deadline across pull, run, port discovery,
  readiness, and inspection; give every subprocess only the remaining time, require exact HTTP
  200/zero redirects, retain malformed curl failures, and contain cleanup command, timeout, and
  OSError paths with executable recording/failing fakes.
- [x] [Review][Patch] Correct the rerun trigger from `receipt-source change` to
  `receipt-source policy change`: individual post-subject source replacement invalidates its own
  receipt and the complete verdict, while policy changes re-mint the subject.
- [x] [Review][Patch] Use `three roster-bound role receipts` in operator-facing claims and preserve
  the explicit fact that both owner roles map to one authenticated human while Test Architect is
  self-attested.
- [x] [Review][Patch] Preserve the complete `dab64f5f...` acceptance/source tree byte-for-byte in
  `superseded-acceptances`, re-mint subject `a8cc777e...`, and initially leave the production packet
  stable and fail-closed at 0/3 without collecting replacement receipts. Fresh receipts were
  collected only in the subsequent separately authorized acceptance pass.

### Review Findings (2026-08-25, loop 6 -- full review, 4 layers x 4 diff chunks, 16 reviewers; none failed)

Scope: `git diff 1b2718c1..HEAD` (34 files, +2358/-317, 3666 lines) -- everything that landed after
loop 4's review: the 13 applied loop-4 patches, the trusted-verifier hardening pass, the 848-line
closure-suite growth, the new smoke-capture suite, the acceptance completion to 3/3, and two
submodule gitlink bumps. Byte-exact 4-way partition (858+414+1222+1172 = 3666). Every load-bearing
claim below was reproduced locally before triage; 12 plausible findings were refuted by running them
and are recorded as dismissed.

**Verified true at HEAD (contrast with Story 3.1, where evidence claims were false):** the verifier
reproduces `pass subject=a8cc777e... selected=4b141085...` exit 0; Contracts 1702/1702; focused
closure+capture 114/114; focused predecessor/provenance 34/34; both gitlinks reachable on their
submodule `origin/main`; loop-4 owner decisions 1-4 all landed; and **loop 4's headline `.pyc`
fail-open is genuinely closed** -- proven with a live control (a plain `import` picked up the
tampered bytecode while the source-only loader ignored it and kept the genuine rerun trigger).

**Landing-cost inversion (drives every decision below).** Loop 4 ran at 0/3, where a re-mint was
free. The packet is now at **3/3**, so every `v1.py` / verifier / `v3.py` edit re-mints the subject
and burns all three receipts. Test-only, record-only and ledger-only patches are free; verifier
patches must be batched into exactly one re-mint, landed together, and only then re-collected.

**Theme: no fail-open was found in the closure verdict.** The findings are concentrated in three
recurring classes -- guards that cannot fire, tests that pass for a different reason than they name,
and records that state properties the code does not have. This is the 10th-plus occurrence of that
family in this story lineage.

- [x] [Review][Decision->Patch] RESOLVED 2026-08-25 (loop 6): BIND A THIRD LIMITATION -- add the tooling-composed-receipt caveat to `REQUIRED_LIMITATIONS` so every receipt must repeat it verbatim, matching how the Test Architect caveat was bound in loop 4. Subject-bound, so it lands inside the single batch re-mint. Original finding: `v1.py:851-852` requires `created_at == accepted_at` AND `updated_at == accepted_at` to the exact second; the retained pair matches exactly (`10:33:29Z`, `10:33:45Z`). A human cannot author a comment whose embedded `accepted_at` equals GitHub's server-assigned `created_at`; it requires post -> read back -> retry, which the story record concedes by documenting two comments marked `SUPERSEDED -- INVALID TIMESTAMP-MISMATCH ATTEMPT`. So both owner acceptances are tooling-generated artifacts posted with the owner's write credential. `REQUIRED_LIMITATIONS` discloses the one-human and self-attested-TA facts but not this one. Frozen AC3 says "exactly the **authenticated** ... owner"; frozen Never forbids trusting "self-declared roles". DECISION: bind a third limitation disclosing tooling-composed receipts (re-mints, costs 3 receipts), or accept and record the caveat outside the subject, or relax the exact-second rule so a human-authored receipt is possible. [tools/deployed_runtime_parity_handlers/v1.py:851]
- [x] [Review][Decision->Patch] RESOLVED 2026-08-25 (loop 6): BIND PRODUCER DIGESTS ONLY -- add the capture script and assembler sha256/size to the closure `dispatch` block so future producer edits re-mint, and record explicitly that the retained 2026-08-21 smoke bytes were produced by the pre-image capture tool. Deliberately NOT re-capturing: that would replace evidence rather than bind it, and needs Docker plus arm64 binfmt emulation. Original finding: `closure.json` `dispatch` binds only `v1.py`, `v3.py`, `release_evidence_handlers/__init__.py` and the verifier. Neither `tools/capture-corrected-deployed-runtime-parity-smokes.py` (`cdd1ee3a...`) nor `tools/assemble-corrected-deployed-runtime-parity.py` (`73634031...`) is bound anywhere in the packet (verified by digest grep). This is precisely why this diff could change the smoke acceptance semantics -- from `200 <= status < 300` with a fixed `--max-time 5` (pre-image, `1b2718c1:100`) to exactly `200` with a computed budget -- **without invalidating a single receipt**. The retained smokes are timestamped `2026-08-21T19:24-19:26`, i.e. produced by the pre-image tool, so the tool of record can no longer reproduce the bytes it certifies. AC2 requires the subject to bind every decision input. DECISION: bind both producers (re-mints; may also require re-capture), or record the gap as an accepted limitation. [evidence/story-3-15/f343bb01.../closure.json]
- [x] [Review][Decision->Patch] RESOLVED 2026-08-25 (loop 6): RECORD AS DOCUMENTED PREREQUISITE -- state the pinned `tonistiigi/binfmt` digest as a required environmental precondition in the capture script docstring and the operator records. Not subject-bound: the emulation registration is host state, not an input byte the packet can hash, so binding the digest would record intent rather than proof. Original finding: the story pins `tonistiigi/binfmt` at `sha256:400a4873...` in prose only (`3-15-...-closure.md:143-144`). It appears nowhere in the packet, nowhere in `technical-sha256.txt` (24 bound files, verified), nowhere in the subject, and the capture script contains no `binfmt` reference at all. One of the two Production smokes AC1 requires is only valid given that registration. DECISION: bind the emulation digest into the packet (re-mints), record it as a documented environmental prerequisite, or accept. [tools/capture-corrected-deployed-runtime-parity-smokes.py]
- [x] [Review][Decision->Patch] RESOLVED 2026-08-25 (loop 6): RECORD AS KNOWN MISMATCH -- document that the retained roster comment names the artifact by its Story 3.13 filename and that the reference is understood to mean `owner-role-registry.json`. Correcting the text would require a new owner comment on `#352`, an Ask First external write; not performed. Original finding: `EXPECTED_REGISTRY_AUTHORITY_BODY` requires the retained comment to read "durable external authority_source for **reviewer-roster.json**", but the packet retains `registry/owner-role-registry.json`; `reviewer-roster.json` exists only under `evidence/story-3-13/`. The wording was copy-carried from Story 3.13. Because the body is exact-match-required, correcting it needs a **new owner comment on #352 plus a re-mint**. DECISION: correct and re-collect, or record the mismatch as known. [tools/deployed_runtime_parity_handlers/v1.py:74]
- [x] [Review][Decision->Patch] RESOLVED 2026-08-25 (loop 6): BATCH -- land every verifier-touching patch together in exactly one re-mint, then re-run the assembler, then collect three fresh receipts once. The re-mint drops the packet to 0/3; re-collection is an Ask First owner action and is NOT performed by this review. Original finding: at 3/3 each one costs three receipts. DECISION: batch all verifier patches into one landing then re-collect receipts once; or land only the free (test/record/ledger) patches now and defer the verifier set to a future re-mint.

- [x] [Review][Patch] Retained GitHub acceptance sources are not closed-schema validated -- REPRODUCED: injecting a stray field into `sources/eventstore-owner.json`, rebinding `durable_source` and the closure receipt binding, yields `pass` exit 0 at 3/3 with the subject **unchanged**, so the rerun trigger never fires. Every sibling structure uses `_exact_object`; this envelope is read entirely through `.get()`. Not a forgery vector (body, login, id, association, anchor and timestamps are all still enforced) but unbounded unreviewed content can persist invisibly inside the sole external authentication artifact [tools/deployed_runtime_parity_handlers/v1.py:844]
- [x] [Review][Patch] Registry authority-source authentication clauses have zero negative tests -- deleting the `author_association` clause, the `(login, id)` clause, or either `#352` URL equality leaves all 108 closure cases green, because the retained comment satisfies them and no test constructs one that does not. The guard is real (rewriting to `CONTRIBUTOR` does fail closed) but unpinned, so the CONTRIBUTOR exception loop 4 removed can silently return. The `(login, id)` clause -- the exact hazard the `OWNER_GITHUB_ACCOUNT` decision claimed to close -- is untested on both paths [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2070]
- [x] [Review][Patch] 11 of the verifier's 69 distinct fail-closed reasons have no assertion anywhere, including the ones the frozen block names -- `lineage does not reproduce the corrective release` (frozen Never: splice another release lineage), `predecessor identity is not the frozen Story 3.14 handoff` (AC1), `closure does not select a trusted live handler` (the primary route key), `closure bytes are not the selected codec's canonical UTF-8 form`, plus `raw OCI index shape is invalid`, `OCI image identity is invalid`, `OCI file binding is invalid`, `file binding path is unsafe`, `closure identity is invalid`, `package mapping lineage is invalid`, `NuGet.org package is not a valid signed archive`. All were shown reachable by live mutation [tests/.../CorrectedDeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] The assembler has no executable caller anywhere in the repo -- verified: no `.cs` test, no `tools/` caller, no workflow step invokes `assemble-corrected-deployed-runtime-parity.py`, yet its `receipts=3 verifier_exit=0` contract is asserted as evidence in four documents. Changing `return 0 if receipts == len(REQUIRED_ROLES) else 1` to an unconditional `return 0` leaves every test green. The always-exit-zero form of this exact defect already shipped once in this story and was fixed by hand [tools/assemble-corrected-deployed-runtime-parity.py:242]
- [x] [Review][Patch] The two operator-facing records have no drift guard while `docs/ci.md` has one -- `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` binds ci.md's digest set to `closure.json`, but nothing reads `3-15-...-closure.md` or the proof packet. Reverting both to their pre-change text (subject `5acb8176...`, "fails closed at zero of three receipts", exit 1) keeps the full Contracts suite green. This is not hypothetical: loop 3 recorded that these exact two files already drifted, asserting `bb58d691` and 3/3 against a `1dee194f` packet at 0 receipts [tests/.../CorrectedDeployedRuntimeParityClosureTests.cs:917]
- [x] [Review][Patch] `ImportedModuleProvenanceCoversTheCompleteVerifiedPath` is vacuous in both halves -- the guard it pins can never fail (below), and the test does not touch the call sites: `_verify_imported_file(handler, module_name.replace(".", "/") + ".py")` never contains the literal, so `ShouldContain($"\"{relative}\"")` matches the pin table at `:31`/`:39` instead; and `Count.ShouldBe(5)` counts the `def` line plus 4 calls. All four calls could be rewritten to name the same module and it stays green [tests/.../CorrectedDeployedRuntimeParityClosureTests.cs:895]
- [x] [Review][Patch] `_verify_imported_file` is tautological at all six call sites -- PROVEN empirically for all four modules: `_load_verified_module` sets `module.__file__` from the same `relative` the check re-derives `expected` from, so `actual == expected` by construction. Its docstring still asserts "importlib resolves through sys.path independently", but importlib no longer resolves these modules at all. Not a security loss (protection moved to `_verify_import_path` + `exec(compile(...))`) -- dead scaffolding plus a false comment [tools/validate-corrected-deployed-runtime-parity.py:234; tools/validate-corrective-release-evidence.py:157]
- [x] [Review][Patch] `CorrectiveDispatcherCannotReusePreloadedHandlerModules` cannot fail -- replacing the displacement loop body with `pass` produces a byte-identical green run, because `_load_verified_module` unconditionally overwrites `sys.modules[module_name]` before the handler is used. The test documents a protection it does not exercise [tests/.../CorrectiveOciProvenanceReleaseTests.cs:955]
- [x] [Review][Patch] The `rostered owner identity` guard is a self-comparison -- `EXPECTED_IDENTITIES` is *built from* `f"github:{OWNER_GITHUB_ACCOUNT[0]}"` at `:51-52` and then compared against that identical expression. It never asserts `OWNER_GITHUB_ACCOUNT[1]` (`6775094`, the half that actually authenticates), and a **third** roster copy now sits as literal text inside `EXPECTED_REGISTRY_AUTHORITY_BODY`. Loop 4's decision moved the duplication hazard rather than removing it [tools/deployed_runtime_parity_handlers/v1.py:315]
- [x] [Review][Patch] `duplicate_roles` and `role_lines != EXPECTED_IDENTITIES` are dead disjuncts -- whole-body equality at `:708` implies both, so neither can ever be the deciding term behind the shared message. The two tests written specifically to pin the last-wins `findall()` fix are green on the equality branch; deleting both clauses keeps every registry test passing [tools/deployed_runtime_parity_handlers/v1.py:726]
- [x] [Review][Patch] The receipt-tree symlink disjunct is unreachable -- CONFIRMED empirically: planting a symlink under the bound acceptance directory fails with `_validate_inventory`'s message (`packet contains a symbolic link outside the closed inventory`), never `_validate_receipts`'. `_validate_inventory` runs at `:900`, `_validate_receipts` at `:909`, and the packet-wide `is_symlink()` check precedes the bound-acceptances `continue` [tools/deployed_runtime_parity_handlers/v1.py:793]
- [x] [Review][Patch] `_is_repository_path` raises an uncaught `TypeError` on a bytes path -- verified: `Path(b"...")` raises `TypeError`, which is in neither the local `except (OSError, RuntimeError, ValueError)` nor `main()`'s catch tuple, so a bytes `sys.path` entry or module origin produces a traceback instead of the support-safe reason **and** the required `rerun:` line. Same defect class this diff fixes elsewhere. Present in both dispatchers [tools/validate-corrected-deployed-runtime-parity.py:113; tools/validate-corrective-release-evidence.py:85]
- [x] [Review][Patch] `STORY_3_15_ISSUE` governs only the receipt path -- `_validate_registry` re-hardcodes `352` inside two full URL literals, so changing the dedicated issue moves the receipt allowlist while leaving the registry check bound to the old thread. Identical duplication hazard to the one `OWNER_GITHUB_ACCOUNT` was introduced to close [tools/deployed_runtime_parity_handlers/v1.py:714]
- [x] [Review][Patch] Smoke cleanup is skipped, not bounded, in the exact failure mode it exists for -- `run(deadline, "docker", "rm", "--force", ...)` reuses the already-exhausted platform deadline, and `remaining_seconds` raises **before** `subprocess.run`. Reproduced: with the readiness loop burning the budget, the docker argv log contains only `pull`, `run`, `port` -- no `rm --force` at all -- while stderr claims the command "timed out after N seconds" and the retained record says `cleanup: "failure"`. The container and its published host port leak; the evidence and the operator message both assert an attempt that never happened [tools/capture-corrected-deployed-runtime-parity-smokes.py:149]
- [x] [Review][Patch] The readiness retry loop never runs a second iteration in any test -- all five capture cases break on attempt 1. Replacing the accept predicate with an unconditional `exit_code = 0; break` passes the entire suite, and the bounded sleep at `:130` is executed by no test. The poll/retry/backoff behaviour the utility exists for is completely unpinned. Also assert the `--max-time` operand value, not just the flag's presence [tests/.../CorrectedDeployedRuntimeParitySmokeCaptureTests.cs]
- [x] [Review][Patch] The smoke duration guard is unfireable by construction -- `deadline` is set at `:66` **before** `started_at` at `:67`, so `(end - start) <= timeout_seconds` always holds for evidence this script produces, and `v1.py:646` can never reject on it. `timeout_seconds` also silently changed meaning (per-command -> whole-platform deadline) with no schema bump, while `v1.py:595` still hard-requires `== 180` [tools/capture-corrected-deployed-runtime-parity-smokes.py:66]
- [x] [Review][Patch] Neither producer emits the rerun trigger on failure -- the frozen Always requires "fail closed with support-safe reason **and** rerun trigger". Loop 4 fixed this for the parity verifier (confirmed: the `rerun:` line is now emitted) but the capture script has no `RERUN_TRIGGER` at all, and the sibling 3.14 dispatcher still prints failures without it. The sibling's `--manifest` default is also cwd-relative, so it breaks when invoked from anywhere but the repo root [tools/capture-corrected-deployed-runtime-parity-smokes.py:211; tools/validate-corrective-release-evidence.py:236]
- [x] [Review][Patch] Running the capture against a live packet root destroys retained evidence -- `mkdir(parents=True, exist_ok=True)` plus unconditional `write_bytes` overwrites the three hash-bound smoke files with failure records, recoverable only by `git checkout`. Add a refuse-if-populated guard or an explicit `--force` [tools/capture-corrected-deployed-runtime-parity-smokes.py:191]
- [x] [Review][Patch] The 3.14 exact-JSON-integer guard is untested and silently removable -- demonstrated: deleting the three new lines in a scratch copy makes `"codec": {"version": 3.0}` print `pass: sha256:f15f8c...` exit 0, because `hash(3.0) == hash(3)` satisfies the `HANDLERS` tuple lookup and `3.0 != 3` is `False` downstream. The 3.15 sibling has this coverage; the 3.14 one does not [tests/.../CorrectiveOciProvenanceReleaseTests.cs]
- [x] [Review][Patch] Both stale-bytecode tests lack the control that makes them meaningful -- neither asserts a `.pyc` was actually produced, and neither has a positive control proving the marker *would* execute under an ordinary import. (My own probe needed exactly that control to be conclusive.) Also exclude `__pycache__`/`*.pyc` from `CopyDirectory`, which currently copies developer-local bytecode into the very tree the test controls [tests/.../CorrectedDeployedRuntimeParityClosureTests.cs:1768]
- [x] [Review][Patch] v3.py's XML hardening is imprecise in both directions -- the DTD scan runs over the whole document, so a legitimate nuspec merely mentioning `<!DOCTYPE` in `<description>`/`<releaseNotes>` is rejected; and the XML-declaration regex uses `[^?]*`, so a declaration containing `?` fails to match and the encoding check is silently **skipped** rather than failing closed [tools/release_evidence_handlers/v3.py:443]
- [x] [Review][Patch] The ledger and the spec still assert that the Ask First actions were NOT performed -- both stale at HEAD: `deferred-work.md:1575` "Opening it and requesting acceptances is an Ask First action and was not performed", and `spec:384` "remain blocked Ask First owner actions and were not fabricated". Issue #352 was opened, at least seven comments were posted and two edited, and the receipts were collected. Append superseding entries (the ledger is append-only) [_bmad-output/implementation-artifacts/deferred-work.md:1575]
- [x] [Review][Patch] Ledger hygiene in the appended block -- four-plus entries duplicate still-open loop-3 items, two are duplicated *within* the new block (the v3-vs-v1 timestamp parser; the hardcoded `closure.json` inventory path), the 11-13 entries from `:1639` omit the `severity:` field their block-mates carry, they switch `source_spec` to absolute machine-local paths mid-block, and they sit under a heading that names a different review pass. Nothing enforces the format: every Dw6 governance test is `[Fact(Skip = ...)]` [_bmad-output/implementation-artifacts/deferred-work.md:1639]
- [x] [Review][Patch] Sprint tracker, spec frontmatter and the story record disagree three ways -- `sprint-status.yaml:231` says `review`, `spec:5` says `status: 'done'`, the story declares parity available. `last_updated: '08-25-2026 09:58'` predates every event it records (subject minted `10:17:36Z`, receipts `10:33:29Z`-`10:34:41Z`), and no comment records issue `#352`, subject `a8cc777e...`, or the self-attestation caveat -- the exact omission loop 2 patched for the Story 3.13 row [_bmad-output/implementation-artifacts/sprint-status.yaml:231]
- [x] [Review][Patch] `_bmad-output/test-artifacts/gate-decision.json` is three re-mints stale and states the opposite verdict -- its rationale names subject `bb58d691` "re-minted to `5acb8176`" and "the packet fails closed at 0 of 3", against a packet that now passes at `a8cc777e...` 3/3 [_bmad-output/test-artifacts/gate-decision.json:13]
- [x] [Review][Patch] Story record and proof packet lost or contradict operator-facing facts -- the heading "Why the subject changed **five** times" is immediately followed by "**Three** 2026-08-25 review loops each re-minted the subject"; the proof packet dropped the technical-inventory digest and file count, the whole "Authority boundary" section naming the four flags an auditor must check, the trust-chain explanation of why receipts sit outside the inventory, and the runnable assembler command (replaced by an unexecutable prose claim); and `git diff --check` is recorded as "recorded at final handoff" rather than an outcome [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:29]
- [x] [Review][Patch] Spec reading-guide anchors drifted -- verified: `v1.py:79` is a line of the roster body, not the `#352` allowlist (that is `:94`, enforced at `:882`); `v1.py:861` is `"test_architect": "bmad:murat"`, not the comment-field check; `ci.md:460` is two lines above its target. The loop-4 "landing window" paragraph also still says "the packet is already at 0/3 receipts, so no acceptance is burned by a re-mint right now", which is now exactly inverted [_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md:497]
- [x] [Review][Patch] Superseded receipts carry dangling source paths -- verified for **both** sets: every superseded receipt declares `durable_source.file = acceptances/<subject>/sources/...`, a path that exists nowhere; the files actually live under `superseded-acceptances/<subject>/sources/`. Harmless to the verifier, but an auditor cannot mechanically re-pair a superseded receipt with its source, which is the entire purpose of retaining them. `superseded-acceptances/README.md` gives no re-rooting rule and still narrates only the first supersession [evidence/story-3-15/superseded-acceptances/]
- [x] [Review][Patch] Test-suite hygiene -- `SyntheticAcceptanceIssue` now holds `352`, the real issue, so the name and doc comment contradict the value and fixture receipts are byte-shaped as fully valid acceptances binding the real current subject; `ForeignLineageIssues.ShouldContain(issue)` is a tautology over the theory's own `InlineData`; `FrozenStory314PacketRemainsByteForByteUnchanged` still proves immutability by hashing one file while this chunk adds four more tests that reach into that packet; negative receipt coverage remains `receipts[0]`-centric so `release-owner` is never the mutated receipt; and the capture suite hardcodes `/usr/bin/python3` where every sibling uses PATH-resolved `python3` and relies on `[SupportedOSPlatform]`, an analyzer attribute, instead of a runtime skip [tests/.../CorrectedDeployedRuntimeParityClosureTests.cs:96]

- [x] [Review][Defer] `redirect_count == 0` is structurally unfireable (the capture never passes `--location`), and the new test now asserts `ShouldNotContain("--location")`, converting an acknowledged deferral into a pinned invariant [tools/deployed_runtime_parity_handlers/v1.py:639] -- deferred, pre-existing
- [x] [Review][Defer] `_verify_no_repository_import_shadows` runs only on the success path, after the code it guards against has executed, and no test reaches it with a repository module loaded [tools/validate-corrected-deployed-runtime-parity.py:164] -- deferred, pre-existing
- [x] [Review][Defer] v3's timestamp parser is looser than v1's, so frozen-predecessor timestamps are checked by the weaker rule [tools/release_evidence_handlers/v3.py:456] -- deferred, pre-existing
- [x] [Review][Defer] `size` has no upper bound and every retained file is read whole into memory; `v3.py` still does an uncapped `archive.read()` on the nuspec entry, so the decompression-bomb half of the ledger entry remains open [tools/deployed_runtime_parity_handlers/v1.py:161] -- deferred, pre-existing
- [x] [Review][Defer] All failures collapse to exit 1, so a tampered verifier is indistinguishable from invalid evidence; and every `_load_verified_module` failure collapses to one message that hides the chained cause [tools/validate-corrected-deployed-runtime-parity.py:195] -- deferred, pre-existing
- [x] [Review][Defer] Roughly 90 lines of security-critical loader code are duplicated across the two dispatchers with divergent signatures and no test that keeps the twins in sync [tools/validate-corrective-release-evidence.py:85] -- deferred, pre-existing
- [x] [Review][Defer] Several distinct fail-closed branches share one message, so no test can show which clause fired -- `GitHub acceptance source is not authenticated to the rostered owner` covers eight or-ed conditions [tools/deployed_runtime_parity_handlers/v1.py:855] -- deferred, pre-existing
- [x] [Review][Defer] The two owner comments rejected for timestamp mismatch (`5409140199`, `5409147909`) are named in three documents but retained nowhere, so the claim that they were marked superseded is unverifiable offline; the same annotation practice was not applied to the `dab64f5f` pair, which remains acceptance-shaped JSON on the now-allowlisted `#352` thread [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:70] -- deferred, pre-existing
- [x] [Review][Defer] The Code Map's frozen fence on `CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235` was extended (156 lines inserted at `:895`, file 1291 -> 1447) and its anchors were not refreshed; the `1124-1235` range now lands on unrelated helpers [_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md:44] -- deferred, pre-existing
- [x] [Review][Defer] Two submodule gitlink bumps (`references/Hexalith.FrontComposer` `a229be7e`->`596e286f`, `references/Hexalith.Tenants` `09c746b3`->`daf6c76c`) rode into commit `67c645ab` undeclared, while the spec entry in that same commit asserts "no ... commit, or push action was performed". Both targets verified reachable on their submodule `origin/main`, so this is unrecorded scope, not a dangling pointer -- deferred, pre-existing
- [x] [Review][Defer] `RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` is build-state dependent -- it shelled out to `dotnet publish` with RIDs and failed once on `NETSDK1047`, then passed on an identical re-run [tests/.../CorrectiveOciProvenanceReleaseTests.cs:55] -- deferred, pre-existing
- [x] [Review][Defer] Nothing enforces deferred-work ledger format: every `Dw6*` governance test is `[Fact(Skip = ...)]` and both `Dw4` ATDD cases are skipped [tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/] -- deferred, pre-existing

**Dismissed as refuted (12), reproduced against the running code -- do not re-raise:**
`--manifest` accepting an arbitrary path does **not** steer the verdict (`packages["manifest_sha256"]` is compared to the caller-supplied manifest's digest at `v1.py:385` and is subject-bound at `:270`, so a foreign manifest fails closed).
"The first transient curl failure aborts the readiness loop" -- curl emits `000 0` on connection-refused (verified, exit 7), which parses cleanly and retries.
`PYTHONPYCACHEPREFIX` does **not** make the stale-bytecode tests vacuous -- `py_compile` and `importlib` both route through `cache_from_source`, verified symmetric.
"Editing `deployed_runtime_parity_handlers/__init__.py` re-mints nothing" -- its pin lives inside the verifier, whose sha256 **is** subject-bound, so the chain closes transitively.
Empty `PLATFORMS` is not a closure fail-open -- `all([])` makes the capture tool print pass, but `v1.py` requires the exact two-platform list, so the packet still fails closed (operator-misleading only).
`attempts: 0` producing "structurally invalid evidence" -- that is a failure log; failing validation is correct behaviour.
The chunk-2 "deletions" (2xx -> exactly 200, computed `--max-time`, the returncode gate) are the spec's own recorded hardening decisions, not regressions.
The parity verifier's `sys.dont_write_bytecode` comment is accurate (only the sibling's is loose).
`validate()`'s signature change is internal with no external callers.
`validate()` reentrancy is not a real consumer scenario for a single-shot CLI.
Removing the predecessor byte comparisons in `_validate_packages`/`_validate_oci` is genuinely redundant, not a weakening.
The deleted `else: raise ... "acceptance source kind is not allowlisted"` was already unreachable behind the role->kind check.

### Review Findings (2026-08-25, loop 7 -- full review, 3 layers x 4 chunks, 12 reviewers; none failed)

Triage: no `intent_gap`, no `bad_spec`; every finding routed `patch` and every one applied in this
pass. **Landing cost was zero**: the packet sat at 0/3 receipts, so the re-mint this pass forced
burned nothing. That is the inverse of loop 6's constraint and is why the whole set landed together.

**Theme: two of loop 6's own fixes were regressions.** Narrowing the nuspec DTD scan and adding
`TypeError` to a path-resolution catch each closed the finding they were written for while opening a
new hole. Both were reproduced here with live controls before being fixed.

**blocking (fixed in this pass, reproduced with a live control):**

- [x] [Review][HIGH] **Fail-open in the nuspec prolog scan, introduced by loop 6.**
  `_reject_prolog_declarations` did a bare `return` when the first non-space character was not `<`.
  `utf-8-sig` strips exactly one BOM, so a doubled `EF BB BF` left a residual `U+FEFF`, the scan
  returned without inspecting anything, and expat then consumed the re-emitted BOM and parsed the
  DTD behind it. **Reproduced end to end:** with the fix reverted in a scratch copy, a nuspec of
  `BOM + BOM + <?xml?> + <!DOCTYPE package [<!ENTITY smuggle "Hexalith.Evil">]> + <id>&smuggle;</id>`
  is ACCEPTED and returns id `Hexalith.Evil`; with the fix in place it is rejected, and the
  single-BOM control is still rejected on the DTD itself. Fixed by rejecting any residual `U+FEFF`
  after decode and raising instead of returning when the prolog does not begin with `<`, so every
  exit is either "reached the document element" or a fail-closed reason.
  [tools/release_evidence_handlers/v3.py]
- [x] [Review][HIGH] **Bytes-path guard bypass in both dispatchers, introduced by loop 6.** Adding
  `TypeError` to `_is_repository_path`'s catch silenced the crash by answering False for a bytes
  repository path, so such a module escaped displacement *and*
  `_verify_no_repository_import_shadows` -- a loud crash traded for a silent guard bypass.
  **Reproduced:** before the fix `str -> True`, `bytes -> False`; after it `bytes -> True`, and
  reverting the fix in a scratch copy flips it back to False. Fixed with `os.fsdecode` before
  `Path(...).resolve()`, keeping `TypeError` only as a backstop, and `TypeError` was added to both
  `main()` catch tuples so the comment claiming that coverage is now true.
  [tools/validate-corrected-deployed-runtime-parity.py; tools/validate-corrective-release-evidence.py]

**patch (fixed in this pass):**

- [x] [Review][Patch] `_verify_roster_configuration` was green by construction -- the exact defect
  its own docstring claimed to fix. `EXPECTED_IDENTITIES[role] != f"github:{login}"` compared the
  table against the expression it was built from, and the derived role-line block was checked
  against a body interpolated from that same block, so rewriting `OWNER_GITHUB_ACCOUNT` to
  `("mallory", 999)` left it green. **Reproduced, then fixed and re-checked live:** the roster body
  is now held as the verbatim authenticated literal, `RATIFIED_OWNER_GITHUB_ACCOUNT` is asserted
  explicitly so the numeric half that actually authenticates is bound, and the test mutates the
  handler in a copied tool tree while rebinding the dispatcher pin and the closure dispatch digest
  so execution reaches the guard. Both `("mallory", 999)` and `("jpiquot", 999)` now fail closed.
- [x] [Review][Patch] `NuspecPrologDtdScanIsPreciseInBothDirections` passed identically against the
  regex it replaced, so it pinned nothing: its accepted fixture used the escaped `&lt;!DOCTYPE`,
  which never matched the old pattern. Replaced with a positive test using a CDATA-quoted literal
  `<!DOCTYPE` (red under the old regex, green under the scanner) and a separate negative test for a
  DTD hidden behind a prolog comment that quotes a tag.
- [x] [Review][Patch] The capture could emit records its own verifier rejects: `started_at` is
  stamped before the platform deadline and `ended_at` after a fresh 30s cleanup budget, so a
  platform window can reach 210s and the aggregate 420s, while the verifier capped them at 180 and
  360. The per-platform bound is now the platform budget plus a `CLEANUP_ALLOWANCE_SECONDS`
  constant and the aggregate is the sum across platforms. The allowance is verifier-side rather than
  a new field in `smoke-results.json` **because the retained smoke bytes are frozen evidence and
  must not be rewritten to satisfy a later schema**; a focused test pins it against the capture
  tool's own `CLEANUP_TIMEOUT_SECONDS`. Both bounds now have breach cases, plus an acceptance case
  at 205s that the old bound would have rejected.
- [x] [Review][Patch] The stale gate was only half-withdrawn: `e2e-trace-summary.json` still read
  `PASS`/`MET`/`100%` and `traceability-matrix.md` still declared `collectionStatus: 'COLLECTED'`,
  while its sibling had moved to `SUPERSEDED`. Every status and coverage field in both is now
  withdrawn.
- [x] [Review][Patch] `closure.json` carries `deployed_runtime_parity: "available"` and a
  `selected_deployed_identity` at zero receipts. Confirmed **not** a verdict fail-open -- the
  verifier exits 1 -- but an auditor grepping the JSON read the opposite of every record. Both
  records, the superseded README and `docs/ci.md` now state that these are the packet's *claim*,
  granted only at 3/3; both fields were added to the Authority boundary tables; the drift guard now
  reads `deployed_runtime_parity` instead of inferring the verdict from the receipt count; and
  `acceptances.directory` is documented and asserted as the address receipts must occupy rather
  than a directory that exists.
- [x] [Review][Patch] Three artifacts stated three different re-mint counts (four, five, six).
  All now say seven subjects across six re-mints, and the superseded README says plainly that three
  re-mints never had receipts collected. The README's re-rooting rule gained its missing second
  half: hash the re-rooted file against `durable_source.sha256`.
- [x] [Review][Patch] Test hygiene: both mutation theories funnelled their last case through
  `default:` (a typo'd `InlineData` silently duplicated it) -- now explicit `case` labels with a
  throwing `default`; the two pre-existing receipt theories gained the positive control the new
  tests already had; the registry theory gained a `registry["created_at"]` case and a
  consistently-rewritten other-comment case, so `REGISTRY_AUTHORITY_COMMENT_ID` can no longer be
  deleted with the theory green; the assembler negative now uses `ShouldFailClosed` rather than
  `ShouldNotBe(0)`, which a traceback satisfies; every `ShouldAllBe` over `platforms` is preceded by
  a count assertion; and `EveryTrustPathModuleExecutesOnlyPreVerifiedSourceBytes` now reads the call
  sites' argument shape instead of grepping the pin table.
- [x] [Review][Patch] Missing drift bindings: `sprint-status.yaml` and this spec now sit in a
  subject-drift theory alongside the two markdown records, and the proof packet's tool-digest table
  is bound to the closure's `dispatch` block, exactly and with no stale row allowed.
- [x] [Review][Patch] Capture ergonomics: `budget` resolves inside `remaining_seconds` instead of
  freezing the constant at def time, `RawDescriptionHelpFormatter` keeps the pinned binfmt command
  copy-pasteable, the `--force` refusal exits **2** (distinct from a genuine smoke failure's 1) and
  its rerun text names `--force` and the empty-root alternative rather than telling the operator to
  re-run the command the guard just refused, and `iterdir()`/`mkdir()` are guarded against `OSError`
  and `FileExistsError`.
- [x] [Review][Patch] Assembler trust surface: `sys.dont_write_bytecode` is set before the handler
  import, the imported handler modules must resolve to their repository paths, the assembler binds
  `Path(__file__).resolve()` -- the bytes actually executing -- and refuses to run from anywhere but
  the bound repository path, and its own failures print the fail-closed reason plus the rerun
  trigger. Stale `__pycache__` trees were removed from `tools/`.

## Spec Change Log

- **2026-08-30 (trusted verifier and packet producers):** Closed all 14 Chunk-1 review findings.
  Both verifier entry points now cross an isolated, no-site interpreter boundary before shadowable
  imports. The Production capture bypasses proxies, rejects unsafe output paths, owns cleanup only
  after successful container creation, and reports write failures support-safely. The assembler
  validates retained inputs before indexing, rejects unsupported packet entries, writes only inside
  the packet, and bounds/support-safely handles its child verifier. The live handler requires exact
  integer facts, distinct GitHub comment identities, regular packet files, canonical smoke-result
  bytes, non-future smoke windows, and bounded timestamp-transition overhead. Executable tests cover
  each new boundary, including all three assembler smoke rejection branches. Binding the updated
  decision inputs re-minted subject `663747b1...` to
  `86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`; repeat assembly reports
  `receipts=0 verifier_exit=1`. No receipts existed to reject, no replacement receipts were
  collected, and no deployment, publication, registry mutation, consumer removal, predecessor
  change, commit, or push was performed.

- **2026-08-25 (loop 7 landed):** The complete loop-7 patch set landed in one re-mint at zero
  receipts, so nothing was burned. Two loop-6 fixes were regressions and both were reproduced with
  live controls before being closed: the nuspec prolog scan skipped entirely behind a residual
  byte-order mark (a smuggled entity resolved into the package id), and the bytes-path catch turned
  a crash into a silent guard bypass in both dispatchers. Also fixed: the roster-configuration guard
  was green by construction, the nuspec precision test pinned nothing, the capture could emit smoke
  records its own verifier rejected, the stale gate artifacts were only half-withdrawn, the
  claim-versus-verdict fields contradicted every record, three artifacts disagreed on the re-mint
  count, nine test-hygiene defects, two missing drift bindings, five capture ergonomics items, and
  the assembler's trust surface. The subject moved from `e27f9f39...` to
  `663747b158387d00b55058b0a259a20655d509a32f60c298c02e2645b3aa4f31`; the packet remains at
  `receipts=0 verifier_exit=1`, parity **unavailable**, nothing granted. Collecting three receipts
  on issue `#352` remains an **Ask First** owner action and was **not** performed. No deployment,
  publication, registry mutation, consumer removal, predecessor change, commit, or push was
  performed.

- **2026-08-25 (loop 6 batch landed):** The complete loop-6 patch set landed as one re-mint, exactly
  as authorized below. Verifier-touching changes: both packet producers bound in the closure
  `dispatch` block; the retained GitHub comment envelopes closed-schema at envelope, user and
  reaction level; a fourth `REQUIRED_LIMITATIONS` entry disclosing tooling-composed receipts; the
  roster body's role lines derived from the identity table and the roster-configuration check made
  able to fail; the registry path's `#352` URLs derived from `STORY_3_15_ISSUE`; the role-mapping
  and body checks split so neither is a dead disjunct; the unreachable receipt-tree symlink disjunct
  removed; `TypeError` added to both dispatchers' path-resolution catch; the tautological
  `_verify_imported_file` removed from both dispatchers; the nuspec DTD scan narrowed to the XML
  prolog and the XML-declaration match made fail-closed; cleanup given its own budget so it is
  bounded rather than skipped; `started_at` stamped before the platform deadline; a
  refuse-if-populated guard plus `--force` on the capture; and rerun triggers emitted on failure by
  the capture and the Story 3.14 dispatcher, whose `--manifest` default is now script-relative.
  Test and record changes: negative cases for every roster-comment authentication clause, the
  reproduced stray-field defect, the fail-closed reasons the frozen block names, an executable
  caller for the assembler, drift guards for both operator records, a real import-provenance test, a
  non-vacuous preloaded-module test, a 3.14 exact-integer guard test, positive controls on the
  stale-bytecode test, `__pycache__` excluded from the test tree copy, a whole-tree digest for the
  frozen 3.14 packet, role-rotated receipt negatives, and a PATH-resolved interpreter with a runtime
  skip in the capture suite.

  The re-mint moved the subject from `a8cc777e...` to
  `663747b158387d00b55058b0a259a20655d509a32f60c298c02e2645b3aa4f31` and, by the packet's own rerun
  trigger, **rejected all three `a8cc777e...` receipts**. That tree was moved unmodified to
  `evidence/story-3-15/superseded-acceptances/a8cc777e.../`, which now also carries a re-rooting rule
  for auditors. The packet is at `receipts=0 verifier_exit=1`; parity is **unavailable** and no
  identity is selected. Collecting three fresh receipts on issue `#352` remains an **Ask First**
  owner action and was **not** performed. No deployment, publication, registry mutation, consumer
  removal, predecessor change, commit, or push was performed.

  Four loop-6 findings were resolved by recording rather than by code: the roster comment's
  `reviewer-roster.json` wording, the QEMU emulation prerequisite, the fact that the retained smoke
  bytes predate the now-bound capture tool, and the structural unreachability of the
  `raw OCI index shape is invalid` branch. All four are in `deferred-work.md` and in the operator
  records.

- **2026-08-25 (loop 6 landing authorization -- BATCH):** The owner authorized landing the complete
  loop-6 patch set as a single batch. Every verifier-touching patch (`v1.py`, `v3.py`, the parity
  verifier, the closure `dispatch` block including the newly bound producer digests, and the third
  `REQUIRED_LIMITATIONS` caveat) lands together in exactly **one** re-mint alongside the free
  test-only, record-only and ledger-only patches. The re-mint invalidates the three retained
  `a8cc777e...` receipts and returns the packet to `receipts=0 verifier_exit=1`; the superseded
  receipt/source tree is retained unmodified for audit. Collecting three fresh receipts on issue
  `#352` for the new subject remains an **Ask First** owner action and is explicitly **not**
  performed by this run. Until it happens, deployed-runtime parity is **unavailable** and no
  identity is selected.

- **2026-08-25 (authorized acceptance completion for hardened subject):** With renewed authorization
  for exact unchanged subject
  `a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`, retained EventStore-owner
  issue comment `5409145568`, Release-owner issue comment `5409148235`, and the Test Architect
  `bmad:murat` self-attested record. Reassembly remains on `a8cc777e...`, reports `receipts=3
  verifier_exit=0`, and the verifier selects only OCI index
  `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`; deployment,
  publication, registry-mutation, consumer-removal, and predecessor-change authority flags all
  remain false. Timestamp-mismatched attempts `5409140199` and `5409147909` were immediately marked
  visibly superseded and are not retained as packet sources. Both owner roles still map to one
  authenticated human; the Test Architect record remains explicitly self-attested.

- **2026-08-25 (trusted-verifier hardening):** Closed all eight review findings above in one re-mint.
  The current subject is
  `a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`; repeat assembly reports
  `receipts=0 verifier_exit=1`. The full `dab64f5f...` receipt/source tree is retained unmodified in
  the superseded audit area. No replacement acceptance, deployment, publication, registry,
  consumer, predecessor, commit, or push action was performed. Verification: Contracts Release
  build 0W/0E; Story 3.15 closure and capture suites 114/114; predecessor/provenance suite 34/34;
  full Contracts suite 1702/1702; no skipped tests.

- **2026-08-25 (authorized acceptance completion):** Created dedicated Story 3.15 issue
  [#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352), retained its
  MEMBER-authenticated roster comment `5407975180`, replaced the issue denylist with a positive
  `#352` allowlist, and removed the cross-story roster and `CONTRIBUTOR` exception. Binding the new
  handler and registry bytes re-minted subject `93559e61...` ->
  `dab64f5fbbf55783630ad75451d35d517d829e194fb618dc8b0526d39761d38d` before any final receipt was
  collected. With renewed exact-subject authorization, EventStore-owner comment `5408186984`,
  Release-owner comment `5408189299`, and the Test Architect `bmad:murat` record were retained
  beneath the new subject. The assembler and verifier pass at 3/3, select only OCI index
  `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`, and preserve every
  deployment/publication/registry/consumer/predecessor authority flag as false. One first owner
  comment crossed GitHub's one-second timestamp boundary; it was visibly marked superseded and is
  not retained. Focused suite: 98 passed, zero failed or skipped; complete Contracts suite:
  1683 passed, zero failed or skipped; Contracts Release build: 0W/0E.

- **2026-08-25 (loop 4, implementation):** Closed every unblocked verifier-core finding with
  mutation evidence: the dispatcher now compiles only the four verified source files and confirms
  every imported path, so stale bytecode cannot replace reviewed bytes; malformed dispatch metadata
  fails support-safely; every failure prints the exact rerun trigger; and `--manifest` is restored.
  The handler now binds the Test Architect self-attestation limitation into all receipts, shares one
  named owner `(login, id)` account across registry and receipt checks, requires the retained roster
  comment body exactly, rejects packet/acceptance symlinks, exposes a safe public nuspec inspector
  that rejects DTD/entity declarations, removes the redundant package-domain byte reread and dead
  source-kind branch, and mutation-proves semantic OCI/smoke/registry/inventory, dispatch, subject,
  and byte-domain guards after their local bindings are corrected. The focused suite is 96/0/0 and
  the Contracts Release build is 0W/0E. These verifier changes re-minted the zero-receipt subject
  `5acb8176...` -> `93559e6134c16d15e295b7c3fbf83d959e86da75d2dfe4201ffdde4d42ac39a0`;
  the checked-in packet remains intentionally fail-closed. The dedicated Story 3.15 issue and
  Story-3.15-scoped roster source remain blocked Ask First owner actions and were not fabricated.

- **2026-08-25 (loop 3, review):** A 27-reviewer pass over 9 diff chunks found the two Story 3.15
  records still asserting subject `bb58d691` and 3/3 receipts against a packet at `1dee194f` with
  zero receipts, and eight verifier defects whose guards did not hold the property they stated.
  Hardening the verifier changed `v1.py`, so by the packet's own rerun trigger the subject was
  re-minted a second time: `1dee194f...` -> `5acb81765201a22d6493d815a56f4b8d9c1ba141280779716013962eca3fa5f5`.
  No receipt was burned -- the packet was already fail-closed at 0/3 and the earlier receipts were
  already superseded, which is why this was the cheapest moment to fix the verifier. `docs/ci.md`,
  both story records, and the dispatcher pin were updated to the new subject; the assembler is
  deterministic and idempotent across repeat runs. Known-bad state avoided: shipping a closure whose
  operator-facing records claim an accepted identity the verifier rejects. KEEP: the packet must stay
  fail-closed at 0/3 until receipts are collected on a *dedicated* Story 3.15 issue against the exact
  bytes of `5acb8176...`; issues `#324` and `#346` are rejected by number, and every new guard is
  mutation-checked with a positive control.

- 2026-08-25 (code review loop 2, amend-and-re-freeze): a 15-reviewer pass over the full baseline
  diff found two reproduced trust defects. The `release_evidence_handlers` package initializer
  executed unhashed **and the packet still validated `pass`, exit 0**; and the transitively imported
  `v3.py` -- which performs most of the closure's validation -- was bound nowhere, so a tampered
  verifier produced the identical subject and identity with all three receipts intact, violating
  frozen AC2 and the closure's own rerun trigger. Both are fixed: every import-path file is pinned
  and verified before the first import, and `dispatch` now binds `v3.py` and both package
  initializers. Also hardened the registry duplicate-role and disclaimer gates, added acceptance-tree
  closure, gave both tamper tests positive controls, made the `docs/ci.md` digest check exact, and
  replaced the vacuous raw-binary guard with an enumeration that revealed 14 unprotected
  digest-bearing `.raw` files (now covered by new `story-3-13/**` rules).
  Binding those bytes changed `v1.py`, so by the rerun trigger the canonical subject moved
  `bb58d691...` -> `1dee194f93612c0861b536023bdb20cb329ad0adfd12f5eafe87913b90c81f26` and **all three
  receipts collected on 2026-08-22 were rejected**. They are genuine and were not deleted: they are
  retained outside the packet root at `evidence/story-3-15/superseded-acceptances/` with a README
  explaining why they no longer bind. The packet is back to **fail-closed at 0 of 3** and selects no
  identity until the three roles accept the new subject -- an Ask First action, not taken here.
  Focused suite 52/0/0 (was 48); Contracts 1633/1633; Contracts Release build 0W/0E.


- 2026-08-21: Implemented and mutation-proved the trusted v1 verifier; independently retained both
  package byte domains, the raw OCI graph, and two passing bounded Production smokes. Canonical
  subject `e6016c0f612ad630647ee2abe286bed345830433cce01f89c49ee687a4f3d522` remains fail-closed
  because the three Ask First owner/Test Architect receipts have not been created or collected.
- 2026-08-22 (code review, all patches applied): closed the deep-content mutation-reachability gap
  for the package-signature, closed-inventory, smoke-log, receipt-source, and registry-authority
  checks with 12 new focused test cases; fixed two uncaught-`AttributeError` fail-open paths; fixed
  an `attempts: true` boolean-coercion gap; added a Production-smoke aggregate-window bound; added a
  closed-inventory walk rejecting stray packet files; tightened the registry authority-source check
  from substring-containment to exact role-mapping plus an explicit deployment-authority disclaimer
  requirement (resolves the registry cross-story-reuse decision); renamed `repository_signed` to
  `repository_signature_entry_present` so the field no longer overclaims cryptographic verification
  (resolves the package-signing decision); fixed a dead variable and an uncaught `KeyError` in the
  assembler; derived the registry's `created_at` from its own retained source instead of a duplicate
  hardcoded literal; reordered the `deferred-work.md` chunks chronologically; corrected this spec's
  own Verification section to state the actual fail-closed expectation. `v1.py`'s own SHA-256 changed
  to `d0eb781f4eeecaccdf4ca895a2fbc21ad80ad41f5f9192c007968954b1a79fa4`, which changed the dispatch
  table pin, the retained closure/subject bytes, and the canonical subject digest to
  `bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709` (regenerated via the assembler
  against the unchanged frozen Story 3.14 predecessor and unchanged OCI/smoke facts; still zero
  receipts, still fails closed). One item (the OCI child-config Labels/os/architecture check) was
  investigated and found structurally unreachable by any Story-3.15-only packet mutation without
  also tampering with the byte-for-byte frozen Story 3.14 predecessor packet, which the suite must
  never do (`FrozenStory314PacketRemainsByteForByteUnchanged`) — left as documented residual risk
  rather than a fabricated test. Focused suite 48/0/0 (was 36); Contracts Release build 0W/0E.
- 2026-08-22 (acceptances collected): with explicit owner authorization, retained the EventStore-owner
  and Release-owner acceptances from GitHub issue comments `5381125968` and `5381126900`, plus the
  Test Architect `bmad:murat` acceptance after a PASS traceability gate. All three receipts bind the
  unchanged subject `bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709` and
  their exact source bytes. The assembled production closure now passes and selects only OCI index
  `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` while all
  deployment, publication, registry-mutation, consumer-removal, and predecessor-change authority
  flags remain false. Focused suite: 48 passed, zero failed, zero skipped; Contracts Release build:
  zero warnings and errors.

## Design Notes

On 2026-08-21 the owner confirmed Story 3.14's spec was authoritative over its then-stale tracker row. That reading is superseded: Story 3.14's spec and tracker row now agree at `in-progress`, and neither authorizes Story 3.15 closure. Story 3.15 depends on the frozen 3.14 *packet* bytes (predecessor digest `4d1a0c33...`), which are unchanged, not on 3.14's lifecycle state. Receipts live beneath `acceptances/<subject-sha256>/` so the subject binds technical evidence and the registry without signing itself. Publication authority is checked for validity at its recorded use, not required to remain unexpired at later verification time.

**2026-08-22 (code review, accepted trade-offs):** Each receipt's `durable_source` is cross-checked against a file retained inside the same packet the receipt author controls, not fetched live from the GitHub API — this proves internal consistency (receipt and source agree byte-for-byte) but not independence. Accepted for this story; mirrors the same accepted gap already recorded for Story 3.13's analogous mechanism.

The owner-role registry's `authority_source` is Story-3.15-scoped GitHub comment `5407975180` on
dedicated issue `#352`. Its exact body ratifies the three mappings and explicitly disclaims package
recovery, release, registry mutation, deployment, consumer migration, and Story 3.15 done authority.
The verifier binds the comment id and all three URLs to `#352`, requires the shared owner login/id,
unchanged timestamp, MEMBER/OWNER/COLLABORATOR association, exact body, and no GitHub App producer.
Both owner receipts are independently constrained to the same positively allowlisted issue.

## Verification

**Commands:**
- `python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json --manifest tools/release-packages.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **actual:** `pass: sha256:4d1a0c33...`, exit 0. The frozen predecessor packet is unchanged, and its whole 66-file tree is now digest-pinned by the focused suite, not just the identity file.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- **actual:** Build succeeded, 0 warnings, 0 errors.
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **actual:** `pass: subject=sha256:86c59c79... selected=sha256:4b141085...`, exit 0. On 2026-09-05 the Ask First owner action completed: EventStore-owner comment `5550273078`, Release-owner comment `5550277712`, and the `bmad:murat` Test Architect record bind subject `86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`. Deployed-runtime **parity is available**; `deployed_runtime_parity` and `selected_deployed_identity` remain the claim fields, now granted by the 3-of-3 receipt gate. Non-authority flags stay false. A synthesized zero-receipt copy still fails closed in `AssemblerReproducesTheSubjectAndPropagatesTheVerifierVerdict`; the positive synthetic path remains in `ThreeRosterBoundRolesClosePositiveParityOnOneUnchangedSubject`.
- `python3 tools/assemble-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **actual:** `subject=sha256:86c59c79... receipts=3 verifier_exit=0`, exit 0, reproduced identically on repeat runs. `AssemblerReproducesTheSubjectAndPropagatesTheVerifierVerdict` still runs over both a zero-receipt and a fully accepted copy and pins both exit rules; focused executable negatives cover failed aggregate smokes, wrong child coverage, failed platform outcomes, malformed retained structures, and symlinked paths.
- `dotnet tests/.../Hexalith.EventStore.Contracts.Tests.dll -class ...CorrectedDeployedRuntimeParityClosureTests -class ...CorrectedDeployedRuntimeParitySmokeCaptureTests -noLogo` -- **actual:** 193 passed, 0 failed, 0 skipped.
- `dotnet tests/.../Hexalith.EventStore.Contracts.Tests.dll -class ...CorrectiveOciProvenanceReleaseTests -noLogo` -- **actual:** 37 passed, 0 failed, 0 skipped.
- Complete Contracts suite -- **actual:** 1846 passed, 29 failed, 0 skipped, 1875 total. All 29
  failures are Story 4.15 OQ8 cases stopped by the same intentional downstream drift gate:
  `Story 4.15 v2 gate-input identity drift: docs/ci.md`. Rebinding that separately reviewed
  successor packet would invalidate its approvals and is outside this story; Story 3.15's 193
  focused cases and the 37 predecessor/provenance cases remain green.
- `git check-attr text eol -- tools/deployed_runtime_parity_handlers/v1.py tools/validate-corrected-deployed-runtime-parity.py tools/release_evidence_handlers/v3.py tools/release_evidence_handlers/__init__.py tools/capture-corrected-deployed-runtime-parity-smokes.py tools/assemble-corrected-deployed-runtime-parity.py` -- **actual:** `text: set`, `eol: lf` for all six, so no SHA-256 pin can be broken by working-tree EOL drift. The two producers are included because their digests are now subject-bound.
- `git diff --check` -- **actual:** no output, exit 0.

**Mutation evidence recorded for guards that previously could not fail:**
- Injecting a stray field into a retained acceptance source and rebinding every digest now yields `fail: GitHub acceptance source schema is invalid`, exit 1, where it previously yielded `pass`, exit 0, with the subject unchanged.
- Neutering the corrective dispatcher's module-displacement loop in a scratch copy makes the preloaded repository-local `zipfile` fake execute and print its marker, so `CorrectiveDispatcherCannotReusePreloadedModules` can now fail.
- **Loop 7, doubled byte-order mark (live control):** with the v3 fixes reverted in a scratch copy, a nuspec of `BOM + BOM + <?xml?> + <!DOCTYPE package [<!ENTITY smuggle "Hexalith.Evil">]>` is ACCEPTED and `nuspec_identity` returns id `Hexalith.Evil`; with the fixes in place it is rejected with `package nuspec is not strict UTF-8 XML`, and the single-BOM control is still rejected on the DTD itself.
- **Loop 7, bytes repository path (live control):** with `os.fsdecode` reverted, `_is_repository_path` answers `str -> True, bytes -> False` -- the bypass; with the fix, `bytes -> True`. Both dispatchers were checked.
- **Loop 7, roster configuration (live control):** rewriting `OWNER_GITHUB_ACCOUNT` to `("mallory", 999)` previously left `_verify_roster_configuration` green; it now raises `rostered owner identity configuration is inconsistent`, and so does `("jpiquot", 999)`, which is the half that actually authenticates.
- **Loop 7, smoke window bound:** a 205-second per-platform window -- the shape the capture tool legitimately produces -- was rejected by the old 180-second bound and is accepted now, while 211 seconds and a 421-second aggregate both fail closed.

## Suggested Review Order

**Start here -- what the packet now claims**

- Fail-closed verdict, exact current subject, the claim-versus-verdict distinction, and the blocking
  owner action in one place.
  [`3-15-...-closure.md:3`](3-15-corrected-deployed-runtime-parity-closure.md#L3)

- Eight subjects, seven re-mints, and which three of them ever carried receipts.
  [`3-15-...-closure.md:61`](3-15-corrected-deployed-runtime-parity-closure.md#L61)

- The loop-7 ledger entries, including the regression class loop 6 introduced.
  [`deferred-work.md:1815`](deferred-work.md#L1815)

**Loop 7 -- the two regressions loop 6 introduced**

- Every prolog exit is now "reached the document element" or a fail-closed reason, and a residual
  byte-order mark is rejected before it can skip the scan.
  [`v3.py`](../../tools/release_evidence_handlers/v3.py)

- A bytes repository path is decoded rather than answered False, so it cannot escape displacement
  or the post-execution shadow check.
  [`validate-...-parity.py:114`](../../tools/validate-corrected-deployed-runtime-parity.py#L114)

- The reproduction of the byte-order-mark bypass, with the single-BOM control.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2192`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2192)

- The bytes-path guard asserted directly, plus a run that must not produce a traceback.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2498`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2498)

**Guards that could not fail**

- The roster configuration compares the table against the verbatim authenticated body and the
  ratified account literal, not against strings built from itself.
  [`v1.py:315`](../../tools/deployed_runtime_parity_handlers/v1.py#L315)

- The verbatim roster body, held as a literal so the comparison has two independent sides.
  [`v1.py:111`](../../tools/deployed_runtime_parity_handlers/v1.py#L111)

- Re-rostering either half of the account now fails closed, proved by mutating the handler in a
  copied tool tree and rebinding both pins so execution reaches the guard.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2561`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2561)

**Producer and verifier must agree**

- The per-platform window is the platform budget plus the cleanup allowance, so the capture cannot
  emit records this verifier rejects.
  [`v1.py:60`](../../tools/deployed_runtime_parity_handlers/v1.py#L60)

- The capture's own cleanup budget, pinned equal to that allowance by a focused test.
  [`capture-...-smokes.py:37`](../../tools/capture-corrected-deployed-runtime-parity-smokes.py#L37)

- Both bounds breached, plus an acceptance case the old bound would have rejected.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2410`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2410)

- The assembler binds the bytes actually executing and refuses to run from anywhere else.
  [`assemble-...-parity.py:43`](../../tools/assemble-corrected-deployed-runtime-parity.py#L43)

**Subject-bound decision inputs**

- Both packet producers are bound, so a producer edit re-mints.
  [`v1.py:51`](../../tools/deployed_runtime_parity_handlers/v1.py#L51)

- The four limitations every receipt must repeat, including tooling-composed authorship.
  [`v1.py:69`](../../tools/deployed_runtime_parity_handlers/v1.py#L69)

- Retained GitHub comment envelopes are closed-schema at all three levels.
  [`v1.py:143`](../../tools/deployed_runtime_parity_handlers/v1.py#L143)

- One closed-envelope loader shared by the roster comment and both owner receipts.
  [`v1.py:998`](../../tools/deployed_runtime_parity_handlers/v1.py#L998)

**Acceptance-source binding -- the cross-lineage splice**

- All four comment fields must resolve to one comment on one issue.
  [`v1.py:1015`](../../tools/deployed_runtime_parity_handlers/v1.py#L1015)

- Only dedicated Story 3.15 issue `#352` is allowlisted, and the registry path derives its URLs from
  that one constant.
  [`v1.py:129`](../../tools/deployed_runtime_parity_handlers/v1.py#L129)

- Each rostered role bound to exactly one source kind, so owners cannot self-attest.
  [`v1.py:133`](../../tools/deployed_runtime_parity_handlers/v1.py#L133)

- Every authentication clause on the roster comment now has its own negative case, including the
  registry timestamp binding and a consistently rewritten other-comment.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2024`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2024)

- A stray unreviewed field cannot persist inside the only external authentication artifact.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2645`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2645)

**Other fail-closed behaviour**

- Date-only timestamps fail closed instead of raising an uncaught `TypeError`.
  [`v1.py:296`](../../tools/deployed_runtime_parity_handlers/v1.py#L296)

- An unknown package id fails closed naming the id, rather than escaping as `KeyError`.
  [`v1.py:613`](../../tools/deployed_runtime_parity_handlers/v1.py#L613)

- The fail-closed reasons the frozen block names are each shown reachable.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2719`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2719)

**Trust chain -- verified bytes must be the executed bytes**

- Repository search roots and preloaded repository modules are removed before the first import.
  [`validate-...-parity.py:144`](../../tools/validate-corrected-deployed-runtime-parity.py#L144)

- Exactly the verified source bytes are compiled and executed.
  [`validate-...-parity.py:187`](../../tools/validate-corrected-deployed-runtime-parity.py#L187)

- The predecessor dispatcher executes only exact verified source bytes under the same isolation.
  [`validate-corrective-release-evidence.py:150`](../../tools/validate-corrective-release-evidence.py#L150)

- The provenance test now reads the loader call sites, not the pin table.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:987`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L987)

- SHA-pinned Python can no longer be CRLF-rewritten by an EditorConfig-honouring editor.
  [`.gitattributes:11`](../../.gitattributes#L11)

**Producer discipline**

- `started_at` is stamped before the platform deadline, so the recorded window encloses the whole
  capture including cleanup.
  [`capture-...-smokes.py:103`](../../tools/capture-corrected-deployed-runtime-parity-smokes.py#L103)

- Running against a populated packet root is refused with a distinct exit code and a rerun message
  that names `--force`.
  [`capture-...-smokes.py:261`](../../tools/capture-corrected-deployed-runtime-parity-smokes.py#L261)

- Refuses to assemble a packet over failed Production smokes.
  [`assemble-...-parity.py:156`](../../tools/assemble-corrected-deployed-runtime-parity.py#L156)

- Both producer digests are written into the closure `dispatch` block.
  [`assemble-...-parity.py:179`](../../tools/assemble-corrected-deployed-runtime-parity.py#L179)

- Package count derived from the items, not asserted as a literal.
  [`assemble-...-parity.py:213`](../../tools/assemble-corrected-deployed-runtime-parity.py#L213)

- Assemble and verify are one operation; exit code reflects the real verdict.
  [`assemble-...-parity.py:276`](../../tools/assemble-corrected-deployed-runtime-parity.py#L276)

- The assembler's contract is pinned by an executable caller, asserted fail-closed.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2841`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2841)

- The readiness loop is exercised past attempt one; cleanup is proved attempted, not skipped;
  retained evidence cannot be silently overwritten.
  [`CorrectedDeployedRuntimeParitySmokeCaptureTests.cs:205`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParitySmokeCaptureTests.cs#L205)
  [`CorrectedDeployedRuntimeParitySmokeCaptureTests.cs:248`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParitySmokeCaptureTests.cs#L248)
  [`CorrectedDeployedRuntimeParitySmokeCaptureTests.cs:298`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParitySmokeCaptureTests.cs#L298)

**Operator handoff and drift binding**

- The current subject, the 3-of-3 available verdict, the claim-versus-verdict distinction, and the
  two facts recorded rather than corrected.
  [`ci.md:588`](../../docs/ci.md#L588)

- The checked-in packet's positive 3-of-3 state, drift-bound to the current subject and reading the
  claim fields explicitly.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:159`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L159)

- Both markdown records are drift-bound and must read `deployed_runtime_parity` itself.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2949`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2949)

- The sprint tracker and this spec are drift-bound too, closing the two surfaces loop 6 missed.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2308`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2308)

- The proof packet's tool-digest table is bound exactly to the closure `dispatch` block.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:2339`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L2339)

- The withdrawn gate, now withdrawn in every status and coverage field rather than in prose alone.
  [`gate-decision.json:1`](../../_bmad-output/test-artifacts/gate-decision.json#L1)

### Review Findings (2026-08-30, Chunk 1 -- trusted verifier and packet producers)

- [x] [Review][Patch] **[HIGH] Establish a hermetic interpreter boundary before either verifier imports shadowable dependencies; the current isolation starts after top-level imports and preserves non-repository `PYTHONPATH`/import-hook sources.** [tools/validate-corrected-deployed-runtime-parity.py:4]
- [x] [Review][Patch] **[HIGH] Force the localhost Production-smoke request to bypass environment proxies so an unrelated proxy response cannot satisfy the `/alive` check.** [tools/capture-corrected-deployed-runtime-parity-smokes.py:149]
- [x] [Review][Patch] **[HIGH] Reject symlinked or escaping producer inputs and outputs before the capture or assembler reads and writes packet files.** [tools/capture-corrected-deployed-runtime-parity-smokes.py:244]
- [x] [Review][Patch] **[HIGH] Track whether this capture created the Docker container and never force-remove a same-named container after `docker run` failed.** [tools/capture-corrected-deployed-runtime-parity-smokes.py:96]
- [x] [Review][Patch] **[MEDIUM] Convert smoke log and summary write failures into the documented support-safe failure plus rerun guidance instead of a traceback.** [tools/capture-corrected-deployed-runtime-parity-smokes.py:220]
- [x] [Review][Patch] **[MEDIUM] Validate malformed retained registry, predecessor, and smoke structures before indexing them so assembler failures remain controlled.** [tools/assemble-corrected-deployed-runtime-parity.py:74]
- [x] [Review][Patch] **[MEDIUM] Bound the assembler's verifier subprocess and handle process-start/write failures without hanging or escaping after a partial packet rewrite.** [tools/assemble-corrected-deployed-runtime-parity.py:272]
- [x] [Review][Patch] **[MEDIUM] Require regular files and reject FIFOs, sockets, devices, and other unsupported entries instead of ignoring or blocking on them.** [tools/deployed_runtime_parity_handlers/v1.py:353]
- [x] [Review][Patch] **[MEDIUM] Require distinct GitHub comment identities for the roster authority and both owner receipts so contradictory snapshots cannot reuse one comment ID.** [tools/deployed_runtime_parity_handlers/v1.py:802]
- [x] [Review][Patch] **[MEDIUM] Add executable assembler tests for failed aggregate smokes, wrong child coverage, and failed platform outcomes before closure emission.** [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2841]
- [x] [Review][Patch] **[MEDIUM] Require exact JSON integers for dispatch version, workflow IDs, attempts, and package counts instead of accepting equal-valued booleans or floats.** [tools/deployed_runtime_parity_handlers/v1.py:455]
- [x] [Review][Patch] **[HIGH] Reject Production-smoke windows that lie in the future so impossible evidence cannot authorize parity.** [tools/deployed_runtime_parity_handlers/v1.py:727]
- [x] [Review][Patch] **[HIGH] Require `smoke-results.json` itself to use the selected canonical UTF-8 representation, as already required for each platform log.** [tools/deployed_runtime_parity_handlers/v1.py:689]
- [x] [Review][Patch] **[LOW] Include bounded timestamping/transition overhead in producer-verifier smoke windows so a legitimate near-budget capture cannot reject its own output.** [tools/capture-corrected-deployed-runtime-parity-smokes.py:103]

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Proof packet Decision says 3/3 available while Current acceptances/Reproduce still said 0/3 | medium | patch | Verified: Decision updated; Current acceptances and Reproduce still claimed no acceptances directory and exit 1. Fixed in this review. |
| Story record vs proof packet disagree on live verdict | medium | patch | Same root cause as above; grouped. Fixed. |
| docs/ci.md still said fails closed at 0/3 / unavailable | medium | patch | Verified live prose still named zero receipts after collection. Fixed. |
| Subject-history arithmetic (7 vs 8 subjects) inconsistent across docs | low | defer | Pre-existing narrative drift across superseded README / ci / story record; not caused by receipt collection. |
| Spec Code Map omits a8cc777e superseded tree | false | reject | Fix would edit this build's spec Code Map; a8cc777e is already narrated in Design Notes / Verification. |
| Spec status in-review / review_loop_iteration 6 mismatch claimed done | false | reject | Status correctly set to in-review for this review step; loop iteration is historical counter. |
| sprint-status comment still said stays in-progress until three receipts | medium | patch | Verified comment contradicted `review` row after 3/3. Fixed. |
| Assembler hardcodes repository_signature_entry_present True | medium | defer | Pre-existing producer behavior; verifier still rejects missing `.signature.p7s`. Not introduced by receipt collection. |
| Proof packet Authority table says four flags then lists six rows | low | reject | Cosmetic table preamble; unlikely everyday harm; more than a one-line fix. |
| Open deferred redirect_count / empty PLATFORMS / smoke logs | medium | defer | Already recorded deferred items; not closed by this receipt-collection change. |
| Scoped review diff omits .gitattributes | false | reject | Artifact of story-scoped review diff, not a missing repo change. |
| Duplicate new-file hunks for acceptance paths in diff | false | reject | Diff-generation noise from combining tracked rewrite with untracked add. |
| Bootstrap execv failure continues non-isolated | maybe-false | defer | Unverified whether OSError path is reachable on supported hosts; pre-existing isolation design. |
| minimized/pin GitHub comments still authenticate | medium | defer | Pre-existing closed-schema accepts null minimized/pin; not introduced by this change. |
| Assembler smoke exit_code JSON false treated as pass | medium | defer | Pre-existing assembler refuse guard; not introduced by receipt collection. |
| Assembler smoke platform/digest set match with swapped platforms | medium | defer | Pre-existing assembler coverage check; not introduced by this change. |
| Import-shadow check skipped on validation failure paths | low | defer | Pre-existing finally-block ordering; fail path already exits 1. |
| Frozen AC wording "authenticated ... Test Architect" vs self-attested record | false | reject | Limitations and operator records already disclose self-attestation; fix would edit frozen intent. |
| Verifier smoke platform outcome value clauses lack mutation proofs | medium | patch | Verification-gap pre-verified: type/window tests do not force observed_platform/http_status/redirect/outcome value predicates. |
| Capture suite never exercises observed-platform mismatch or non-200/redirect readiness | medium | patch | Verification-gap pre-verified: DockerFake always returns matching platforms; no 201/200+redirect cases. |

