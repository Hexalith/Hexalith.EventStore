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

**Current canonical subject:** `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`.
The fresh 2026-09-26 two-platform Production capture and corrected limitation 4 superseded the
receipt-free `c98fdef2...` subject. The older three receipts remain superseded as well.
The retained verifier now passes at **3 of 3** and selects the pinned OCI index as bounded
`evidence-validated` parity evidence. All four operational-authority flags remain false.

**Current continuation (2026-09-26):** The candidate packet captured both immutable children in
Production with the bound `curl -q` producer, and limitation 4 now distinguishes the two
credential-posted owner comments from the Test Architect's local self-attested source. The new
subject has a fresh independent Test Architect ACCEPT decision report. The user approved the
new-subject materials, separately authorized credentialed owner actions, and explicitly accepted
as both EventStore owner and Release owner. The new-subject review request was posted on issue
`#352` as comment `5844480896`; distinct accepted owner comments `5844573563` and `5844574016`
and the self-attested Test Architect source now bind this subject. The packet passes at 3/3.
The spec is `done` for bounded evidence validation, and a dated 2026-09-26 owner decision
closed the sprint row `done` for FR36-C2 only; G-HIGH-RISK stays blocked and is owned by Story 9.2,
and later authority gates remain open. Keep Story 4.15 OQ8 seal reconciliation separate.

## Code Map

- `tools/validate-corrective-release-evidence.py:12-73` and `tools/release_evidence_handlers/v3.py:58-404,863-974` -- trusted Story 3.14 dispatcher/canonical-byte gate; preserve v3 behavior and require predecessor digest `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- `tools/validate-corrected-deployed-runtime-parity.py` and `tools/deployed_runtime_parity_handlers/v1.py` -- new allowlisted closure dispatcher/handler for independent package, OCI, Production-smoke, subject, registry, receipt, and non-authority validation.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:317-603,1124-1235` -- predecessor mutation and canonicalization patterns; do not extend its frozen candidate contract.
- `tools/assemble-corrected-deployed-runtime-parity.py` -- deterministic packet producer: re-mints the subject, derives the package count and parity verdict from retained evidence rather than asserting them, and runs the pinned verifier over its own output before exiting.
- `tools/capture-corrected-deployed-runtime-parity-smokes.py` -- bounded two-platform Production smoke capture.
- `_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances/` -- complete
  receipt/source trees bound to superseded subjects `bb58d691...`, `dab64f5f...`, `a8cc777e...`, and
  `86c59c79...`, and `7d64f87e...`, retained unbound for audit. They must never be moved back into the packet; the
  `bb58d691...` owner sources are anchored on issue `#346` and are rejected on lineage as well as on
  subject.
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
- [x] [`3-15-corrected-deployed-runtime-parity-acceptance-review.md`](3-15-corrected-deployed-runtime-parity-acceptance-review.md) -- prepare exact-subject, role-specific EventStore-owner, Release-owner, and Test Architect
  acceptance review material without representing a draft as a receipt; refresh the existing
  Story 4.15 OQ8 seal-reconciliation record for the changed `docs/ci.md`.
- [x] Obtain three fresh roster-bound role acceptances for the current subject and re-run the
  assembler and retained verifier before claiming positive parity again. The Test Architect
  source is self-attested without independent external authentication.
- [x] `tools/{release_evidence_handlers/v3.py,deployed_runtime_parity_handlers/v1.py,assemble-corrected-deployed-runtime-parity.py}`, `evidence/story-3-15/f343bb01…/{subject,closure}.json`, `tools/validate-corrective-release-evidence.py:35`, `tools/validate-corrected-deployed-runtime-parity.py:48` and `3-15-corrected-deployed-runtime-parity-closure-proof-packet.md:55` -- carry out the single authorized re-mint that batches every correction to the sha256+size-pinned trust path, rejecting the three existing receipts and obtaining fresh architecture/security/test sign-off. Scope is owned by **DW-508** and must include **DW-506** (both v3 canonical encoders emit non-JSON `NaN`/`Infinity`; v1 and the capture copy are already correct), **DW-507** (tautological assembler-identity guard; settle what an independent repository root is before re-landing A8), **DW-509** (the sealed v3 `global.json` gate input hashes the CRLF worktree file, not the committed blob) and **DW-511** (the missing layout-preserving-copy refusal and NaN characterization cases). Landing these one at a time is what turned the Contracts lane red at 1987/204/0 and forced revert `dfc0ac55`. Recorded 2026-09-13 by the Story 4.15 Group Q code review, Decision 3. Technical re-mint and AI sign-offs are complete; this check does not represent the three separate owner acceptance receipts.

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

- **2026-09-26 (current-subject acceptance and bounded closure):** After approving the
  new-subject review materials, the owner separately authorized credentialed actions and accepted
  as both EventStore owner and Release owner. Dedicated issue `#352` now carries distinct canonical
  [EventStore-owner](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844573563)
  and [Release-owner](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844574016)
  comments with `created_at == updated_at == accepted_at`. The independent Test Architect ACCEPT
  decision was transcribed into a later local self-attested source, with that limitation explicit.
  The six new source/receipt files were checked in a temporary packet copy before being added to
  the current packet. Both assembler runs and the direct retained verifier pass at 3/3 on unchanged
  subject `66be1b4a...`, selecting only the pinned OCI index for bounded evidence validation.
  The Release Contracts test project builds with zero warnings/errors; the two focused Story 3.15
  classes pass 235/235 and the guarded lifecycle test passes 1/1. The spec is `done` for this
  bounded result and the sprint row is `review` pending G-HIGH-RISK. Story 4.15 OQ8 seal
  reconciliation remains separate; no operational-authority flag changed. The broader Contracts
  suite ran 2131 tests: 2128 passed and three Story 4.15 OQ8 v5 clean-checkout tests failed.

- **2026-09-26 (fresh Production capture and controlled re-mint):** Copied the receipt-free
  `c98fdef2...` packet to a candidate root, registered QEMU from the pinned `tonistiigi/binfmt`
  digest, and ran the current `curl -q` capture producer once against the same immutable amd64 and
  arm64 child digests. Both Production `/alive` smokes passed from `07:29:14.107654Z` through
  `07:30:34.182021Z` with HTTP 200, zero redirects, platform match, and cleanup pass. Corrected
  subject-bound limitation 4 to describe the two tooling-composed, credential-posted owner comments
  and the local self-attested Test Architect source. Archived the former packet byte-for-byte under
  `superseded-packets/c98fdef2.../`; older receipt sets and submodule pointers remain untouched.
  Reassembly minted `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`.
  The 24-file inventory matched, the predecessor verifier passed, the Release build had zero
  warnings/errors, and the focused Story 3.15 classes passed 235/235. The current verifier exits
  1 solely because it requires three packet-bound receipts; the four authority flags remain false.
  The [independent Test Architect decision](3-15-test-architect-decision-66be1b4a.md) ACCEPTS the
  technical evidence in a self-attested local report; it is not a packet receipt. No owner comment
  was posted or collected. Owner acceptance awaits the user's review and explicit authorization.

- **2026-09-26 (owner validation request):** With explicit user authorization, posted the
  validated request as [issue `#352` comment `5844166955`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844166955)
  from authenticated `github:jpiquot`. Its retained local draft matches the posted body. The
  request names the then-current `c98fdef2...` subject and Test Architect decline but is not an acceptance receipt;
  both owner decisions are still missing and parity remains at 0/3.

- **2026-09-26 (independent Test Architect decision):** The `bmad:murat` review declined the
  unchanged `c98fdef2...` subject because the retained 2026-08-21 Production smokes do not
  preserve the original curl arguments or `.curlrc`, and the fourth required limitation says
  every receipt is credential-posted although the Test Architect source is local/self-attested.
  No receipt was created. A local owner validation comment draft records the two missing owner
  decisions and the needed controlled re-mint if these bound inputs are corrected.

- **2026-09-26 (acceptance review preparation):** Prepared a separate, non-receipt review brief
  for the EventStore owner, Release owner, and Test Architect against unchanged subject
  `c98fdef266671a8b35e05c64b95eed275cb506e4368e28ab6957aa7750640df3`.
  Rechecked the 24 technical inventory files and the 0/3 verifier refusal, and refreshed the
  existing Story 4.15 OQ8 seal-reconciliation ledger entry with the live guide digest and
  failing active-v5 diagnostics. No current receipt or positive parity verdict was created.

- **2026-09-26 (curl configuration isolation re-mint):** The owner chose to isolate the smoke
  capture from default `.curlrc` settings. `curl -q` is now the first curl argument, and the
  recording fake pins that order. The bound capture digest changed to
  `7d134165963877d7633295bdb00504b4ea6b7424f26142cc6e3495b18ef48236`; the assembler
  derived subject `c98fdef266671a8b35e05c64b95eed275cb506e4368e28ab6957aa7750640df3`.
  All six files from the prior three-receipt set moved byte-for-byte to the superseded audit area.
  The verifier exits 1 at 0/3, so parity and index selection are unavailable pending new role
  acceptances. The spec and sprint row are `in-progress`.

- **2026-09-24 (completion):** Completed the three-layer review of the Story 3.15 closure
  patches. The spec is `done`; the sprint row remains `review` for the tracker handoff, and the
  lifecycle assertion pins both values. No new patch or intent gap survived triage. Three
  pre-existing evidence/capture risks were recorded in deferred work. The post-review Release
  build has zero warnings/errors, both focused closure classes pass 506/506, and the retained
  Story 3.15 verifier selects only the pinned OCI index.

- **2026-09-24 (review-patch completion):** The Story 3.15 spec remained `done` and the sprint
  row remained `review` while closing the eight review action items. The pending independent
  G-HIGH-RISK handoff kept the row in `review`. The valid-lineage off-path regression now uses a
  checked-out clone, so disabling the bound-path refusal can
  rewrite the copied closure; the subject-order guard also recognizes elided eight-character
  superseded digests. Both operator records identify comment `5803577826` as an agent-composed,
  after-the-fact ratification. The DW-507/508 resolutions and Epic 3 retrospective item 22 now
  reflect the verified closure. Release build: zero warnings and errors; focused Story 3.15
  closure class: 216/216; both closure classes: 506/506; full Contracts suite: 2108/2108,
  with zero failures, skips, or unrun tests. The Story 3.14 and Story 3.15 retained verifiers
  both exit 0 with their previously bound digests.

- **2026-09-23 (receipt-collection review patches):** Restored the sprint row to `review` and
  aligned its guarded prose and lifecycle test with the accepted 3/3 packet. Added a valid-lineage
  clone regression that reaches the assembler's bound-path refusal, expanded the superseded-subject
  drift guard, and guarded the trailing-space path test on Windows. The story record, proof packet,
  and DW-506/507/508/511 ledger entries now record the current checks and sign-off accounting.
  The full Contracts suite passes 2106/2106 and the focused closure class passes 214/214. The
  separate owner authorization required by review decision D1 is recorded in issue `#352`
  comment `5803577826`, which quotes the owner's exact written response and is cited separately
  from the receipt comments.

- **2026-09-23 (current subject accepted and guide resealed):** The rostered `github:jpiquot`
  owner accepted unchanged subject `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274`
  for both owner roles in issue `#352` comments `5789893766` and `5789897143`. The separately
  reviewed, self-attested `bmad:murat` record supplied the third receipt. Real packet assembly and
  retained verification now pass at 3/3, selecting only the pinned OCI index with four false
  operational-authority flags. The positive `docs/ci.md` guide re-minted the active Story 4.15 v4
  review subject to `8a59c89c276e0958f2066dfe8173d15ace2df6df2efb0120f6589c0ce20809b5`;
  fresh AI architecture, security, and Test Architect reviews and the updated manifest, selector,
  and lifecycle record bind it. The default OQ8 validator passes. Six timestamp-mismatched owner
  posting attempts were marked superseded and are not retained in the packet.

- **2026-09-23 (owner-authorized Story 4.15 v4 reseal):** Corrected `docs/ci.md` to identify
  the active v4 lineage and the current Story 3.15 subject, then removed the doc test's fallback
  to a superseded subject. The new v4 review subject is
  `171d8e3bd9f9a39fbb0a79e4f028f00c3653bd3269b3bba387088baf752f46ac`;
  fresh AI architecture, security, and self-attested BMAD Test Architect reviews bind it.
  The manifest, selector, and lifecycle record were resealed in that order. Default OQ8
  validation passes, active-v4 mutations pass 19/19, the OQ8 focused class passes 476/476,
  and the full Contracts suite passed 2096/2096 at that intermediate zero-receipt point. Story 3.15
  architecture sign-off passed; the later 3/3 owner action supersedes this intermediate state.

- **2026-09-23 (DW-508 review correction):** The assembler now requires the release commit to be
  an ancestor of `HEAD`, preserving whitespace in Git's checkout path. All trusted JSON loaders
  and both dispatchers reject nonfinite numeric tokens and exponent overflow. Focused regressions
  cover an unrelated `HEAD` with the release object available, Git environment redirects, and
  `1e999`. The technical subject re-minted to
  `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274`; it remains at
  zero receipts and grants no parity or operational authority.

- **2026-09-22 (DW-508 technical trust-path re-mint):** The v3 canonical encoders and both
  retained-evidence JSON loaders now reject non-JSON numeric tokens. The v1 canonical encoder
  delegates to the trusted v3 codec. The assembler resolves its repository root through Git
  with environment redirections removed, requires the immutable release commit in that object
  store, and rejects an executing copy outside the bound path. Updated both dispatch pins and
  re-minted the zero-receipt subject as
  `02f9dd40bd1a2d619a207d67709c5b3b1bb30bf51be1a2ae4ec27d905b15d87c`. The frozen
  Story 3.14 predecessor remains unchanged. DW-509 had already been resolved by the CRLF
  `.gitattributes` pin. Fresh role receipts and architecture/security/test sign-off remain an
  Ask First owner action; parity remains unavailable and no operational authority is granted.

- **2026-09-09 (Story 5.3 producer hardening):** Replaced the reusable symmetric Production-smoke
  input in the capture producer with an explicit HTTPS authority and RS256 allow-list. The governed
  assembler re-bound the changed producer and re-minted the zero-receipt subject as
  `aafe9040786c4f3af496b7ecbe62282c89396a15362b668a7b81ee148fe3f9c5`; it remains fail-closed and
  grants no deployment or publication authority.

- **2026-09-06 (review patches):** Closed restore-`OSError` fail-closed, timeout-reap cleanup
  failure, a zero-signature package case, assembler bound-path refusals, and `[*.py]` EditorConfig
  LF. Binding those producer bytes re-minted the subject to
  `a5c07d178412d8fbac72ec660a3c0a94826a823f7376c61e0e7b98ea554c3448`; the packet reports
  `receipts=0 verifier_exit=1` and **fails closed**. Replacement receipts were not collected
  (Ask First). No deployment, publication, registry mutation, consumer removal, predecessor
  change, commit, or push was performed.

- **2026-09-06 (Group A tools patch):** Closed the six Group A patches in one remint. A
  `TimeoutExpired` during `docker run` now inspects/rms the uuid-named container without changing the
  non-zero-exit “do not rm” rule. The assembler restores the previous `closure.json` when the pinned
  verifier does not complete (timeout, spawn failure); a completed verifier, including the expected
  receipt-gate exit 1, keeps the newly assembled claim file so zero-receipt assembly remains
  inspectable. Encrypted or unsupported-compression nuspec reads fail closed as `EvidenceError`. The
  closed inventory exempts only the validated evidence path. Story 3.14 tests now exercise the
  script-adjacent `--manifest` default and assert the `rerun:` trigger. Binding those live bytes
  re-minted subject `86c59c79...` to
  `84dee6e51844ddd0be403fefc56848f1b8f1dd916456f3b205f5bc52066db75f`; the packet reports
  `receipts=0 verifier_exit=1` and **fails closed**. The three `86c59c79...` receipts collected on
  2026-09-05 remain byte-for-byte in the superseded audit area. Replacement receipts were not
  collected (Ask First). No deployment, publication, registry mutation, consumer removal, predecessor
  change, commit, or push was performed.

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
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 post-collection actual:** `pass: subject=sha256:66be1b4a... selected=sha256:4b141085...`, exit 0, with 3/3 packet-bound receipts and all four authority flags false.
- `python3 tools/assemble-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 post-collection actual:** `subject=sha256:66be1b4a... receipts=3 verifier_exit=0`, exit 0. A temporary candidate packet passed the same assembler before the six files were copied into the current packet.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- **2026-09-26 post-collection actual:** zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParitySmokeCaptureTests -noLogo` -- **2026-09-26 post-collection actual:** 235/235 passed, zero errors, failures, skips, or unrun tests.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.DeployedRuntimeParityClosureTests.CorrectedLifecycleRowsRetainTheirCorrectedStatus -noLogo` -- **2026-09-26 post-collection actual:** 1/1 passed.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -noLogo` -- **2026-09-26 post-collection broad gate:** 2131 total, 2128 passed, 3 failed, 0 skipped/unrun. The only failures were `Oq8V5CandidateTests.V5DraftValidatorRejectsAuthorityAndSourceMutations`, `Oq8V5CandidateTests.V5SubjectPacketRemainsInactiveAndRejectsChangedInputs`, and `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`; the first two report `V5 activation requires a clean committed checkout`, and the third reports the dependent OQ8 validation failure. Story 4.15 owns this separate reconciliation.
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 pre-collection historical:** `fail: exactly three packet-bound receipts are required`, exit 1. Subject `66be1b4a...` was at 0/3 before the new receipts.
- `python3 tools/assemble-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 pre-collection historical:** `subject=sha256:66be1b4a... receipts=0 verifier_exit=1`, exit 1.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- **2026-09-26 new-subject actual:** build succeeded with zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParitySmokeCaptureTests -noLogo` -- **2026-09-26 new-subject actual:** 235/235 passed, zero errors, failures, skips, or unrun tests. The initial run exposed a fresh-timestamp test-fixture assumption; shifting only those synthetic timing fixtures into the past made the intended duration guards observable, and the rerun passed.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectiveOciProvenanceReleaseTests -noLogo` -- **2026-09-23 Test Architect actual:** 268 passed, 0 failed, 0 skipped before receipt collection. After collection, the updated Story 3.15 closure class alone passed 213/213 with zero failures or skips.
- `python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json --manifest tools/release-packages.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **actual:** `pass: sha256:4d1a0c33...`, exit 0. The frozen predecessor packet is unchanged, and its whole 66-file tree is now digest-pinned by the focused suite, not just the identity file.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- **actual:** Build succeeded, 0 warnings, 0 errors.
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 historical `c98fdef2...` actual:** `fail: exactly three packet-bound receipts are required`, exit 1. That subject was at 0/3 before the fresh capture and limitation correction.
- `python3 tools/assemble-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- **2026-09-26 historical `c98fdef2...` actual:** `subject=sha256:c98fdef2... receipts=0 verifier_exit=1`, exit 1. `AssemblerReproducesTheSubjectAndPropagatesTheVerifierVerdict` runs over both an isolated zero-receipt and a synthetic fully accepted copy and pins both exit rules.
- `python3 tools/validate-oq8-platform-evidence.py` -- **2026-09-26 current actual:** exit 1, `Story 4.15 v5 reviewed packet, selector, or lifecycle validation failed`. The nested `python3 tools/oq8-v5-packet.py --validate-active` reports `V5 activation requires a clean committed checkout`. The Story 3.15 `docs/ci.md` correction also changes a v4-bound gate input; Story 4.15 must reconcile its own seal before a current OQ8 pass can be claimed. `--lifecycle-mode closed` and `--historical-v3-only` pass, but neither evaluates current evidence.
- `dotnet tests/.../Hexalith.EventStore.Contracts.Tests.dll -class ...CorrectedDeployedRuntimeParityClosureTests -class ...CorrectedDeployedRuntimeParitySmokeCaptureTests -noLogo` -- **historical actual:** 221 passed, 0 failed, 0 skipped before the in-clone path regression. The focused closure class passed 216/216 in the 2026-09-24 review-patch run, with zero failed, skipped, or unrun; it included two elided-subject cases.
- `dotnet tests/.../Hexalith.EventStore.Contracts.Tests.dll -class ...CorrectiveOciProvenanceReleaseTests -noLogo` -- **actual:** 55 passed, 0 failed, 0 skipped.
- Complete Contracts suite -- **2026-09-25 post-patch recorded actual:** 2121 total, 2118
  passed, 3 failed. All three failures were Story 4.15 OQ8 v5 tests (`Oq8V5CandidateTests`
  twice and `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`);
  every Story 3.15 test, including the PRD guard, passed. The 2108/2108 run was a 2026-09-24
  snapshot with zero failures, skips, or unrun tests. The earlier 2096/2096 run preceded the
  `review` tracker transition and in-clone assembler path regression. `sprint-status.yaml` and
  `docs/ci.md` named the 2026-09-25 positive subject and receipt verdict at that historical run.
  Both were updated to the 2026-09-26 pre-acceptance 0/3 snapshot and, after collection, to the
  current 3/3 verdict; the guide's changed hash awaits separate Story 4.15 OQ8 seal
  reconciliation. At that historical run the guide is bound through the final Story 4.15 v4 review subject
  `8a59c89c276e0958f2066dfe8173d15ace2df6df2efb0120f6589c0ce20809b5`, fresh
  architecture/security/test review records, manifest, selector, and lifecycle record; the
  default OQ8 validator passes.
- Final Story 4.15 v4 mutation checks -- **actual:** active-v4 successor mutations 19/19,
  bound-snapshot case 1/1, required lifecycle-record mutations 10/10 and validated-selector
  snapshot 1/1, all with zero failures or skips; the nine-file v4 manifest passes `sha256sum
  --check`.
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

- Positive verdict, exact current subject, the claim-versus-verdict distinction, and the separate
  owner authorization citation in one place.
  [`3-15-...-closure.md:3`](3-15-corrected-deployed-runtime-parity-closure.md#L3)

- Nine subjects, eight re-mints, and which four of them ever carried receipts.
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

- The current subject, the fail-closed 0-of-3 verdict, the claim-versus-verdict distinction, and the
  two facts recorded rather than corrected.
  [`ci.md:588`](../../docs/ci.md#L588)

- The checked-in packet's fail-closed 0-of-3 state, drift-bound to the current subject and reading
  the claim fields explicitly.
  [`CorrectedDeployedRuntimeParityClosureTests.cs:166`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs#L166)

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
| docs/ci.md Story 3.15 rewrite without a Story 4.15 v3 successor remint | medium | defer | Complete Contracts suite is red on `Story 4.15 v3 current source identity drift: docs/ci.md`. Reminting that separately reviewed packet is outside this story's frozen Never (no rewrite of published artifacts / other lineage). |
| `.gitattributes` LF-pins `story-4-15-successors/v2/**` while docs name v3 | low | defer | Story 4.15 successor packet ownership; not caused by the parity-closure intent. |
| `.editorconfig` `[*]` `end_of_line = crlf` still covers `*.py` | low | patch | `.gitattributes` already has `*.py text eol=lf`, but an EditorConfig-honouring editor can still rewrite SHA-pinned producers/verifiers in the working tree. Direct `[*.py]` override. |
| `_bmad-output/test-artifacts/gate-decision.json` remint chain stops at `86c59c79` | low | defer | Artifact is already `SUPERSEDED`; regeneration belongs to the trace workflow (existing deferred-work entries). |
| Timeout `docker run` reap ignores failed `docker rm` and records `cleanup: pass` | medium | patch | `container_created` stays false, so `finally` always stores `cleanup: pass`. `_reap_timed_out_run_container` runs `rm --force` with `check=False` and ignores its returncode. |
| `_restore_previous_closure` swallows `OSError` | high | patch | Failed restore leaves the success-shaped `closure.json` the incomplete-child path was written to remove. |
| Assembler restore skipped when verifier child returns a negative `returncode` | maybe-false | defer | `subprocess.run` treats a signal-killed child as a completed wait. Unverified whether operators can hit this; if true it would be medium. |
| `closure.json` claim fields say `available` at 0/3 receipts | false | reject | Documented claim-versus-verdict: verifier exits 1 and grants nothing; `CheckedInPacketFailsClosedAtZeroOfThreeReceipts` pins both. |
| Retained Production smokes predate the bound capture-tool digest | medium | defer | Recapture is Ask First. Smoke logs vs summary already deferred as restatements; the bound producer cannot reproduce those Aug-21 bytes. |
| `review_loop_iteration: 6` / Code Map omits smoke-capture tests | false | reject | Loop counter is historical; fixing the Code Map would edit this build's spec. |
| Proof/story authority tables say four flags then list six rows | low | reject | carried: cosmetic table preamble; unlikely everyday harm; more than a one-line fix. |
| Isolation `execv` has no `OSError` handler | maybe-false | defer | carried: unverified whether OSError is reachable on supported hosts; pre-existing isolation design. |
| Remint omitted `sprint-status.yaml` / `deferred-work.md` / 4.15 packet | false | reject | `sprint-status.yaml` already names `84dee6e5…` and is drift-bound. 4.15 remint grouped with the docs/ci.md defer above. |
| CLI `evidence_path.read_bytes()` / inventory re-hash can block on a FIFO | low | reject | Unlikely everyday path; packet `_verify_file` already requires `S_ISREG`. Extra TOCTOU guards are more than a direct correction. |
| Capture `subprocess.run(text=True)` without UTF-8 replace | low | reject | `docker`/`curl` write-outs used here are ASCII integers; assembler child I/O already uses `errors="replace"`. |
| Assembler can execute stale `.pyc` beside hashed sources | false | reject | Live verifier isolates and execs hashed bytes; a shadowed producer cannot make the isolated verifier accept a bad packet. |
| Test `Kill` after budget does not drain redirected pipes | low | reject | Unlikely everyday (2-minute / 15-second budgets); adding WaitForExit+drain is more than a one-line correction. |
| Zero-receipt CLI never calls `_validate_predecessor` | false | reject | `validate_identity` pins the predecessor digest before the receipt-count gate; packet-file recompute runs on a schema-valid (3-receipt) document. 0/3 is the invalid-acceptance row. Independent Story 3.14 validator still recomputes. |
| Issued subject has no roster-bound receipts | false | reject | Documented fail-closed 0/3; collecting `#352` receipts remains Ask First. |
| No test removes `.signature.p7s` (only the duplicate-entry case) | medium | patch | Verification-gap pre-verified: `signature_count != 1` can be weakened to `> 1` with the suite still green. |
| Assembler provenance refusals never execute off the bound path | medium | patch | Verification-gap pre-verified: every assembler test runs the repository script; deleting the `__file__` comparison keeps the suite green. |
| 2026-09-23 BH1 release object need not be in current history | medium | patch | `repository_root` calls only `git cat-file -e`; an unrelated repository with the object via alternates passes that guard. Require ancestry from the recorded release commit to HEAD. |
| 2026-09-23 BH2 Git resolved through inherited PATH | low | reject | PATH is the operator's toolchain boundary for this local producer, as it is for the `python3` entry point; pinning an OS Git binary would add platform-specific trust machinery without protecting against an operator-controlled runtime. |
| 2026-09-23 BH3 assembler imports v1 before provenance checks | medium | defer | The ordinary import at line 22 predates this remint and can execute copied module bytes before the later path check. The isolated pinned verifier remains the verdict authority; source-only producer import is separately tracked. |
| 2026-09-23 BH4 `strip()` corrupts a whitespace-suffixed Git root | low | patch | Git prints a root followed by one newline; `strip()` also deletes legal path whitespace. Remove only the output newline. |
| 2026-09-23 BH5 JSON float overflow returns infinity | medium | patch | Python decodes valid `1e999` to nonfinite float in both trusted loaders; their new nonfinite-value invariant needs a bounded float parser. |
| 2026-09-23 BH6 dispatch parsers admit non-JSON numeric constants | low | patch | Both `_load_dispatch_metadata` functions parse the whole input before the strict handler loader; add the same rejection at this first parsing boundary for deterministic failure. |
| 2026-09-23 BH7 copied-repository test has no unrelated HEAD with release object | medium | patch | The new test has an empty object store and cannot falsify the `cat-file`-only gate; cover an alternate object store plus unrelated HEAD. Same root cause as BH1. |
| 2026-09-23 BH8 no GIT_DIR/GIT_WORK_TREE test | medium | patch | The changed `env=environment` guard has no caller-path test and deletion leaves checked tests green. Exercise both redirected variables. |
| 2026-09-23 BH9 docs/ci names superseded subject | medium | defer | The guide already named `86c59c79` before this remint; editing it trips Story 4.15's separately sealed OQ8 gate. Record the current subject in the Story 3.15 operator packet until that seal is reconciled. |
| 2026-09-23 BH10 story record still says four of nine subjects | low | patch | The prose count predates the latest remint and is now wrong; remove the numeric denominator. |
| 2026-09-23 EH1 release object without ancestry | medium | patch | Confirmed by the same `cat-file`-only check as BH1; an unrelated HEAD with the release object passes. |
| 2026-09-23 EH2 checkout path with trailing whitespace | low | patch | `result.stdout.strip()` deletes valid path bytes; same root cause as BH4. |
| 2026-09-23 EH3 v1 float overflow | medium | patch | `json.loads` accepts `1e999` as infinity; same root cause as BH5. |
| 2026-09-23 EH4 v3 float overflow | medium | patch | Both v3 loaders share the unbounded `json.loads` float conversion; same root cause as BH5. |
| 2026-09-23 EH5 docs/ci exact-lineage claim | medium | defer | The stale subject is verified, but its sealed Story 4.15 gate is a pre-existing cross-story constraint; same root cause as BH9. |
| 2026-09-23 VG1 missing Git environment isolation test | medium | patch | Pre-verified: deleting the `env=environment` arguments leaves current tests green; add a redirected `GIT_DIR`/`GIT_WORK_TREE` case. |
| 2026-09-23 VG2 unrelated repository can use alternate object store | medium | patch | Pre-verified with `git init` plus alternates: the assembler reaches the zero-receipt gate; same root cause as BH1. |
| 2026-09-24 BH1 owner authorization is not retained in the hash-closed packet | medium | patch | Comment `5803577826` is cited only in the operator records; the verifier does not read it. The citation must say it is an as-observed external audit source, distinct from the sealed 3/3 verdict. |
| 2026-09-24 BH2 no automated check of the authorization comment | low | reject | No local test references the comment. A local text assertion would only mirror a one-time external citation, while a live GitHub check would add network dependence to Contracts; the source is checked during the human audit. |
| 2026-09-24 BH3 architecture sign-off lacks a dedicated review record | medium | patch | The spec Change Log says only that architecture sign-off passed. The owner explicitly chose it under D2; disclose that narrow source and the absence of a separate final-subject architecture attestation. |
| 2026-09-24 BH4 security sign-off is a general four-layer review | medium | patch | The cited `a2f5cba2` review has no dedicated security acceptance. D2 counts it; state that choice and limitation rather than implying a Security Reviewer attestation. |
| 2026-09-24 BH5 Test Architect receipt is a parity acceptance | medium | patch | The `bmad:murat` receipt accepts the parity subject and does not enumerate DW-508 tests. D2 counts it; describe its actual scope and the lack of a dedicated trust-path-test attestation. |
| 2026-09-24 BH6 shared-owner fact described as a receipt limitation | low | patch | `REQUIRED_LIMITATIONS` includes self-attestation and tooling composition, but no same-account sentence. The account mapping is in the subject-bound registry; correct the proof packet wording. |
| 2026-09-24 BH7 tooling limitation misdescribes the Test Architect source | medium | defer | carried: the handler's sealed limitation says every receipt was posted with a credential, while `bmad:murat` is a local source. The prior review explicitly deferred changing these constants because it re-mints the subject and invalidates 3/3 receipts. |
| 2026-09-24 BH8 operator-record subject guard omits recent old hashes | medium | patch | `OperatorRecordsStateTheCurrentSubjectAndVerdict` lacks three constants already present in the spec/tracker guard, so a record can first name `aafe9040...` and pass. Reuse the same constants. |
| 2026-09-24 BH9 guards do not derive every intermediate old subject | low | reject | Some intermediate hashes, including `02f9dd40...`, have no retained receipt set; the current and latest superseded subjects are explicitly covered. Deriving all historical candidates would add a new inventory mechanism for a future editorial drift case. |
| 2026-09-24 BH10 Story 3.14 lifecycle sentence is stale | low | reject | Both Story 3.14 specs and its sprint row now say `done`; the cited sentence says `in-progress`. The fix edits this build's spec, which this review route rejects. |
| 2026-09-24 BH11 subject-count review navigation is stale | low | reject | The Suggested Review Order's nine-subject count predates later re-mints. The fix edits this build's spec, which this review route rejects. |
| 2026-09-24 BH12 adjacent lifecycle test comment says 0/3 | low | patch | The spec frontmatter guard still describes the earlier zero-receipt state while the packet has 3/3. Correct the comment directly. |
| 2026-09-24 EH1 capture imports shadowable subprocess | high | defer | `capture-corrected-deployed-runtime-parity-smokes.py` imports `subprocess` before any isolated bootstrap; a repository-local shadow module can run. This producer predates the current patch, and changing its sealed bytes would re-mint the subject. |
| 2026-09-24 EH2 failed Docker inspect can be mistaken for absence | medium | defer | `_reap_timed_out_run_container` returns success for every nonzero inspect result, including a transient daemon failure. This pre-existing capture path is sealed into the current subject. |
| 2026-09-24 EH3 failed Docker run can leave a created container | medium | defer | carried: `container_created` is false on nonzero exit, and the owner previously chose not to remove a container in that case to protect a same-named external container. Reopening that policy would change sealed producer bytes. |
| 2026-09-24 EH4 Test Architect source is self-attested | false | reject | carried: the prior review accepted and bound that limitation, and the frozen AC wording would need amendment for a different authentication claim. The packet does not present the local source as externally authenticated. |
| 2026-09-24 current BH1 retained GitHub envelope can be forged | medium | defer | The verifier compares retained JSON with fixed account, issue, body, and timestamp facts but performs no live fetch or signature check. The earlier durable-source review required disclosure, not independent authentication; fixing the accepted subject requires a new evidence contract and receipts. |
| 2026-09-24 current BH2 Test Architect source is reconstructed | false | reject | carried: EH4 above and the 2026-08-25 owner decision accept and bind the self-attested limitation; the operator packet identifies this source as self-attested. |
| 2026-09-24 current BH3 late authorization is outside the verdict | false | reject | The changed records date the after-the-fact ratification and explicitly say the mutable comment is not retained or checked by the 3/3 verifier. They do not claim that verdict proves authorization. |
| 2026-09-24 current BH4 no dedicated DW-508 sign-offs | false | reject | The records name each artifact counted under the owner's D2 decision and explicitly disclose the missing dedicated architecture, security, and trust-path-test attestations. The review found no new undisclosed claim. |
| 2026-09-24 current BH5 roster comment names a different file | low | reject | The exact retained body names `reviewer-roster.json`, while the packet file is `owner-role-registry.json`; the verifier and records disclose the copy-carried label. Correcting it requires a new owner comment and subject, and the operator can identify the only roster binding from the packet. |
| 2026-09-24 current BH6 retained smokes predate the bound producer | medium | defer | carried: the existing triage row and loop-6 owner decision state that the August smoke bytes came from the pre-hardening producer and deliberately bind them without recapture. |
| 2026-09-24 current BH7 smoke log restates summary | false | reject | carried: the earlier review identified log-equals-summary as the retained contract; neither the verifier nor records claim an independent raw Docker or curl transcript. |
| 2026-09-24 current BH8 platform observation reads image metadata | false | reject | carried: the earlier review verified digest-addressed `docker run` and Docker image inspection as the capture contract, with QEMU registration disclosed as host state. The claim assumes a process-architecture attestation the contract does not assert. |
| 2026-09-24 current BH9 NuGet origin lacks retained response metadata | medium | defer | Each packet entry stores archive bytes and a derived NuGet download URL, but no retained HTTP response or registry-signed provenance proves that URL served those bytes. The package identity and signature-entry checks cannot establish their transport origin. |
| 2026-09-24 current BH10 package signature bytes are not verified | false | reject | carried: the 2026-08-22 owner decision explicitly chose a signature-entry check, renamed the claim `repository_signature_entry_present`, and left PKCS#7 chain validation outside this packet. |
| 2026-09-24 current BH11 retained-file and nuspec size unbounded | medium | defer | carried: the earlier triage and DW-413 record whole-file reads and uncapped nuspec decompression; this code predates the current patch. |
| 2026-09-24 current BH12 failed Docker inspect reports cleanup pass | medium | defer | carried: EH2 above already records that any nonzero inspect result is treated as absence, including a daemon failure. |
| 2026-09-24 current EH1 Docker container appears after absent inspect | medium | defer | After a timed-out `docker run`, dockerd may finish creating the named container after the one-shot inspect returns absent; the helper returns success and records cleanup pass. This pre-existing sealed producer needs a bounded retry and a new subject. |
| 2026-09-24 current EH2 package changes between hash and ZIP inspection | low | reject | `_validate_packages` reopens the path after `_verify_file`, so a concurrent writer could substitute bytes temporarily. `_validate_inventory` later rehashes the package, and exploitation requires a concurrent local writer to restore the original bytes before that check; fixing this single-writer boundary requires a shared byte-buffer parsing path. |

### Review Findings (2026-09-06, Group A — tools / verifier chunk)

Scope: `94591f35...HEAD` narrowed to the seven Story 3.15 tool files (`assemble-corrected-deployed-runtime-parity.py`, `capture-corrected-deployed-runtime-parity-smokes.py`, `deployed_runtime_parity_handlers/{__init__,v1}.py`, `validate-corrected-deployed-runtime-parity.py`, `validate-corrective-release-evidence.py`, `release_evidence_handlers/v3.py`). 2,665 diff lines. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed. Groups B (tests), C (spec/story/proof), D (docs), and E (evidence remint) landed with this Group A remint.

**decision (resolved):**

- [x] [Review][Decision→Patch] RESOLVED 2026-09-06 (owner: on `TimeoutExpired` during `docker run`, inspect/rm the uuid-named container; keep the existing non-zero-exit “do not rm” policy). Timeout during `docker run` can leak the capture's container, but `FailedDockerRunNeverRemovesAContainerTheCaptureDidNotCreate` forbids `rm --force` whenever `docker run` did not return success. Follow-up patch below.

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] On `TimeoutExpired` during `docker run`, inspect/rm the uuid-named container; do not `rm` on non-zero `docker run` exit — `container_created` is set only after `docker run --detach` returns 0, so a hung run that created the container never reaches `docker rm`. Closed by `_reap_timed_out_run_container` and `TimedOutDockerRunInspectsAndRemovesTheUuidNamedContainer`. [tools/capture-corrected-deployed-runtime-parity-smokes.py:92]
- [x] [Review][Patch] Assembler writes success-shaped `closure.json` before the pinned verifier finishes and leaves it on verifier failure, timeout, or non-locale stderr decode — `canonical_write(.../closure.json)` then `subprocess.run`. Closed for incomplete children (`TimeoutExpired` / `OSError` / `SubprocessError`): `_restore_previous_closure` restores previous bytes or unlinks if none; child I/O uses `encoding="utf-8", errors="replace"`. A completed verifier, including receipt-gate exit 1, still keeps the new claim file so zero-receipt assembly remains inspectable. Proved by `AssemblerRestoresPreviousClosureWhenThePinnedVerifierTimesOut`. [tools/assemble-corrected-deployed-runtime-parity.py:97]
- [x] [Review][Patch] Encrypted or unsupported-compression `.nuspec` reads raise `RuntimeError`/`NotImplementedError` instead of `EvidenceError` — `nuspec_identity` now maps those plus zip/XML errors to `EvidenceError("package archive could not be independently inspected")`. Proved by `EncryptedNuspecEntryFailsClosedWithoutTraceback`. [tools/release_evidence_handlers/v3.py:512]
- [x] [Review][Patch] Closed inventory always exempts a basename `closure.json` even when the validated evidence file is a different path — `_validate_inventory` now exempts only the validated evidence path when it sits inside the packet. Proved by `LeftoverPacketClosureJsonIsNotExemptWhenEvidenceIsADifferentPath`. [tools/deployed_runtime_parity_handlers/v1.py:904]
- [x] [Review][Patch] Story 3.14 `--manifest` default (script-adjacent `release-packages.json`) is never exercised — closed by `CanonicalReleaseIdentityUsesTheScriptAdjacentManifestDefault` (cwd ≠ repo root, no `--manifest`). [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:932]
- [x] [Review][Patch] Story 3.14 fail line never asserts the new `rerun:` trigger — `CanonicalReleaseIdentityRejectsUnhashableDispatchMetadata` now asserts `rerun:` and the Story 3.14 trigger text. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:891]

**Rejected**

- false — Producer `-I -S -B` isolation (assembler/capture import `v1` under the caller environment): the pinned verifier is the documented authority and recomputes from retained bytes; a shadowed producer cannot make the isolated verifier accept a bad packet.
- false — Assembler overwrites derived files with no `--force` and does not delete a previous `acceptances/<old-subject>/` tree: `_validate_inventory` rejects an acceptance tree outside the bound subject, so remint leftovers fail closed rather than authorize stale receipts.
- false — `Path.rglob` following directory symlinks hashes foreign bytes into the inventory: `_packet_file` refuses symlink path parts for bound reads; the walk raises on the symlink itself or treats followed extras as files outside the closed inventory.
- false — Production-smoke records omit Docker-observed image identity, `curl --location`, QEMU digest, and a raw stdout transcript: pull and `docker run` already use `registry.hexalith.com/eventstore@<child_digest>`; a redirect is not HTTP 200 so capture fails closed; the binfmt digest is documented as unhashable host state; log-equals-summary is the retained contract.
- false — Verifier does not require `packages/{id}.{version}.nupkg` / `oci/` names / index-descriptor digest pairing, and uses magic `14`/`2`: the selected index is digest-pinned, package order is checked against the manifest, and hashes still bind the bytes.
- false — `v3.validate_packet_files` dropped the one-shared-summary `summary_bindings` check: production callers (`validate-corrective-release-evidence.validate` and `_validate_predecessor`) still run `validate_identity`, which requires one shared two-platform summary at `v3.py:397`.
- false — Uncapped `archive.read` / `ElementTree.fromstring` without `resolve_entities=False`: `_verify_file` / package hash checks run first; DTD/entity declarations in the prolog already fail closed.
- false — Assembler `predecessor_children[platform]` `KeyError` becomes a traceback: `build_document` already ran predecessor `validate_identity`, which requires both `PLATFORMS` children; the packages loop is already guarded.
- false — `_require_object` allows extra keys the verifier will reject: the assembler is not the authority; `_exact_object` fails closed.
- false — This tools chunk adds no tests / pin-hash drift is uncaught: the suite lives in Group B (`CorrectedDeployedRuntimeParityClosureTests.cs` and the smoke-capture tests), out of this chunk's diff.
- false — Cleanup-budget agreement is only a source grep: `CleanupAllowanceAgreesBetweenVerifierAndCaptureTool` fails on the cited `CLEANUP_TIMEOUT_SECONDS` drift; the suite would not stay green.
- false — Trusted packet `lstat` then `read_bytes` without `O_NOFOLLOW`, and predecessor OCI `read_bytes` skipping `_repository_file`: an attacker who can swap a symlink between those calls can already replace the retained files; predecessor bytes are hash-closed by v3 before `_validate_oci` rereads them.
- false — `minimized`/`pin`/`user.type` are schema-closed but not value-checked: already deferred 2026-08-30 on this spec (closed-schema accepts null `minimized`/`pin`); not re-opened as a Group A patch.
- false — Duplicate capture/verifier constants (`INDEX_DIGEST`, `TIMEOUT_SECONDS`, `PLATFORMS`) and `--force` leaving extra `smokes/` leftovers: extras fail the closed-inventory sweep; recapture into an empty root is the documented alternative; cleanup/timestamp constants are already grep-pinned.
- low — In-process `validate()` without `-I` from a non-repo cwd can shadow stdlib: the operator CLI re-execs `-I -S -B`; adding cwd/stdlib-origin guards is more than a direct correction for a library path operators do not use.
- maybe-false — `subject.json` deleted between timestamp load and restamp keeps a stale `created_at`: requires a concurrent deleter during a single-threaded assemble; if true it would only be low (content restamp already runs when the file still exists and changed).

### Review Findings (2026-09-06, Group B — tests chunk)

Scope: `94591f35...HEAD` narrowed to Story 3.15 test files (`CorrectedDeployedRuntimeParityClosureTests.cs` and `CorrectedDeployedRuntimeParitySmokeCaptureTests.cs`). 5,516 diff lines. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed.

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] Missing Windows OS check in SymbolicLinksCannotEvadeClosedInventory [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:1274]
- [x] [Review][Patch] Negative coverage for nested durable source and stray fields omits the Test Architect role [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2187, :3157]
- [x] [Review][Patch] Platform smoke execution interval containment within aggregate window is unverified [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2915-2925, :3524-3580]
- [x] [Review][Patch] Receipt claim divergence from retained GitHub acceptance comment payload is unverified [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3474-3522]
- [x] [Review][Patch] Vacuous test execution in BytesRepositoryPathsAreRecognisedRatherThanSilentlyDropped due to interpreter bootstrap re-exec [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3025-3042]
- [x] [Review][Patch] Malformed XML doc comment on Utc and missing doc on MutateRegistrySourceBody [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4313-4325]
- [x] [Review][Patch] Brittle byte offset calculation in StaleBytecodeCannotStandInForVerifiedSource [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2417-2422]

**Rejected**

- false — Missing disk verification for GitHub release asset package domain: `github_release_asset` files are in the predecessor Story 3.14 packet and are verified via predecessor identity checks, not inside Story 3.15's own packages folder.
- false — Dead assertion branch in `OperatorRecordsStateTheCurrentSubjectAndVerdict`: `if (receipts == RequiredRoles.Length)` is a valid intentional branch supporting future/varied packet state.
- false — One-sided collision testing in `RosterAndOwnerAcceptanceCommentsRequireDistinctIdentities`: set uniqueness `len({roster, eventstore, release}) == 3` is symmetric; testing collision against roster and against eventstore-owner fully exercises duplicates.
- false — Multiline regex end-of-line matching on CRLF checkouts in `CleanupAllowanceAgreesBetweenVerifierAndCaptureTool`: `.gitattributes` and `.editorconfig` pin Python files to LF; CRLF would invalidate their SHA-256 digests before regex runs.
- spec-edit — Test assertions enforce current reminted subject `a5c07d17...`, conflicting with superseded subject `84dee6e5...` in Spec § Verification: rejected because fix would edit the spec under review.
- low — Inconsistent platform filtering (`return;` in ClosureTests vs `Assert.Skip` in SmokeCaptureTests): deliberate to satisfy AC4 "no skipped case" on Windows in ClosureTests while SmokeCaptureTests requires Unix host; no impact on Linux CI.
- low — Unused `role` parameter on duplicate/missing rows and missing switch `default` throw in `InvalidAcceptanceNeverAuthorizesParity`: purely defensive/cosmetic in test helper with known theory data.
- low — Misleading variable naming `expectedError = "pass:"` in `VerifiersCrossAHermeticInterpreterBoundaryBeforeShadowableImports`: cosmetic naming in private test method.
- low — Attribute discrepancy between `[SupportedOSPlatform("linux")]` and `RequireUnixHost()`: documented analyzer hint vs runtime Unix skip.
- low — Defensive missing `else throw` / `default: throw` across test helpers: inputs are statically bounded by theory `InlineData`.
- low — Matrix Row 2 tag-only fact mutation: OCI validator strictly checks digest strings (`sha256:...`); tag values fail equality without separate test case.

### Review Findings (2026-09-06, Group C — spec / story / proof chunk)

Scope: `94591f35...HEAD` narrowed to Story 3.15 spec, story, and proof packet files (`spec-3-15-corrected-deployed-runtime-parity-closure.md`, `3-15-corrected-deployed-runtime-parity-closure.md`, `3-15-corrected-deployed-runtime-parity-closure-proof-packet.md`). 1,526 diff lines. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed.

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] Superseded acceptance history states non-authorization for superseded subject `84dee6e5...` instead of current subject `a5c07d17...` [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:113]

**Rejected**

- false — Group B review patch items unchecked in spec: all 7 patches verified and checked `[x]` on disk at lines 1060-1066.
- spec-edit — Spec frontmatter status claims `done` while parity is unavailable: rejected because fix would edit the spec under review.
- spec-edit — Spec Verification command output cites superseded subject `84dee6e5...`: rejected because fix would edit the spec under review.
- spec-edit — Spec Verification section reports 212 tests instead of 220: rejected because fix would edit the spec under review.
- spec-edit — Spec Change Log missing Group B tests patch entry: rejected because fix would edit the spec under review.
- spec-edit — Spec Change Log chronological ordering: rejected because fix would edit the spec under review.
- spec-edit — Spec Suggested Review Order line anchors drifted: rejected because fix would edit the spec under review.
- spec-edit — Spec Code Map omits SmokeCaptureTests and retains drifted line range: rejected because fix would edit the spec under review.
- spec-edit — Spec review findings section organization under Tasks & Acceptance: rejected because fix would edit the spec under review.
- spec-edit — Spec triage log entry cites `84dee6e5...`: rejected because fix would edit the spec under review.
- spec-edit — Spec task checkbox claims retention of subject-addressed receipts: rejected because fix would edit the spec under review.
- low — Proof packet authority boundary preamble says four flags, but table has six rows: cosmetic preamble; already triaged in Group A as rejected.
- low — Proof packet omits linux/arm64 QEMU binfmt emulation prerequisite: documented in story record lines 356-361 and capture script; proof packet is a concise summary.
- low — Proof packet omits the known reviewer-roster.json wording mismatch: documented in story record lines 305-309 and spec line 808; proof packet is a concise summary.
- low — Proof packet runtime evidence omits platform attempt counts and execution results table: detailed in smoke-results.json and story record.
- low — Proof packet displays superseded 2026-09-05 comments table under `## Current acceptances`: prose immediately clarifies "No packet-bound receipt currently binds subject a5c07d17...".
- low — Operator records report stale subject count arithmetic of nine subjects rather than eleven: cosmetic historical prose count; no invariant affected.
- low — Story record subject-change accounting heading and itemized list omit latest re-mint to `a5c07d17...`: cosmetic historical narrative section; current subject explained in § Decision.
- low — Story record superseded acceptance history itemizes only three rounds, omitting `bb58d691...`: cosmetic narrative omission; bb58d691 is discussed at line 105 as rejected on lineage.
- low — Failed timestamp-mismatch note misplaced after `86c59c79...` instead of `a8cc777e...`: cosmetic narrative placement in historical notes.

### Review Findings (2026-09-23, receipt-collection closure commit `a2f5cba2`)

Scope: `a2f5cba2^..a2f5cba2` (32 files, +638/-205, 1,566 diff lines) -- the commit that moved `3-15` to `review`. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed. Triage evidence: the retained verifier passes at 3/3 (exit 0, selects `4b141085...`); predecessor `pass sha256:4d1a0c33...` exit 0; the default OQ8 validator exit 0; both owner comments on `#352` match the retained bytes on live GitHub (author `jpiquot`/6775094, body, `created_at`, `updated_at`). **The complete Contracts suite at this commit is 2105 total / 1 failed / 0 skipped**, and `ci / contracts` failed on pushed merge `d0291d09` on that same test.

**decision-needed:**

- [x] [Review][Decision→Patch] RESOLVED 2026-09-23 (owner: option 2, stronger form -- the owner posts one hand-written authorization comment on `#352`; the story record and proof packet cite its id; no re-mint). Owner authorization of the two posted receipts is not retained -- The six `SUPERSEDED — INVALID TIMESTAMP-MISMATCH ATTEMPT` comments (2 s apart, 06:00:03Z-06:00:14Z, each edited 1 s later) and the two kept receipts (06:00:46Z, 06:01:05Z) show scripted posting through the `jpiquot` credential. Receipt limitation 4 discloses tooling-composed posting, but nothing in the packet records that the owner authorized this collection run (Ask First: "creation or collection of owner receipts"; Never: "fabricate approvals"). Options: confirm the owner authorized it and accept as-is; or confirm it and add a retained authorization reference; or withdraw the receipts (parity reverts to unavailable).
- [x] [Review][Decision→Patch] RESOLVED 2026-09-23 (owner: option 1 -- record per role what counted as each sign-off, name the missing dedicated Security Reviewer record, no new review artifacts). DW-508 "fresh architecture/security/test sign-off" is only partly recorded -- The task line is now `[x]` with "Technical re-mint and AI sign-offs are complete", but the only Story 3.15 record is "Story 3.15 architecture sign-off passed" (Spec Change Log, 2026-09-23 reseal entry). No security or test sign-off of the 3.15 trust-path changes (ancestry gate, `GIT_*` scrub, finite-float loaders) is retained; the v4 `reviews/security.json` and `reviews/test.json` bind Story 4.15 subject `8a59c89c...`. Options: accept the Test Architect receipt plus the 2026-09-23 code review as the test/security sign-off; or obtain and retain dedicated 3.15 security/test sign-off records.

**patch:**

- [x] [Review][Patch] Cite the owner's hand-written `#352` authorization comment for the 2026-09-23 collection run in the story record and proof packet (from D1) [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:188]
- [x] [Review][Patch] Record which artifact counted as the DW-508 architecture, test, and security sign-off, and state that no dedicated Security Reviewer record exists, in the story record and the DW-508 ledger resolution (from D2) [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:281]
- [x] [Review][Patch] Flipping the 3.15 row to `review` turned the Contracts lane red, and the records claim a green run that preceded the flip -- `DeployedRuntimeParityClosureTests.CorrectedLifecycleRowsRetainTheirCorrectedStatus` still pins `in-progress` (and its comment says "fails closed at 0 of 3"). Reproduced locally (2105/1 failed) and in CI (`ci / contracts`, run 35826919892). Update the pin and comment to the row's post-review status, re-run the full suite, and correct the "2096 passed, 0 failed" claims in the story record and proof packet. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4275]
- [x] [Review][Patch] The guarded 3.15 comment block in the sprint tracker contradicts itself -- "the packet fails closed at 0 of 3 receipts and grants nothing ... still in progress" sits directly above "three current roster-bound receipts validate ... the verifier now exits 0". The digest-order guard cannot see prose. [_bmad-output/implementation-artifacts/sprint-status.yaml:137]
- [x] [Review][Patch] The assembler's run-from-bound-path check is no longer reached by any test -- The `/tmp` shadow copy in `AssemblerRefusesExecutionOffTheBoundRepositoryPath("assembler")` now fails at `repository_root()`; the two new tests stop at the lineage check. The verification-gap layer confirmed in a scratch clone that deleting `if path != expected` keeps the suite green and lets an off-path copy re-mint and bind its own bytes. Add an in-clone case asserting "assembler is not executing from the bound repository path" and an unchanged closure. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3991]
- [x] [Review][Patch] The subject drift guard does not know the latest superseded subjects -- `SubjectRestatingSurfacesNameTheCurrentSubject` checks order only against `bb58d691`/`dab64f5f`/`a8cc777e`/`86c59c79`. A surface naming `aafe9040...` or `a5c07d17...` (both actually named by records before this commit) first would stay green. Add them as superseded constants. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2820]
- [x] [Review][Patch] The deferred-work ledger still lists the batched DW-506/507/508/511 as `status: open` while the spec task that owns them is `[x]` -- A ledger sweep will re-open finished work. Record the resolution on each, including what remains open (for example DW-506's third hand-written encoder in the capture tool). [_bmad-output/implementation-artifacts/deferred-work.md:4026]
- [x] [Review][Patch] `AssemblerPreservesWhitespaceAtEndOfCheckoutPath` lacks the `OperatingSystem.IsWindows()` guard the class uses elsewhere -- Win32 strips trailing spaces from directory names, so the case cannot pass on Windows. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4124]

**defer:**

- [x] [Review][Defer] The Story 3.14 records still say `v3.py`/`validate-corrective-release-evidence.py` are "do not change; freeze-verify only" and quote an old `v3.py` digest [_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release-2.md:61] — deferred: the fix edits another story's spec. DW-508 authorized the change and the predecessor digest `4d1a0c33...` still reproduces.
- [x] [Review][Defer] The receipt limitations do not state that both owner roles are one account, and limitation 4 ("posted with the rostered role holder's credential") does not describe the unposted `bmad:murat` record [tools/deployed_runtime_parity_handlers/v1.py] — deferred: pre-existing handler constants. Changing them re-mints the subject and burns all three receipts.
- [x] [Review][Defer] The exact-second `created_at == updated_at == accepted_at` rule forces scripted retry posting (six superseded comments on `#352`) and proves nothing about when a human decided [tools/deployed_runtime_parity_handlers/v1.py] — deferred: pre-existing acceptance-envelope design, not introduced by this commit.
- [x] [Review][Defer] Story 4.15 v4 reseal governance: `V4_REVIEW_DATE` is hard-coded in `validate-oq8-platform-evidence.py`, so every reseal edits the gate's own hashed validator; the three v4 review receipts share one `issuedAt` (06:10:25Z); `reviews/test.json` defers "final active v4 validation" with no retained record; and the reseal rides inside a Story 3.15 commit [tools/validate-oq8-platform-evidence.py:409] — deferred: pre-existing Story 4.15 validator design, owned by Story 4.15. It was owner-authorized, and the default OQ8 validator passes.

**Rejected**

- spec-edit — Spec frontmatter `status: 'done'` versus sprint row `review`: fixing it edits the spec under review. This workflow's status sync sets both.
- spec-edit — The spec's Verification "2096 passed, 0 failed" claim is false at this commit: it edits the spec. The story/proof-packet copies are covered by the red-lane patch.
- spec-edit — The triage-table rows "Issued subject has no roster-bound receipts" and BH9/EH5 `defer` are stale after 3/3 and the v4 reseal: fixing them edits the spec.
- spec-edit — `review_loop_iteration: 6` is unchanged: fixing it edits the spec, and it is a historical counter.
- spec-edit — The Change Log cites uncommitted intermediate subjects `171d8e3b...`/`02f9dd40...` with no retained evidence: fixing it edits the spec. It is also low, being narrative about superseded states.
- false — The ancestry gate "is satisfied by any clone", so DW-507 is unsettled: a clone descending from the release commit *is* the release lineage. An altered assembler at the bound path binds its own digest, which re-mints the subject and invalidates every receipt, and the isolated pinned verifier recomputes every byte. No fail-open was demonstrated.
- false — `unrelatedHead: true` uses related history: `SourceSha^` is a HEAD that does not descend from the release commit, which is exactly the predicate under test (`merge-base --is-ancestor` exit 1 is asserted).
- false — `_reject_non_json_number`/`_finite_json_float` are duplicated in four files: this is intentional. Each dispatcher parses before importing any handler, and each file is independently hash-pinned, so a shared import would reopen the trust boundary.
- false — The new deferred-work section uses a legacy bullet format: every neighbouring 2026-09-21/22 section uses the same `- source_spec:` bullet format.
- low — `RunProcessWithEnvironment` inherits caller `GIT_DIR`/`GIT_WORK_TREE`, so `git update-ref HEAD` could hit the real repository: this was checked, and `git rebase -x` does not export `GIT_DIR`, and the only repository hook is `commit-msg`. It needs a manually exported `GIT_DIR`, and the fix adds a guard.
- low — The temp directory might sit inside a Git work tree, changing the refusal message: `Path.GetTempPath()` is outside any repository in supported environments.
- low — The `ShouldBe(root)` precondition may fail on a symlinked root: this is a spurious-failure edge only on unusual layouts, and the fix adds normalization.
- low — `docs/ci.md` omits the retained `86c59c79...` set from its list: `docs/ci.md` is a sealed Story 4.15 v4 gate input, so a one-word fix forces another reseal.
- low — The `docs/ci.md` v4 paragraph still describes only what v3 binds: same sealed-input cost. The v3 text is still historically true.
- low — `CheckedInPacketClosesAtThreeRosterBoundReceipts` asserts only `pass:`: the subject is drift-bound by `SubjectRestatingSurfacesNameTheCurrentSubject`, and the verifier recomputes it from `closure.json`.
- low — The story record's "Why the subject changed" list stops at the Group A re-mint: this is cosmetic historical narrative, and the current subject is explained in the verdict section.
- low — The six superseded posting attempts have no retained comment IDs: they remain visible on `#352` with `SUPERSEDED` bodies (enumerated via `gh api` during triage).
- low — A shallow checkout gets an opaque "release lineage could not be verified", and Git stderr is discarded: CI checks out with `fetch-depth: 0`, it is an operator-only producer, and the fix adds diagnostics.
- low — One `feat`-typed commit bundles the 3.15 closure, trust-path fixes and a 4.15 reseal: the history is already pushed, no release fired (CI red), and `eeeae00b` is already a `feat` in the same unreleased window.
- reject (prior disposition) — The Test Architect receipt is self-attested rather than "authenticated": this was already rejected as a frozen-intent edit, and it is disclosed in the receipt limitations and operator records.

### Review Findings (2026-09-24, review-patch commit `a37ec86f`)

Disposition (2026-09-24): D1 resolved as option (1) and converted to a patch; the eight patches are left as action items. Status moved to `in-progress` in this frontmatter, the `3-15` sprint row, and the `CorrectedLifecycleRowsRetainTheirCorrectedStatus` row pin, all in the same change.

Scope: `ea87414a..a37ec86f` (7 files, +207/-34, 457 diff lines) -- the commit that applied the eight `a2f5cba2` patch action items. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed. Independently reproduced at `a37ec86f`: focused closure class 214/214, both closure classes 504/504, full Contracts suite 2106/2106 (one layer saw an unrelated timing flake, see defer). All eight prior patches are present; two are only partly effective (the in-clone path regression and the superseded-subject guard, below). Note: `a37ec86f` and `ea87414a` are unpushed, and `origin/main` (`7a94ad5d`, #362) independently pinned the same `3-15` row to `review` with a different comment, so merging conflicts in `DeployedRuntimeParityClosureTests.cs:4275`.

**decision-needed:**

- [x] [Review][Decision] The cited D1 authorization comment is tooling-composed, not hand-written — D1 chose "the owner posts one hand-written authorization comment on `#352`". Live comment `5803577826` (`jpiquot`, 2026-09-23T21:53:37Z) is an agent-framed "Authorization request: … requires a hand-written owner authorization comment …" paragraph that quotes one owner line, "I Jérôme Piquot, owner; authorize". The scope text is the agent's; the owner's words name no scope. Options: (1) accept it as satisfying D1 and have the records say it is an agent-composed comment quoting the owner's words; (2) owner posts a genuinely hand-typed comment that states its own scope, and the records cite that id instead; (3) accept as-is with no record change. **Resolved 2026-09-24: option (1)** — became the patch below.

**patch:**

- [x] [Review][Patch] (from D1, option 1) Describe comment `5803577826` accurately: an agent-composed request that quotes the owner's written line "I Jérôme Piquot, owner; authorize" verbatim, and treat that quoted line as the D1 authorization. Stop calling the comment "hand-written". Update it together with the after-the-fact ratification patch in both records. [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:64]
- [x] [Review][Patch] The in-clone path regression cannot observe a re-mint: `--no-checkout` leaves the Story 3.14 producer inputs absent, so with `if path != expected` disabled the shadow still exits 1 ("producer input is unavailable") and both closure hashes stay equal — only the message assertion discriminates, and a guard demoted to a warning would pass. Reproduced in triage: in a checked-out `--shared` clone the unguarded off-path copy re-mints (`subject=sha256:7d20ed33…`) and rewrites `closure.json`. Drop `--no-checkout` for this test so the unchanged-closure assertions can fail, and have the DW-507 resolution say what the test proves. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4106]
- [x] [Review][Patch] `a37ec86f` set the spec frontmatter to `done` while the tracker row is `review`, the Change Log does not record the flip, and nothing pins the 3.15 spec frontmatter (only Story 3.13's is pinned). Pin `FrontmatterValue` of this spec next to the row pin in `CorrectedLifecycleRowsRetainTheirCorrectedStatus` so the two surfaces cannot diverge silently; this workflow's status sync sets both values. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4277]
- [x] [Review][Patch] The records call comment `5803577826` "the owner's separate authorization of the 2026-09-23 receipt-collection run", but it was created 2026-09-23T21:53:37Z, ~16 h after the receipts it covers (06:00:46Z, 06:01:05Z) and after `a2f5cba2`. State its `created_at` and that it is an after-the-fact ratification in the story record and proof packet. [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:64]
- [x] [Review][Patch] The superseded-subject guard (BH8) matches only full 64-hex digests, so a surface that first names a superseded subject in the elided `aafe9040...` form — the exact form BH8 described, and the dominant form in these records — still passes. Also match the 8-hex elided prefixes of the subject constants when finding the first subject; confirm the four guarded surfaces stay green. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:2843]
- [x] [Review][Patch] `AssemblerRefusesExecutionOffTheBoundRepositoryPath`'s summary still says it proves refusal "to run from a copied script path" and that "neither bound-path check ran off the repository file", while its `assembler` case now stops at `repository_root()` ("bound repository root could not be verified"); the bound-path check is proven by the new in-clone test. Correct the summary. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:3956]
- [x] [Review][Patch] The DW-508 resolution is silent on the "subprocess hardening and path-redaction items recorded in the Group P findings" that DW-508's own reason includes; record that they landed in the re-mint and cross-reference the newly deferred capture `subprocess` shadowing gap (EH1). [_bmad-output/implementation-artifacts/deferred-work.md:4046]
- [x] [Review][Patch] `epic-3-retro-item-22` ("collect the three owner receipts on issue #352 and re-run the assembler, or move 3-15 off done") is still `status: open` although its condition is met; close it with a note citing the 3/3 verifier pass. [_bmad-output/implementation-artifacts/sprint-status.yaml:445]

**defer:**

- [x] [Review][Defer] `prd.md` still presents superseded subject `aafe9040…` as current with 0/3 receipts and a FAIL/BLOCKED G-RUNTIME-PARITY row, and OR15 tells readers to adopt `aafe9040…` [_bmad-output/planning-artifacts/prd.md:613] — deferred: pre-existing; the fix edits a planning artifact outside this story, and `prd.md` is not a guarded surface.
- [x] [Review][Defer] `ProofPacketDaprConflictProcessContractTests.ProcessContractDistinguishesOwnedExternalAndExitedProcesses` timed out (`WaitForExit(5000)`) once in a loaded full-suite run and passed twice alone [tests/Hexalith.EventStore.Contracts.Tests] — deferred: pre-existing timing flake, not touched by this diff.

**Rejected**

- false — D2 names no architecture source: D2's resolution is "record per role what counted as each sign-off", and its finding text names the Change Log statement as the only 3.15 architecture record; the records count exactly that.
- false — Carried EH3 (failed `docker run` leaves a created container) lacks a ledger entry: not open work — it is a recorded owner policy (do not `rm` on nonzero `docker run` exit), closed as the `[x]` patch at line 1126.
- false — The new test skips the CLI entry: the guard reads `__file__`, which `exec_module` sets to the shadow path; the `__main__` block only calls `main()`.
- false — DW-507 accepts any `--shared` clone as the bound root: a clone descending from the release commit that executes its own `tools/` binds its own honest bytes; the guard exists to stop a copy stamping another tree's digests.
- false — No mutation evidence for the new test: the verification-gap layer disabled the check and the test's message assertion fails.
- spec-edit — Task line "Technical re-mint and AI sign-offs are complete" is unqualified: fixing it edits the spec under review.
- spec-edit — BH10/BH11 were rejected as spec edits while `a37ec86f` edits the spec; stale "agree at `in-progress`" and "Nine subjects, eight re-mints" lines remain: fixing them edits the spec.
- spec-edit — The new Change Log entry is dated 2026-09-23 while its triage rows say 2026-09-24: fixing it edits the spec (the commit is 2026-09-23 in UTC).
- spec-edit — The 2026-09-24 build-review rows lack their own section/checkboxes, EH4's severity cell reads `false`, and IDs repeat across rounds: fixing them edits the spec.
- low — `AssemblerPreservesWhitespaceAtEndOfCheckoutPath` returns instead of `Assert.Skip` on Windows: matches the class's five existing guards, and `ci / contracts` runs only on ubuntu.
- low — The new in-clone test has no Windows guard: Windows is not a supported lane for these python-driven tests.
- low — `Path.GetFullPath` vs Python `resolve()` on a symlinked temp dir (macOS): CI is ubuntu and development is Linux/WSL.
- low — Test preconditions inherit caller `GIT_DIR`/`GIT_WORK_TREE`: only under a git hook, never in CI or a normal run.
- low — The `merge-base` precondition has no diagnostic message: CI uses `fetch-depth: 0`; cosmetic.
- low — The proof packet's new "Reproduce" claims name no commands, SHA or green CI run: the counts were reproduced independently (2106/2106, 214/214); no CI run exists because `a37ec86f` is unpushed.

### Review Findings (2026-09-24, tracker-reconciliation range `edadf77b`, `6690475a`, `ab40348d`, `06aaf950`)

Scope: the four Story 3.15 commits after `a37ec86f`, concatenated per commit (unrelated interleaved OQ8/release merges excluded) -- 13 paths, +413/-76, 947 diff lines. Four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed. Independently re-run at `06aaf950`: full Contracts suite 2121 total, 2118 passed, 3 failed -- all three are Story 4.15 OQ8 v5 tests (`Oq8V5CandidateTests` x2, `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`), outside this diff; every Story 3.15 test, including the new PRD guard, passes.

Disposition (2026-09-25): the four patches were first left as action items, then applied the same day, uncommitted, with the lifecycle status left unchanged. Patch 1 was mutation-checked: an uncommitted `if False and path != expected` edit to the worktree assembler turns `AssemblerRefusesOffPathCopyInsideValidReleaseLineage` red (exit code 0 instead of 1). The assembler was then restored byte-identical (SHA-256 `30328249...`). Full Contracts suite after the patches: 2121 total, 2118 passed, and the same 3 Story 4.15 OQ8 v5 failures as at `06aaf950`. The owner chose to leave the lifecycle status unchanged -- spec `done`, tracker `review` -- per the frozen 2026-09-24 tracker-reconciliation Decision, rather than the workflow's `in-progress` flip, which would have required rewriting the row and spec pins, the guarded handoff comment, and the PRD G-RUNTIME-PARITY row together.

**patch:**

- [x] [Review][Patch] The in-clone path-guard regression now runs the committed `HEAD` assembler, not the worktree bytes under test: `6690475a` dropped `--no-checkout` as asked but also deleted `CopyDirectory(Path.Combine(root, "tools"), Path.Combine(temporary, "tools"))`, so the shadow copies `temporary/tools` (HEAD). An uncommitted edit that demotes `if path != expected` stays green locally until committed -- the DW-507 "load-bearing" claim holds only for committed code. Restore the overlay after the checked-out `--shared` clone (the checkout keeps the Story 3.14 producer inputs present) [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4128]
- [x] [Review][Patch] Two new prose-bound guards carry no in-file guard marker -- the d45206f7 trap again. The 4-line "2026-09-24 tracker handoff" comment asserted verbatim by `CorrectedLifecycleRowsRetainTheirCorrectedStatus` sits above the `>>> GUARDED COMMENTS` fence (`sprint-status.yaml:131-134`, fence at :135), and `PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl` binds exact `prd.md` sentences (summary bullet, Story 3.15 history row, G-RUNTIME-PARITY/G-HIGH-RISK/G-PUBLICATION-AUTH/G-CONSUMER rows) with no notice in `prd.md`, so a comment cleanup or a bmad-prd/correct-course rewrite turns Contracts red without warning. Move the handoff comment inside the fence (or give it its own `>>> GUARDED <<<` markers) and add an HTML guard-notice comment naming the test beside the bound `prd.md` lines [_bmad-output/implementation-artifacts/sprint-status.yaml:131]
- [x] [Review][Patch] Epic 3 retrospective item 22's closing note says "At closure of this retrospective item, the Story 3.15 lifecycle row was in-progress pending review", but the same commit (`6690475a`) that closes the item moves the row from `in-progress` to `review`. State the row value actually committed at closure (`review`, spec `done`) [_bmad-output/implementation-artifacts/sprint-status.yaml:456]
- [x] [Review][Patch] The `a37ec86f` deferral "`prd.md` still presents superseded Story 3.15 subject `aafe9040...` as current" is fixed by `06aaf950`'s PRD reconciliation and its new guard, but the ledger entry has no `resolution:` line, so a bmad-loop sweep re-triages it as open. Add a resolution citing `06aaf950` and `PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl` [_bmad-output/implementation-artifacts/deferred-work.md:4839]

**Rejected**

- false — The row/spec pin locks in the `done`/`review` split the `a37ec86f` patch was meant to prevent: the patch's goal was that the surfaces cannot diverge *silently*; both values are now pinned, and the split itself is the human-owned 2026-09-24 Decision in the frozen `spec-3-15-tracker-reconciliation.md` intent. The `[x]` item's wording is a spec edit.
- decided — The row should be `awaiting-operator`, not `review`: the frozen tracker-reconciliation Decision keeps `review` deliberately; changing it requires renegotiating human-owned intent.
- false — The "Current canonical subject" header makes the spec's first-subject guard green by construction: the Code Map names `bb58d691...` explicitly as a superseded subject, so the header truthfully puts the current subject first; the guard applies the same first-subject rule to all four surfaces.
- false — The PRD's G-PUBLICATION-AUTH command was removed before the release owner defined a replacement (proposal §4 A.2): the removed path and `tools/validate-publication-authority.py` never existed, the edit invents no replacement record, and keeping it would present superseded `aafe9040...` as current.
- false — OR15 drops Story 3.15 without a dated decision: the OR15 row still narrates 3.15 with the dated 2026-09-24 state that proposal §4 A.2 prescribes.
- false — The story record's and proof packet's "2108/2108" are stale: both scope the count to the run "after the 2026-09-24 in-clone and elided-subject regressions", a dated measurement.
- false — The retained-closure hash assertion can never fail: it guards against the assembler resolving back to the original checkout (the regression class the neighbouring `GIT_*` redirect test covers); the packet-closure assertion is unaffected.
- false — 8-hex elided prefixes could collide: all eight subject constants have distinct prefixes (`7d64f87e`, `84dee6e5`, `86c59c79`, `a5c07d17`, `a8cc777e`, `aafe9040`, `bb58d691`, `dab64f5f`).
- false — DW-508's "2026-09-24 owner-authorization closure EH1 entry below" is ambiguous: it resolves to the deferred-work heading of that name, whose entry is the smoke-capture `subprocess` shadowing gap.
- false — The PRD guard's `File.ReadAllText` + `Split('\n')` breaks on CRLF: every check is `StartsWith`/`Contains`, which a trailing `\r` does not affect.
- low — The elided-prefix regex matches only 8-hex elisions: no guarded surface uses a longer elision; widening adds matching complexity.
- low — `summary[summary.IndexOf("On 2026-09-24")..]` throws `ArgumentOutOfRangeException` when the anchor is missing: the test still fails loudly at the right line.
- low — The PRD current clauses reject only `aafe9040...`, and the SM6/OR15 rows are unguarded: coverage extension; `aafe9040...` is the only stale subject the PRD ever carried.
- low — The handoff comment is asserted as a whole-file substring, not by adjacency to the 3-15 row: needs new adjacency logic for an unlikely relocation.
- low — The elided-subject theory passes 3 subjects and has no negative controls: the production guards pass all 8, and prefix uniqueness is verified above.
- low — No mutation run is recorded for the patched in-clone test: subsumed by the in-clone overlay patch; the re-mint was reproduced in the prior triage.
- low — `ab40348d` is typed `fix:` and carries three unannounced submodule bumps; `06aaf950` is `docs:` but adds a test: already published on `origin/main`; all three gitlinks are reachable on their remotes; the bump is disclosed in the tracker spec's Implementation Notes, and its FrontComposer Gate 2c consequence is already deferred.
- low — The PRD omits the retained-envelope authenticity gap (BH1): G-RUNTIME-PARITY stays non-authorizing and G-HIGH-RISK blocked; the gap is recorded in deferred work.
- low — PRD §11.3's renamed column "Identity/status at stated date" leaves undated rows ambiguous: cosmetic; the fix dates every row.
- low — `epic-3-context.md`'s Goal dropped two scope sentences: their content survives in the same file's Technical Decisions.
- low — `ab40348d` briefly added a false G-COMPAT pass claim to `epic-3-context.md`: already corrected by `06aaf950`.
- spec-edit (partly resolved 2026-09-25) — The "2026-09-24 (review-patch completion)" Change Log entry now states the `done`/`review` pair; the `a37ec86f` disposition line is still stale and `review_loop_iteration` was not bumped.
- spec-edit (resolved 2026-09-25) — Verification now dates the 2108/216 snapshots and records the later 2118-passed, 3-failed Contracts run; the Change Log's 506/506 count remains tied to its 2026-09-24 run.
- spec-edit — D1's resolution does not name who chose option (1).
- spec-edit — Triage IDs collide ("2026-09-24 current EH1/EH2" beside earlier EH2/EH3).
- spec-edit — `spec-3-15-tracker-reconciliation.md` bookkeeping: `review_loop_iteration: 0`, applied patch rows not marked, its Implementation Note says the PRD edit "remains in the worktree", and `done` without a green full-suite run.

### Review Findings (2026-09-26, Story 3.15 tooling chunk at `f9a4b5be`)

Scope: seven verifier, handler, assembler, and smoke-capture files, diffed from the story's recorded baseline `94591f35`; +2742/-36, 2942 diff lines. Blind Hunter and Edge Case Hunter returned findings. Verification Gap Reviewer and Acceptance Auditor ran but returned no findings, which this workflow records as empty layers. At triage, the checked-in 3/3 closure passed the pinned verifier. The owner's subsequent decision changed the bound producer and re-minted the packet at 0/3.

**decision-needed:**

- [x] [Review][Decision→Patch] RESOLVED 2026-09-26 (owner: option 1, re-mint the subject). Isolate curl from the operator's default configuration before accepting a Production `/alive` smoke. The capture now passes `-q` as curl's first argument, its recording test checks that position, the producer is re-minted, and the three prior receipts are retained as superseded. Current parity fails closed at 0/3 pending fresh acceptances. [tools/capture-corrected-deployed-runtime-parity-smokes.py:228]

**defer:**

- [x] [Review][Defer] The assembler imports `v1` and its predecessor before `verify_handler_provenance`, so changed or shadowed local module bytes can execute before the isolated verifier later rejects the result [tools/assemble-corrected-deployed-runtime-parity.py:20] — deferred: existing DW-452 producer import gap; the pinned verifier remains the verdict authority, and a producer change re-mints the accepted subject. (BH1, medium)
- [x] [Review][Defer] Smoke capture imports `subprocess` from the caller's Python path, so a repository-local shadow can execute during evidence production [tools/capture-corrected-deployed-runtime-parity-smokes.py:19] — deferred: previously recorded EH1 risk in the sealed capture producer; rework needs a controlled re-mint. (BH2, high)
- [x] [Review][Defer] Retained GitHub comment envelopes are internally checked but have no independent live or signed authenticity proof; a forged envelope with matching fixed fields can pass as an owner source [tools/deployed_runtime_parity_handlers/v1.py:1049] — deferred: the 2026-08-22 accepted consistency-versus-independence trade-off and existing ledger item require a new evidence contract and receipts. (BH4, medium)
- [x] [Review][Defer] The derived NuGet.org URL has no retained response or registry attestation binding that service to the archive bytes [tools/deployed_runtime_parity_handlers/v1.py:649] — deferred: the origin limitation is already recorded for this accepted packet; package and nuspec checks establish content identity, not transport origin. (BH5, medium)
- [x] [Review][Defer] Any nonzero `docker inspect` result is treated as container absence, including daemon or permission failure; cleanup can be reported as passed while a timed-out run's container remains [tools/capture-corrected-deployed-runtime-parity-smokes.py:114] — deferred: previously recorded sealed-producer risk. (BH6 + EH3, medium)
- [x] [Review][Defer] A single immediate inspect can precede late container creation after `docker run` times out, leaving the container behind after cleanup reports success [tools/capture-corrected-deployed-runtime-parity-smokes.py:107] — deferred: previously recorded race in the sealed capture producer. (BH7, medium)
- [x] [Review][Defer] A nonzero `docker run --detach` can leave a created container, but the cleanup branch skips inspect when `container_created` is still false [tools/capture-corrected-deployed-runtime-parity-smokes.py:213] — deferred: existing owner policy avoids force-removing a possibly external same-named container; changing it requires a deliberate cleanup policy and re-mint. (BH8 + EH2, medium)
- [x] [Review][Defer] Retained files are read before any size limit, and `archive.read()` can expand an oversized nuspec in memory [tools/deployed_runtime_parity_handlers/v1.py:398; tools/release_evidence_handlers/v3.py:488] — deferred: existing DW-413/DW-433 resource-exhaustion gap. (BH11 + EH5, medium)
- [x] [Review][Defer] The subject-bound limitation says every receipt was posted with a role holder's credential, but the Test Architect record is local and self-attested [tools/deployed_runtime_parity_handlers/v1.py:83] — deferred: previously recorded BH7 wording mismatch; changing the limitation would re-mint the subject again. The prior 3/3 receipts are already superseded. (BH12, medium)
- [x] [Review][Defer] On an incomplete verifier run, rollback restores only `closure.json`; `build_document` may already have rewritten the registry, inventory, and subject, leaving the old closure bound to new support files [tools/assemble-corrected-deployed-runtime-parity.py:98,489] — deferred: this incomplete rollback is already tracked in deferred work; correction changes sealed producer bytes. (EH1, medium)

**Rejected**

- low — BH9: `--force` can leave partial smoke files after a write failure, but overwriting populated evidence is the explicit purpose of that flag, the verifier rejects mixed evidence, and an atomic multi-file producer protocol is disproportionate to this rare interruption.
- low — BH10: a failed capture's rerun hint omits `--force`, but the next refusal exits 2 and names both `--force` and a fresh root; correcting a bound producer string would invalidate current receipts for one extra diagnostic step.
- false — EH4: the code deliberately calls the field `repository_signature_entry_present` and checks only one `.signature.p7s` archive entry; it does not claim to authenticate PKCS#7 bytes. The 2026-08-22 owner decision explicitly kept that shallow check.
- false — EH6: alternate evidence correctly leaves packet `closure.json` outside the exempt set; `_evidence_relative_to_packet` adds only the selected evidence file, so the claimed rejection is the intended closed-inventory behavior.

### Review Findings (2026-09-26, re-mint and re-acceptance range `f9a4b5be..61ebd332`)

Scope: `git diff f9a4b5be..61ebd332`, limited to the 59 paths touched by `400589a5`, `f480688c`, the Story 3.15 slice of `cdad8278`, and `61ebd332`, with submodule gitlinks excluded. That is 63 files, +862/-162, and 1839 diff lines, of which 23 are binary evidence. Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) and none failed. Checks run at clean HEAD `61ebd332`: the Story 3.15 verifier exits 0 at 3/3. `python3 tools/oq8-v5-packet.py --validate-active` exits 1 with `V5 source tree changed outside reviewed evidence and selector`, and `python3 tools/validate-oq8-platform-evidence.py` exits 1.

**decision-needed:**

- [x] [Review][Decision→Patch] RESOLVED 2026-09-26 (owner: option 1, correct `docs/ci.md` to the 3/3 verdict and add a verdict assertion to the existing `CiDoc…` test before the Story 4.15 reseal; also update the story record, the spec Verification notes, and the Story 4.15 reconciliation deferred-work hash). `docs/ci.md` states the opposite verdict from the packet — The Story 3.15 section says `receipts=0 verifier_exit=1`, that the verifier "rejects the claim because no receipt binds the current subject", that the receipt address is "current, empty", and that "the present index claim is not granted" [docs/ci.md:570,579-580,603]. The checked-in packet passes at 3/3 and selects the pinned index. The records justify leaving this 0/3 snapshot until Story 4.15 reseals, but that reason does not hold. This range already moved the guide off the active v5 pin: `8e12004e…` → `e48417d8…` → `28e02f35…`. The v5 seal also binds the whole tracked tree, so v5 is red at clean HEAD whichever text the guide carries. No test catches the contradiction. `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` compares only the digest set, and `docs/ci.md` is not a row of `OperatorRecordsStateTheCurrentSubjectAndVerdict` or `SubjectRestatingSurfacesNameTheCurrentSubject`. All four layers raised this. Options: (1) correct the section to the 3/3 verdict, including receipt IDs `5844573563`/`5844574016`, and add a verdict assertion scoped to the `### Story 3.15` slice; (2) keep the snapshot but add an explicit in-section "pre-acceptance snapshot, do not cite" notice, and guard that the notice stays while the packet is at 3/3; (3) leave it as recorded.

**patch:**

- [x] [Review][Patch] Correct the `docs/ci.md` Story 3.15 section to `receipts=3 verifier_exit=0`, the validated claim, receipt comments `5844573563`/`5844574016` plus the self-attested Test Architect record, and the bounded selected identity. Write superseded subjects in short form so the digest-set check keeps passing. In `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests`, assert `receipts={n} verifier_exit={0|1}` within the section slice. In the same change, update story record line 341, the spec Verification snapshot notes, and the `28e02f35…` hash in the Story 4.15 reconciliation deferred-work entry [docs/ci.md:566-605]
- [x] [Review][Patch] The new superseded subject `c98fdef2…` (`PreRecaptureSupersededSubjectSha256`) is missing from the known-subject lists in `OperatorRecordsStateTheCurrentSubjectAndVerdict` and the PRD current-summary/current-history `ShouldNotContain` checks. A closure record or proof packet that leads with `c98fdef2…`, or a PRD current slice that names it, stays green. The sibling `SubjectRestatingSurfacesNameTheCurrentSubject` already lists it [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4565,4493,4505]
- [x] [Review][Patch] No test mutates the value of `accepted_limitations`. This range corrected limitation 4, which is enforced only by the `receipt["accepted_limitations"] != list(REQUIRED_LIMITATIONS)` clause at `v1.py:1035`, and dropping that clause leaves every test green. Add a `limitations-mismatch` row to `InvalidAcceptanceNeverAuthorizesParity`. It should rewrite one receipt's limitation 4 to the superseded "Every acceptance receipt is composed…" wording and expect `acceptance receipt does not bind` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:830]
- [x] [Review][Patch] Nothing guards the new `superseded-packets/` audit tree. Its README says the tree is a byte-for-byte copy and that its `subject.json` hashes to the directory name. Both are true today, but no test reads the tree, whereas the sibling `superseded-acceptances/` tree is pinned per file with an exact file set. Add a pin for the README and every file, an exact file-set check, and `sha256(subject.json) == directory name` [_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-packets/README.md:1]
- [x] [Review][Patch] The sprint-status comments dropped two things that a previous review patch required the tracker to keep. The first is the identity caveat: both owner roles map to one authenticated account, the Test Architect record is self-attested, and 3/3 is not three-party review. The second is the list of missing G-HIGH-RISK controls: the matrix, the validator, the independent second-identity check, and the sealed CI control. Restore both as comment lines outside the verbatim-guarded four-line handoff block [_bmad-output/implementation-artifacts/sprint-status.yaml:153]
- [x] [Review][Patch] Narrative records still describe 0/3 after collection. `superseded-acceptances/README.md:86-88` says "The new subject has zero acceptances until…"; that file is hash-pinned in `SupersededArtefacts`, so re-pin it. `deferred-work.md:4901` says "the packet remains at 0/3 receipts" in a `resolved` status line. `3-15-owner-validation-comment-draft.md:8` says the owner decisions "are still missing" for a subject that will never receive them [_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances/README.md:88]

**defer:**

- [x] [Review][Defer] The retained smoke logs and `smoke-results.json` carry no producer digest, curl argv, or curlrc indicator, so the retained bytes cannot show that the fresh capture used the `-q` producer. That was the reason the Test Architect declined `c98fdef2…`. The claim now rests on narration plus the producer's current file hash [tools/capture-corrected-deployed-runtime-parity-smokes.py:228] — deferred: the Test Architect decision accepts this as a disclosed evidence limit (`3-15-test-architect-decision-66be1b4a.md`, "Limits and gate boundary"). Recording a producer digest in the smoke schema would re-mint the subject and burn all three receipts, so batch it with the next controlled re-mint.

**Rejected**

- false — "The spec is marked `done` on a red suite": the three failures are OQ8 v5 tests. OQ8 v5 was already red at `06aaf950` because the v5 seal binds the whole tracked tree, and spec `done` / row `review` is the frozen owner Decision of `spec-3-15-tracker-reconciliation.md`.
- reject (edits the spec under review) — The Verification section gives the v5 failure message as `V5 activation requires a clean committed checkout`, but at clean HEAD it is `V5 source tree changed…`. The historical paragraph still ends "the default OQ8 validator passes", and one sentence has no subject ("…at that historical run; were updated…"). `deferred-work.md` already records the correct clean-HEAD message.
- reject (edits the spec under review) — The Code Map has no `superseded-packets/` entry. The Tasks item claims the OQ8 seal-reconciliation record was refreshed, although that gate is `status: open`. The 2026-09-26 tooling-chunk BH12 is still marked Defer even though this range changed limitation 4.
- low — The Test Architect receipt `accepted_at` is the source-creation second (08:23:47Z), not the 07:39 decision minute, and it is not linked to the decision report. Three places disclose this: brief lines 84-88, the proof packet's "Current acceptances" table note, and limitation 3. Linking the report would require a source-schema change and a re-mint.
- false — The Test Architect decision report still says 0/3 and "no receipt created": it is a point-in-time report dated 07:39 UTC and was accurate then. The later transcription is recorded in the brief and the proof packet.
- false — "The re-mint did not batch the deferred producer fixes (subprocess shadowing, Docker cleanup)": the owner chose option 1 (re-mint for `-q` only) in the 2026-09-26 tooling-chunk review, which deferred those items at the same time.
- false — "`ShiftSmokeWindows(-1 day)` is undiagnosed": the fixtures extend the window up to +300 s past a capture that ended at 07:30:34Z, so `v1.py:774/811` "window lies in the future" fires when the tests run within minutes of the capture. The shift makes the duration guards observable.
- false — "A smoke that postdates the subject is not rejected": the subject hashes the smoke bytes, so the smokes must exist before the subject is minted.
- false — "The predecessor verification was run without `--manifest`": `--manifest` defaults to `tools/release-packages.json` (`tools/validate-corrective-release-evidence.py:262`), so both runs check the same package set.
- low — The new brief and Test Architect decision surfaces are not drift-guarded. They are completed or point-in-time records, and guarding them would add tests for little benefit.
- low — The superseded-acceptances README no longer states the total subject count. This is cosmetic: every retained set is still listed.
- low — The `deferred-work.md` tooling block re-records items that are already tracked. The entries cross-reference DW-413/DW-433, and the two entries on the limitations issue split it into halves that are consistent with each other.
- low — The commit messages of `61ebd332` ("modified the status…") and `400589a5` ("added new acceptance records") overstate their changes: the row value was unchanged and the receipts were moved byte-for-byte. Published history cannot be rewritten.

**Disposition (2026-09-26):** D1 was resolved as option 1. All 6 patches were at first left as action items, then APPLIED the same day at the owner's request. `docs/ci.md` now states `receipts=3 verifier_exit=0`, and its section hash is `3800b3d4…`, which the Story 4.15 reconciliation entry records. Verification: the Release build of the Contracts tests had 0 warnings and 0 errors. The focused Story 3.15 classes passed 237/237 (235 plus the new `limitations-mismatch` case and `SupersededPacketSnapshotIsRetainedByteForByte`), and the lifecycle test passed 1/1. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are the known Story 4.15 OQ8 v5 tests. Mutation checks all failed as intended: the ci.md verdict reverted to 0/3 fails `CiDoc…`; a closure record leading with `c98fdef2…` fails at `firstSubject`; a stray file and a one-byte smoke-log change under `superseded-packets/` each fail the snapshot test. The verifier's limitations clause is hash-pinned, so it cannot be mutation-tested in place; the new case instead proves the clause rejects the superseded limitation-4 wording with `acceptance receipt does not bind`. Status is **not** flipped to `in-progress`. The frozen `spec-3-15-tracker-reconciliation.md` Decision (2026-09-24) holds spec `done` / row `review`. That pair is pinned by the lifecycle row test, the spec-status test, the verbatim handoff comment, and the PRD. A flip must edit all four together and needs an explicit owner decision.

### Review Findings (2026-09-26, review-patch commit `ddf87f25`, reworded to `adc3aae8`)

Scope: `git diff 61ebd332..ddf87f25`, which is 8 files, +152/-27, and 343 diff lines. Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) and none failed. The verification-gap layer reported no gaps. Checks run at HEAD `ddf87f25`: the Release Contracts build had 0 warnings and 0 errors. `CorrectedDeployedRuntimeParityClosureTests` passed 219/219, and with `CorrectedDeployedRuntimeParitySmokeCaptureTests` the total is 237. `DeployedRuntimeParityClosureTests` passed 290/290. `sha256sum docs/ci.md` = `3800b3d4…`. `ddf87f25` is **not** on `origin/main`, which is still at `61ebd332`.

**decision-needed:**

- [x] [Review][Decision→Resolved] RESOLVED 2026-09-26 (owner: option 1). Before any push, the local commit message was amended to `test(contracts): guard Story 3.15 CI verdict and superseded packet snapshot`, which passed validation with pinned `@commitlint/cli@21.1.0` (exit 0, 0 problems). The tree `e6c792fd…` and parent `61ebd332` are unchanged, and the reviewed commit is now `adc3aae8`. Original finding: `ddf87f25` is typed `feat` but ships no feature, and it is still unpushed — Its subject is `feat: update documentation and tests for Story 3.15 …`, and its body repeats the subject. It changes only Markdown, YAML comments and one test file, with no production or `tools/` source. Once it reaches `main`, semantic-release reads `feat` as a minor version bump. That is the same "feat minor bump stays in history" problem as the Story 4.15 reseal. Unlike the earlier cases, this commit is local only, so it can still be corrected. Options: (1) before any push, reword it with `git commit --amend` to a commitlint-validated `test:` or `docs:` subject (e.g. `test(contracts): guard Story 3.15 CI verdict and superseded packet snapshot`); the tree is unchanged; (2) leave it and accept the minor bump. Raised by all three non-gap layers.

**patch:**

- [x] [Review][Patch] The new `docs/ci.md` verdict assertion only checks that the right token is present, so a guide stating both verdicts stays green. `ci[section..sectionEnd].ShouldContain("receipts=3 verifier_exit=0")` passes when the old `receipts=0 verifier_exit=1` sentence is still in the section next to a new 3/3 sentence. That is the exact contradiction D1 set out to prevent, and the same presence-only gap the test's own comment says it closed. Fix: every `receipts=(\d+) verifier_exit=(\d+)` match in the Story 3.15 slice must equal the expected token, and there must be at least one match. Deriving the exit code from the count is sound, because `CheckedInPacketClosesAtThreeRosterBoundReceipts` independently asserts that the live verifier exits 0 with 3 receipts [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:1314]
- [x] [Review][Patch] The restored sprint-status caveat has no guard. The three lines "not three-party review" and "G-HIGH-RISK still lacks its matrix, validator, independent second-identity check, and sealed CI control" were restored because an earlier comment edit dropped them unnoticed. No test asserts them, so the next cleanup can drop them again with the suite green. Fix: assert both phrases in the tracker's Story 3.15 GUARDED block, next to the existing verbatim four-line assertion in `CorrectedLifecycleRowsRetainTheirCorrectedStatus` [_bmad-output/implementation-artifacts/sprint-status.yaml:149]
- [x] [Review][Patch] The re-pinned superseded-acceptances README skips the receipt-free `c98fdef2…` subject. It says the `-q` change superseded the `7d64f87e…` set, and that receipts were "later collected for the re-minted subject `66be1b4a...`". But the `-q` change actually minted `c98fdef2…`, and only the fresh capture plus the limitation-4 correction produced `66be1b4a…`. The README never names `c98fdef2…` or the sibling `superseded-packets/` tree. An auditor reading it cannot rebuild the chain `7d64f87e → c98fdef2 → 66be1b4a`. Fix: name the intermediate subject and point to `../superseded-packets/`, then re-pin the `SupersededArtefacts` README hash [_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances/README.md:88]
- [x] [Review][Patch] The story record mixes pre-patch test counts with post-patch prose. Its Verification bullets still give "the two focused Story 3.15 classes passed **235/235**" and "**2131 total, 2128 passed**" as the post-collection result. The bullet this commit edited, just below them, describes the post-patch `CiDoc…` verdict binding, and the post-patch run is 237/237 and 2133/2130. Fix: add the dated post-review-patch counts, and keep the earlier counts labelled as pre-patch [_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:335]

**defer:**

- [x] [Review][Defer] The PRD current-slice checks catch only `c98fdef2` in short form [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4562] — deferred, pre-existing. The new lines use `PreRecaptureSupersededSubjectSha256[..8]`, but the older `IntermediateTrustPath…` (`aafe9040`) and `CurlIsolation…` (`7d64f87e`) checks use the full 64-hex value. A current slice naming `7d64f87e...` in short form therefore stays green. Today neither current slice contains any of these prefixes, so switching all four to `[..8]` would pass.

**Rejected**

- false — EH "`verifierExit` is inferred from the receipt count, not from running the verifier": `CheckedInPacketClosesAtThreeRosterBoundReceipts` (line ~219) runs the verifier on the live packet and asserts exit 0 together with `receipts.Count == 3`. So a 3-receipt closure the verifier rejects already turns the suite red.
- reject (edits the spec under review) — The Disposition calls `3800b3d4…` the ci.md "section hash", but it is the whole-file SHA-256 (`sha256sum docs/ci.md`). `deferred-work.md` says "the current guide hashes to", which is correct, and that is the entry the Story 4.15 reseal reads.
- reject (edits the spec under review) — The spec Verification section still has 235/235 and 2131/2128 labelled "post-collection actual", the stale `V5 activation requires a clean committed checkout` message, "the default OQ8 validator passes", and no `superseded-packets/` Code Map entry.
- low — The receipt comment IDs `5844573563`/`5844574016` in `docs/ci.md` are not test-bound to the retained sources. They match today. Any re-collection re-mints the subject, which already forces a `CiDoc…` edit of the same section, and adding the guard costs more than the drift is likely to.
- false — "`docs/ci.md` overclaims: 'the only identity the closure may **ever** select'": the frozen spec lets the closure select only `sha256:4b141085…`, and changing the approved identity is an Ask-First renegotiation, so "ever" matches the spec.
- false — "One rewrapped `docs/ci.md` line is about 130 characters": the guide has no wrap convention. Existing lines run to 365 characters (e.g. lines 11-17, 53-54).
- false — "The new deferred-work entry lacks a DW- ID, origin and severity": every adjacent Story 3.15 `source_spec:` entry in that section uses the same ID-less schema. DW IDs belong to the separately migrated `### DW-` sections.
- false — "`inventory.Length.ShouldBe(24)` is redundant, and the `closure.json` comment contradicts the README exemption": the count gives a sharper failure than the file-set diff. The "one retained file" comment is scoped to the packet directory, and the README sits outside it, one level up.
- false — AA "`limitations-mismatch` covers only the owner path, where source-body comparison masks the clause": `RewriteReceipt` leaves the GitHub source unchanged. Without the `v1.py:1035` clause, the failure message would become `GitHub acceptance source is not authenticated to the rostered owner` and the expected `acceptance receipt does not bind` would fail, so the case does observe the clause.
- false — AA "`docs/ci.md:541-543` still describes the superseded limitation-4 wording as current": that paragraph narrates what the sixth review loop did, and it is historically accurate. The current correction is stated in the 2026-09-26 paragraph that follows.
- false — BH "the tracker comment says 'All five prior acceptance rounds remain under superseded-acceptances/' and omits `superseded-packets/`": the statement is true. The `c98fdef2…` snapshot is a receipt-free packet, not an acceptance round.

**Disposition (2026-09-26):** D1 was resolved as option 1: `ddf87f25` was reworded before push to `adc3aae8` `test(contracts): …`, with the tree unchanged. All 4 patches were APPLIED at the owner's request, and are uncommitted. The `CiDoc…` check now requires at least one stated verdict and requires every `receipts=N verifier_exit=N` token in the Story 3.15 slice to equal the expected one. `CorrectedLifecycleRowsRetainTheirCorrectedStatus` asserts the three sprint-status caveat lines verbatim, and the tracker's pointer comment now says so. The superseded-acceptances README names `c98fdef2...` and `../superseded-packets/`, and is re-pinned to `d3baa2ef…`. The story record separates the pre-patch and post-patch counts. Verification: the Release build had 0 warnings and 0 errors. The focused Story 3.15 classes passed 237/237, and the lifecycle test passed 1/1. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are the known Story 4.15 OQ8 v5 tests. Two mutation checks failed as intended: an added `receipts=0 verifier_exit=1` sentence fails `CiDoc…`, where it passed before this patch, and deleting one caveat line fails the lifecycle test. `docs/ci.md` is unchanged at `3800b3d4…`, so the Story 4.15 reseal input did not move. Status is **not** flipped. The frozen `spec-3-15-tracker-reconciliation.md` Decision still holds spec `done` / row `review`.

### Review Findings (2026-09-26, review-patch application commit `b56d4c86`)

Scope: `git diff adc3aae8..b56d4c86`, which is 7 files, +75/-12, and 167 diff lines. Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) and none failed. `b56d4c86` is on `origin/main`, so the preceding Disposition's "uncommitted" was accurate only until this commit. The Acceptance Auditor re-ran the checks at `b56d4c86`: the Release build had 0 warnings and 0 errors. The focused Story 3.15 classes passed 237/237, and the lifecycle test passed 1/1. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are `Oq8V5CandidateTests` (2) and `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`. `README.md` hashes to `d3baa2ef…`, which matches its pin, and `docs/ci.md` is unchanged at `3800b3d4…`.

**patch:**

- [x] [Review][Patch] The new `docs/ci.md` verdict check misses a stale verdict wrapped across a line break. The pattern `receipts=\d+ verifier_exit=\d+` needs one literal space, but the Story 3.15 section of `docs/ci.md` is hard-wrapped prose at about 100 columns. A stale `receipts=0` at the end of one line followed by `verifier_exit=1` on the next matches nothing. `verdicts` then holds only the valid 3/3 token, and both `ShouldNotBeEmpty` and `ShouldAllBe` pass, so the guide states both results again with the suite green. That is the contradiction this patch was written to close. The Disposition's mutation check only tried the single-line form. Fix: match `receipts=\d+\s+verifier_exit=\d+`, collapse each match's whitespace to one space with `Regex.Replace(match.Value, @"\s+", " ")` before comparing, and re-run the mutation check with the stale token split across a newline. `docs/ci.md` does not change, so the Story 4.15 reseal input stays at `3800b3d4…` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:1322]
- [x] [Review][Patch] The restored caveat guard is a whole-file substring, so it cannot notice the caveat moving, and the tracker comment that describes it is unguarded. `sprint.ShouldContain("  # Caveat: …")` passes wherever the three lines sit in `sprint-status.yaml`, and the four-line block is asserted separately. So the caveat can leave the Story 3.15 GUARDED fence, or be separated from the `3-15` row, with the suite green. The pointer comment between the two blocks now says the test asserts "the three caveat lines above and the next four lines verbatim", but nothing checks that adjacency. The fix text asked for the assertion to sit "in the tracker's Story 3.15 GUARDED block", and the tracker header promises "exact wording, order and adjacency". Fix: replace both `ShouldContain` calls with one contiguous assertion, from `  # Caveat:` through the pointer lines and the four dated lines, ending with the `  3-15-corrected-deployed-runtime-parity-closure: review` row line. Keep the `SingleLineValue` row pin [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4284]
- [x] [Review][Patch] The new `deferred-work.md` entry misstates the gap it defers and cites stale references.
  - It says "switching all four checks to `[..8]`", but there are five full-hex checks: `currentSummary` ×2, `currentHistory` ×2 and `parityGate` ×1.
  - The `parityGate` slice does not check `CurlIsolation…` or `PreRecapture…` in any form, so a G-RUNTIME-PARITY row naming `7d64f87e` or `c98fdef2` stays green even after the proposed switch.
  - The cited lines `~4562/4575` are now `4570/4583`.
  - The heading names `ddf87f25`, which the reword orphaned. It is on no branch or remote, and the tree-equivalent is `adc3aae8`.

  Fix: correct the count, name the `parityGate` gap, update the line numbers, and cite `adc3aae8` (reworded from `ddf87f25`). The spec's own defer item keeps its `:4562` citation, because this review does not edit the spec under review [_bmad-output/implementation-artifacts/deferred-work.md:4921]

**Rejected**

- reject (edits the spec under review) — Raised by all four layers: the preceding Disposition says the four patches "are uncommitted", and its scope line says `ddf87f25` is not on `origin/main`. Both were true when written. This commit publishes them, which the scope line of this section records.
- low — Raised by all four layers: `b56d4c86` is typed `fix(tests)` although it ships only tests, Markdown and YAML comments, and "update acceptance records" overstates it, because no receipt changed. The commit is on `origin/main` and cannot be reworded. The Acceptance Auditor's "the next Release run will cut a patch release from this commit" is false: `v3.108.1..origin/main` already holds 8 `feat` commits, so the pending release is a minor either way. This commit only adds a Bug Fixes changelog line. The same range already holds 6 other `fix(test)`/`fix(tests)` commits, so the mistyping is a repo-wide habit, not a defect of this change.
- low — Raised by edge-case-hunter, blind-hunter and acceptance-auditor: prose verdicts ("three of three", "zero of three", "verifier exits 1") are not guarded. The stale text that D1 fixed used the token form, and guarding free prose needs heuristic wording matches that would also trip on the dated historical paragraphs in the same section.
- low — Blind-hunter: unlike the digest regex, the verdict regex has no left-boundary anchor. An unanchored match could only mis-fire on a longer identifier ending in `receipts=`, and the guide has none.
- low — Blind-hunter: the superseded-acceptances README does not say *why* `c98fdef2…` was abandoned. The README indexes retained bytes. The decline is recorded in `3-15-corrected-deployed-runtime-parity-acceptance-review.md:62` and `deferred-work.md:4918`, and the README's "re-mints before any receipt have no receipt set" intro is consistent, because the `c98fdef2…` packet (not a receipt set) is in `superseded-packets/`.
- false — Blind-hunter: "the README chain narrative is protected only by a whole-file hash, so it could be dropped with the suite green". Any byte change fails `SupersededArtefacts` until someone deliberately re-pins, so dropping the chain cannot happen silently.
- reject (edits the spec under review) — Raised by blind-hunter and acceptance-auditor: the two findings the previous review rejected (the ci.md "section hash" wording, and the stale spec Verification section) have no follow-up tracker.
- low — Blind-hunter: the story record's post-patch Verification bullet has no commit anchor and does not list the mutation checks. The neighbouring bullets have no anchors either, and the spec Disposition carries the mutation details.

**Disposition (2026-09-26):** All 3 patches were first left as action items, then APPLIED the same day at the owner's request, and are uncommitted. The `CiDoc…` verdict check now matches `receipts=\d+\s+verifier_exit=\d+` and collapses whitespace before comparing. `CorrectedLifecycleRowsRetainTheirCorrectedStatus` asserts the caveat, the pointer comment, the four dated lines and the `3-15` row as one contiguous block. The tracker's pointer comment was reworded to say so, and the test binds the new wording. The `deferred-work.md` entry now counts five full-hex checks, names the `parityGate` gap, cites lines ~4569-4590, and names `adc3aae8` (reworded from `ddf87f25`); its claim that the switch passes today was checked against the three PRD slices. Verification: the Release build had 0 warnings and 0 errors. `CorrectedDeployedRuntimeParityClosureTests` passed 219/219 and `DeployedRuntimeParityClosureTests` passed 290/290. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are the known Story 4.15 OQ8 v5 tests. Two mutation checks failed as intended: a stale `receipts=0` / `verifier_exit=1` verdict wrapped across a newline in `docs/ci.md` fails `CiDoc…`, and moving the caveat lines elsewhere inside the Story 3.15 GUARDED fence fails the lifecycle test. Both mutations were reverted, and `docs/ci.md` is unchanged at `3800b3d4…`, so the Story 4.15 reseal input did not move. Status is **not** flipped: the frozen `spec-3-15-tracker-reconciliation.md` Decision still holds spec `done` / row `review`, pinned in four places.

### Review Findings (2026-09-26, review-patch application commit `8baf91b8`)

Scope: `git diff 048c8930..8baf91b8`, which is 5 files, +51/-17, and 126 diff lines. `048c8930` and `159c26d8` (the Dapr app-channel token work) are not part of Story 3.15 and are excluded. Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) and none failed. Checks re-run at `8baf91b8`: the Release build of the Contracts test project had 0 warnings and 0 errors. `CorrectedDeployedRuntimeParityClosureTests` passed 219/219, `CorrectedDeployedRuntimeParitySmokeCaptureTests` passed 18/18 (the same 237 focused set as before), and `DeployedRuntimeParityClosureTests` passed 290/290. `docs/ci.md` is untouched, so the Story 4.15 reseal input stays at `3800b3d4…`.

**patch:**

- [x] [Review][Patch] The contiguous Story 3.15 block assertion has no anchor at either end, so the "leaves the GUARDED fence" half of the patch it applies is still open. The block runs from `  # Caveat:` to `  3-15-corrected-deployed-runtime-parity-closure: review\n`, and no test in `tests/`, `tools/`, `scripts/` or `.github/` names the `>>> GUARDED COMMENTS` or `>>> END GUARDED COMMENTS <<<` markers. Moving the 11 lines as one unit below `  # >>> END GUARDED COMMENTS <<<` (or deleting the fence markers) keeps both the block `ShouldContain` and the `SingleLineValue` row pin green. The Disposition's mutation check only moved the caveat *inside* the fence. The block also begins mid-line, so a caveat appended to another comment line still matches. Fix: prefix the asserted block with `"\n"` and end it with `"  # >>> END GUARDED COMMENTS <<<\n"` after the row line, then re-run a mutation that moves the whole block below the END marker. `docs/ci.md` does not change [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4282]
- [x] [Review][Patch] The method's header comment now contradicts its own Story 3.15 assertion. `CorrectedLifecycleRowsRetainTheirCorrectedStatus` says the rows are "Anchored per line, never a whole-file substring, because this file narrates its own lifecycle corrections in prose that quotes these very tokens". This commit pulled the `3-15` row line into a whole-file `sprint.ShouldContain(...)`. A later editor will either "fix" the block back into per-line checks and lose the adjacency guard, or copy the whole-file pattern for another row. Fix: reword the header comment so it says that row *values* are pinned per line with `SingleLineValue`, and that the Story 3.15 wording block is a deliberate multi-line substring, which the tracker's prose cannot quote by accident [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4267]

**Rejected**

- reject (edits the spec under review) — Raised by all four layers: the new Disposition says the 3 patches "are uncommitted", but `8baf91b8` is the commit that publishes them, and it is on `origin/main`. This is the third Disposition in a row to go stale when its own commit lands. Recording it here is enough.
- low — Raised by blind-hunter, edge-case-hunter and acceptance-auditor: `8baf91b8` is typed `fix(tests)` for a tests-and-records change, and "entries" is plural although one entry changed. The commit is on `origin/main` and cannot be reworded. The pending release is already a minor. Future review-patch commits should use `test(contracts): …`, as the owner chose for `adc3aae8`.
- low — Raised by blind-hunter, edge-case-hunter and acceptance-auditor: the verdict regex misses other token forms, such as two separate code spans, a comma or "and" between the tokens, or the tokens in reverse order. `docs/ci.md` writes the verdict as one code span (`receipts=3 verifier_exit=0`, line 572), and the stale text D1 fixed used that same form. Each widened form adds heuristic regex surface; see the review-budget follow-up rule that regex-guardrail loops do not converge.
- low — Edge-case-hunter: a lone stale `receipts=0` or `verifier_exit=1` token with no partner is not caught. The same reasoning as the item above applies.
- false — Blind-hunter: "a wrapped blockquote or list continuation that starts with `> ` defeats the regex". The Story 3.15 section of `docs/ci.md` has no blockquote or list prefix between lines, and the verification-gap layer checked that.
- low — Blind-hunter: a failing 11-line `ShouldContain` does not name the line that broke. The fix is a line-by-line rewrite of the assertion, and a failure still names the test and prints the expected block.
- false — Raised by blind-hunter and acceptance-auditor: "the story record was not updated, and 219 + 290 cannot be compared with the recorded 237". The story record's latest counts still hold at `8baf91b8`: 219 + 18 = 237 focused, and the lifecycle test passes inside 290/290. Both were re-run in this review.
- reject (edits the spec under review) — Acceptance-auditor: the spec's own defer item still says "four checks" and cites `:4562`, while the corrected ledger entry says five and cites `~4569-4590`. The earlier review deliberately left the spec's item alone. `deferred-work.md` is the authoritative ledger.
- false — Blind-hunter: "the deferred-work entry's fix leaves out the six older superseded subjects and has no verification line". The entry names that gap in its own evidence text, so whoever picks it up sees it. The acceptance-auditor independently confirmed that none of the nine superseded prefixes appears in the three current PRD slices, which makes "passes today" true.
- false — Blind-hunter: "the rejection of 'no follow-up tracker for the previous review's rejected findings' has the wrong reason, and those items belong in `deferred-work.md`". Rejected findings get no tracker by design. Both items were rejected because their fixes edit the spec under review.
- low — Blind-hunter: the pointer comment's "this note" and "the next four lines" wording is ambiguous. The acceptance-auditor confirmed that the wording is accurate. It is also pinned verbatim, so rewording it means editing both the test and the tracker, for no gain in behaviour.

**Disposition (2026-09-26):** Both patches were APPLIED at the owner's request and are uncommitted at the time of writing. `CorrectedLifecycleRowsRetainTheirCorrectedStatus` now starts the Story 3.15 block at a line boundary (`"\n  # Caveat: …"`) and ends it with `  # >>> END GUARDED COMMENTS <<<\n` after the `3-15` row. The tracker's pointer comment was reworded to name the fence's END marker, and the test binds the new wording. The method's header comment now says that row *values* are pinned per line with `SingleLineValue`, and that the Story 3.15 block is a deliberate multi-line substring that runs through the row and the END marker. Verification: the Release build had 0 warnings and 0 errors. `DeployedRuntimeParityClosureTests` passed 290/290, `CorrectedDeployedRuntimeParityClosureTests` passed 219/219, and `CorrectedDeployedRuntimeParitySmokeCaptureTests` passed 18/18. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are the known Story 4.15 OQ8 v5 tests (`Oq8V5CandidateTests` ×2 and `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`). Two mutation checks failed as intended, and each was restored from a byte copy: moving the whole caveat-to-row block below the END marker fails the lifecycle test, and so does joining the caveat onto the preceding comment line. `docs/ci.md` is unchanged at `3800b3d4…`, so the Story 4.15 reseal input did not move. Status is **not** flipped: the frozen `spec-3-15-tracker-reconciliation.md` Decision still holds spec `done` / row `review`, pinned in four places.

### Review Findings (2026-09-26, review-patch application commit `e21f1baf`, originally `2d2665a7`)

Scope: `git diff 159c26d8..e21f1baf` (tree identical to `2d2665a7`), which is 3 files, +40/-10, and 95 diff lines. `2d2665a7` is local only (`main` is 1 ahead of `origin/main` = `159c26d8`). Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) and none failed. The verification-gap layer found no behavioural gap. Checks re-run at `2d2665a7`: the Release build of the Contracts test project had 0 errors. `DeployedRuntimeParityClosureTests` passed 290/290, `CorrectedDeployedRuntimeParityClosureTests` passed 219/219, and `CorrectedDeployedRuntimeParitySmokeCaptureTests` passed 18/18.

**decision-needed:**

- [x] [Review][Decision] (resolved 2026-09-26: owner chose option 1 → patch below) The fence's BEGIN marker is still unguarded, so the "leave the fence" claim is only half true — The `8baf91b8` patch this commit applies (now `[x]`) named "(or deleting the fence markers)" as an escape. `2d2665a7` binds only `  # >>> END GUARDED COMMENTS <<<`. No test in `tests/`, `tools/`, `scripts/` or `.github/` names `  # >>> GUARDED COMMENTS -- DO NOT DELETE (see GUARDED COMMENT BLOCKS in the header) <<<` (`sprint-status.yaml:132`). Two mutations were run in this review, and in both `CorrectedLifecycleRowsRetainTheirCorrectedStatus` stayed green (1/1): M1 deleted line 132; M2 moved the caveat-to-END block above line 132, which leaves the block outside any fence and the BEGIN marker unclosed. The worktree was restored from a byte copy after each. The new test comment says the block cannot "leave the fence with the row" (`DeployedRuntimeParityClosureTests.cs:4283-4285`), which M1 and M2 falsify. The tracker pointer comment names only the END marker and is accurate. Options: (1) add an ordering guard: BEGIN appears exactly once, END appears exactly once, and BEGIN's index is below the caveat's; re-run M1/M2 to confirm they fail. (2) Narrow the test comment to what is enforced (the block ends at the END marker), and add no guard. (3) Accept as is and stop the fence-anchoring loop under the review-budget rule that regex-guardrail loops do not converge. Fix sources: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4283]
- [x] [Review][Decision] (resolved 2026-09-26: owner chose option 1 — message reworded, commitlint 21.1.0 exit 0, tree identical, still unpushed; `2d2665a7` → `e21f1baf` `test(contracts): anchor Story 3.15 lifecycle block at the GUARDED fence END`) `2d2665a7` was typed `fix(tests)` again and its subject never names Story 3.15 — The rejected list in the diff's own Review Findings says "Future review-patch commits should use `test(contracts): …`". The subject "update comments and assertions in DeployedRuntimeParityClosureTests for clarity and accuracy" also hides the guarded tracker edit and the guard strengthening. Three layers said the commit "is on `main` and cannot be reworded". That is false: it is unpushed. Options: (1) amend the message now (for example `test(contracts): anchor Story 3.15 lifecycle block at the GUARDED fence END`), validated with the repository's pinned commitlint before use, and fold in any patch from the decision above; (2) leave it and record it. Fix sources: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor.

**patch:**

- [x] [Review][Patch] Anchor the Story 3.15 lifecycle block inside the one GUARDED fence: require the line-anchored BEGIN marker (`  # >>> GUARDED COMMENTS -- DO NOT DELETE (see GUARDED COMMENT BLOCKS in the header) <<<`) and the END marker each to occur exactly once, and BEGIN to precede `\n  # Caveat: both owner roles`, with ordinal `IndexOf`/`LastIndexOf` and no regex. Then re-run M1 (delete BEGIN), M2 (move the caveat-to-END block above BEGIN) and a second-END-above-caveat edit, each of which must fail, restoring from a byte copy [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4298]

**Rejected**

- low — Edge-case-hunter: inserting a second END marker above the caveat closes the fence early and stays green. The mutation is contrived, and the fix adds another count guard; see decision 1, option 1, which would cover it anyway.
- low — Edge-case-hunter: moving the block and its END marker together further down stretches the fence over unrelated rows. The block stays inside BEGIN..END, so nothing leaves a fence. Only more lines become fenced.
- low — Blind-hunter: the test does not prove the subject-digest notice and the lifecycle block share one fence. `SubjectRestatingSurfacesNameTheCurrentSubject` guards the digest separately, and a single-fence-order guard is more regex-guardrail surface with no named harm.
- reject (edits the spec under review) — Raised by blind-hunter and acceptance-auditor: the new Disposition says the patches "are uncommitted at the time of writing" inside the commit that carries them (the fourth time). The hedge makes it literally true.
- reject (edits the spec under review) — Blind-hunter: the `8baf91b8` Scope line says `048c8930` and `159c26d8` "are excluded" when neither is inside `048c8930..8baf91b8`, and the Disposition's counts name no tree. This review re-ran the three classes at the committed `2d2665a7` with the same 290/219/18 result.
- false — Blind-hunter: "the header notice at `sprint-status.yaml:49-52` does not warn that adding a row before the END marker turns the build red". The header already says the tests assert "exact wording, order and adjacency". The pointer comment directly above the row names the END marker. A failure names the test and prints the expected block.
- false — Blind-hunter: "the story record's post-patch verification bullet is stale". Its counts still hold at `2d2665a7`: the two focused classes are 219 + 18 = 237, and the lifecycle test passes inside 290/290. The mutation details are in the spec Disposition.

**Disposition (2026-09-26):** Both decisions were resolved as option 1. The commit message alone was reworded, so `2d2665a7` became `e21f1baf` with an identical tree; it is still unpushed, and commitlint 21.1.0 exited 0 on the exact message. The one patch was APPLIED at the owner's request; it is uncommitted as this is written and lands in the commit that adds this section. `CorrectedLifecycleRowsRetainTheirCorrectedStatus` now requires the line-anchored BEGIN marker and the END marker each to occur exactly once, and BEGIN to precede the caveat. It uses ordinal `IndexOf`/`LastIndexOf` and no regex. The sprint-status tracker and its pinned pointer comment are unchanged. Verification: the Release build had 0 warnings and 0 errors. `DeployedRuntimeParityClosureTests` passed 290/290, `CorrectedDeployedRuntimeParityClosureTests` passed 219/219, and `CorrectedDeployedRuntimeParitySmokeCaptureTests` passed 18/18. The full Contracts suite ran 2133 tests with 2130 passed; the 3 failures are the known Story 4.15 OQ8 v5 tests (`Oq8V5CandidateTests` ×2 and `Oq8PlatformClosureTests.CheckedInRepositoryFullValidationPassesWithoutMutation`). Three mutation checks failed as intended, each on its own assertion, and each was restored from a byte copy: M1 deleted the BEGIN marker, M2 moved the caveat-to-END block above the BEGIN marker, and M3 inserted a second END marker above the caveat. `docs/ci.md` is unchanged at `3800b3d4…`, so the Story 4.15 reseal input did not move. Status is **not** flipped: the frozen `spec-3-15-tracker-reconciliation.md` Decision still holds spec `done` / row `review`, pinned in four places.
