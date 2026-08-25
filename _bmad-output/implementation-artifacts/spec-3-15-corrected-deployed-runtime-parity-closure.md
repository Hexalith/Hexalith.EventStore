---
title: 'Story 3.15 Corrected Deployed Runtime Parity Closure'
type: 'feature'
created: '2026-08-21'
status: 'done'
baseline_commit: '94591f3539ce30372db58e5fdd3ba017ea8c07b8'
review_loop_iteration: 5
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
- [ ] [Review][Defer] `FrozenStory314PacketRemainsByteForByteUnchanged` hashes one file.
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

**Landing window:** the packet is already at 0/3 receipts, so no acceptance is burned by a re-mint right now.
Every `v1.py` / verifier / `v3.py` edit below re-mints the subject; landing them together, before receipts are
collected against `5acb8176...`, costs nothing. Collecting receipts first makes each fix cost three receipts.

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

## Spec Change Log

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
- `python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json --manifest tools/release-packages.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: exact predecessor digest passes.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -noLogo` -- expected: all matrix and mutation cases pass with none skipped.
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: **pass**, exact current subject `a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`, exactly three roster-bound role receipts, and selected identity only `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`, exit 0. The prior `dab64f5f...` receipt/source tree remains byte-identical outside the packet.
- `python3 tools/assemble-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: reproduces identical subject `a8cc777e...` on repeat runs, runs the pinned verifier over its own output, reports `receipts=3 verifier_exit=0`, and exits 0.
- `git check-attr text eol -- tools/deployed_runtime_parity_handlers/v1.py tools/validate-corrected-deployed-runtime-parity.py tools/release_evidence_handlers/v3.py tools/release_evidence_handlers/__init__.py` -- expected: `text: set`, `eol: lf` for each, so the SHA-256 pins cannot be broken by working-tree EOL drift.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Start here — what the packet now claims**

- Passing verdict, exact current subject, and evidence-only boundary in one place.
  [`3-15-...-closure.md:3`](3-15-corrected-deployed-runtime-parity-closure.md#L3)

- Fresh owner comments and self-attested Test Architect receipt bind the unchanged subject.
  [`3-15-...-closure.md:58`](3-15-corrected-deployed-runtime-parity-closure.md#L58)

- The blocking release defect this loop recorded rather than actioned.
  [`deferred-work.md:1569`](deferred-work.md#L1569)

**Acceptance-source binding — the cross-lineage splice**

- All four comment fields must resolve to one comment on one issue.
  [`v1.py:861`](../../tools/deployed_runtime_parity_handlers/v1.py#L861)

- Only dedicated Story 3.15 issue `#352` is allowlisted; every sibling thread is rejected.
  [`v1.py:79`](../../tools/deployed_runtime_parity_handlers/v1.py#L79)

- Each rostered role bound to exactly one source kind, so owners cannot self-attest.
  [`v1.py:82`](../../tools/deployed_runtime_parity_handlers/v1.py#L82)

**Guards that did not hold the property they stated**

- Word-bounded negation; `authorizes nothing beyond deployment...` no longer passes.
  [`v1.py:71`](../../tools/deployed_runtime_parity_handlers/v1.py#L71)

- Date-only timestamps now fail closed instead of raising an uncaught `TypeError`.
  [`v1.py:191`](../../tools/deployed_runtime_parity_handlers/v1.py#L191)

- An unknown package id fails closed naming the id, rather than escaping as `KeyError`.
  [`v1.py:471`](../../tools/deployed_runtime_parity_handlers/v1.py#L471)

**Trust chain — verified bytes must be the executed bytes**

- Repository-local dependency shadows cannot participate in verified handler execution.
  [`validate-...-parity.py:138`](../../tools/validate-corrected-deployed-runtime-parity.py#L138)

- The predecessor dispatcher executes only exact verified source bytes under the same isolation.
  [`validate-corrective-release-evidence.py:138`](../../tools/validate-corrective-release-evidence.py#L138)

- Strict numeric smoke facts reject JSON booleans and equal-valued floats.
  [`v1.py:565`](../../tools/deployed_runtime_parity_handlers/v1.py#L565)

- SHA-pinned Python can no longer be CRLF-rewritten by an EditorConfig-honouring editor.
  [`.gitattributes:11`](../../.gitattributes#L11)

**Producer discipline**

- One monotonic deadline bounds pull, run, readiness, inspection, and cleanup attempts.
  [`capture-...-smokes.py:63`](../../tools/capture-corrected-deployed-runtime-parity-smokes.py#L63)

- Refuses to assemble a packet over failed Production smokes.
  [`assemble-...-parity.py:120`](../../tools/assemble-corrected-deployed-runtime-parity.py#L120)

- Package count derived from the items, not asserted as a literal.
  [`assemble-...-parity.py:170`](../../tools/assemble-corrected-deployed-runtime-parity.py#L170)

- Re-stamps `created_at` on content change so a re-mint cannot misdate itself.
  [`assemble-...-parity.py:190`](../../tools/assemble-corrected-deployed-runtime-parity.py#L190)

- Assemble and verify are one operation; exit code reflects the real verdict.
  [`assemble-...-parity.py:240`](../../tools/assemble-corrected-deployed-runtime-parity.py#L240)

**Operator handoff**

- Records the 3/3 subject-bound verdict and the one-human/self-attested identity limitation.
  [`ci.md:460`](../../docs/ci.md#L460)

**Tests and supporting artifacts**

- 108 closure cases mutation-prove the trusted verifier and current 3/3 packet.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:106`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L106)

- Six executable fake-command cases prove capture arguments and bounded failure retention.
  [`CorrectedDeployedRuntimeParitySmokeCaptureTests.cs:19`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParitySmokeCaptureTests.cs#L19)

- Restored remediation assertion plus four mutation-proved dead guards.
  [`DeployedRuntimeParityClosureTests.cs:1`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1)

- Stale `PASS` gate withdrawn rather than left standing over a superseded subject.
  [`gate-decision.json:1`](../../_bmad-output/test-artifacts/gate-decision.json#L1)
