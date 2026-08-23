---
title: 'Story 3.15 Corrected Deployed Runtime Parity Closure'
type: 'feature'
created: '2026-08-21'
status: 'in-review'
baseline_commit: '94591f3539ce30372db58e5fdd3ba017ea8c07b8'
review_loop_iteration: 0
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

## Spec Change Log

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

The owner confirmed on 2026-08-21 that Story 3.14's `status: done` spec is authoritative over its stale `review` tracker row. Receipts live beneath `acceptances/<subject-sha256>/` so the subject binds technical evidence and the registry without signing itself. Publication authority is checked for validity at its recorded use, not required to remain unexpired at later verification time.

**2026-08-22 (code review, accepted trade-offs):** Each receipt's `durable_source` is cross-checked against a file retained inside the same packet the receipt author controls, not fetched live from the GitHub API — this proves internal consistency (receipt and source agree byte-for-byte) but not independence. Accepted for this story; mirrors the same accepted gap already recorded for Story 3.13's analogous mechanism.

The owner-role registry's `authority_source` is a GitHub comment ratifying reviewer-role identities. The retained comment (`role-registry-source.json`) is dated 2026-08-14 and its own body reads "Story 3.13 reviewer-roster ratification" — it predates Story 3.15 and never names it. Reuse across stories is intentional: the comment records a role-holder identity fact (who currently holds each rostered role), not release-lineage evidence scoped to one closure, so it is not "spliced 3.13 evidence" in the sense the epic-3 no-splicing rule prohibits (packages/OCI/smokes/predecessor lineage). To keep that reuse from silently widening into a release authorization, `_validate_registry` (`tools/deployed_runtime_parity_handlers/v1.py`) now requires the comment to explicitly disclaim deployment authority (`REGISTRY_AUTHORITY_DISCLAIMER_MARKERS`) and requires its role-mapping lines to match the required identities exactly, rejecting any extra or contradicting role assignment.

## Verification

**Commands:**
- `python3 tools/validate-corrective-release-evidence.py _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json --manifest tools/release-packages.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: exact predecessor digest passes.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests -noLogo` -- expected: all matrix and mutation cases pass with none skipped.
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: canonical pass for subject `bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`, selecting index `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` after all three real subject-bound receipts validate.
- `git diff --check` -- expected: no whitespace errors.
