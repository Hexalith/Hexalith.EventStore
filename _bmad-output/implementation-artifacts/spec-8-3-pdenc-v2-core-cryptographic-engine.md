---
title: 'pdenc-v2 core cryptographic engine'
type: 'feature'
created: '2026-09-14'
status: 'in-progress'
baseline_commit: 'e8886ec4c277460de3d3208b3fc0b9c261c4967d'
route: 'dispatch'
review_loop_iteration: 4
context:
  - '_bmad-output/implementation-artifacts/epic-8-context.md'
  - '_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '_bmad-output/implementation-artifacts/evidence/story-8-3/requirements-amendment-approval.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 8.2 froze the portable `pdenc-v2` bytes and authorized Story 8.3, but EventStore has no provider-neutral implementation that can produce and authenticate them safely.

**Approach:** Add a non-packable core library containing internal codecs, JSON/path transformation, AES-256-GCM, bounds, cancellation, diagnostics, and buffer cleanup; prove it against the frozen goldens and every Story 8.3 vector without wiring Server, storage, or a provider.

## Boundaries & Constraints

**Always:** Revalidate packet `AR-20260914-01`, digest `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, and all Story 8.2 hashes before editing. Implement sections 5-8, 14-15 and V004-V048/V135-V136/V138 exactly except for the Story 8.3 constructibility amendments below; keep output byte-stable, failures bounded, and owned sensitive buffers zeroed.

**Never:** Change the frozen authority, Contracts, fixtures/verifiers, Server/no-op hooks, package manifest, `.slnx`, domain/Parties code, topology, persisted data, or external resources. Do not implement authority section 8.4's durable metadata allowlist and `Unprotected()` pairing, which are Server-owned. Do not add Azure/DAPR/domain/UI dependencies, public contracts, automatic registration, packability, release claims, durable lifecycle behavior, or claim V127/V128 and later-story evidence.

## Reapproved Story 8.3 Constructibility Amendments

Approval packet `AR-20260914-02` reapproves the following evidence-backed Story 8.3 interpretations. They supersede contrary registry wording only for this core-engine story; the shared authority bytes and digest remain unchanged.

- V008 accepts the required HXP2 version byte `02` at offset 4 and rejects `03`/`ff` there; `02`/`ff` remain the unsupported representatives for the other closed identifier and flag bytes.
- V004 envelope-ordinal bit flips at offsets 16-19 and V016 ordinal-only substitutions are rejected locally before lookup because manifest/ordinal consistency is mandatory; V016 still performs exact substituted-record lookup and returns authenticated mismatch for a valid DEK-version substitution.
- V023 has no nullable or caller-selected format source at the byte-core seam. Missing/empty raw AAD format fields and a `json+pdenc-v1` substitution must fail authentication; no synthetic public format-selection seam is authorized.
- V030 retains the 4,096-byte defensive total-AAD cap, but its executable boundary is the 3,617-byte runtime-constructible maximum. Prove that maximum and reject the first individually over-bound 2,049-byte path; do not weaken field bounds to fabricate unreachable 4,096/4,097-byte values. The schema-only mathematical maximum remains 3,873 bytes.
- V038/V039 cover only the core-owned bounded depth/cycle-equivalent rejection, cancellation during JSON walking, and atomic cancellation after partial internal mutation. Policy discovery, converter/getter invocation, and policy-fault mapping remain owned by Story 8.5.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Golden protect/read | G-001 context, path set, DEK and envelope | Exact manifest, AAD, nonce, ciphertext, tag, envelope and complete authenticated plaintext | No partial output |
| Hostile carrier/context | Malformed, oversized, tampered, cross-scope or noncanonical input | Reject before unsafe allocation/use; zero external calls where locally decidable | Bounded unreadable taxonomy; no raw crypto/JSON text |
| Limits/concurrency | Exact/max+1 payload, depth, nodes, paths, plaintext; collision, cancellation and parallel calls | Exact maxima pass, over-limit work fails early, per-call state and nonces remain isolated | No partial transform; owned buffers clear on every exit |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/evidence/story-8-2/story-completion-approval.md` -- exact authorization and source/fixture hashes.
- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md:224` -- package, codec, crypto, threat, no-leak, vector, and handoff authority.
- `src/Hexalith.EventStore.Contracts/Security/` -- reuse aggregate occurrence, snapshot carrier, metadata, completion, result, and unreadable-outcome contracts unchanged.
- `tests/Hexalith.EventStore.Contracts.Tests/Security/Fixtures/PayloadProtectionV2/` -- immutable G-001/NIST inputs and ownership manifest; link read-only into the new tests.
- `tools/release-packages.json`, `Hexalith.EventStore.slnx` -- preservation baselines; Story 8.8 owns their future integration.

## Review Re-derivation Requirements

- `src/Hexalith.EventStore.PayloadProtection/` -- validate one owned byte snapshot and one bounded immutable path snapshot, then use only those snapshots. Keep JSON plaintext byte-oriented: do not create engine-owned plaintext strings or a plaintext `JsonNode`/`JsonDocument`; zero every mutable plaintext/output staging buffer after transfer.
- `src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs` -- keep the JSON index and selected-path lookup proportional to payload bytes, node count, and encoded selected-path bytes: do not retain a full concatenated path or decoded managed member-name string per node, and do not repeatedly scan wide sibling lists once per selected path. Preserve otherwise-valid unselected JSON even when its eventual pointer would exceed the selected-path cap; enforce the 2,048-byte path bound only when a selected or discovered protected path is materialized. Materialize discovered paths with bounded incremental decoding rather than renting a buffer sized to an arbitrarily long raw property token. Check cancellation at least every 256 examined nodes, children, ancestor/path bytes, replacement copies, sort comparisons, or lookup comparisons, including wide-object wrapper discovery. Accept only the literal unescaped `$pdenc` member spelling as a canonical wrapper. Reconstructed event and snapshot plaintext must reject any residual or emergent `$pdenc` member before transfer.
- `src/Hexalith.EventStore.PayloadProtection/ProtectedPathManifestCodec.cs` -- reject after at most 4,097 enumerated paths, accept cancellation before, during, and after sorting/encoding/hashing, checkpoint bounded work, and detect every duplicate or ancestor/descendant overlap without quadratic string allocation. Do not assume an ancestor is adjacent to its descendant after ordinary byte sorting; maintain an active-prefix structure or use another bounded algorithm that catches interposed names such as `/a`, `/a-foo`, `/a/b`.
- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs` -- validate all context/AAD sources, selected-path overlap, and predictably computable protected output byte/node/depth expansion before pass-through, material creation, or key lookup; aggregate envelope ciphertext bounds before key lookup; bound null/invalid material and ensure caller cancellation wins after every external factory/resolver return or exception; implement the root-manifest snapshot crypto seam without Server integration; reject decrypted event-root `null`; track prospective reconstructed node/depth/byte and cumulative plaintext limits while authenticating rather than allowing many individually valid fragments to multiply work; observe cancellation during and after every 256 items of transformation and wrapper-map/AAD validation.
- `src/Hexalith.EventStore.PayloadProtection/AadCodec.cs` -- enforce the complete canonical snapshot type grammar, including a non-empty lowercase ASCII kebab-case suffix with no leading, trailing, or consecutive hyphen.
- `src/Hexalith.EventStore.PayloadProtection/{AadCodec,Base64UrlCodec,BoundedJsonDocument,EnvelopeCodec,JsonContainerFrame,PayloadProtectionCore,PayloadCryptography,ProtectedPathManifestCodec}.cs` -- establish cleanup ownership before the first sensitive allocation and retain it until the complete result object or collection entry is successfully constructed; on every unsuccessful exit zero every earlier allocation, including partial AAD fields, decoded carriers, envelope fields, selected/decrypted plaintext, transformed output, manifest paths, nonce/ciphertext/tag, and parser snapshots. Cleanup must be allocation-free before zeroing.
- `src/Hexalith.EventStore.PayloadProtection/{Base64UrlCodec,PayloadProtectionCore,PayloadProtectionDiagnostics,PayloadCryptography}.cs` -- reject oversized string carriers before scanning or copying; reject configured snapshot oversize before JSON parsing; preserve and clear ownership when cancellation wins after material creation; recheck cancellation after snapshot AAD validation and before lookup; prevent diagnostic listeners from changing operation outcomes; and classify unsupported AES-GCM as a bounded cryptographic failure rather than malformed input.
- `src/Hexalith.EventStore.PayloadProtection/{PayloadCryptography,PayloadProtectionDiagnostics,CryptographicPayloadProtectionEntropy}.cs` -- retain full-path V010 authenticated-mismatch semantics with post-auth nonce/ordinal validation, map encryption failures and typed read outcomes to closed diagnostics, and remove reliance on Contracts' transitive `Hexalith.Commons.UniqueIds` compile surface.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- load and assert the linked immutable G-001, NIST, and ownership fixtures rather than relying only on duplicated constants; add snapshot positive/tamper, reserved-marker writer rejection, carrier metadata/type mismatch, canonical snapshot-type, and key-outcome/cleanup tests; mutable-input/path isolation; exact output/reconstruction maxima including pre-material wrapper-induced depth/node/byte expansion and reader-side cumulative plaintext; full-reader V010-V012 and escaped `~`/`/` member-name round trips; genuine in-core gated hostile-unprotect V138 concurrency plus cancellation during wide lookup/wrapper/path/replacement scans and manifest enumeration/sort/encoding/hash with checkpoints 1/256/512/768; exact discovered vector-trait membership; literal/escaped decoded-equivalent duplicate names and obfuscated-wrapper rejection; malformed wire-key zero-lookup cases; invalid-context empty-selection rejection; zero-material-call assertions for every locally invalid JSON/path/output selection; a complete 4,096-wrapper read; event and snapshot missing/wrong-length keys; invalid factory-material matrices for event and snapshot; one material factory call per payload; exceptional generator cleanup and post-key-reference cancellation; post-factory/resolver cancellation cleanup and precedence; protected-result format labels; exact per-operation protect/unprotect metrics and activities; unsupported-AES classification where constructibly testable; and observer/allocation-failure cleanup. Keep one C# type per file and document all internal helpers.
- `.github/workflows/payload-protection.yml` and `scripts/ci-local.sh` -- run the focused project as a blocking direct test lane with the current complete 292-case minimum (updated when the suite changes), plus the dedicated invariant-globalization regression, while preserving V138 as observation-only and without adding either project to `Hexalith.EventStore.slnx` or changing release packaging. The required `ci / build-and-test` lane guards the workflow contents through `ReleasePackageManifestTests`.
- All changed C# must satisfy the tracked Allman-brace and XML-documentation rules; verify whitespace formatting as well as analyzer/style diagnostics.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-8-3/` -- preserve preflight, then bind the re-derived source/test/CI inventory, authority/8.2 hashes, Contracts API, 14-package baseline, NIST recheck, and exact review results; halt on drift.
- [x] `src/Hexalith.EventStore.PayloadProtection/` -- re-derive the Contracts-only, `IsPackable=false` byte-oriented event/snapshot core and every bounded validation, cancellation, diagnostic, exception, dependency, and buffer-ownership rule in Review Re-derivation Requirements; cite the digest and normative sections in material files.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- re-derive the runnable xUnit v3/Shouldly project with internal access, linked fixtures, deterministic test-only seams, exact execution manifest, and all review-regression cases.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/{Envelope,AadPath,Cryptography,JsonTransform,LimitsAndConcurrency,Diagnostics}Tests.cs` -- execute inherited V001-V003 and owned V004-V048/V135-V136/V138, including every constructible named mutation, all reapproved interpretations, snapshot crypto, full-path outcomes, true concurrency, exact/max+1 reconstruction, cancellation, zeroing/no-leak, and exceptional-exit assertions.
- [x] `.github/workflows/payload-protection.yml`, `scripts/ci-local.sh` -- add a blocking direct-project PayloadProtection test lane, guard its complete command from the required Contracts test lane, and preserve the frozen `.slnx` and 14-package release inventory.
- [x] `_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md` -- replace superseded hashes/results with the re-derived commands, counts, limits, review state, and authorization boundary; authorize only 8.4/8.5 after exact approval.

**Acceptance Criteria:**
- Given activation, when preflight runs, then every authorized digest/hash matches current bytes and any mismatch blocks source work.
- Given canonical inputs, when the real core protects or authenticates, then G-001/NIST and all assigned vectors produce their exact bytes, frozen typed outcome, or `AR-20260914-02` constructible interpretation.
- Given hostile, concurrent, cancelled, or maximum-bound work, when each path exits, then work remains bounded, no partial plaintext escapes, and every engine-owned sensitive buffer is observed zeroed without mutating caller-owned or transferred buffers.
- Given dependency and preservation scans, when verification completes, then the core is provider-neutral/non-packable, old behavior and 14-package output remain unchanged, and no later-story capability is claimed.

## Implementation Notes

- Preflight reproduced packet `AR-20260914-01`, normative digest
  `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`,
  every Story 8.2 source/fixture/verifier identity, and the 14-package release
  baseline before source work.
- The core requests cryptographic material through a delayed factory only after
  complete local path/budget validation and at least one non-null selection.
  This makes V041/V045 zero-material-call behavior directly observable without
  implementing Story 8.5 lifecycle storage.
- The six required test areas contain 51 unique vector traits and execute 292
  passing cases after the review-loop re-derivation audit. Approval packet
  `AR-20260914-02` accepts the evidence-backed constructible interpretations
  for V008/V016/V023/V030/V038/V039 without weakening bounds or importing
  Story 8.5 behavior. Exact covered cases and the mathematical proof are
  recorded in `evidence/story-8-3/verification.md`.
- The earlier 137 results bind the superseded pre-review implementation at
  `220e722df0f088bd6790b4815eedd3e993de09fb`; they are continuity evidence, not
  acceptance evidence for review-loop iteration 1.

## Spec Change Log

- 2026-09-14: Added the bounded provider-neutral core, focused vector suite,
  preflight/verification evidence, and completion artifact. Frozen intent and
  the authority, Contracts, fixture/verifier, solution, and release-manifest
  bytes remain unchanged.
- 2026-09-14: Closed the safely constructible Step-3 audit gaps with exact
  carrier/header, AAD/manifest, lookup-before-authentication, ordering,
  collision, limit, atomic-exit/clearing, and diagnostic assertions. Retained
  `in-progress` and the unchecked vector task because V030 and the Story
  8.5-owned policy cases remain unresolved without changing frozen ownership.
- 2026-09-14: Human approval `AR-20260914-02` amended and reapproved the frozen
  Story 8.3 requirements to the safe, constructible V008/V016/V023/V030/V038/V039
  behavior proven in verification evidence; shared authority bytes remain
  unchanged.
- 2026-09-14: Revalidated all 51 vector traits and 137 passing cases against
  `AR-20260914-02`; completed the final test task and advanced to independent
  review without source changes.
- 2026-09-15: Closed all 13 mutable second-pass patch findings, added four
  focused cases for a 250-case suite, and reverified the provider-neutral core.
  The human EventStore owner then authorized the two remaining document patches;
  the V004 amendment-list and authority section 8.4 Server-ownership corrections
  are applied in the approval packet and frozen requirements block.
- 2026-09-15: Closed two follow-up runtime findings by making final-collision
  cancellation win after DEK cleanup and isolating best-effort metric
  instruments; added one focused regression for each and raised the lane floor
  to 252 cases.
- 2026-09-14: Independent review found missing snapshot proof, plaintext
  string/DOM and uncleared staging copies, validation/use races, quadratic and
  uncancellable manifest work, pre-external-call validation gaps, unbounded
  transformed/reconstructed output, incomplete V010/V138 and failure-path
  verification, and no blocking CI lane. Re-derivation requirements now make
  those constraints executable and avoid the known-bad pre-review design.
  KEEP exact G-001/NIST bytes, all approved vector interpretations, strict
  codecs/AAD/manifest ordering, internal non-packable Contracts-only scope,
  linked fixtures, delayed single material creation, fresh collision retries,
  closed diagnostics, caller-input immutability, Story 8.5 ownership, frozen
  `.slnx`, and the 14-package release boundary.
- 2026-09-14: Review-loop iteration 2 found that the byte-range JSON parser still
  retained decoded member names and a complete concatenated path for every node,
  allowing bounded input to amplify into unbounded managed strings. It also
  found allocation-before-bound checks, cleanup allocation and cancellation
  ownership gaps, residual protected-marker acceptance, diagnostic/AES failure
  misclassification, style drift, and missing reader/snapshot/zero-external-call
  regressions. The non-frozen requirements now require a payload-proportional
  byte index, allocation-free zeroing, pre-allocation guards, exact cancellation
  ownership, best-effort diagnostics, residual-marker rejection, complete
  reader/snapshot boundary tests, discovered vector-trait verification, and the
  current full-suite CI minimum. Avoid the known-bad full-path string index and
  cleanup that allocates before zeroing. KEEP the approved wire bytes and
  constructibility interpretations, owned caller snapshots, adjacent sorted
  manifest validation, post-auth V010 semantics, root snapshot seam, bounded
  typed diagnostics, true concurrent V138 checkpoints without a performance
  gate, Contracts-only/non-packable scope, frozen `.slnx`, and 14-package output.
- 2026-09-14: Review-loop iteration 3 found repeated wide-sibling selected-path
  scans, incomplete cancellation coverage, multiplicative per-wrapper reader
  work, and systematic sensitive-buffer ownership gaps before collection/result
  construction. The non-frozen requirements now demand payload-proportional
  selected-path lookup, cancellation during every bounded phase and after every
  external outcome, aggregate pre-lookup bounds, cumulative reconstruction
  accounting, and ownership established before the first sensitive allocation
  through successful result construction. It also makes the missing reader,
  metadata, invalid-material, decoded-duplicate, diagnostics, and wrapper-
  expansion regressions explicit. Avoid the known-bad repeated sibling scans,
  per-fragment-only limits, cancellation checks only between large phases, and
  ownership transfer before allocation can no longer fail. KEEP the approved
  wire bytes and constructibility interpretations, byte-range/no-plaintext-DOM
  design, decoded duplicate detection, residual-marker rejection, pre-external
  validation, full snapshot seam, closed diagnostics and error taxonomy,
  allocation-free observer isolation, Contracts-only/non-packable scope,
  frozen `.slnx`, and 14-package output.
- 2026-09-14: Re-derived the loop-3 implementation around a
  payload-proportional byte lookup, cumulative reader bounds, exact caller
  cancellation precedence, root snapshot crypto, and cleanup ownership through
  result construction. Added the missing 22-case regression matrix and blocking
  CI lanes; the focused gate now passes 159/159 with no skips while the frozen
  vectors, solution, and 14-package release boundary remain unchanged. Story
  status stays `in-progress` pending independent review.
- 2026-09-14: Review-loop iteration 4 found that adjacent-only manifest overlap
  detection misses ancestors when a sibling name sorts between them, and that
  predictable protected-output expansion was still rejected only after key
  creation and encryption. The non-frozen requirements now mandate complete
  bounded prefix detection, pre-material output projection, canonical literal
  wrapper spelling, bounded incremental discovered-path decoding, cancellation
  throughout wrapper maps/path materialization/replacement work, strict snapshot
  kebab suffixes, fixture-consuming golden tests, and the missing full-reader
  and hostile-concurrency regressions. Avoid adjacent-only prefix validation,
  raw-token-sized path rentals, post-crypto locally decidable rejection, and
  synchronous tasks presented as concurrent evidence. KEEP exact G-001/NIST
  bytes and reapproved semantics, payload-proportional child lookup, cleanup
  ownership through result construction, cumulative reader bounds, closed
  taxonomy/diagnostics, root snapshot crypto, Contracts-only/non-packable scope,
  frozen `.slnx`, and 14-package output.
- 2026-09-14: Re-derived loop 4 with complete bounded prefix validation,
  pre-material output projection, literal-wrapper and incremental path rules,
  full cancellation coverage, fixture-consuming/full-reader regressions, and
  genuinely concurrent hostile V138 reads. The focused gate passes 185/185
  with no skips; frozen vectors, the solution, and the 14-package release
  boundary remain unchanged. Advanced to independent review.
- 2026-09-14: Applied the final independent-review patch set for early reserved-
  member/AES rejection, cancellation boundaries, exact diagnostics, snapshot
  cleanup and configured limits, caller isolation, malformed wire keys,
  authenticated residual markers, reconstruction bounds, and resolver
  isolation. The complete focused gate now passes 201/201 with no skips.
- 2026-09-14: Closed the final review findings for canonical nonce validation,
  path-decoding and disposal cancellation, exact wrapper/raw-token behavior,
  frozen vector ownership, exact writer/reader maxima, invalid-material
  cleanup, and snapshot callback isolation. The complete focused gate passes
  217/217 with no skips, and all release, analyzer, style, preservation, and
  packaging checks pass.

- 2026-09-15: Independent four-layer code review of chunk A
  (`src/Hexalith.EventStore.PayloadProtection/`, 34 files) recorded 14 patch
  action items, 1 deferral (DW-516), and 1 decision, with 18 findings rejected
  on verification. The decision was resolved and applied: the AOT/trim analyzers
  became project properties on the core and the `AdditionalProperties` override
  on the frozen Contracts reference was removed, so the analysis now runs on
  every build including the blocking lane rather than in one manual command CI
  never invoked. Contracts' own 12 IL2026/IL3050 violations are tracked as
  DW-517. No engine source changed; the focused gate remains 217/217 with no
  skips and the frozen vectors, solution, and 14-package boundary are unchanged.
  Status returns to `in-progress` pending the 14 action items, of which the
  material one is that invalid UTF-8 inside JSON string values is accepted by
  both writer and reader against authority sections 6.1 and 6.3.
- 2026-09-15: Closed all 14 independent-review patch items with strict UTF-8
  validation, production entropy coverage, centralized wire constants,
  no-leak record formatting, escaped-member and snapshot-ordinal regressions,
  explicit resolver ownership, direct snapshot plaintext transfer, corrected
  failure taxonomy/null flow, and removal of dead guards. The focused gate now
  passes 226/226 with no skips; analyzer, style, documentation, solution,
  preservation, and 14-package checks remain clean.
- 2026-09-15: Closed the final review verification gaps with 20 focused cases
  covering post-auth nonce validation, mixed null manifests, multi-digit array
  paths, observer/diagnostic isolation, reader input snapshots, resolver and
  factory cancellation boundaries, shared-key consistency, maximum-wrapper
  lookup count, and concurrent successful core use. The focused gate now passes
  246/246 with no skips; the frozen intent and production wire bytes are unchanged.

- 2026-09-15: Third independent review, chunk 1 of 6 (codec and wire-format group).
  Three decisions resolved by the owner and ten patches applied. Restored the blocking
  direct-project CI lane that commit `7d6402c1` had demoted to advisory on a premise that
  does not hold (`ci.yml` is bound at frozen commit `17e47a39`, not live). Closed a
  reproduced fail-open in which `CanonicalText` accepted decomposed non-NFC durable
  identities whenever the platform normalizer is inert. Armed the previously unverifiable
  `EnvelopeCodec.Write` nonce-derivation guard, which mutation testing showed could be
  deleted with the whole suite still green. Single-sourced the closed pdenc-v2 wire
  constants, demultiplexed the manifest sort checkpoint, and cleared seven smaller items.
  Suite 252 -> 254, all green in Debug and Release; both new cases mutation-verified.
  Chunks 2-6 (bounded JSON engine, core orchestration and crypto, tests, evidence, and the
  non-8.3 carry-along) are NOT yet reviewed. No frozen authority byte, Contracts type,
  fixture, `.slnx` entry, or release-manifest entry changed.
- 2026-09-15: Rebound the executable verification and completion evidence to
  the third-pass implementation: the focused gate is 254/254, current source,
  test, and CI hashes replace superseded values, and review chunks 2-6 remain
  explicitly pending rather than being represented as closed.
- 2026-09-16: Closed all seven third-pass bounded-JSON review patches with
  allocation-free frame disposal, explicit failed/cancelled parse and rewrite
  cleanup evidence, prior-envelope cleanup on later malformed wrappers, exact
  discovered-path limits, over-limit unselected-path preservation, and literal
  multibyte wrapper discovery. The focused gate is now 263/263; review chunks
  3-6 remain pending.
- 2026-09-17: Closed all 24 third-pass chunk-4a test-scaffolding, envelope,
  AAD/path, and cryptography patches. Added a required-lane completeness guard
  for the dedicated workflow, made buffer-zeroing observations fail closed,
  added end-to-end cross-scope and wrapper-relocation proofs, completed boundary
  and malformed-input matrices, hardened deterministic entropy for concurrency,
  and bound vector metadata and NIST fixture fields. The focused gate is now
  290/290 with no skips; later third-pass review chunks remain pending.
- 2026-09-17: Completed a full-diff fourth review pass across Blind Hunter,
  Edge Case Hunter, Verification Gap, and acceptance audit. Hardened the
  required-lane guard with YAML-aware active-step validation, preserved caller
  cancellation over simultaneous malformed JSON, made the 300-fill entropy
  seam genuinely distinct, and corrected disclosure of separately authored
  trusted-extension and Hexalith.Builds changes. The 290-case focused gate and
  115-case required Contracts guard both pass with no skips; the full-diff pass
  supersedes the remaining partial-chunk review plan.
- 2026-09-17: Closed both fifth-pass group-1 codec findings. Base64url staging
  arrays now use non-elidable cryptographic zeroing, and the protected JSON
  format label is derived from the canonical UTF-8 wire bytes so AAD and result
  labels cannot drift independently. The focused Release gate remains 290/290
  with no skips.
- 2026-09-17: Closed both independent chunk-1 follow-up findings with a
  mutation-armed invariant-globalization invocation and NFC multibyte UTF-8
  ceiling cases for event payload types and property paths. The complete
  focused gate now passes 291/291, and the separate invariant invocation passes
  1/1 with no skips.

## Review Triage Log

- Twelve independent review layers were triaged below. All surviving Story 8.3
  findings were patched and independently verified; rejected findings retain
  their evidence, and the externally authored FrontComposer gitlink remains
  out of scope. Requirement renegotiation and human approval are recorded in
  `AR-20260914-02`.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | medium | defer | `references/Hexalith.FrontComposer` advanced from `6e064785` to `4e6ce047` in externally authored commit `7bbe24d0`; it is outside Story 8.3 and cannot be silently removed. |
| BH-02 | medium | bad_spec | `CryptographicPayloadProtectionEntropy` compiles against `Hexalith.Commons.UniqueIds` only through Contracts' transitive assets, so a Contracts dependency cleanup would break this core despite its Contracts-only project declaration. |
| BH-03 | high | bad_spec | Story 8.3's epic acceptance explicitly covers a selected snapshot value, but only event orchestration is implemented and no snapshot crypto path runs. |
| BH-04 | high | bad_spec | `BoundedJsonDocument.Parse` allocates `ownedJson` containing plaintext and `Dispose` never zeroes that engine-owned mutable array. |
| BH-05 | high | bad_spec | `GetRawText()` creates an immutable managed plaintext string, contradicting the authority's rule that engine-created strings never contain plaintext material. |
| BH-06 | medium | bad_spec | `ProtectedPathManifestCodec.Create` materializes arbitrary `IEnumerable` input before enforcing the 4,096-item bound. |
| BH-07 | medium | bad_spec | The all-pairs ancestor loop performs more than eight million prefix comparisons and repeated string allocations at the valid 4,096-path maximum. |
| BH-08 | medium | bad_spec | Manifest enumeration, sorting, and overlap checks accept no cancellation token and can continue to material creation after cancellation. |
| BH-09 | medium | bad_spec | Final JSON serialization has no periodic or post-serialization cancellation check, so a late cancellation can return success. |
| BH-10 | high | bad_spec | Context/AAD validation occurs after early pass-through and after material/key resolution, permitting locally invalid scope to trigger external work or succeed when nothing is selected. |
| BH-11 | high | bad_spec | Protected output is not re-bounded, so a valid near-limit input can expand beyond 16 MiB and become unreadable by the same core. |
| BH-12 | high | bad_spec | Wrapper replacement can add depth/nodes beyond 64/65,536, yet the returned transformed payload is not structurally revalidated. |
| BH-13 | high | bad_spec | Unprotect enforces per-wrapper plaintext bounds but not cumulative plaintext or reconstructed global depth/node/output ceilings. |
| BH-14 | medium | patch | The full reader rejects a nonce-bit mutation before lookup, while V010 requires the mutation to reach authenticated mismatch; the direct crypto test bypasses this behavior. |
| BH-15 | medium | patch | V138 builds already-completed tasks sequentially, so its claimed hostile concurrency is not exercised. |
| BH-16 | low | patch | The execution-manifest test checks count and samples rather than exact membership, allowing an unasserted vector ID to be replaced undetected. |
| BH-17 | medium | patch | Missing-key and inconsistent-key returns retain diagnostic result `malformed`, producing operationally incorrect metric classification. |
| BH-18 | medium | patch | Encryption-side `CryptographicException` is not converted to the core's bounded safe exception taxonomy. |
| VG-01 | medium | bad_spec | Repository CI and `scripts/ci-local.sh` omit both new projects, so normal blocking verification can remain green while the 137-test core suite fails. |
| VG-02 | medium | patch | No test distinguishes null resolver material as `MissingKey` from wrong-length material as `ConsistencyMismatch`, including zeroing of the latter buffer. |
| VG-03 | medium | patch | Successful multi-value tests use a repeatable factory without counting calls, so a regression to per-field material creation would remain green. |
| VG-04 | medium | patch | No test enters the material generator's exceptional cleanup path after partial entropy fill or a throwing reservation predicate. |
| VG-05 | medium | defer | The complete diff includes the externally bundled FrontComposer gitlink despite the Story 8.3 preservation claim; `7bbe24d0` confirms the inclusion. |
| EC-01 | medium | bad_spec | `BoundedJsonDocument.Parse` validates caller bytes before copying them, so concurrent caller mutation can make the later parsed copy differ from the validated bytes. |
| EC-02 | medium | bad_spec | Protect discards the first validated manifest and later iterates the caller's mutable path collection, allowing validation/use divergence. |
| EC-03 | medium | bad_spec | The 4,096-path overlap check is quadratic and allocation-heavy; sorted adjacent comparison is sufficient. |
| EC-04 | medium | bad_spec | Maximum-size manifest construction has no cancellation checkpoints. |
| EC-05 | high | bad_spec | A non-null selection with invalid identity/type reaches `materialFactory` before AAD validation rejects it. |
| EC-06 | medium | patch | A material record with a null DEK dereferences `.Length` and leaks `NullReferenceException` instead of a bounded failure. |
| EC-07 | high | bad_spec | Unprotect reaches `keyResolver` before proving all context-derived AAD values are locally valid. |
| EC-08 | medium | patch | Any resolver `OperationCanceledException` is propagated even when the caller token is active, so provider timeout cancellation can masquerade as caller cancellation. |
| EC-09 | high | bad_spec | Multiple authenticated wrapper values can reconstruct a tree beyond global node/depth/output bounds. |
| EC-10 | low | patch | A throwing internal buffer observer can interrupt the cleanup loop and leave later plaintext/DEK arrays uncleared. |
| EC-11 | medium | defer | The unrelated FrontComposer gitlink change is present in the reviewed change set and lacks Story 8.3 evidence. |
| EC-12 | medium | bad_spec | The manifest helper's eager `ToArray()` permits over-limit allocation before rejection. |
| EC-13 | high | bad_spec | `Serialize` copies plaintext out of `ArrayBufferWriter` but never clears the abandoned writer storage. |
| EC-14 | medium | patch | The full unprotect path reports local mismatch for V010 instead of exercising authenticated mismatch. |
| EC-15 | medium | patch | V138 omits the 768-node periodic checkpoint and does not assert CPU stability or zero backend calls. |
| EC-16 | medium | defer | The completion claim that old behavior was preserved is not true of the bundled FrontComposer pointer in `7bbe24d0`; ownership remains outside this story. |
| BH2-01 | medium | defer | carried: the baseline diff still contains the externally authored FrontComposer gitlink advance already logged as BH-01/VG-05/EC-11/EC-16; it remains outside Story 8.3 and is not patched or deferred again. |
| BH2-02 | medium | patch | `Base64UrlCodec.Decode(string)` scans and copies an unbounded snapshot carrier string before the span overload applies the 1,398,211-character ceiling, allowing attacker-controlled allocation amplification. |
| BH2-03 | low | patch | `ProtectSnapshot` parses and indexes up to the 16 MiB payload ceiling before applying the normally smaller configured protected-value bound; moving the byte-length rejection before parse is a direct early-bound correction. |
| BH2-04 | high | patch | `CreateMaterial` checks cancellation after the factory returns but before its result is assigned to the caller, so the newly transferred DEK is lost and cannot be cleared on that cancellation path. |
| BH2-05 | medium | bad_spec | Core `finally` blocks and `ClearAll` allocate lists, LINQ state, and arrays before zeroing sensitive buffers; allocation failure can bypass the explicit every-exit cleanup invariant. |
| BH2-06 | high | bad_spec | `BoundedJsonDocument` retains a full concatenated path for every node, so a single long ancestor name shared by many nodes can amplify a valid bounded payload into extreme managed-memory use. |
| BH2-07 | medium | bad_spec | `Utf8JsonReader.GetString()` and the string-keyed full-path index retain decoded JSON member/path material despite the byte-oriented and no-engine-owned-plaintext-string requirement. |
| BH2-08 | high | bad_spec | Post-decryption and reconstructed-output validation does not inspect for residual `$pdenc` members, permitting authenticated nested marker content to escape a reader even though the writer rejects it. |
| BH2-09 | medium | patch | The blocking lanes permit only 48 test cases while the current suite contains 159, so substantial test loss could remain green. |
| BH2-10 | medium | patch | The execution-manifest test compares the fixture only with another hard-coded list and never proves that discovered `Vector` traits match it. |
| BH2-11 | low | reject | V138 deliberately records allocation/CPU/latency without a numeric performance gate, as required by Design Notes; deterministic limits, zero backend calls, true concurrency, and cancellation remain asserted. |
| BH2-12 | low | patch | Changed C# still uses same-line braces and leaves internal constants undocumented despite the tracked Allman and XML-documentation rules; this is a direct formatting/documentation correction. |
| VG2-01 | medium | patch | Pre-verified gap: V034-V036 assert only the exception and do not prove duplicate-member, unresolved-path, and ancestor/descendant failures make zero material-factory calls. |
| VG2-02 | medium | patch | Pre-verified gap: no full-reader malformed wire-key-reference case proves lowercase/forbidden/out-of-range ULID bytes are rejected before resolver lookup. |
| VG2-03 | medium | patch | Pre-verified gap: V044/V136 prove only the 4,096-value writer boundary, so a reader regression rejecting the exact maximum would pass. |
| VG2-04 | medium | patch | Pre-verified gap: snapshot resolution lacks parallel assertions for missing, inconsistent, failed, and provider-cancelled keys. |
| VG2-05 | medium | patch | Pre-verified gap: snapshot success/failure paths have no observer evidence for plaintext, DEK, envelope, and abandoned-output cleanup. |
| VG2-06 | low | reject | Pre-verified observation: V138 has no numeric resource budget, but the approved design explicitly makes those measurements observational and supplies no stable threshold to enforce. |
| VG2-07 | medium | patch | `Base64UrlCodec.Decode(string)` duplicates an unconstrained snapshot envelope before checking the frozen text ceiling. |
| VG2-08 | high | patch | A post-factory cancellation thrown inside `CreateMaterial` loses the transferred material before outer cleanup can observe its DEK. |
| EC2-01 | medium | patch | An oversized snapshot envelope is scanned and copied before the closed carrier length check. |
| EC2-02 | high | bad_spec | Per-node full-path concatenation and retention makes hostile-input work unbounded relative to the payload ceiling. |
| EC2-03 | high | patch | Cancellation after material-factory return can leave a transferred DEK live because assignment never reaches the owning outer scope. |
| EC2-04 | medium | patch | Snapshot unprotect has no cancellation check between AAD validation and resolver invocation, so cancellation arriving there can still trigger external work. |
| EC2-05 | false | reject | A successful reservation followed by cancellation may leave the bounded orphan explicitly permitted by the frozen ordered-reservation protocol; durable release/activation belongs to Story 8.5 rather than this bool collision seam. |
| EC2-06 | high | patch | Meter listener callbacks can throw from final diagnostic recording; on successful unprotect this aborts the return after ownership transfer and leaves the abandoned plaintext result uncleared. |
| EC2-07 | medium | patch | `!AesGcm.IsSupported` currently throws a format exception, misclassifying platform cryptographic unavailability as malformed caller input. |
| EC2-08 | high | bad_spec | The retained full-path string index contradicts the bounded-hostile-input acceptance claim. |
| EC2-09 | high | patch | The post-factory cancellation check can strand engine-owned key material outside every cleanup path. |
| EC2-10 | medium | defer | carried: the externally authored FrontComposer pointer remains the same already-logged out-of-scope change and is not patched or deferred again. |
| BH3-01 | medium | patch | Event reading knows every envelope ciphertext length before lookup but does not reject an aggregate over the 8 MiB plaintext ceiling until after resolver work and multiple decryptions. |
| BH3-02 | high | bad_spec | Selected paths repeatedly scan sibling lists and compare member bytes, allowing path-count × object-width × name-length CPU amplification despite bounded payload size. |
| BH3-03 | medium | bad_spec | The same selected-path sibling scans have no cancellation checkpoints, contradicting the bounded traversal requirement. |
| BH3-04 | medium | patch | Wrapper discovery checks cancellation per outer node but not while scanning all children of one maximum-width object. |
| BH3-05 | medium | bad_spec | Manifest sorting checks cancellation only before and after `Array.Sort`; the existing V138 checkpoint runs later and cannot prove cancellation during comparisons. |
| BH3-06 | medium | patch | Both resolver general-catch branches can return provider-unavailable after the resolver cancels the caller token and throws a non-cancellation exception; caller cancellation must be rechecked after every external outcome. |
| BH3-07 | medium | patch | `InvokeMaterialFactory` has the same caller-cancellation precedence gap when a factory cancels the token and throws another exception. |
| BH3-08 | high | bad_spec | Several paths detach transformed or decrypted plaintext from cleanup ownership before allocating the result object, so an allocation failure can abandon sensitive bytes. |
| BH3-09 | high | bad_spec | `ParseCore` marks its owned input successful before constructing the document, so failed construction skips zeroing. |
| BH3-10 | high | bad_spec | Encryption allocates nonce, ciphertext, and tag before a cleanup-safe ownership region and clears only cryptographic exceptions, allowing later allocation/construction failure to strand buffers. |
| BH3-11 | medium | patch | Event unprotection accepts authenticated JSON `null` even though the canonical event writer never protects a null selected value. |
| BH3-12 | high | bad_spec | Reader node limits are applied independently to each decrypted fragment, permitting multiplicative parsing work before final reconstruction rejects the aggregate tree. |
| BH3-13 | medium | bad_spec | V138 counts harness overlap around a pre-call spin rather than overlap inside the core, so it remains green if core calls serialize. |
| BH3-14 | medium | patch | Verification records focused `dotnet test --no-build` without a preceding focused-project build, so the bound source could differ from the executed assembly. |
| BH3-15 | low | patch | Verification substitutes prose for reproducible style/dependency/surface/package commands, preventing exact replay of claimed gates. |
| BH3-16 | low | patch | `ThrowingBufferObserver` and `PartialThrowEntropy` are secondary types in unrelated test files, violating the tracked one-type-per-file rule. |
| BH3-17 | low | patch | Internal helper methods in `TestFixture` lack the XML documentation required by repository guidance. |
| BH3-18 | medium | defer | carried: the baseline diff still contains the externally authored FrontComposer gitlink advance already logged in earlier review rows; it remains outside Story 8.3 and is not patched or deferred again. |
| VG3-01 | medium | patch | Pre-verified gap: no protected-event test asserts `SerializationFormat == "json+pdenc-v2"`, so encrypted bytes can be mislabeled without failing the suite. |
| VG3-02 | medium | patch | Pre-verified gap: decoded-equivalent duplicate names such as `email` and `\u0065mail` are untested on protection and reader paths. |
| VG3-03 | medium | patch | Pre-verified gap: exact 8 MiB and 8 MiB+1 cumulative plaintext are tested only on the writer, not full event unprotection. |
| VG3-04 | medium | patch | Pre-verified gap: wrapper-induced post-protection depth and node expansion is tested only through the parser rather than the core writer's atomic rejection. |
| VG3-05 | medium | patch | Pre-verified gap: snapshot carrier format and type mismatches are not asserted through the core reader with zero resolver calls. |
| VG3-06 | medium | patch | Pre-verified gap: null and malformed factory material shapes lack event/snapshot closed-taxonomy and cleanup regression coverage. |
| VG3-07 | medium | patch | Pre-verified gap: diagnostics tests permit every unprotect activity/tag to be mislabeled as protect. |
| EC3-01 | high | bad_spec | `ParseCore` can receive an owned snapshot and observe cancellation before entering its cleanup region, leaving the snapshot uncleared. |
| EC3-02 | high | bad_spec | Document construction occurs after the parser marks ownership successful, duplicating BH3-09's exceptional allocation leak. |
| EC3-03 | high | bad_spec | A decoded member-name copy can be allocated and then lost if collision-list growth fails. |
| EC3-04 | high | bad_spec | AAD field arrays are allocated sequentially before the cleanup region, so failure allocating a later field strands earlier canonical identity/path bytes. |
| EC3-05 | medium | bad_spec | Base64url canonical re-encoding can fail after decoded envelope allocation without a catch that clears the decoded bytes. |
| EC3-06 | medium | bad_spec | Envelope field copies are allocated directly in a constructor expression, so a later allocation can strand earlier nonce/ciphertext copies. |
| EC3-07 | high | bad_spec | carried within this iteration's BH3-10 root cause: envelope construction or other non-cryptographic failure after AES succeeds bypasses nonce/ciphertext/tag cleanup. |
| EC3-08 | medium | bad_spec | If the manifest encoded-path list cannot grow after `encodedPath` allocation, that unowned byte array is absent from the final cleanup loop. |
| EC3-09 | medium | bad_spec | carried within this iteration's BH3-05 root cause: manifest sort comparison work has no in-sort cancellation observation. |
| EC3-10 | medium | bad_spec | Manifest hashing has only a pre-hash cancellation check and can return success when cancellation arrives during the maximum-size hash. |
| EC3-11 | medium | bad_spec | carried within this iteration's BH3-03 root cause: wide object/array resolution scans lack periodic cancellation checks. |
| EC3-12 | medium | patch | Wrapper discovery performs potentially large base64 decoding and envelope parsing without cancellation checks between those phases. |
| EC3-13 | high | bad_spec | If wrapper construction or list growth fails after envelope parsing, newly parsed envelope buffers are not yet owned by the cleanup list. |
| EC3-14 | medium | patch | Material generation has no cancellation check after entropy filling and before the reservation predicate, allowing an external side effect after cancellation. |
| EC3-15 | high | bad_spec | carried within this iteration's BH3-08 root cause: pass-through result allocation can fail after the copied plaintext is no longer cleanup-owned. |
| EC3-16 | high | bad_spec | carried within this iteration's BH3-08 root cause: protected result allocation can fail after transformed output is detached. |
| EC3-17 | high | bad_spec | Decrypted plaintext is allocated before `plaintextBuffers.Add`; list-growth failure can leave the newest value outside final cleanup. |
| EC3-18 | high | bad_spec | carried within this iteration's BH3-08 root cause: event readable-result allocation can fail after reconstructed plaintext is detached. |
| EC3-19 | high | bad_spec | carried within this iteration's BH3-08 root cause: snapshot readable-result allocation can fail after output plaintext is detached. |
| EC3-20 | false | reject | `--fail-skips on` makes any skipped focused test fail the GitHub lane, so the claimed green-with-skips outcome does not occur. |
| EC3-21 | false | reject | The local lane also uses `--fail-skips on`, disproving the same skipped-test claim. |
| EC3-22 | medium | bad_spec | carried within this iteration's BH3-05/EC3-10 root cause: sort and final hash work do not observe cancellation throughout and after completion. |
| EC3-23 | high | bad_spec | carried within this iteration's EC3-01/BH3-09 root cause: parser ownership begins too late and ends before document construction succeeds. |
| EC3-24 | medium | defer | carried: the externally authored FrontComposer pointer remains the already-logged out-of-scope history and is not patched or deferred again. |
| BH4-01 | medium | defer | carried: the externally authored FrontComposer gitlink advance is the same already-logged out-of-scope change and is not patched or deferred again. |
| BH4-02 | medium | defer | carried: the evolving external FrontComposer target and its provenance are the same prior gitlink issue; it remains excluded from Story 8.3 rather than deferred again. |
| BH4-03 | false | reject | During Step 4 the spec is intentionally `in-review` while sprint tracking remains `in-progress`; final synchronization occurs only after review succeeds. |
| BH4-04 | medium | patch | The focused project copies the immutable Story 8.2 fixtures but no focused test consumes them, so its golden assertions rely on duplicated constants. |
| BH4-05 | false | reject | Current `JsonPointer.Decode` and `CanonicalText.Decode` validate with allocation-free `GetByteCount`; the cited abandoned validation byte arrays no longer exist. |
| BH4-06 | medium | bad_spec | Wrapper discovery compares decoded names but records escape state only for values, so an obfuscated `\u0024pdenc` member is accepted as canonical. |
| BH4-07 | high | bad_spec | Writer output byte/node/depth expansion is locally predictable but is rejected only after material creation and AES work. |
| BH4-08 | medium | patch | Event wrapper-path copying, dictionary construction, and aggregate AAD validation lack periodic cancellation checks across 4,096 entries. |
| BH4-09 | medium | bad_spec | Discovered-path materialization has no cancellation parameter or checkpoints across thousands of ancestor walks. |
| BH4-10 | high | bad_spec | Discovered-path decoding rents the complete raw property-token length before enforcing the 2,048-byte canonical path cap, allowing near-payload-sized allocation for inevitable rejection. |
| BH4-11 | medium | patch | Material generation does not recheck caller cancellation immediately after key-reference entropy returns, so it can continue to DEK generation or let a non-cancellation exception win. |
| BH4-12 | medium | patch | V138 hostile unprotect calls complete synchronously during enumeration; the separate in-core gate covers protection only and does not prove concurrent hostile reads. |
| BH4-13 | medium | patch | V031 tests only a standalone escaping helper; no complete protect/read case verifies actual `MaterializePath` behavior for `~` and `/` names. |
| BH4-14 | medium | patch | V011/V012 mutate tag/ciphertext only through the primitive decryptor, leaving full-reader resolver, atomic-result, and cleanup behavior unverified. |
| BH4-15 | false | reject | The diagnostics test explicitly asserts every expected `(instrument, operation, result, format_version)` tuple and every measurement's exact three-tag shape at lines 145-162. |
| BH4-16 | low | patch | Verification still abbreviates formatter, packaging, and dependency/surface scan invocations instead of recording exact replayable commands. |
| EC4-01 | high | bad_spec | Adjacent-only sorted overlap validation misses `/a` as an ancestor of `/a/b` when `/a-foo` sorts between them, causing locally invalid selections to reach material/AES. |
| EC4-02 | medium | patch | Snapshot type validation permits consecutive hyphens although the authoritative suffix is lowercase ASCII kebab-case. |
| EC4-03 | medium | patch | Material generation can call DEK entropy after key-reference entropy cancels the caller token; cancellation must be checked immediately after that boundary. |
| EC4-04 | medium | patch | Replacement descriptor copying and sorting omit bounded cancellation observation at the 4,096-replacement maximum. |
| EC4-05 | medium | patch | carried within this iteration's BH4-08 root cause: wrapper dictionary and aggregate AAD loops lack periodic cancellation checks. |
| EC4-06 | false | reject | Current path validation calls allocation-free `CanonicalText.GetByteCount`; the cited abandoned mutable validation buffer is absent. |
| EC4-07 | false | reject | Concurrent measurements use the same closed three-tag vocabulary and the test requires all expected tuples without asserting counts, so unrelated valid measurements cannot make it fail. |
| EC4-08 | medium | defer | carried: the FrontComposer gitlink is the already-logged externally authored out-of-scope history and is not patched or deferred again. |
| VG4-01 | medium | patch | Pre-verified gap: manifest enumeration, sort, and hash cancellation are tested, but the encoding-phase checkpoint is never exercised. |
| VG4-02 | medium | patch | Pre-verified gap: V138 creates synchronously completed hostile-unprotect values, so it does not prove overlap inside that read path. |
| VG4-03 | medium | patch | Pre-verified gap: event missing-key and wrong-length-key classifications and wrong-length cleanup are not directly tested. |
| VG4-04 | medium | patch | Pre-verified gap: snapshot protection's reserved-marker rejection lacks a zero-material-call regression. |
| VG4-05 | medium | patch | Pre-verified gap: invalid event context on empty-selection pass-through is not exercised through `ProtectEvent`. |
| VG4-06 | false | reject | Despite the filed gap, the current diagnostics test already asserts exact protect/unprotect operation/result tuples for both instruments at lines 145-162, directly disproving the proposed regression. |
| BH5-01 | high | patch | An escaped `$pdenc` member elsewhere beside a valid literal wrapper is locally visible but is rejected only after key lookup/decryption. |
| BH5-02 | low | reject | A single property-name decode is bounded by the 16 MiB payload ceiling; making framework UTF-8 decoding interruptible would add disproportionate parser complexity for negligible bounded latency. |
| BH5-03 | medium | patch | `FindPropertyNameEnd` scans a raw property token without cancellation before incremental discovered-path decoding starts. |
| BH5-04 | medium | patch | Manifest enumeration checks only after `MoveNext`; a pre-cancelled or cancellation-producing external enumerable can still run observable work. |
| BH5-05 | low | reject | Carrier decode/copy is capped at about 1.4 MiB and followed immediately by cancellation checks; threading cancellation through closed codecs is disproportionate to this bounded delay. |
| BH5-06 | medium | patch | AES-GCM support is locally known but checked only after material creation or key lookup, causing avoidable external work on unsupported platforms. |
| BH5-07 | medium | patch | Metrics flatten tags across instruments and do not prove exact per-measurement operation/result association. |
| BH5-08 | medium | patch | The activity test invokes only protection and does not require the unprotect activity name. |
| BH5-09 | medium | patch | Snapshot success/authentication-failure tests do not observe clearing of plaintext, DEK, envelope, and abandoned-output buffers. |
| BH5-10 | medium | patch | Several locally invalid event selection tests use an uncounted material factory, so they do not prove rejection precedes material creation. |
| BH5-11 | medium | patch | No factory callback mutates caller bytes and the mutable path collection to prove the core uses only pre-callback snapshots. |
| BH5-12 | medium | patch | No protected-event test asserts `SerializationFormat == "json+pdenc-v2"`. |
| BH5-13 | medium | patch | Noncanonical wire key references lack complete-reader cases proving local rejection and zero resolver calls. |
| BH5-14 | medium | patch | Authenticated event-root `null` and authenticated residual/emergent `$pdenc` plaintext lack reader regressions. |
| BH5-15 | medium | patch | V136 parser-only depth/node maxima do not directly prove core reconstruction accounting and atomic rejection. |
| BH5-16 | medium | patch | Snapshot resolver cancellation precedence and post-resolver DEK cleanup are not directly tested. |
| BH5-17 | medium | patch | V138 does not count resolver calls and its cancellation cases do not assert full-reader backend isolation. |
| BH5-18 | medium | defer | carried: the FrontComposer gitlink remains the already-logged externally authored out-of-scope history and is not patched or deferred again. |
| VG5-01 | medium | patch | Pre-verified gap: exact unprotect activity and metric operation identity are not required by the current diagnostics tests. |
| VG5-02 | medium | patch | Pre-verified gap: metric callbacks discard instrument identity, so removal of the duration histogram remains green. |
| VG5-03 | medium | patch | Pre-verified gap: missing-key, consistency-mismatch, and cryptographic-failure metric classifications are not observed. |
| VG5-04 | medium | patch | Pre-verified gap: snapshot configured write maximum exact/max+1 behavior is untested. |
| VG5-05 | medium | patch | Pre-verified gap: correctly authenticated event/snapshot plaintext containing reserved markers is not exercised. |
| VG5-06 | medium | patch | Pre-verified gap: caller payload and path mutation after snapshotting is not tested through the material callback. |
| EC5-01 | medium | patch | Manifest code copies an arbitrarily oversized caller path before applying the 2,048-byte bound. |
| EC5-02 | medium | patch | Canonical context validation normalizes and scans strings whose UTF-16 length already proves they exceed the byte maximum. |
| EC5-03 | high | patch | carried within this iteration's BH5-01 root cause: escaped reserved names accompanying a valid wrapper reach lookup before rejection. |
| EC5-04 | medium | patch | A process-wide meter listener can capture concurrent valid results not in the test's limited set, causing nondeterministic failures; isolate diagnostics tests from parallel execution. |
| EC5-05 | medium | defer | carried: the FrontComposer gitlink is the same already-logged externally authored out-of-scope history and is not patched or deferred again. |
| BH6-01 | false | reject | `PayloadProtectionMaterial` explicitly transfers fresh, one-invocation material to the core, while durable reuse detection/reservation belongs to Story 8.5; the claimed reuse requires an internal caller to violate that seam contract. |
| BH6-02 | false | reject | The generator emits initial version 1, while the core intentionally accepts every positive recorded version so the same byte engine can process later rotation/re-encryption material without owning lifecycle policy. |
| BH6-03 | false | reject | Enumerator implementation faults are caller-code failures rather than malformed serialized input; acquisition, `MoveNext`, and `Current` already preserve caller-cancellation precedence, while disposal precedence is tracked separately as EC6-04. |
| BH6-04 | medium | patch | `JsonPointer.Decode` can inspect the complete 2,048-byte selected path without the required 256-byte cancellation observation; thread an internal cancellation/checkpoint seam through path validation. |
| BH6-05 | low | reject | carried from BH5-02: a single framework token read/copy is capped by the 16 MiB payload ceiling, and replacing `Utf8JsonReader` primitives to interrupt one token would add disproportionate parser complexity for bounded latency. |
| BH6-06 | false | reject | A null nested identity is explicit programmer misuse already specified and tested as `ArgumentNullException`; validation happens before any resolver call, and serialized carrier failures still use the typed unreadable result. |
| BH6-07 | false | reject | V046 owns the generator's concurrent collision/reservation behavior; the core consumes the documented fresh-material factory once per payload and cannot add durable reuse state without importing Story 8.5 lifecycle ownership. |
| BH6-08 | low | reject | carried from BH2-11/VG2-06: V138 resource values are deliberately observational, while deterministic input ceilings, overlap, cancellation, and zero resolver calls are asserted. |
| BH6-09 | medium | patch | V040 proves semantic round trips but not exact restoration of alternate valid token spellings or selected-value whitespace, leaving the byte-oriented rewrite invariant under-tested. |
| BH6-10 | medium | patch | The editable execution manifest and reflected traits can replace an unsampled assigned ID while retaining the 201-case minimum; derive and assert the exact frozen ownership set independently. |
| BH6-11 | medium | defer | carried: the FrontComposer gitlink advance remains the already-recorded externally authored change and is neither patched nor deferred again by Story 8.3. |
| BH6-12 | false | reject | The command is a working-tree preservation check and did pass; baseline-to-review FrontComposer provenance is separately disclosed in the completion artifact and prior carried rows rather than hidden by that command. |
| BH6-13 | false | reject | `AR-20260914-02` binds the requirements evidence reviewed by the human and conditionally authorizes later implementation closure; subsequent verification revisions do not amend those approved requirements and cannot be retroactively represented as human-reviewed bytes. |
| VG6-01 | medium | patch | Pre-verified gap: exact frozen ownership is not derived independently of editable `vector-execution.json`, so an assigned vector and trait can be replaced together. |
| VG6-02 | medium | patch | Pre-verified gap: V001 extracts the carrier through JSON parsing but never asserts the core writer's complete canonical wrapper bytes. |
| VG6-03 | medium | patch | Pre-verified gap: the complete reader lacks zero-resolver cases for an otherwise valid wrapper with an extra member, non-string carrier, or escaped carrier value. |
| VG6-04 | medium | patch | Pre-verified gap: core writer projection and authenticated reader reconstruction exercise over-limit rejection but not successful exact byte/node/depth maxima. |
| VG6-05 | medium | patch | Pre-verified gap: invalid factory-material tests do not retain non-null DEK arrays or prove event/snapshot cleanup and observer notification. |
| VG6-06 | medium | patch | Pre-verified gap: event callback mutation proves stable snapshots, but snapshot protection has no equivalent material-callback mutation regression. |
| EC6-01 | high | patch | `EnvelopeCodec.ValidateFields` writes only the final eight bytes of a 12-byte stack nonce before comparison; explicitly clear the four-byte prefix. |
| EC6-02 | high | patch | `PayloadCryptography.HasExpectedNonce` likewise compares four uninitialized stack bytes; explicitly clear the expected nonce before writing the ordinal. |
| EC6-03 | false | reject | carried from EC2-05: a bounded orphan after successful reservation and cancellation is explicitly permitted by the frozen ordered-reservation protocol; durable release/activation is Story 8.5-owned. |
| EC6-04 | medium | patch | Enumerator disposal can replace an in-flight caller cancellation with an arbitrary disposal exception; dispose through a cancellation-precedence guard. |
| EC6-05 | medium | patch | `ReadProtectedWrappers` has no final cancellation check, so cancellation within its last 255 non-object nodes can return metadata mismatch instead of cancellation. |
| EC6-06 | false | reject | The factory-returned DEK's ownership explicitly transfers to the core; aliasing it with another caller reference does not revoke that transfer, and zeroing the transferred key is the required behavior. |
| EC6-07 | false | reject | The same transferred-ownership rule applies when a snapshot byte array is deliberately returned as the DEK; the core must zero material whose ownership the factory transferred. |
| EC6-08 | high | patch | carried within EC6-01's root cause: canonical envelope validation reads an uncleared four-byte stack prefix. |
| EC6-09 | false | reject | carried within EC6-06's ownership result: the array returned as cryptographic material is transferred to the core and must be zeroed even when the caller deliberately aliases it. |
| BH7-01 | medium | defer | carried from BH-01/VG-05/EC-11: the baseline contains the externally authored FrontComposer gitlink advance; Story 8.3 neither owns nor approves it, so it is not patched or deferred again. |
| BH7-02 | false | reject | carried from BH6-12: the cited `git diff --name-only` command is only the working-tree preservation check; the baseline gitlink drift is separately disclosed in the completion artifact and review ledger. |
| BH7-03 | false | reject | carried from BH4-03: `in-review` in the spec and `in-progress` in sprint tracking are the required Step-4 intermediate state; final synchronization follows successful review. |
| BH7-04 | false | reject | The suite covers the required 768 cancellation boundary in `Manifest_CancellationCheckpointsCoverEveryBoundedPhase`; V138 separately proves in-core hostile-read overlap, checkpoints 1/256/512, and zero resolver calls, so the aggregate requirement is exercised. |
| BH7-05 | low | reject | Deterministic CLR allocation failure is not injectable here, while every allocation-adjacent ownership transition was traced and cleanup itself is allocation-free; adding allocator seams throughout the core is disproportionate for an OOM-only verification gap. |
| BH7-06 | medium | patch | No test makes `ISensitiveBufferObserver` throw, so removal of the best-effort guards could change outcomes and interrupt later cleanup; add focused protect, unprotect, parse, and material-generation coverage. |
| BH7-07 | medium | patch | No test uses throwing activity or meter callbacks, so the diagnostic-listener isolation catches can regress without detection; add a nonparallel end-to-end regression. |
| BH7-08 | medium | patch | Reader input is copied before lookup, but no resolver mutates the caller buffer to prove the reader continues from its owned snapshot; add an exact-output mutation regression. |
| BH7-09 | medium | patch | The authenticated event-root `null` rejection has no complete-reader test, so deleting the guard leaves the suite green; add a validly authenticated null wrapper regression. |
| BH7-10 | medium | patch | The pre-lookup shared key-reference/DEK-version consistency guard lacks mixed-wrapper tests; add both mutations and require zero resolver calls. |
| BH7-11 | medium | patch | The complete 4,096-wrapper success test does not count resolver calls, leaving provider-call amplification undetected; assert exactly one shared-DEK resolution. |
| BH7-12 | medium | patch | No successful `ProtectEvent` calls run concurrently through one core instance, so per-call state and nonce isolation can regress; add concurrent protected round trips with fresh material. |
| VG7-01 | medium | patch | Pre-verified gap: V010's nonce-bit mutations invalidate authentication and never exercise either post-auth deterministic-nonce guard; add valid-tag noncanonical-nonce cases for event and snapshot readers. |
| VG7-02 | medium | patch | Pre-verified gap: V033 resolves `/items/10` without asserting the selected node; add a distinguishable complete-core multi-digit array round trip. |
| VG7-03 | medium | patch | Pre-verified gap: no test mixes selected null and non-null paths, so manifest rebuilding and ordinal compaction are unprotected; cover both input orders. |
| VG7-04 | medium | patch | Pre-verified gap duplicating BH7-09: the full event reader lacks an authenticated-null-wrapper regression. |
| VG7-05 | medium | patch | Pre-verified gap: material factories never throw in tests, leaving exception closure and caller-cancellation precedence unverified for both writers. |
| VG7-06 | medium | patch | Pre-verified gap: neither reader tests a resolver-owned `OperationCanceledException` while the caller token remains active; require `ProviderUnavailable`. |
| VG7-07 | medium | patch | Pre-verified gap duplicating BH7-07: only well-behaved activity and meter listeners are tested. |
| EC7-01 | false | reject | Authority section 6.1 explicitly permits insignificant JSON whitespace on reads; the wrapper still enforces one literal unescaped `$pdenc` string member and canonical carrier bytes. |
| EC7-02 | low | reject | A caller snapshot copy can delay cancellation only for the fixed 16 MiB input ceiling; chunked copy machinery is disproportionate for this bounded everyday-negligible delay. |
| EC7-03 | low | reject | carried from BH6-05: strict UTF-8 validation and one framework token read are capped by the 16 MiB payload limit; replacing framework primitives for intra-token cancellation is disproportionate. |
| EC7-04 | low | reject | carried from BH5-05: carrier decoding is capped at about 1.4 MiB and immediately followed by cancellation checks; threading cancellation through closed codecs is disproportionate. |
| EC7-05 | false | reject | carried from the prior key-resolver-timeout rejection: the core supplies the caller token, while enforcing provider timeouts belongs to the excluded Story 8.5 lifecycle seam. |
| EC7-06 | false | reject | carried from EC7-05 for the snapshot reader; the same caller-token and later-story ownership boundary applies. |
| EC7-07 | false | reject | carried from EC7-05/EC7-06: a resolver that violates its cancellation contract does not make this Story 8.3 byte core own timeout policy. |
| EC7-08 | low | reject | The production-entropy assertions can fail only on astronomically improbable valid CSPRNG collisions/all-zero 256-bit keys; weakening the only production-path test is not justified by an everyday-negligible risk. |
| EC7-09 | false | reject | carried from BH2-11/VG2-06: V138 resource values are expressly observational by Design Notes, while deterministic limits, concurrency, cancellation, and zero backend calls are asserted. |
| EC7-10 | high | defer | The live active GitHub ruleset omits `ci / payload-protection`, so its failure does not block merge; branch-rule mutation is an external resource explicitly excluded by frozen Story 8.3 intent and requires owner action. |
| BH8-01 | medium | patch | The baseline diff contains separately committed idempotency-adapter, command-status, AggregateActor, and FrontComposer changes excluded by Story 8.3, but the completion artifact discloses only one FrontComposer advance; correct the evidence narrative without claiming or reverting the external work. |
| BH8-02 | false | reject | The content-binding table explicitly binds the named Story 8.3 production, test, CI, and evidence inventories; it does not claim to hash every out-of-scope file present after the preserved baseline. |
| BH8-03 | medium | defer | carried from BH4-02 and BH7-01: the FrontComposer gitlink continued from the already-recorded external `4e6ce047` target to `12523054` in separately authored commit `163acb77`; Story 8.3 neither owns nor reverts that evolving external history. |
| BH8-04 | false | reject | carried from BH6-12: the preservation command establishes the Story 8.3 working-tree delta, while baseline-to-current external changes require accurate disclosure rather than pretending the command compares commit history. |
| BH8-05 | high | defer | carried from EC7-10: `ci / payload-protection` is not a required GitHub context; the already-recorded owner-only ruleset action remains external to Story 8.3 and is not deferred again. |
| BH8-06 | false | reject | Concurrent commit `610f64ee` now rejects null, incomplete, cross-message, noncanonical, unknown, and name/code-inconsistent command-status bodies in `IsValidCommandStatus`. |
| BH8-07 | false | reject | Concurrent commit `610f64ee` changed `CommandStatusQueryResponse.IsRejected` to require both the rejected status code and the exact rejected status name. |
| BH8-08 | medium | defer | `CommandStatusPath` still accepts null, whitespace, and slash-only configuration, causing a null dereference or an identifier-only route; this public Client option was introduced by separately authored command-status work explicitly excluded from Story 8.3. |
| BH8-09 | false | reject | Concurrent commit `610f64ee` added focused Client tests for configured/escaped routing, 404, invalid bodies, cross-message responses, and contradictory status representations. |
| BH8-10 | medium | defer | Adding abstract `GetCommandStatusAsync` to the released `IEventStoreGatewayClient` breaks source compatibility for external implementations; the public-contract change is separately authored and explicitly excluded from Story 8.3. |
| BH8-11 | false | reject | The Client DTO deliberately documents a caller-facing subset of the Server response, current shared fields align, and no present wire mismatch was demonstrated; speculative future drift does not establish this claimed defect. |
| BH8-12 | medium | defer | The reusable fake returns one global status without recording or routing by `messageId`, so it can hide cross-command polling defects; this separately authored Testing-package surface is excluded from Story 8.3. |
| BH8-13 | medium | defer | The new AggregateActor no-op payload test and existing handler/controller tests do not exercise the actor-to-handler-to-HTTP path together, so caller-visible preservation can still regress; the Server change is separately authored and excluded from Story 8.3. |
| BH8-14 | false | reject | The focused built xUnit assembly invocation for `StateMachineIntegrationTests.ProcessCommand_NoOp_WithResultPayload_PreservesTerminalPayload` passed 1/1 with zero skips; the unrelated full-project failures do not invalidate this focused path. |
| BH8-15 | false | reject | Story 8.3 requires exact vector, boundary, and behavior gates, not a coverage-percentage artifact; `coverlet.collector` is repository-standard test infrastructure and its presence creates no missing acceptance condition. |
| VG8-01 | medium | patch | Pre-verified against the staged review snapshot: the new Client status-read path lacked executable contract tests. Concurrent commit `610f64ee` supplied the requested configured-path, escaping, 404, deserialization, and invalid-response cases before triage completed. |
| VG8-02 | medium | patch | Pre-verified against the staged review snapshot: the published fake lacked response, exception, and cancellation tests. Concurrent commit `610f64ee` supplied those focused cases before triage completed. |
| VG8-03 | high | defer | carried from EC7-10: the active ruleset omits `ci / payload-protection`; the owner-only external mutation is already recorded and is not deferred again. |
| VG8-04 | false | reject | Concurrent commit `610f64ee` rejects successful JSON `null`, `{}`, missing identities, mismatched message IDs, and inconsistent status fields before returning a result. |
| VG8-05 | false | reject | Concurrent commit `610f64ee` requires both status representations for `IsRejected`, disproving the filed contradictory-OR behavior. |
| EC8-01 | false | reject | Concurrent commit `610f64ee` added `IsValidCommandStatus`, which rejects every stated null, incomplete, inconsistent, and cross-message trigger. |
| EC8-02 | medium | defer | carried with BH8-08: null, whitespace, or slash-only `CommandStatusPath` remains invalid but belongs to the separately authored public Client feature, not Story 8.3. |
| EC8-03 | false | reject | Concurrent commit `610f64ee` changed the property to an exact conjunction, so contradictory status representations are no longer reported as rejected. |
| EC8-04 | medium | patch | Cancellation arriving while the sixteenth collided DEK is cleared falls through to `PayloadProtectionCryptographicException`; recheck caller cancellation after collision cleanup before exhausting retries. |
| EC8-05 | low | patch | A throwing operations-counter listener is caught around both instruments and therefore suppresses the duration measurement; isolate each best-effort instrument recording call. |
| EC8-06 | low | reject | carried from the second-pass low disposition: `BoundedJsonDocument` is internal and every production use is `using`-scoped, so adding disposal guards to all accessors is disproportionate for an unreachable everyday path. |
| EC8-07 | medium | patch | The AggregateActor behavior change is real but separately authored and excluded; the Story 8.3 completion evidence must disclose it rather than implying FrontComposer is the baseline diff's only external change. |
| BH9-01 | false | reject | carried from BH6-13: `AR-20260914-02` records the requirements evidence reviewed at commit `220e722d` and conditionally authorizes later closure; rebinding the mutable implementation verification must not be represented as a retroactive human approval of different bytes. |
| BH9-02 | high | defer | carried from EC7-10: the live GitHub ruleset does not require `ci / payload-protection`; that owner-only external mutation remains outside frozen Story 8.3 and is not deferred again. |
| BH9-03 | medium | defer | carried from the third-pass CI decision: the advisory matrix still does not directly build PayloadProtection and uses incompatible VSTest flags under Microsoft.Testing.Platform; the dedicated direct-project lane is the Story 8.3 gate, and the broader advisory-job defect was already left to its owner. |
| BH9-04 | low | reject | carried from BH5-02: a single framework JSON-token read is bounded by the 16 MiB payload ceiling; replacing `Utf8JsonReader` to make an individual token interruptible adds disproportionate parser complexity for bounded latency. |
| BH9-05 | medium | defer | carried from BH8-08: null, whitespace, or slash-only `CommandStatusPath` is a real misconfiguration defect in separately authored command-status work excluded from Story 8.3; it is not deferred again. |
| BH9-06 | medium | defer | carried from BH8-10: the abstract `GetCommandStatusAsync` addition breaks external interface implementors, but that public-contract change is separately authored command-status work excluded from Story 8.3; it is not deferred again. |
| BH9-07 | medium | defer | carried from BH8-12: the reusable fake's global unrecorded command-status response can hide cross-command polling defects, but it belongs to separately authored command-status work and is not deferred again. |
| BH9-08 | medium | defer | `CommandStatusQueryResponse.MessageId` explicitly permits legacy `null`, while the separately authored gateway rejects every such response; legacy status polling can therefore fail despite the contract, outside Story 8.3. |
| BH9-09 | medium | defer | carried from BH8-13: no actor-to-router-to-handler test proves a non-null no-op result payload crosses the production boundary, but that separately authored Server change is excluded from Story 8.3 and is not deferred again. |
| BH9-10 | medium | defer | carried from DW-516: the Story 8.3 resolver seam cannot express revocation, denial, or unsupported versions; the richer typed outcome belongs to Stories 8.5/8.6 and is not deferred again. |
| BH9-11 | medium | defer | carried from DW-518: Story 8.3 emits an activity source and meter but frozen intent forbids the Server/host integration that registers them; the Story 8.7/8.8 work is not deferred again. |
| BH9-12 | medium | defer | carried from DW-519: without the Story 8.4 persisted-format routing input, plaintext JSON cannot be distinguished from stripped protected content; this later-story seam is not deferred again. |
| EC9-01 | medium | defer | carried from the third-pass CI decision and BH9-03: on a clean runner the advisory job does not build PayloadProtection before `--no-build`; the broader advisory-job defect was already left to its owner. |
| EC9-02 | medium | defer | carried from BH8-08: `CommandStatusPath` misconfiguration can throw or route incorrectly in separately authored command-status work; it is not deferred again. |
| EC9-03 | medium | defer | Same verified root cause as BH9-08: the contract documents legacy null message identifiers, but the gateway's strict correlation guard rejects them; this separately authored client behavior is outside Story 8.3. |
| EC9-04 | medium | defer | `CommandStatus.Rejected` explicitly includes infrastructure rejections with `FailureReason` and no `RejectionEventType`, while `CommandStatusQueryResponse.IsRejected` claims domain-rejection semantics from status alone; callers can misclassify infrastructure conflicts in separately authored command-status work. |
| EC9-05 | low | reject | carried from BH5-02: the maximum individual JSON-token scan is bounded by the 16 MiB input ceiling, and interruptible framework parsing would require disproportionate replacement machinery. |
| EC9-06 | low | reject | carried from BH5-05: canonical carrier decode is capped near 1.4 MiB and followed by cancellation checks; threading cancellation through the closed codec adds complexity for bounded latency. |
| EC9-07 | low | reject | A malicious or defective synchronous material callback can block after cancellation because its internal Story 8.3 seam has no token, but the spec requires cancellation precedence after callback outcomes and adding an asynchronous/token-bearing provider seam imports Story 8.5 lifecycle complexity for an uncommon misuse case. |
| EC9-08 | low | reject | A malicious or defective synchronous collision predicate can block until it returns, but it is a bounded internal Story 8.3 test seam and adding cancellation-aware durable reservation changes the Story 8.5-owned lifecycle contract for an uncommon misuse case. |
| EC9-09 | medium | defer | carried from BH8-10: the new abstract gateway member is source-breaking separately authored public Client work excluded from Story 8.3; it is not deferred again. |
| VG9-01 | high | defer | carried from EC7-10: the active repository ruleset omits `ci / payload-protection`; the pre-verified owner-only settings action remains external to Story 8.3 and is not deferred again. |
| VG9-02 | medium | defer | carried from BH8-13: existing tests do not transport a sentinel no-op payload across the actor-router production seam, but the pre-verified gap belongs to separately authored Server work and is not deferred again. |
| VG9-03 | medium | defer | carried from BH8-08: no test covers invalid `CommandStatusPath`, and the verified defect belongs to separately authored Client work excluded from Story 8.3; it is not deferred again. |
| BH10-01 | medium | defer | carried from BH9-03/EC9-01: the advisory matrix still does not build PayloadProtection before its `--no-build` invocation; the dedicated Story 8.3 lane is the executable gate and the broader advisory defect is not deferred again. |
| BH10-02 | high | defer | carried from EC7-10/BH9-02/VG9-01: the active ruleset still omits `ci / payload-protection`; the owner-only external mutation remains outside Story 8.3 and is not deferred again. |
| BH10-03 | false | reject | carried from BH6-12/BH7-02/BH8-04: the cited command is intentionally a working-tree preservation check, while baseline-to-current external history is disclosed separately in the completion artifact and triage ledger. |
| BH10-04 | medium | defer | carried from BH-01 and later FrontComposer rows: the baseline contains externally authored gitlink advances that Story 8.3 neither owns nor reverts, so they are not deferred again. |
| BH10-05 | false | reject | The checked task list covers implementation acceptance, not completion of every independent-review chunk; the spec remains `in-review` and both the spec and completion artifact explicitly state that chunks 3-6 are pending. |
| BH10-06 | false | reject | The style and one-type scans cover every Story 8.3-owned C# file; the Client, Contracts, Server, and related test changes are separately committed work explicitly excluded and disclosed by this story. |
| BH10-07 | medium | defer | carried from BH8-08/BH9-05/EC9-02: invalid `CommandStatusPath` values can still throw or route incorrectly in separately authored Client work and are not deferred again. |
| BH10-08 | medium | defer | carried from BH9-08/EC9-03: the gateway still rejects legacy null message identifiers despite the contract allowing them; that separately authored Client issue is not deferred again. |
| BH10-09 | medium | defer | carried from EC9-04: `IsRejected` still cannot distinguish an infrastructure rejection without a rejection event type from a domain rejection; the separately authored Contracts issue is not deferred again. |
| BH10-10 | medium | defer | `IsValidCommandStatus` accepts a terminal non-rejected response carrying a rejection-only event type, so a contradictory server body can cross the separately authored Client boundary. |
| BH10-11 | medium | defer | carried from BH8-12/BH9-07: the reusable fake still returns one global unrecorded response and can hide cross-command routing errors; it is not deferred again. |
| BH10-12 | medium | defer | carried from BH8-13/BH9-09/VG9-02: no actor-to-handler-to-HTTP test transports a sentinel no-op payload, but that separately authored Server gap is not deferred again. |
| BH10-13 | low | reject | The cited rows are a historical point-in-time triage record and later rows capture the concurrent compatibility change; rewriting the build spec is not a code correction and review findings whose fix is spec editing are rejected. |
| BH10-14 | medium | defer | Case-distinct request extension keys collapse into the case-insensitive trusted dictionary after separate policy evaluation, allowing last-write selection in separately authored trusted-extension work. |
| BH10-15 | false | reject | A policy exception fails closed before mediator admission and the registered `GlobalExceptionHandler` converts it to bounded RFC ProblemDetails; the claimed raw or unclassified escape does not occur. |
| BH10-16 | false | reject | The unified baseline diff contains separately committed work, but the current working-tree patch is Story 8.3-only and its completion artifact now discloses the idempotency, command-status, AggregateActor, and FrontComposer changes rather than claiming them. |
| EC10-01 | medium | defer | carried from BH8-08/BH9-05/EC9-02: invalid `CommandStatusPath` configuration remains a real separately authored Client defect and is not deferred again. |
| EC10-02 | medium | defer | carried from BH9-08/EC9-03: strict message correlation still rejects contract-permitted legacy null identifiers in separately authored Client work and is not deferred again. |
| EC10-03 | medium | defer | carried from EC9-04: the status-only `IsRejected` property still misclassifies infrastructure rejection as domain rejection in separately authored Contracts work and is not deferred again. |
| EC10-04 | medium | defer | carried from BH8-12/BH9-07: the reusable fake's global status response can still hide cross-command routing defects and is not deferred again. |
| EC10-05 | false | reject | A throwing policy cannot authorize the extension or reach admission; the repository's global exception handler produces a bounded 500 ProblemDetails response, so the stated unsafe escape is disproved. |
| EC10-06 | false | reject | The owner already resolved this exact codec-observability question: call-frame staging is zeroed unconditionally, and the observer evidence contract covers buffers that survive a codec call. |
| EC10-07 | high | defer | carried from EC7-10/BH9-02/VG9-01: the active ruleset still does not require the focused job; that external owner action is not deferred again. |
| VG10-01 | high | defer | carried from EC7-10/BH9-02/VG9-01: the pre-verified active-ruleset gap remains external to Story 8.3 and is not deferred again. |
| VG10-02 | medium | defer | Pre-verified: `GetCommandStatusAsync` has no endpoint-specific ProblemDetails regression, so its separately authored public Client error contract can drift without a focused failure. |
| VG10-03 | medium | defer | Pre-verified: malformed or empty successful command-status bodies do not exercise the method's `JsonException` translation, leaving the separately authored Client exception abstraction unprotected. |
| VG10-04 | medium | defer | Pre-verified: the interface default checks cancellation, but the legacy-implementation test does not, so a regression can silently ignore caller cancellation in separately authored compatibility code. |
| VG10-05 | medium | defer | Pre-verified: policy composition with several registered policies and exactly one accepter is implemented but untested, so separately authored trusted-extension admission can regress. |
| BH11-01 | high | defer | carried from BH9-08/BH10-08: the gateway still rejects contract-permitted legacy null message identifiers in separately authored Client work; it is not deferred again. |
| BH11-02 | false | reject | The public Client method explicitly accepts the message identifier returned by submission, not the Server's correlation-compatibility lookup key; rejecting a resolved different message ID enforces that narrower correlation contract. |
| BH11-03 | medium | defer | carried from EC9-04/BH10-09: status-only `IsRejected` still misclassifies infrastructure rejection as domain rejection in separately authored Contracts work; it is not deferred again. |
| BH11-04 | medium | defer | carried from BH10-10: a non-rejected response can still carry rejection-only metadata in separately authored Client work; it is not deferred again. |
| BH11-05 | medium | defer | carried from BH8-08/BH9-05/BH10-07: invalid `CommandStatusPath` configuration remains a separately authored Client defect and is not deferred again. |
| BH11-06 | false | reject | The default interface implementation deliberately documents its compatibility behavior as failing closed with no recorded status; distinguishing legacy implementation capability would require a new public result contract, not correction of the stated fallback. |
| BH11-07 | medium | defer | Successful in-flight status reads discard the Server's `Retry-After` header, leaving callers of the separately authored Client surface without its polling cadence. |
| BH11-08 | medium | defer | carried from BH8-12/BH9-07/BH10-11: the reusable fake still ignores status identifiers and records no history; it is not deferred again. |
| BH11-09 | high | defer | carried from BH10-14: case-distinct trusted-extension keys can still collapse after separate policy evaluation in separately authored Server work; it is not deferred again. |
| BH11-10 | medium | defer | The no-op result-payload test's checkpoint scrub assertion is vacuous when the captured checkpoint list is empty, leaving the separately authored AggregateActor persistence proof incomplete. |
| BH11-11 | medium | defer | carried from BH8-13/BH9-09/BH10-12: no actor-to-handler-to-HTTP test transports a sentinel no-op payload; it is not deferred again. |
| BH11-12 | high | defer | carried from EC7-10/BH9-02/BH10-02/VG10-01: the active ruleset still omits `ci / payload-protection`; the owner-only external mutation is not deferred again. |
| BH11-13 | high | patch | The required-lane workflow guard parsed no YAML and accepted spoofable substrings; it now selects the active named test step from the YAML document and rejects job/step bypass conditions. |
| BH11-14 | low | reject | `docs/ci.md` does omit the dedicated workflow, but the omission is documentation-only and correcting the Story 4.15 v3 sealed file requires disproportionate owner-authorized re-sealing for this low-impact finding. |
| BH11-15 | medium | patch | Unlike the earlier external-history claim, the exact current preservation command failed on the Story-owned workflow guard in `ReleasePackageManifestTests`; the replayable check now allows exactly that file while rejecting every other protected path. |
| BH11-16 | medium | patch | The completion evidence now discloses the later Hexalith.Builds advance to `000abf86`; the content-binding table remains intentionally scoped to named Story 8.3 inventories rather than out-of-scope baseline history. |
| VG11-01 | medium | defer | carried from VG10-02: pre-verified endpoint-specific ProblemDetails coverage remains absent from separately authored command-status Client tests; it is not deferred again. |
| VG11-02 | medium | defer | carried from VG10-03: pre-verified malformed-successful-body translation coverage remains absent from separately authored Client tests; it is not deferred again. |
| VG11-03 | medium | defer | carried from VG10-04 for the default implementation; the pre-verified concrete HTTP path also lacks an already-cancelled, no-request regression, so only that new half is deferred. |
| VG11-04 | medium | defer | carried from VG10-05: pre-verified multi-policy/exactly-one-accepter coverage remains absent from separately authored trusted-extension tests; it is not deferred again. |
| VG11-05 | medium | defer | carried from BH9-08/EC9-03/BH10-08: pre-verified legacy-null status handling remains inconsistent in separately authored Client work; it is not deferred again. |
| VG11-06 | medium | defer | carried from EC9-04/BH10-09: pre-verified infrastructure-rejection classification remains incorrect in separately authored Contracts work; it is not deferred again. |
| VG11-07 | medium | defer | carried from BH8-08/BH9-05/BH10-07: pre-verified invalid status-path handling remains a separately authored Client defect; it is not deferred again. |
| EC11-01 | medium | defer | carried from BH9-08/EC9-03/BH10-08: legacy null message identifiers remain rejected in separately authored Client work; it is not deferred again. |
| EC11-02 | low | defer | The compatibility default silently maps null or whitespace message identifiers to no status while the concrete client rejects them, leaving inconsistent argument validation in separately authored public Client code. |
| EC11-03 | medium | defer | carried from BH8-08/BH9-05/EC10-01: invalid `CommandStatusPath` configuration remains a separately authored Client defect; it is not deferred again. |
| EC11-04 | medium | defer | carried from BH10-10: contradictory rejection-only metadata remains accepted for non-rejected statuses in separately authored Client work; it is not deferred again. |
| EC11-05 | false | reject | carried from BH10-15/EC10-05: a throwing policy cannot authorize admission, and the registered global handler converts the failure to bounded ProblemDetails rather than exposing a raw escape. |
| EC11-06 | high | defer | carried from DW-516: the event resolver cannot express denial or revocation; the richer typed lifecycle outcome belongs to Stories 8.5/8.6 and is not deferred again. |
| EC11-07 | high | defer | carried from DW-516: the snapshot resolver has the same later-story lifecycle boundary and is not deferred again. |
| EC11-08 | medium | patch | Malformed JSON exception mapping now rechecks caller cancellation first, and the existing cleanup test proves cancellation wins even when the parser callback simultaneously raises `JsonException`. |
| EC11-09 | medium | patch | `SequenceEntropy` now encodes fill numbers above 255 into each deterministic DEK, and the concurrent 300-fill regression requires every key to be distinct. |
| EC11-10 | high | patch | grouped with BH11-13: the YAML-aware guard binds the active test command and fails closed on job/step bypass conditions. |
| EC11-11 | medium | patch | The completion evidence now discloses the separately authored trusted-extension policy and sanitizer work, so the Story 8.3 preservation claim no longer obscures the intentional colon-key behavior change. |
| BH12-01 | high | defer | carried from BH11-12: the active ruleset still omits the standalone Payload Protection check; the owner-only external mutation remains outside frozen Story 8.3 and is not deferred again. |
| BH12-02 | medium | patch | The owner handoff added by this story names `ci / payload-protection`, but the standalone workflow/job pair emits `Payload Protection / payload-protection`; correct the actionable deferred-work entry without rewriting historical triage rows. |
| BH12-03 | medium | defer | carried from BH11-05: invalid `CommandStatusPath` configuration remains a separately authored Client defect and is not deferred again. |
| BH12-04 | high | defer | carried from BH11-01: the gateway still rejects contract-permitted legacy null message identifiers in separately authored Client work and is not deferred again. |
| BH12-05 | medium | defer | carried from BH11-03: status-only `IsRejected` still misclassifies infrastructure rejection as domain rejection in separately authored Contracts work and is not deferred again. |
| BH12-06 | medium | defer | carried from BH11-04: non-rejected responses can still carry rejection-only metadata in separately authored Client work and are not deferred again. |
| BH12-07 | medium | defer | carried from BH11-07: successful in-flight reads still discard `Retry-After` in separately authored Client work and are not deferred again. |
| BH12-08 | medium | defer | carried from BH11-08: the reusable fake still ignores status identifiers and records no history; it is not deferred again. |
| BH12-09 | high | defer | carried from BH11-09: case-distinct trusted-extension keys can still collapse after policy evaluation in separately authored Server work and are not deferred again. |
| BH12-10 | medium | defer | carried from VG11-04: multi-policy/exactly-one-accepter coverage remains absent from separately authored trusted-extension tests and is not deferred again. |
| BH12-11 | medium | defer | carried from BH11-10: the no-op payload test's checkpoint assertion remains vacuous when no checkpoint is captured; it is not deferred again. |
| BH12-12 | medium | defer | carried from BH11-11: no actor-to-handler-to-HTTP test transports a sentinel no-op payload in separately authored Server work; it is not deferred again. |
| BH12-13 | medium | defer | carried from VG11-01: endpoint-specific ProblemDetails coverage remains absent from separately authored command-status Client tests and is not deferred again. |
| BH12-14 | medium | defer | carried from VG11-02: malformed-successful-body translation coverage remains absent from separately authored Client tests and is not deferred again. |
| BH12-15 | medium | defer | carried from VG11-03: the concrete HTTP path still lacks an already-cancelled, no-request regression in separately authored Client work and is not deferred again. |
| BH12-16 | high | defer | carried from EC11-06/EC11-07 and DW-516: the resolver seams cannot express denial, revocation, or unsupported versions; the richer lifecycle outcome belongs to Stories 8.5/8.6 and is not deferred again. |
| BH12-17 | medium | defer | carried from BH9-11/DW-518: host registration for the new telemetry belongs to later integration stories and is not deferred again. |
| BH12-18 | medium | defer | carried from BH9-12/DW-519: persisted-format downgrade detection belongs to Story 8.4 routing and is not deferred again. |
| BH12-19 | false | reject | carried from BH9-01/BH6-13: the approval packet binds its approval-time requirements evidence; later verification revisions must not be relabeled as human-approved bytes. |
| VG12-01 | medium | defer | Pre-verified: no test asserts the default `CommandStatusPath` route, so separately authored Client defaults can drift while configured-path tests stay green. |
| VG12-02 | medium | defer | Pre-verified: trusted-extension tests do not assert that policies receive the authenticated principal and exact submitted command; this gap belongs to separately authored Server work. |
| VG12-03 | medium | defer | carried from VG10-04: cancellation of the legacy interface default remains untested in separately authored compatibility code and is not deferred again. |
| VG12-04 | medium | defer | carried from VG11-02: malformed command-status JSON translation remains untested in separately authored Client work and is not deferred again. |
| VG12-05 | medium | defer | Pre-verified: no gateway JSON test binds and asserts `RejectionEventType`, so separately authored Client serialization coverage can miss field loss. |
| VG12-06 | high | defer | carried from BH11-01/VG11-05: the concrete gateway still rejects contract-permitted legacy null message identifiers in separately authored Client work and is not deferred again. |
| EC12-01 | medium | defer | carried from EC11-03: invalid `CommandStatusPath` handling remains a separately authored Client defect and is not deferred again. |
| EC12-02 | medium | defer | carried from EC11-04: contradictory rejection-only metadata remains accepted for non-rejected statuses in separately authored Client work and is not deferred again. |
| EC12-03 | high | defer | carried from BH11-09: case-distinct trusted-extension keys can still collapse after policy evaluation in separately authored Server work and are not deferred again. |
| EC12-04 | medium | defer | The REST-generator fake ignores caller cancellation in its separately authored status-read override, so generated-client tests can mask cancellation regressions. |
| EC12-05 | medium | defer | The sample API fake has the same separately authored cancellation omission, so sample tests can mask status-read cancellation regressions. |
| BH13-01 | high | defer | carried from EC7-10/BH12-01: the standalone Payload Protection check remains absent from the active required-check ruleset; that owner-only external mutation is outside Story 8.3 and is not deferred again. |
| BH13-02 | medium | defer | carried from BH9-10/DW-516: the byte-core resolver still cannot express revocation, deletion, denial, or unsupported versions; the richer lifecycle result belongs to Stories 8.5/8.6 and is not deferred again. |
| BH13-03 | medium | defer | carried from BH9-11/DW-518: host registration for the PayloadProtection activity source and meter belongs to later integration work and is not deferred again. |
| BH13-04 | medium | defer | carried from BH9-12/DW-519: persisted-format routing is required to distinguish legitimate plaintext from stripped wrappers and belongs to Story 8.4. |
| BH13-05 | medium | defer | carried from the third-pass chunk-4a deferral: an authority-owned snapshot golden and non-empty external AES-GCM vector require Story 8.2 fixture ownership and are not deferred again. |
| BH13-06 | medium | defer | carried from BH8-08/BH12-03: invalid `CommandStatusPath` configuration remains a separately authored Client defect and is not deferred again. |
| BH13-07 | high | defer | carried from BH9-08/BH12-04: the gateway rejects contract-permitted legacy null message identifiers in separately authored Client work and is not deferred again. |
| BH13-08 | medium | defer | carried from EC9-04/BH12-05: status-only `IsRejected` still misclassifies infrastructure rejection as domain rejection in separately authored Contracts work. |
| BH13-09 | medium | defer | carried from BH10-10/BH12-06: non-rejected responses can carry rejection-only metadata in separately authored Client work and are not deferred again. |
| BH13-10 | medium | defer | carried from BH11-07/BH12-07: successful in-flight status reads discard `Retry-After` in separately authored Client work and are not deferred again. |
| BH13-11 | medium | defer | carried from BH8-12/BH12-08: the reusable fake still ignores status identifiers and records no history; it is not deferred again. |
| BH13-12 | medium | defer | carried from EC12-04/EC12-05: the REST-generator and sample command-status fakes ignore caller cancellation in separately authored tests and are not deferred again. |
| BH13-13 | high | defer | carried from BH10-14/BH12-09: case-distinct trusted-extension keys can collapse after policy evaluation in separately authored Server work and are not deferred again. |
| BH13-14 | medium | defer | carried from BH8-13/BH12-12: no actor-to-handler-to-HTTP test transports a sentinel no-op payload in separately authored Server work. |
| BH13-15 | medium | defer | carried from BH11-10/BH12-11: the no-op payload test's checkpoint assertion remains vacuous when no checkpoint is captured. |
| BH13-16 | low | defer | carried from EC11-02: invalid message-identifier behavior remains inconsistent between the compatibility default and concrete Client implementations. |
| EC13-01 | high | defer | carried from BH11-01/BH12-04: legacy status records with a null message identifier remain unreadable through the separately authored concrete gateway. |
| EC13-02 | medium | defer | carried from BH8-08/BH12-03: null, empty, whitespace, or slash-only `CommandStatusPath` values remain invalid in separately authored Client work. |
| EC13-03 | low | defer | carried from EC11-02: the interface compatibility default and concrete implementations still disagree on invalid message identifiers. |
| EC13-04 | medium | defer | A successful status response whose content stream raises `HttpRequestException` or `IOException` can bypass the gateway exception abstraction because only `JsonException` is translated; this separately authored Client surface is outside Story 8.3. |
| EC13-05 | false | reject | carried from BH10-15/EC10-05: a throwing trusted-extension policy cannot authorize admission, and the registered global handler converts the failure to bounded ProblemDetails rather than exposing an unsafe response. |
| EC13-06 | medium | defer | carried from EC12-04: the REST-generator command-status fake ignores its cancellation token and can mask cancellation regressions. |
| EC13-07 | medium | defer | carried from EC12-05: the sample command-status fake has the same separately authored cancellation omission. |
| EC13-08 | false | reject | carried from BH8-01/EC8-07/BH10-16: the no-op payload behavior change is separately authored and explicitly disclosed in the completion evidence, so Story 8.3 no longer claims it as unchanged owned behavior. |
| VG13-01 | high | defer | carried from EC7-10/BH12-01: the active required-check ruleset omits the standalone Payload Protection job; the owner-only settings action is not deferred again. |
| VG13-02 | medium | defer | carried from BH8-13/BH12-12: a sentinel no-op payload is not transported through a composed actor/router/handler/HTTP test in separately authored Server work. |
| VG13-03 | medium | defer | carried from VG12-01: the default command-status request route remains unpinned in separately authored Client tests. |
| VG13-04 | medium | defer | carried from VG12-05: `RejectionEventType` JSON binding remains unverified through the separately authored gateway client. |
| VG13-05 | medium | defer | carried from VG10-02/VG10-03/BH12-13/BH12-14: status-specific ProblemDetails and malformed/empty successful-body translation remain unverified in separately authored Client tests. |
| VG13-06 | medium | defer | carried from VG10-04/VG11-03/BH12-15: legacy-default and concrete command-status cancellation behavior remains incompletely verified in separately authored Client work. |
| VG13-07 | medium | defer | carried from VG12-02: trusted-extension tests still do not prove policies receive the authenticated principal and exact submitted command. |
| VG13-08 | medium | defer | carried from VG10-05/VG11-04/BH12-10: multi-policy admission with exactly one accepter remains untested in separately authored Server work. |
| VG13-09 | high | defer | carried from BH9-08/BH12-04: the concrete gateway remains incompatible with the contract-permitted legacy null message identifier. |
| VG13-10 | medium | defer | carried from EC9-04/BH12-05: `IsRejected` still cannot distinguish infrastructure rejection from domain rejection. |
| VG13-11 | medium | defer | carried from BH8-08/BH12-03: invalid `CommandStatusPath` values remain a separately authored Client configuration defect. |
| VG13-12 | high | defer | carried from BH10-14/BH12-09: case-distinct trusted-extension keys can still collapse after separate policy evaluation. |
| BH14-01 | false | reject | carried from BH10-16: the baseline comparison intentionally includes unrelated committed history, while the current Story 8.3 working-tree changes are disclosed and do not silently claim ownership of those production changes. |
| BH14-02 | high | defer | carried from EC7-10/BH12-01/BH13-01: the active required-check ruleset still omits the standalone Payload Protection job; that owner-only settings action remains outside Story 8.3 and is not deferred again. |
| BH14-03 | false | reject | carried from BH12-19: `AR-20260914-02` binds the approval-time requirements evidence, not a mutable later verification report, so the later verification digest does not invalidate the recorded approval. |
| BH14-04 | medium | defer | carried from BH9-12/BH13-04/DW-519: persisted-format routing is required to distinguish legitimate plaintext from stripped wrappers and belongs to Story 8.4. |
| BH14-05 | medium | defer | carried from BH9-10/BH13-02/DW-516: the byte-core resolver still cannot express revocation, deletion, denial, or unsupported versions; that lifecycle result belongs to Stories 8.5/8.6 and is not deferred again. |
| BH14-06 | medium | defer | carried from BH9-11/BH13-03/DW-518: host registration for the Payload Protection activity source and meter belongs to later integration work and is not deferred again. |
| BH14-07 | low | reject | carried from BH2-11/VG2-06: V138 is an observational bounded-workload probe by approved design and intentionally records, rather than invents, a performance gate. |
| BH14-08 | medium | defer | carried from BH13-05: an authority-owned snapshot golden requires Story 8.2 fixture ownership and is not deferred again. |
| BH14-09 | medium | defer | carried from BH13-05: a non-empty external AES-GCM vector requires Story 8.2 fixture ownership and is not deferred again. |
| BH14-10 | medium | defer | carried from BH8-08/BH12-03/BH13-06: invalid `CommandStatusPath` configuration remains a separately authored Client defect and is not deferred again. |
| BH14-11 | high | defer | carried from BH9-08/BH12-04/BH13-07: the gateway rejects contract-permitted legacy null message identifiers in separately authored Client work and is not deferred again. |
| BH14-12 | medium | defer | carried from EC9-04/BH12-05/BH13-08: status-only `IsRejected` still misclassifies infrastructure rejection as domain rejection in separately authored Contracts work. |
| BH14-13 | medium | defer | carried from BH10-10/BH12-06/BH13-09: non-rejected responses can carry rejection-only metadata in separately authored Client work and are not deferred again. |
| BH14-14 | medium | defer | carried from EC13-04: a successful status response can leak response-body transport exceptions past the separately authored Client gateway abstraction; the existing deferral is not duplicated. |
| BH14-15 | medium | defer | carried from BH11-07/BH12-07/BH13-10: successful in-flight status reads discard `Retry-After` in separately authored Client work and are not deferred again. |
| BH14-16 | high | defer | carried from BH10-14/BH12-09/BH13-13: case-distinct trusted-extension keys can collapse after policy evaluation in separately authored Server work and are not deferred again. |
| BH14-17 | medium | defer | carried from BH8-12/BH12-08/BH13-11: the reusable fake still ignores status identifiers and records no history; it is not deferred again. |
| BH14-18 | medium | defer | carried from BH11-10/BH12-11/BH13-15: the no-op payload test's checkpoint assertion remains vacuous when no checkpoint is captured. |
| BH14-19 | medium | defer | carried from EC12-04/EC12-05/BH13-12: the REST-generator and sample command-status fakes ignore caller cancellation in separately authored tests and are not deferred again. |
| BH14-20 | false | reject | the cited deferred-work row is an append-only point-in-time record from before the interface gained its compatibility default; later review rows already record the current default-method behavior, so rewriting history is not a code fix. |
| EC14-01 | medium | defer | carried from BH8-08/BH12-03/BH13-06: null, empty, whitespace, or slash-only `CommandStatusPath` values remain invalid in separately authored Client work and are not deferred again. |
| EC14-02 | false | reject | carried from BH10-15/EC10-05/EC13-05: a throwing trusted-extension policy cannot authorize admission, and the registered global handler converts the failure to bounded ProblemDetails. |
| EC14-03 | high | defer | carried from BH10-14/BH12-09/BH13-13: case-distinct trusted-extension keys can collapse after separate policy evaluation in separately authored Server work and are not deferred again. |
| EC14-04 | low | reject | carried from EC8-06: `BoundedJsonDocument` is internal and every production use is `using`-scoped, so adding disposal guards to all accessors is disproportionate for an unreachable normal path. |
| EC14-05 | low | reject | carried from the third-pass diagnostics review: `PayloadProtectionOperation` is internal, has exactly two members, and all callers pass explicit valid constants; guarding undefined future enum values is speculative. |
| EC14-06 | low | reject | carried from BH9-04/EC9-05: a single JSON token is capped by the 16 MiB payload bound and surrounded by cancellation checkpoints; an interruptible token parser would add disproportionate machinery for bounded work. |
| EC14-07 | false | reject | carried from BH8-01/EC8-07/BH10-16/EC13-08: the no-op payload behavior is separately authored and explicitly disclosed in completion evidence, so Story 8.3 does not claim that behavior as unchanged. |
| VG14-01 | medium | defer | pre-verified; carried from VG12-01/VG13-03: the default command-status request route remains unpinned in separately authored Client tests and is not deferred again. |
| VG14-02 | medium | defer | pre-verified; carried from VG10-02/VG10-03/BH12-13/BH12-14/VG13-05: non-404 status-specific ProblemDetails translation remains unverified in separately authored Client tests and is not deferred again. |

## Design Notes

Keep types internal until a frozen later-story seam requires otherwise. V046-V048 use bounded test seams without implementing Story 8.5's durable SPI. V138 records counts, bounds, cancellation, memory and latency without inventing a performance gate. Validate directly because Story 8.8 owns solution/package integration.

## Verification

**Commands:**
- Story 8.1 normative digest plus packet-bound `sha256sum` preflight -- expected: all approved identities match.
- `node scripts/payload-protection/verify-golden-vectors.mjs` and `python3 scripts/payload-protection/verify-golden-vectors.py` -- expected: V001-V003 pass unchanged.
- `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --no-build --minimum-expected-tests 292 --fail-skips on --no-ansi` and the same built project under `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` filtered to `V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams` with a one-test floor -- expected: all core vectors and the inert-normalizer fail-closed path pass with no skip/unrun.
- `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Release -m:1 -nodeReuse:false -p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true` and `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -nodeReuse:false` -- expected: zero warnings/errors.
- Existing release pack and both package validators in a temporary directory -- expected: exactly 14 archives; the new project is excluded.
- `git diff --check` -- expected: no whitespace errors.

**Observed results (2026-09-17):** both independent V001-V003 verifiers passed;
focused Release tests passed 292/292 and the invariant-globalization invocation
passed 1/1 with no skips; code-style verification and
the LF-normalization scan, the AOT/trim-analyzed core Release build, the complete
solution Release build, and `git diff --check` passed; release packing plus both
validators produced exactly 14 archives. The required Contracts packaging-test
class passed 115/115 and binds the dedicated workflow's project, floor, and
fail-skips policy. Stories 8.4/8.5 remain unauthorized pending independent
review and exact approval.

### Review Findings

Independent four-layer code review, 2026-09-14, against baseline
`e8886ec4c277460de3d3208b3fc0b9c261c4967d`..`accada16`, chunk A
(`src/Hexalith.EventStore.PayloadProtection/`, 34 files, 4,423 insertions).
Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor.
Every claim was re-verified at its cited location before a verdict was rendered.

- [x] [Review][Decision] csproj disables the AOT/trim analyzers on frozen Contracts, and the story's zero-warning evidence depends on it — RESOLVED 2026-09-15, applied. `AdditionalProperties="EnableAotAnalyzer=false;EnableTrimAnalyzer=false"` was the repository's only use of those properties, and it existed solely to let one manual command in `verification.md:54` pass; the blocking lane (`ci.yml:135-141`) never passed the flags, so CI had never run these analyzers at all. Measured: removing the override and building with the flags fails with 12 IL2026/IL3050 errors, every one emitted by Contracts' own compilation and none by core source; a control probe (`JsonSerializer.Serialize(object)` injected into the core) does fail the build, proving the core was genuinely analyzed. Because the diagnostics come from a different project's compilation, a core-side `NoWarn`/`IsAotCompatible` could not have suppressed them. Fix applied: `EnableAotAnalyzer`/`EnableTrimAnalyzer` promoted to project properties on the core and the `AdditionalProperties` override removed — project properties do not propagate to project references, which is exactly why the command-line flags leaked into Contracts. The analysis is now permanent and CI-enforced instead of a manual command, and the second-MSBuild-configuration hazard that would have surfaced when Story 8.8 adds this project to `Hexalith.EventStore.slnx` is gone. Verified: core build clean with no flags; control probe still fails on core code; CI-equivalent restore plus `-warnaserror` build of the test project clean; suite 217/217 with zero skips. The documented command dropped the two flags — leaving them would make them global again and reintroduce the 12 errors. Content Binding hashes for the production stream, core project, and verification evidence were recomputed. Contracts' own AOT/trim violations remain open and are tracked as DW-517.

- [x] [Review][Patch] Invalid UTF-8 inside JSON string values is never rejected, by writer or reader [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:494] — RESOLVED 2026-09-15: strict validation now covers complete input and authenticated plaintext bytes, with writer and reader regressions.
- [x] [Review][Patch] `ProtectSnapshot` is the only entry point with no `OverflowException` to typed-failure mapping [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:353] — RESOLVED 2026-09-15: overflow maps to the bounded format exception used by event protection.
- [x] [Review][Patch] The production entropy implementation has no caller and no test, and mutations to it survive the suite [src/Hexalith.EventStore.PayloadProtection/CryptographicPayloadProtectionEntropy.cs:9] — RESOLVED 2026-09-15: the generator defaults to production entropy and a regression proves fresh references and nonzero distinct keys.
- [x] [Review][Patch] `JsonPointer.Escape` has no production caller; the V031-tagged escaping test asserts it instead of the production escaper [src/Hexalith.EventStore.PayloadProtection/JsonPointer.cs:14] — RESOLVED 2026-09-15: the dead helper/test were removed and V031 now round-trips escaped source names through the complete core.
- [x] [Review][Patch] Five new internal records drop the Story 8.2 no-leak `ToString()` override [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionContext.cs:13] — RESOLVED 2026-09-15: each sensitive record returns only its type name and diagnostics tests enforce the boundary.
- [x] [Review][Patch] The escaped-member-name decode path has zero test coverage and survives injected defects [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:889] — RESOLVED 2026-09-15: five production round trips cover tilde, slash, quote, backslash, and surrogate-pair spellings.
- [x] [Review][Patch] The snapshot non-zero `FieldOrdinal` pre-lookup guard has zero test coverage [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:724] — RESOLVED 2026-09-15: a snapshot regression requires local consistency rejection with zero resolver calls.
- [x] [Review][Patch] The `keyResolver` DEK buffer-ownership transfer is undocumented at the seam [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:402] — RESOLVED 2026-09-15: both unprotect seams document transfer and clearing on every exit.
- [x] [Review][Patch] Envelope wire offsets are duplicated as magic numbers across two files [src/Hexalith.EventStore.PayloadProtection/EnvelopeCodec.cs:21] — RESOLVED 2026-09-15: `PayloadProtectionWireFormat` centralizes all HXP2 identifiers, offsets, and sizes.
- [x] [Review][Patch] The `json+pdenc-v2` / `json` format literals are repeated at six sites with no shared constant [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:212] — RESOLVED 2026-09-15: the same wire-format authority now owns both format constants and encoded protected-format bytes.
- [x] [Review][Patch] Snapshot unprotect copies decrypted plaintext instead of transferring ownership, doubling peak residency [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:808] — RESOLVED 2026-09-15: success transfers the owned buffer directly; failure still clears it.
- [x] [Review][Patch] Three guards cannot fire by construction and give false assurance [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:147] — RESOLVED 2026-09-15: all three unreachable checks were removed.
- [x] [Review][Patch] Key-generation collision exhaustion is reported as a format failure and classified `Malformed` [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionMaterialGenerator.cs:60] — RESOLVED 2026-09-15: exhaustion is now a cryptographic failure with a focused assertion.
- [x] [Review][Patch] `ValidateMaterial` null-checks a non-nullable parameter its caller reaches via `material!` [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:898] — RESOLVED 2026-09-15: nullable factory flow is explicit while ownership is retained before validation so invalid returned DEKs are cleared.

- [x] [Review][Defer] The key resolver cannot signal revocation, denial, or unsupported version, leaving five `UnreadableProtectedDataReason` members unreachable [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:402] — deferred: real, but the resolver seam's richness is assigned to Story 8.5 (Policy And Key-Lifecycle Mechanics) and Story 8.6 (provider adapter); not constructible at the Story 8.3 byte-core boundary. Tracked as DW-516.

#### Rejected

False — the claimed bad outcome does not occur:

- Neither new project is registered in `Hexalith.EventStore.slnx` (3 layers) — mandated, not omitted: the frozen spec requires the lane "without adding either project to `Hexalith.EventStore.slnx`", lists the frozen `.slnx` in every KEEP set, and assigns solution/release integration to Story 8.8. `.github/workflows/ci.yml:113-163` builds both with `-warnaserror` and runs the suite as a blocking lane.
- Ordinal-derived nonces plus an undocumented DEK-freshness obligation risk AES-GCM nonce reuse (2 layers) — refuted by the frozen authority: §8.1 specifies "fresh for each event or snapshot protection write" and nonce construction ID `01`; §8.2 carries the uniqueness argument and forbids DEK reuse across events, retries, and migrations. `PayloadProtectionMaterial` documents "fresh per-payload cryptographic material", and `PayloadProtectionMaterialGenerator` produces a new DEK and ULID per call with collision retry. Cross-call reuse detection is impossible in a stateless per-call core by design.
- `EnvelopeCodec.Read` should validate the nonce invariant at parse time — would break the frozen requirement. The spec mandates "post-auth nonce/ordinal validation" to retain V010 authenticated-mismatch semantics; the story's own BH-14 patch moved this check after `Decrypt` deliberately.
- Pass-through protect rejects payloads that legitimately contain a `$pdenc` member — `$pdenc` is the authority's reserved wrapper marker and the spec requires residual and emergent marker rejection. Fail-closed on a reserved name is the specified behavior.
- The non-protected JSON skeleton is not bound into the AAD, so tampered unprotected fields authenticate — the AAD is the frozen 11-field §7.1 structure; adding a skeleton digest would change the frozen wire format, which the spec forbids. Whole-record integrity is not this engine's contract.
- The key resolver has no timeout, so unprotect can hang — the core passes the caller's `CancellationToken` through; timeout policy is caller-owned and, for lifecycle, Story 8.5-owned.
- A null `JsonReplacement.Value` escapes as a `NullReferenceException` — not constructible: `JsonReplacement` is internal and built only inside `PayloadProtectionCore` from non-null wrapper and plaintext buffers.
- `BoundedJsonLookupKey` truncates SHA-256 and the reader may resolve the wrong member — collision handling exists and is consulted: `BoundedJsonDocument.cs:17,156,747-777` maintains and reads a `Dictionary<BoundedJsonLookupKey, List<int>>`.
- The csproj omits `GenerateDocumentationFile` — matches repository convention; XML documentation is generated only when `ApiReferenceBuild=true` on packable projects, and this project is non-packable.
- `PayloadProtectionMaterialGenerator` hard-codes `DekVersion` to 1 — §8.3 assigns DEK/KEK version semantics to lifecycle, which is Story 8.5-owned; the generator is an authorized V046-V048 seam.

Low — real but not worth the fix:

- No `ObjectDisposedException` guard on `BoundedJsonDocument` accessors — the type is internal and every use is `using`-scoped inside the core; the fix adds guards to every accessor.
- No all-zero-DEK rejection in `ValidateMaterial` — only reachable through a defective internal entropy seam whose sole production implementation uses `RandomNumberGenerator.Fill`.
- `PayloadProtectionDiagnostics` switches on `PayloadProtectionOperation` without a default throw — the enum has exactly two members; speculative future-proofing.
- `Base64UrlCodec.Encode` leaves up to four unzeroed managed strings — its only inputs are complete envelopes, which are the persisted public artifact; no plaintext is involved.
- `AadCodec` and `EnvelopeCodec` zero their staging buffers without notifying `ISensitiveBufferObserver` — the spec mandates zeroing, which they do; observability is not required, and threading the observer changes several static signatures.
- `PayloadCryptography.HasExpectedNonce` lacks an `ArgumentNullException.ThrowIfNull` guard — both call sites pass an envelope already validated non-null by `Decrypt`.
- `PayloadProtectionContext` duplicates three of the four fields of the frozen `PayloadProtectionOccurrenceContext` rather than composing it — an internal superset that adds `AggregateIdentity`; noted for Story 8.7, which will need an adapter either way.
- `TryUnprotectEventAsync` re-resolves each wrapper by path solely to read its depth — a bounded per-wrapper cost; carrying depth on `ProtectedWrapper` is more than a direct correction.

### Review Findings (second pass, 2026-09-15)

Independent four-layer code review, 2026-09-15, against baseline
`e8886ec4c277460de3d3208b3fc0b9c261c4967d`..`5347899c`, chunk A
(`src/Hexalith.EventStore.PayloadProtection/`, 35 files, 4,597 insertions).
This range extends the 2026-09-14 pass by one commit (`accada16..5347899c`), so
every finding below was additionally triaged against the first pass's recorded
verdicts; anything that pass already resolved, deferred, or refuted is listed in
this section's Rejected appendix rather than re-raised. Layers: Blind Hunter,
Edge Case Hunter, Verification Gap, Acceptance Auditor. Every claim was
re-verified at its cited location before a verdict was rendered.

- [x] [Review][Decision] Registry V004 requires `AUTH-MM` for ordinal-dependent AAD, but the core rejects an ordinal mutation locally, and `AR-20260914-02` grants that only for V016 — `spec-shared-payload-protection-engine.md:1939` reads `V004|g001-header-bit-matrix|flip each bit independently at envelope offsets 0 through 27|LOCAL-MM for closed grammar fields and AUTH-MM for ordinal-dependent AAD`. The field ordinal occupies envelope offsets 16-19, inside that range, and is AAD-bound. `PayloadProtectionCore.cs:489` rejects `wrapper.Envelope.FieldOrdinal != index` with `BytesMetadataMismatch` before `keyResolver` is reached at `:521`, so a low-bit flip of the ordinal on the single-wrapper G-001 fixture produces a local mismatch with zero resolver calls. The reapproved amendment extends pre-lookup local rejection to V016 only ("an ordinal-only substitution is rejected locally before lookup because manifest/ordinal consistency is mandatory") and never names V004. Either the approval packet's scope is short by one identifier or the engine deviates from a frozen registry expectation the story claims to implement exactly. Resolving it means amending a human-owned approval packet or changing authenticated-read ordering; neither is patchable without your intent. **RESOLVED 2026-09-15 (Option 1): amend the packet.** V004's bit-flip at offsets 16-19 and V016's ordinal substitution are the identical byte mutation on the single-wrapper G-001 fixture, so the two rows cannot hold opposite expectations; the V016 reading is the later human-approved one. The coverage argument for changing the read path is refuted: V025 (`AadPathTests.cs:193-217`) already proves the ordinal reaches the AAD by asserting `PayloadProtectionAuthenticationException` at ordinal 1 while ordinal 0 decrypts, so the pre-lookup check masks nothing unproven. Authority `:1907` also requires zero Key Vault/state lookups for invalid local grammar, which local rejection satisfies. Action carried as a patch below; no code or test change is required.
- [x] [Review][Decision] Authority section 8.4 (durable metadata allowlist and `Unprotected()` pairing) falls inside the "sections 5-8 exactly" mandate but is unimplemented, uncarved, and unrecorded — no file in the diff references `EventStorePayloadProtectionMetadata`, `EventStorePayloadProtectionMetadataCarrier`, or `Unprotected()`. `CoreProtectionResult` carries `(PayloadBytes, SerializationFormat, ProtectedPathCount)` and `CoreUnprotectionResult` carries `(PayloadBytes, UnreadableReason)`; neither surfaces the section 8.4 `Scheme`/`CompatibilityFlags`/`ContentHint` allowlist or post-unprotect metadata. Sections 5.2 and 5.3 are explicitly carved out by the frozen "Never" list, but 8.4 has no carve-out and no Review Triage Log row. It is arguably unreachable without the Server work the story forbids, which makes the "Implement sections 5-8 exactly" claim overbroad rather than met. Decide whether 8.4 is core-owned (implement), Server-owned (carve out in the frozen Never list), or deferred (record it). **RESOLVED 2026-09-15: Server-owned, carve out.** Section 8.4 joins sections 5.2 and 5.3 in the frozen Never list for the same reason they are there -- it is unreachable without the Server integration this story forbids. This narrows the "sections 5-8 exactly" claim to what was actually built. Action carried as a patch below.

- [x] [Review][Patch] From D1: add V004 to the `AR-20260914-02` amendment list beside V016, reusing the granted wording, and mirror the bullet into the frozen Reapproved Story 8.3 Constructibility Amendments section -- RESOLVED 2026-09-15: explicitly authorized by the human EventStore owner in the current interactive BMad build session. [_bmad-output/implementation-artifacts/evidence/story-8-3/requirements-amendment-approval.md:1]
- [x] [Review][Patch] From D2: add authority section 8.4 to the frozen Boundaries "Never" list as Server-owned, beside the existing 5.2/5.3 carve-outs -- RESOLVED 2026-09-15: explicitly authorized by the human EventStore owner in the current interactive BMad build session. [_bmad-output/implementation-artifacts/spec-8-3-pdenc-v2-core-cryptographic-engine.md:27]
- [x] [Review][Patch] Snapshot AAD, root-path manifest, and snapshot envelope have no golden-byte vector; every snapshot test round-trips through the writer that produced them [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:161] — RESOLVED 2026-09-15: pinned an independently calculated root-manifest, HXAD, HXP2 envelope, and base64url snapshot vector before retaining the round-trip assertion.
- [x] [Review][Patch] The plaintext-to-ordinal pairing depends on an unasserted, undocumented sort-stability invariant [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:154] — RESOLVED 2026-09-15: asserted the canonical non-null path sequence against the rebuilt manifest before material creation and expanded V040 across differently ordered requests with three explicit ordinals.
- [x] [Review][Patch] Zero-length and whitespace-only payload bytes are never fed to the parser, and removing the zero-token disjunct escapes an unclosed exception type [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:603] — RESOLVED 2026-09-15: added writer, reader, parser, and zero-external-work coverage for both inputs.
- [x] [Review][Patch] The task "cite the digest and normative sections in material files" is checked, but 20 of 34 source files cite neither and the digest appears in exactly one [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:1] — RESOLVED 2026-09-15: all 34 production C# files now cite the exact normative digest and applicable sections.
- [x] [Review][Patch] The two-character `\/` escape arm in protected member names is never executed by the suite [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:904] — RESOLVED 2026-09-15: added a complete protected-member path assertion for `a\/b`.
- [x] [Review][Patch] HXAD and HXPM constants stayed inline after the first pass centralized only the HXP2 envelope, and the key-reference length is hardcoded as `26` beside the constant that means it [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:157] — RESOLVED 2026-09-15: centralized both magics, both schema versions, the HXAD field count, and the canonical key-reference character count.
- [x] [Review][Patch] The wrapper-count upper bound cannot fire: `ReadProtectedWrappers` already caps the collection at the same limit [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:449] — RESOLVED 2026-09-15: removed the dead duplicate upper-bound arm while preserving the required non-empty check.
- [x] [Review][Patch] `ValidateBeforeMaterial` passes `context.PayloadKind` as its own expected kind, making the payload-kind guard a tautology [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:88] — RESOLVED 2026-09-15: derive the expected kind from root/non-root path shape and test both mismatches.
- [x] [Review][Patch] `PayloadProtectionWireFormat` computes a compile-time constant with a runtime `Encoding.UTF8.GetBytes` and declares the field above the constants it reads [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionWireFormat.cs:10] — RESOLVED 2026-09-15: replaced the allocation-backed field with a UTF-8 literal span.
- [x] [Review][Patch] `format_version` is tagged `v2` on the V041 pass-through protect that returns format `json` [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionDiagnostics.cs:65] — RESOLVED 2026-09-15: successful pass-through operations emit `format_version=none`; protected and failure paths retain `v2`, with exact measurement coverage.
- [x] [Review][Patch] `ProtectSnapshot` records `Success` before constructing its result; `ProtectEvent` deliberately does the reverse [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:350] — RESOLVED 2026-09-15: construct the snapshot result before changing the diagnostic outcome.
- [x] [Review][Patch] `CanonicalUlid.IsValid` bounds a UTF-16 character count with `KeyReferenceBytes` [src/Hexalith.EventStore.PayloadProtection/CanonicalUlid.cs:279] — RESOLVED 2026-09-15: introduced and consumed a semantic key-reference character-count constant.
- [x] [Review][Patch] The 32-character Crockford alphabet is duplicated verbatim in two files with no owner [src/Hexalith.EventStore.PayloadProtection/CryptographicPayloadProtectionEntropy.cs:305] — RESOLVED 2026-09-15: centralized the alphabet with the wire-format constants and reused it for generation and validation.

- [x] [Review][Defer] The pdenc-v2 activity source and meter are never registered, so every span and metric the core emits is dropped [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionDiagnostics.cs:12] — deferred: correct for Story 8.3, whose frozen boundary forbids Server and host integration; registration belongs to Story 8.7 or 8.8. Tracked as DW-518.
- [x] [Review][Defer] A legitimately unprotected `json` event payload is indistinguishable from one whose wrappers were stripped [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:449] — deferred: separating them requires the persisted-format reader-routing parameter that authority section 12.1 defines and Story 8.4 owns. Tracked as DW-519.

#### Rejected (second pass)

Already dispositioned by the 2026-09-14 pass and re-raised unchanged; the first pass's verdict stands and was re-verified against current source:

- Neither new project appears in `Hexalith.EventStore.slnx` (3 layers) — `false`. Mandated by the frozen spec, and the residual worry that the engine's `-warnaserror` coverage hangs on a `Hexalith.EventStore.Contracts.Tests` project reference is refuted: `.github/workflows/ci.yml:113-163` builds both projects directly with `-warnaserror` in a dedicated blocking lane.
- `EnvelopeCodec.Read` should apply the ordinal-nonce check that `ValidateFields` applies on write (2 layers) — `false`. Would break V010: `spec-shared-payload-protection-engine.md:1945` requires `AUTH-MM` for every nonce-bit flip, and Review Re-derivation Requirements mandate post-auth nonce/ordinal validation. The companion claim that `HasExpectedNonce` can take a null `Nonce` is also refuted — every envelope reaching it comes from `EnvelopeCodec.Read`, which builds `Nonce` via `.ToArray()`.
- A caller-supplied `materialFactory` can return duplicate or all-zero key material (2 findings) — `false`/`low`. Refuted by authority sections 8.1-8.2 and by `PayloadProtectionMaterialGenerator`, which mints a fresh DEK and ULID per call behind a collision predicate; the proposed `_usedKeyReferences` guard is per-core-instance and would not prevent reuse across instances or processes.
- The key resolver has no timeout — `false`. The core forwards the caller's `CancellationToken`; deadline policy is caller-owned and, for lifecycle, Story 8.5-owned.
- Every non-cancellation resolver fault is classified `ProviderUnavailable`, leaving `KeyInvalidatedOrDeleted` and `ProviderDenied` unreachable (3 layers) — real and confirmed at `PayloadProtectionCore.cs:536-541` and `:783-787`, but already deferred as DW-516 with the same evidence; not a new finding.
- `Base64UrlCodec.Encode` leaves up to four unzeroed managed strings — `low`, already rejected: its only inputs are complete envelopes, which are the persisted public artifact, so no plaintext is involved.
- `TryUnprotectEventAsync` re-resolves each wrapper by path solely to read its depth — `low`, already rejected as more than a direct correction. Re-confirmed present at `PayloadProtectionCore.cs:589`.
- `BoundedJsonLookupKey` truncates SHA-256 and the reader may resolve the wrong member — `false`. `BoundedJsonDocument` maintains and consults a collision list and a `PropertyNameEquals` fallback; the code never trusts the key alone. The overstatement is in a commit message, not the source.
- `BoundedJsonDocument` has no finalizer and `_disposed` is not thread-safe — `low`. Every construction is `using`-scoped inside the core, and a finalizer on a plaintext holder extends object lifetime rather than shortening it.

New in this pass and refuted on verification:

- A selected path resolving to JSON null is silently dropped from the manifest — `false`. Exactly the specified behavior: `spec-shared-payload-protection-engine.md:367` ("Null values are not protected and do not consume an ordinal"), `:1098` ("Null values are skipped"), and V041 at `:1976` ("valid payload with zero selected non-null values|PASS original bytes and format with no key or AES call").
- The core zeroes a DEK the resolver may have returned from a cache — `false`. The seam documents the contract: `PayloadProtectionCore.cs:412-415`, "Ownership of every returned non-null mutable DEK transfers to the core, which clears it on every exit." The first pass added this.
- Two zero-length replacements sharing a `Start` slip past the overlap check — `false`. The gap exists in isolation (`Rewrite` tests `Start < previousEnd` and `Length < 0`, not `Length < 1`), but is unreachable: every `JsonReplacement` is built at `PayloadProtectionCore.cs:179` from a resolved node whose raw JSON value is at least one byte, and duplicate and ancestor/descendant paths are rejected before any node resolves, so no two replacements share a `Start`.
- `ProtectedPathManifestCodec.Create(..., allowEmpty: true)` can build a `count: 0` HXPM manifest that section 7.2 forbids — `low`. Confirmed at `ProtectedPathManifestCodec.cs:103`, but those bytes are never committed to AAD; the zero-path manifest is only a validation vehicle for the pass-through branch, and removing the shape is more than a direct correction.
- The 110 `PayloadProtectionFormatException` sites carry no bounded reason code — `low`. Local-mismatch opacity is required by authority section 12 and enforced by `CoreFailureSurfaces_UseOnlyClosedMessagesAndReasonsAsync`. Section 10.8 does authorize a closed `reason_code` metric tag the core never emits, but choosing that vocabulary is a diagnostic-surface change, not a direct correction.

### Review Findings (third pass, 2026-09-15)

Chunk 1 of 6 (codec and wire-format group): `AadCodec`, `Base64UrlCodec`,
`CanonicalText`, `CanonicalUlid`, `EnvelopeCodec`, `JsonPointer`,
`PayloadProtectionWireFormat`, `ProtectedPathManifestCodec`,
`ProtectedPathManifest`, `ProtectedWrapper`, `BoundedJsonLookupKey`,
`JsonReplacement`. Reviewed at `dfb01196`, source inventory
`556066e7569ba4581129c4c5b8e8bbe45ddf9f200d26d590fda475c3ce51ad0b`.
Four independent layers; all reported. Suite re-executed during triage: 252/252,
zero skips.

- [x] [Review][Decision] PayloadProtection suite has no blocking CI execution — Three independent verified causes: the project is absent from `Hexalith.EventStore.slnx`, so the `-warnaserror` solution build never compiles it; `.github/workflows/ci.yml` passes an explicit `unit-test-projects` list that omits it and the reusable `domain-ci.yml` has no discovery step; and `advisory-tests.yml:26` names it but is `continue-on-error: true`, builds only the solution that excludes it, and passes VSTest flags while `global.json` pins `Microsoft.Testing.Platform` (reproduced locally: `Zero tests ran`, exit 5). Commit `7d6402c1` moved the lane out of `ci.yml` on the stated premise of restoring the sealed OQ8 v3 hash; that premise is false twice over — the result is `0abf0556…` not the sealed `6a28bd96…`, and `tools/validate-oq8-platform-evidence.py:3360` enforces that entry via `sha256_git_file(COMPLETED_V1_CLOSURE_COMMIT, …)` against frozen commit `17e47a39`, never the live file. This falsifies the checked Execution task and the `verification.md` row "both direct lanes require 252 tests and fail on skips". Decision needed because the frozen requirements forbid adding either project to `.slnx`, so the fix must be a direct-project lane mirroring `scripts/ci-local.sh:132`, and because it reverses an owner commit. — RESOLVED 2026-09-15: owner chose the direct-project lane. `.github/workflows/ci.yml` gained a blocking `payload-protection` job (restore, `-warnaserror` build, `--minimum-expected-tests 254 --fail-skips on`) mirroring `scripts/ci-local.sh`; `.slnx` and the 14-package inventory untouched; `actionlint` passes. The `advisory-tests.yml` VSTest/MTP flag defect is left to its owner.
- [x] [Review][Decision] Canonical NFC rejection fails open under globalization-invariant mode — `CanonicalText.GetByteCount` gates non-NFC input solely on `value.IsNormalized(NormalizationForm.FormC)`. Under `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` that API returns `true` unconditionally (probed directly on .NET 10; it does not throw), so decomposed spellings are accepted into durable AAD identity fields. Reproduced: running the suite with that switch fails `AadPathTests.V029_UnicodeCanonicalization_IsRejectNotNormalize(value: "Te\u0301", valid: False)` — 251/252. Authority section 7.1 requires rejection "rather than normalizing, because normalization could alias a durable identity", and V029 requires PROTECT-REJECT. The repository sets no `InvariantGlobalization` today, so nothing ships broken, but a consuming host can disarm the guard with a supported configuration and no test would catch it. Decision needed on the failure mode: fail fast at first use, probe-and-throw per call, or an ICU-free NFC check. — RESOLVED 2026-09-15: owner chose fail-fast. `CanonicalText` probes the normalizer once (U+00C5 must not report as Form D) and throws `PayloadProtectionCryptographicException` on every call when it is inert, matching the `AesGcm.IsSupported` precedent. Verified: 254/254 with ICU, fails closed under `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
- [x] [Review][Decision] Codec-internal sensitive buffers are unobservable through the `ISensitiveBufferObserver` seam — AC3 requires that "every engine-owned sensitive buffer is observed zeroed". `AadCodec`, `Base64UrlCodec`, `EnvelopeCodec`, and `ProtectedPathManifestCodec` zero with bare `CryptographicOperations.ZeroMemory`/`Array.Clear`; none takes an observer (only `PayloadProtectionCore`, `PayloadCryptography`, `BoundedJsonDocument`, `PayloadProtectionMaterialGenerator` reference the interface). The buffers are zeroed, so there is no runtime leak — but `AadCodec.Write`'s six identity arrays and `Base64UrlCodec`'s staging buffers (up to ~1.05 MB and ~1.4 M chars per wrapper) fall outside the observed evidence. Decision needed: thread an observer through four internal codecs, or record that AC3's "observed" is satisfied by surviving buffers only. — RESOLVED 2026-09-15: owner chose to record the scope rather than add plumbing. `evidence/story-8-3/verification.md` now states that AC3's "observed" covers buffers surviving a codec call; call-frame-internal staging is zeroed unconditionally but has no reachable observer. Each codec now documents the obligation at its return seam.
- [x] [Review][Patch] `EnvelopeCodec.Write` nonce-derivation guard is unarmed — mutation-confirmed [src/Hexalith.EventStore.PayloadProtection/EnvelopeCodec.cs:177]
- [x] [Review][Patch] Closed pdenc-v2 wire constants are not single-sourced in `PayloadProtectionWireFormat` [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionWireFormat.cs:30-103, AadCodec.cs:111-188]
- [x] [Review][Patch] Manifest checkpoint callbacks are multiplexed and `ValidateOverlap`'s sort uses `checkpoint` instead of `sortCheckpoint` [src/Hexalith.EventStore.PayloadProtection/ProtectedPathManifestCodec.cs:156,365-369]
- [x] [Review][Patch] `JsonReplacement` and `BoundedJsonLookupKey` omit the no-leak `ToString()` override their sibling records carry [src/Hexalith.EventStore.PayloadProtection/JsonReplacement.cs:10, BoundedJsonLookupKey.cs:10]
- [x] [Review][Patch] Snapshot AAD property-path bound is expressed as 0..2048 rather than the frozen "exactly zero bytes" [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:104,156]
- [x] [Review][Patch] `Base64UrlCodec.Encode` allocates four intermediate strings where one `string.Create` suffices [src/Hexalith.EventStore.PayloadProtection/Base64UrlCodec.cs:31]
- [x] [Review][Patch] Codec return seams do not document the caller's buffer-ownership and zeroing obligation [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:120, EnvelopeCodec.cs:65, Base64UrlCodec.cs:33, ProtectedPathManifestCodec.cs:13]
- [x] [Review][Patch] `V030_AadTotalLength_IsBoundedAfterEveryFieldBound` name overstates what it proves — its 2,049-byte case is rejected by the field bound, never the total-length check [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:353]
- [x] [Review][Patch] Surrogate UTF-8 byte accounting for cancellation cadence is unexercised — no test places a non-BMP member name in a pointer [src/Hexalith.EventStore.PayloadProtection/JsonPointer.cs:105-113]
- [x] [Review][Patch] Dead and inconsistent residue in the codec group: unreachable `value is null` after `GetByteCount` already threw; unreachable `result < 0` under `NumberStyles.None`; `CryptographicOperations` fully qualified four times where siblings use a `using` [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:229, JsonPointer.cs:123, EnvelopeCodec.cs:60]

#### Rejected (third pass)

- `IsNormalized` throws `PlatformNotSupportedException` under invariant globalization, escaping the `ArgumentException` catch — `false`. Probed directly on .NET 10 with `InvariantGlobalization=true`: it returns `True` and does not throw. The real defect is the inverse fail-open, filed above as a decision item.
- C1 controls U+0080-U+009F, U+FEFF and U+2028/U+2029 pass the control-character filter — `false`. Authority section 7.1 closes exactly "unpaired surrogates, U+0000..U+001F, or U+007F"; `CanonicalText` implements that set precisely. Widening it would change accept/reject on a frozen wire contract.
- A length divergence between `AadCodec.Validate` and `AadCodec.Write` escapes as `ArgumentOutOfRangeException` from `WriteField` — `false`. Unreachable: both paths size every field from the same immutable `PayloadProtectionContext` with identical bounds (`snapshot` agrees because `ValidateContext` enforces kind ⟺ path-emptiness), and `keyReference` is pinned to exactly 26 ASCII bytes by `CanonicalUlid.IsValid` plus `Encode(26, 26)`.
- `CanonicalText` has no test coverage and no negative vectors — `false`. V029 exercises NFC rejection and every control boundary through it at `AadPathTests.cs:310-338`; `ValidateUtf8` is reached from `BoundedJsonDocument.cs:519`; the span `Decode` overload is reached from `EnvelopeCodec.Read`.
- `JsonPointer.ParseArrayIndex` yields `NullReferenceException` for a null segment — `false`. Its only caller, `BoundedJsonDocument.cs:188`, passes tokens produced by `JsonPointer.Decode`, which builds every token with `StringBuilder.ToString()` and never returns null.
- `Base64UrlCodec.Decode("")` succeeds and returns an empty array — `low`. True, but `EnvelopeCodec.Read` rejects anything below `MinimumEnvelopeBytes` (83) immediately after, so no empty decode reaches any consumer; adding a second bound guards state already rejected.
- `ProtectedPathManifest`/`JsonReplacement` expose mutable arrays and inherit reference-based record equality — `low`. The mutability is deliberate: `ClearManifest` and `ClearEnvelope` zero those very arrays. No site compares either record for equality, so the reference-equality trap has no caller to mislead.
- `Base64UrlCodec` performs up to five uncancellable passes over a 1.4 M-character carrier — `low`. The design bounds first and then works: `Decode` rejects anything over `EnvelopeTextCharacters` before scanning, so the residual work is a few milliseconds over at most 1.4 MB. Threading a token through would add parameters for no reachable benefit.
- `ProtectedPathManifestCodec.Create(allowEmpty: true)` can emit a count-0 HXPM manifest outside section 7.2's "count is 1..4096" — `low`, and a repeat of the second-pass disposition at `ProtectedPathManifestCodec.cs:103`. The zero-path manifest is only a validation vehicle; `PayloadProtectionCore.cs:56` short-circuits to pass-through before those bytes reach AAD.
- Manifest path values are retained as un-zeroable managed strings despite the cleanup list naming "manifest paths" — `low`. Paths are not plaintext under section 15.1, and the string representation is systemic across `ProtectedWrapper`, `PayloadProtectionEnvelope` and `PayloadProtectionMaterial`; converting is a redesign, not a correction.
- `PayloadProtectionFormatException` carries no bounded reason code — `low`, and a repeat of the second-pass disposition. Local-mismatch opacity is what authority section 12 requires here; the closed reason vocabulary is Story 8.4's routing scope.
- The spec recorded "focused Release tests passed 250/250" after the suite had
  advanced — resolved in the current implementation handoff by rebinding the
  command and observed result to the verified 254/254 suite.

### Review Findings (third pass, chunk 2 of 6, 2026-09-16)

- [x] [Review][Patch] Parser-frame disposal allocates before clearing retained decoded member names [src/Hexalith.EventStore.PayloadProtection/JsonContainerFrame.cs:126] — RESOLVED 2026-09-16: disposal now enumerates the dictionary directly instead of allocating its values collection first; a warmed allocation regression requires zero disposal-thread allocations.
- [x] [Review][Patch] Failed parse and cancellation exits do not verify owned input-snapshot cleanup [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:642] — RESOLVED 2026-09-16: malformed and checkpoint-cancelled parses both observe the owned snapshot only after zeroing and preserve caller bytes.
- [x] [Review][Patch] Rewrite cancellation does not verify abandoned-output cleanup [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:444] — RESOLVED 2026-09-16: cancellation after output allocation now has direct zeroing-observer evidence for `AbandonedOutput`.
- [x] [Review][Patch] A malformed later wrapper does not verify cleanup of previously parsed envelope buffers [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:334] — RESOLVED 2026-09-16: a valid first wrapper followed by a malformed second carrier proves all three retained envelope fields and both decode buffers are cleared.
- [x] [Review][Patch] Discovered-wrapper path exact/max-plus-one boundaries are unverified [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:808] — RESOLVED 2026-09-16: complete wrapper discovery accepts an exact 2,048-byte pointer and rejects the 2,049-byte form while clearing decoded envelope state.
- [x] [Review][Patch] An over-limit unselected member path lacks a complete-core preservation regression [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:519] — RESOLVED 2026-09-16: a selected sibling protects and authenticates while the over-limit unselected member round-trips byte for byte.
- [x] [Review][Patch] Literal multibyte UTF-8 member-name discovery lacks a complete reader round trip [src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs:870] — RESOLVED 2026-09-16: a literal NFC multibyte member is protected, discovered, authenticated, and restored through the complete core.

#### Rejected (third pass, chunk 2)

- [Rejected][low] Full-input UTF-8 validation and a single maximum-size JSON token can delay cancellation — the work is capped by the 16 MiB payload ceiling, and replacing framework parsing for intra-token checkpoints is disproportionate.
- [Rejected][low] `JsonContainerFrame.SetProperty` has no cancellation seam inside one property copy/hash/compare — the token is payload-bounded, node checkpoints remain enforced, and an incremental hashing/copy redesign is disproportionate.
- [Rejected][low] `CopyPayload` performs one uncancellable payload clone — it checks cancellation immediately before and after the fixed 16 MiB maximum copy, so chunking adds complexity for bounded latency.
- [Rejected][false] `CopyRawValue` is uncancellable and has unclear ownership — production callers check cancellation before the selected-value copy, cap it at 1 MiB, retain the returned owned array, and clear it in the core cleanup path.
- [Rejected][low] Wrapper decoding happens before an over-limit discovered path is rejected — decoding is capped near 1.4 MiB, cleared in `finally`, and reordering merely trades one bounded allocation for another without changing safety.
- [Rejected][false] Wrapper discovery rescans documents known to contain no protected member — the proportional scan is bounded to 65,536 indexed nodes, checks cancellation every 256 children/nodes, and violates no acceptance constraint.
- [Rejected][low] Discovered property paths scan their raw token before decoding it — both passes are bounded and cancellation-aware; retaining extra parser state or replacing the decoder is not justified for this bounded case.
- [Rejected][false] Mutable `BoundedJsonNode` instances can corrupt the index after parsing — all mutation sites are confined to index construction, and no current caller mutates an exposed node.
- [Rejected][false] Buffer-ownership contracts are absent across the bounded JSON API — the type and method documentation already identifies the owned snapshot, stable borrowed buffer, ownership-transfer copy, and wrapper/rewrite roles; no diverging caller was demonstrated.
- [Rejected][low] The truncated-hash collision fallback lacks a forced-collision test seam — a natural 64-bit SHA-256-prefix collision is negligible, and adding a production hashing seam solely for this case is disproportionate.
- [Rejected][medium] Broad `Rewrite` malformed-range and boundary coverage is missing — caller-produced ranges are non-null, positive, bounded indexed nodes and exact core output maxima are already tested; only abandoned-output cleanup remains actionable and is recorded above.
- [Rejected][low] Cancellation can arrive during the initial owned-input snapshot copy — the copy is capped at 16 MiB and preceded by a cancellation check; chunked snapshot machinery is disproportionate.
- [Rejected][low] Cancellation can arrive while one maximum-size token is parsed — this duplicates the bounded intra-token limitation above and does not create unbounded work.
- [Rejected][low] Property comparison and carrier decoding lack internal cancellation checkpoints — child traversal is cancellable, carrier decoding is capped near 1.4 MiB with immediate checks, and deeper token/codec cancellation would add disproportionate plumbing.
- [Rejected][false] `CopyRawValue` accepts a node from another document — the type and method are internal, and every production call passes the node returned by the same document's `Resolve` method.
- [Rejected][low] `CopyPayload` can delay cancellation during its clone — this duplicates the bounded-copy finding above; pre/post checks and abandoned-result cleanup are present.
- [Rejected][false] `Rewrite` can allocate from an unbounded replacement count — both production callers derive replacements from the 4,096-path ceiling, so no reachable unbounded list was demonstrated.
- [Rejected][false] Null replacements or null replacement values escape as runtime exceptions — the internal callers construct non-null records from owned non-null wrapper/plaintext buffers; hostile serialized input cannot create these values.
- [Rejected][false] Zero-length equal-start replacements make ordering ambiguous — production replacements always cover a non-empty indexed JSON value or wrapper, and duplicate/overlapping paths are rejected before construction.
- [Rejected][low] Operations after `BoundedJsonDocument.Dispose` can observe cleared bytes — the type is internal and every production use is `using`-scoped; adding disposal guards to every accessor is disproportionate for an unreachable normal path.

### Review Findings (third pass, chunk 3 of 6, 2026-09-16)

Chunk 3 is the core-orchestration and cryptography group: `PayloadProtectionCore.cs`,
`PayloadCryptography.cs`, `PayloadProtectionDiagnostics.cs`,
`PayloadProtectionMaterialGenerator.cs`, `CryptographicPayloadProtectionEntropy.cs`,
`PayloadProtectionLimits.cs`, the sixteen supporting records/enums/interfaces/exceptions,
and the core `.csproj` (22 files, 1,796 added lines). Four independent layers ran.
Chunks 4-6 (tests, evidence, non-8.3 carry-along) remain unreviewed.

- [x] [Review][Decision] Story 8.3 has no blocking CI gate, and the chunk-1 evidence that authorized restoring one is refuted — Commit `dee2d9cf` removed the `payload-protection` job from `.github/workflows/ci.yml`, so nothing merge- or release-blocking builds or runs the pdenc-v2 engine; it is not even a compile gate, because `Hexalith.EventStore.slnx` contains neither project and nothing in the solution references them. `dee2d9cf`'s stated fallback is false: `.github/workflows/advisory-tests.yml` is `continue-on-error: true` (`:32`), restores and builds only `Hexalith.EventStore.slnx` (`:56`, `:59`), then runs `dotnet test "$proj" --no-build` (`:74`) against a project that build never produced output for, under VSTest-only flags on a `Microsoft.Testing.Platform` runner. `scripts/ci-local.sh:132-139` still pins `--minimum-expected-tests 263 --fail-skips on` but is wired to no workflow and no hook. This directly contradicts the Code Map line requiring `.github/workflows/ci.yml` to "run the focused project as a blocking direct test lane with the current complete 263-case minimum" and the `[x]` execution task claiming that lane was added. **The chunk-1 refutation recorded above is wrong:** it cited only `tools/validate-oq8-platform-evidence.py:3361`, the v1 historical binding `sha256_git_file(COMPLETED_V1_CLOSURE_COMMIT, ...)`. The Story 4.15 **v3** gate reads the live worktree file — `:2892-2898` calls `read_bounded_regular_snapshot(ROOT / relative, ...)` for every path in `V3_GATE_INPUT_PATHS`, which includes `SUCCESSOR_SOURCE_PATHS` and therefore `.github/workflows/ci.yml` (`:155-157`, `:272`), and `:3005-3006` asserts `sha256_bytes(current_bytes) == expected` under "Story 4.15 v3 gate-input identity drift". So editing the live `ci.yml` does break a sealed gate, and `dee2d9cf` was correct to revert it. Two frozen constraints now conflict — the Story 8.3 blocking-lane requirement and the Story 4.15 v3 worktree seal — and only the human owner can choose between re-sealing the v3 manifest, adding a separate unsealed blocking workflow, hardening `advisory-tests.yml`, or amending the Story 8.3 requirement. **RESOLVED 2026-09-16 by the human EventStore owner: option 1.** Restore the deleted job verbatim into a new unsealed workflow `.github/workflows/payload-protection.yml` rather than into `ci.yml`, so no v3 gate input changes. Verified safe: the validator's workflow gate inputs are the fixed literal pair `.github/workflows/ci.yml` and `.github/workflows/integration.yml` (`tools/validate-oq8-platform-evidence.py:155-157`, `:272`); `advisory-tests.yml` is absent from the validator entirely, and no test enumerates `.github/workflows/`, so an additional workflow file is unconstrained. Two items were split out of this decision and are recorded as separate patches below.
- [x] [Review][Patch] Restore the blocking 263-case PayloadProtection lane in a new unsealed `.github/workflows/payload-protection.yml` (resolution of the decision above) [.github/workflows/payload-protection.yml]
- [x] [Review][Patch] `advisory-tests.yml` executes zero tests for all four of its projects: the job restores and builds only `Hexalith.EventStore.slnx`, then runs `dotnet test --no-build` with VSTest-only flags on a `Microsoft.Testing.Platform` runner [.github/workflows/advisory-tests.yml:56-77]
- [x] [Review][Patch] `ProtectEvent` reaches the external material factory with no cancellation check after its final AAD validation [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:159-162]
- [x] [Review][Patch] The writer material seam is undocumented, so the caller-owned per-payload freshness obligation whose breach is catastrophic AES-GCM key/nonce reuse is stated nowhere at the call site [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:18-28, 291-299]
- [x] [Review][Patch] The writer-side cumulative selected-plaintext bound has no test, while its reader-side twin does [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:101]
- [x] [Review][Patch] Snapshot operations and the unprotect `malformed`/`cancelled`/`cryptographic-failure` tokens are emitted but never asserted [tests/Hexalith.EventStore.PayloadProtection.Tests/DiagnosticsTests.cs:210-221]
- [x] [Review][Patch] Checkpoint seams are inconsistent: `Rewrite`'s two distinct counters receive one delegate, and `ProtectEvent` exposes no traversal checkpoint at all [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:637-641, 57, 222]
- [x] [Review][Patch] `PayloadCryptography.Encrypt`'s `catch (CryptographicException)` is unreachable [src/Hexalith.EventStore.PayloadProtection/PayloadCryptography.cs:49-52]
- [x] [Review][Patch] A redundant `catch (OperationCanceledException)` clause is fully subsumed by the following bare `catch` in both readers [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:548-559, 795-806]
- [x] [Review][Patch] `CoreProtectionResult` and `CoreUnprotectionResult` omit the no-leak `ToString()` override their five sibling records carry, and one of them holds authenticated plaintext [src/Hexalith.EventStore.PayloadProtection/CoreProtectionResult.cs:10, CoreUnprotectionResult.cs:11]
- [x] [Review][Patch] The diagnostics type documents a `reason` field it never emits, and `Stop` closes every activity with no status or result tag [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionDiagnostics.cs:8, 41-51]
- [x] [Review][Patch] The reader's `remainingDepth < 0` disjunct and `Math.Max(1, remainingDepth)` clamp are unreachable [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs:611-621]


#### Chunk 3 patch outcome (2026-09-16)

All 12 patches applied and verified. The focused gate moves **263 -> 268**; both blocking floors
(`.github/workflows/payload-protection.yml`, `scripts/ci-local.sh:135`) were raised to 268. Debug and
Release build clean under `-warnaserror`, and `Oq8PlatformClosureTests` stays green, confirming that
`.github/workflows/ci.yml` was not touched and the Story 4.15 v3 seal still holds.

Five new cases were added and four were **mutation-verified** — disabling the writer cumulative-plaintext
bound, the writer traversal checkpoint, the activity outcome status, or the snapshot missing-key bucket
each turns its case red.

One honest exception. The cancellation check restored before `ProtectEvent`'s material factory
(`PayloadProtectionCore.cs:166`) has **no armed test**. The case first written for it passed with the
guard deleted: every checkpoint-reachable window is already closed by `AadCodec`'s own invoke-then-throw
cadence, so the only window the new line closes is cancellation requested concurrently between
`ValidateBeforeMaterial`'s last internal check and its return. That is not deterministically
constructible at this seam. Rather than keep a case that proves nothing, it was deleted. The guard is
retained as normative-section-8.1 symmetry with the three sibling seams
(`ProtectSnapshot:350`, `TryUnprotectEventAsync:542`, `TryUnprotectSnapshotAsync:786`), and is recorded
here as unverified by construction rather than as proven.

One constraint surfaced while applying the CI patches. Removing the engine from
`ADVISORY_TEST_PROJECTS` turned
`ReleasePackageManifestTests.Test_projects_are_classified_into_release_live_advisory_or_deferred_lanes`
red: it enumerates every `tests/**/*.csproj` and requires each one to appear as a substring of
`ci.yml`, `integration.yml`, `advisory-tests.yml` or `docs/ci.md`. It has no knowledge of other
workflows, and three of those four files plus the test itself are Story 4.15 v3 sealed gate inputs.
`advisory-tests.yml` is the only writable channel, so the project's lane assignment is now recorded
there as a comment naming the blocking workflow it actually runs in. Full Contracts lane re-verified
at **2023/2023**.

Two items remain open and are **not** closed by these patches:

- **Owner action, outside the repository.** The new `payload-protection` job must be added to `main`'s
  required-checks ruleset. Until then it reports on pull requests without blocking a merge.
- `advisory-tests.yml` keeps `continue-on-error: true` for its remaining three suites. Its flag defect is
  fixed (they now execute; they previously did not), but making those suites blocking is their owner's
  decision, not Story 8.3's.

#### Rejected (third pass, chunk 3)

- [Rejected][false] The production entropy implementation has zero test coverage — `LimitsAndConcurrencyTests.cs:243` constructs `new PayloadProtectionMaterialGenerator()`, whose default `_entropy` is `CryptographicPayloadProtectionEntropy`, and asserts `CanonicalUlid.IsValid` on two distinct references plus 32-byte non-zero keys. The proposed off-by-one mutation in the Crockford bit-packing loop yields an invalid ULID and fails that test.
- [Rejected][false] `CoreUnprotectionResult.IsReadable` is dead code with no caller — it is asserted at `AadPathTests.cs:472`, `DiagnosticsTests.cs:98,306,322`, `EnvelopeTests.cs:31`, `CryptographyTests.cs:52,83,103,217` and `JsonTransformTests.cs:143`.
- [Rejected][false] Trailing `null` slots in `orderedWrappers` raise an uncaught `NullReferenceException` — `ProtectedPathManifestCodec.ValidateOverlap` throws `PayloadProtectionFormatException` on adjacent equal sorted paths (`ProtectedPathManifestCodec.cs:350-358`), so `Create` rejects duplicates instead of de-duplicating and `manifest.Paths.Count` always equals `wrappers.Count`.
- [Rejected][false] The two `ProtectEvent` count/order guards cannot fail by construction — unlike the reader clamp filed above, these are a deliberate, commented assertion (`PayloadProtectionCore.cs:146-147`) protecting the plaintext-to-ordinal pairing that binds each value to its AAD. No harm from retaining them was shown.
- [Rejected][false] `CoreUnprotectionResult` can be built with both members null or both non-null — the record is internal and constructed only through `Readable` and `Unreadable`, which each pin one member to `null`.
- [Rejected][false] The sync `Func<PayloadProtectionMaterial>` protect seam and the hard-coded `DekVersion` 1 are defects — authority section 8.2 mints a new reference at version 1 for every new event or snapshot write; version increments belong to re-encryption, which is Story 8.5-owned. The async provider seam is Story 8.7 integration.
- [Rejected][low] The `format_version` tag is effectively constant on every unprotect — true, but the correct token for a non-v2 rejection is undefined at this seam, and `DiagnosticsTests.cs:210-221` already pins the dimension; changing it would add a parameter and a branch for no operator benefit.
- [Rejected][low] The cleared-buffer observer can only ever see zeros, and `Clear(byte[], SensitiveBufferKind)` has no null guard — `RecordingBufferObserver` records the buffer *kind*, and the tests assert exact per-kind counts, so category coverage is proved; proving no buffer was missed needs a redesign. `CryptographicOperations.ZeroMemory` takes a `Span<byte>`, so a null array converts to an empty span rather than throwing, and no call site passes null.
- [Rejected][low] A host clock before 1970 or beyond the 48-bit millisecond range escapes as a raw `OverflowException` — reachable only on a misconfigured clock, and the fix adds a guard for a condition never shown to occur.
- [Rejected][low] A wrong-length or all-zero DEK span is accepted — `PayloadProtectionMaterialGenerator.Generate` allocates the 32-byte buffer itself, and the production entropy path is covered by a non-zero assertion.
- [Rejected][low] A throwing `isAvailable` collision predicate lets a provider exception escape the bounded surface — the predicate is caller-supplied, so its own exception returns to the caller that raised it; nothing crosses a trust boundary.
- [Rejected][low] An exception outside the catch chain makes `TryUnprotect*Async` throw instead of returning a bounded reason — no reachable escaping type was demonstrated, and the proposed blanket `catch (Exception)` would mask genuine defects.
- [Rejected] Resolver denial, quota and revocation collapse into `ProviderUnavailable` — already accepted and tracked as DW-516.
- [Rejected] Unsupported envelope versions and parse faults should map to `UnknownMetadataVersion`/`MalformedMetadata` — already rejected in chunk 1; the closed reason vocabulary is Story 8.4's routing scope and section 12 requires local-mismatch opacity here.
- [Rejected] `BoundedJsonLookupKey` retains only a 64-bit name digest — chunk 2 scope, already closed; the reviewer filed it at low confidence and `Resolve` does still call `PropertyNameEquals`.


### Review Findings (third pass, chunk 4a of 6, 2026-09-16)

Chunk 4 is the test project. It was split because its diff is 4,455 added lines, above the
review size guideline. **Chunk 4a** is the scaffolding plus the envelope, AAD/path and
cryptography suites: `Hexalith.EventStore.PayloadProtection.Tests.csproj`,
`Fixtures/vector-execution.json`, `TestFixture.cs`, `SequenceEntropy.cs`,
`RecordingBufferObserver.cs`, `DiagnosticsCollection.cs`, `EnvelopeTests.cs`,
`AadPathTests.cs`, `CryptographyTests.cs` (9 files, 1,774 added lines, 58 of the project's
142 test methods). Four independent layers ran. **Chunk 4b** (`JsonTransformTests.cs`,
`DiagnosticsTests.cs`, `LimitsAndConcurrencyTests.cs`, 2,681 lines), chunk 5 (evidence) and
chunk 6 (non-8.3 carry-along) remain unreviewed.

Triage baseline established before verdicts: Release build clean under `-warnaserror`
(0 warnings, 0 errors); the exact gate command
(`dotnet test --project … --minimum-expected-tests 268 --fail-skips on`) passes **268/268,
0 skipped, exit 0**; runtime trait enumeration yields exactly the 51 authorized vectors
(V001-V048, V135, V136, V138), matching `vector-execution.json`. Findings below are
fidelity-of-proof defects, not failing behaviour. Because chunk 4a and 4b are one assembly,
every claimed coverage absence was re-checked against the whole test project; seven were
refuted that way and are listed under Rejected.

- [x] [Review][Patch] The 268-case suite runs only in a lane nothing guards, and the repo's lane-completeness test is satisfied by a comment rather than by the lane — `Hexalith.EventStore.slnx` contains neither the engine nor its test project (spec-mandated), and `.github/workflows/ci.yml` has no PayloadProtection job, so the only build-and-run of either project is the new unsealed `.github/workflows/payload-protection.yml`, which is not in `main`'s required-checks ruleset. `ReleasePackageManifestTests.Test_projects_are_classified_into_release_live_advisory_or_deferred_lanes` reads only `ci.yml`, `integration.yml`, `advisory-tests.yml` and `docs/ci.md`, and the project's sole occurrence across those four files is the lane-assignment **comment** at `advisory-tests.yml:20-21`. The classifier never opens `payload-protection.yml`. Deleting that workflow, or dropping its `Run PayloadProtection vectors` step, therefore leaves the guard green, leaves all seven required checks green, and silently stops all 268 tests — including the engine's only compile check — with nothing red. Fix: add a fact in the style of `Advisory_tests_workflow_preserves_non_release_blocking_suites` that reads `payload-protection.yml` and asserts the csproj path, `--minimum-expected-tests` and `--fail-skips on`, and bind this project's classification to that file instead of the comment. `ReleasePackageManifestTests.cs` runs in the required `ci / build-and-test` lane and `payload-protection.yml` is not a v3 sealed gate input, so both edits are safe. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:969-1001]
- [x] [Review][Patch] The zero-byte assertion that carries AC3 can never fail a test, and the `ShouldNotContain` assertions built on it are fail-open — `RecordingBufferObserver.BufferCleared` asserts `buffer.ToArray().ShouldAllBe(value => value == 0)` before `Observed.Add(kind)`, but all four engine call sites wrap the callback in `catch { }` by deliberate design (`PayloadProtectionCore.cs:1049-1056`, `PayloadCryptography.cs:158-165`, `BoundedJsonDocument.cs:1124-1131`, `PayloadProtectionMaterialGenerator.cs:78-85`), and the suite itself depends on that swallow (`LimitsAndConcurrencyTests.cs:354` installs a throwing observer and expects a successful protect). A non-zeroed buffer is therefore silently dropped from `Observed`: positive count/`ShouldContain` assertions fail with a misleading "did not contain" message, and negative assertions such as `CryptographyTests.cs:225` `successObserver.Observed.ShouldNotContain(SensitiveBufferKind.DecryptedPlaintext)` **pass** for both "never cleared" and "cleared without being zeroed". The class docstring "fails immediately if any observed byte is nonzero" is false. Fix: record `(kind, wasZero)` pairs plus buffer length inside the callback and assert after it returns. [tests/Hexalith.EventStore.PayloadProtection.Tests/RecordingBufferObserver.cs:14-21]
- [x] [Review][Patch] No test ever reads a protected payload under a different context, so cross-scope rejection is inferred from the AES primitive and never observed end to end — `TestFixture.UnprotectAsync` and `UnprotectSnapshotAsync` both expose `PayloadProtectionContext? context = null` and fall back to `Context()`/`SnapshotContext()`; a project-wide search for the `context:` named argument returns **zero** call sites, and the two direct `TryUnprotectEventAsync` calls (`LimitsAndConcurrencyTests.cs:312,839`) also pass `TestFixture.Context()`. Every identity and occurrence substitution (V018-V022, V026-V028) instead terminates at `PayloadCryptography.Decrypt(TestFixture.Envelope(), aad, TestFixture.Dek())` through `AssertRawAadSubstitutionFails`, with a hand-built AAD. Nothing proves that protecting under `tenant-a/party-01/seq 1` and reading under a foreign tenant, aggregate or record sequence yields `BytesMetadataMismatch` with exactly one lookup, no plaintext and cleared buffers — the frozen I/O matrix's "cross-scope -> reject" row. The seam already exists; only the call is missing. [tests/Hexalith.EventStore.PayloadProtection.Tests/TestFixture.cs:66-104, AadPathTests.cs:475-479]
- [x] [Review][Patch] Four `byte[31]` zeroization assertions and two empty-collection assertions are vacuous — `new byte[31]` is already all zeros, so `ShouldAllBe(value => value == 0)` holds whether or not the engine cleared it: `CryptographyTests.cs:297` (snapshot wrong-length resolver DEK), `CryptographyTests.cs:757` (`CreateInvalidMaterial`'s `dek-length` shape), `LimitsAndConcurrencyTests.cs:1150` (event wrong-length), `DiagnosticsTests.cs:203`. Separately, for the `"null"` and `"dek-null"` shapes `CreateInvalidMaterial` adds no key to `transferredKeys`, so `eventKeys.ShouldAllBe(...)` runs over an empty list and `observer.Observed.Count(...).ShouldBe(eventKeys.Count)` reduces to `0.ShouldBe(0)`. The `"reference"` and `"version"` shapes are genuine, because `TestFixture.Dek()` is bytes 0..31. Fix: fill every buffer handed to the engine with a nonzero sentinel before transfer, and assert a non-empty key list for the shapes that transfer one. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:297, 307-323, 753-770]
- [x] [Review][Patch] V017's eleven AAD offsets are unverified magic numbers and the matrix cannot fail per field — the test flips one bit at hard-coded offsets 14…215 and asserts only `PayloadProtectionAuthenticationException`. The read path never parses the AAD; `AadCodec` regenerates it and the whole buffer goes to AES-GCM, so **any** mutated byte fails wherever it lands. Two layers independently decoded the frozen record and confirmed the offsets do sit in the value regions of the eleven distinct fields `0x01`-`0x0b` today, but nothing asserts that: if `AadCodec` stopped encoding a field, or two offsets collided in one field, or an offset fell inside a 6-byte field header, all eleven rows would still pass while the `V017` trait continues to count as executed coverage in the manifest check. The docstring's claim that it "proves every one of the eleven encoded AAD fields is independently authenticated" is unsupported. `g-001.json` already supplies `aadFields[0..10]` and `AadPathTests.cs:494` already contains a `FindAadFieldHeader` walker, so the offsets are derivable from frozen data. Fix: derive each offset from the fixture and assert it lands in a distinct field identifier. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:694-714]
- [x] [Review][Patch] V028 cannot falsify the delimiter concatenation authority section 7.1 forbids — `V028_DelimiterCaseAndNulLikePrintableInputs_AreAuthenticated` asserts only `aad.ShouldNotBe(TestFixture.Aad())` plus an authentication failure, for two rows that already differ from the baseline in the payload-type field; the two rows are never compared with each other. A codec that concatenated fields with delimiters instead of length-prefixing them passes both rows unchanged, so the injectivity property the vector exists to prove is unenforced. The encoding is length-delimited today (6-byte field headers carrying a big-endian length), so this is a regression guard rather than a live collision. Fix: add a cross-field boundary pair at the same seam the test already uses — `Aad(type: "x/y", path: "/z")` versus `Aad(type: "x", path: "/y/z")`, and the `TenantId`/`Domain` equivalent — asserting byte inequality. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:296-310]
- [x] [Review][Patch] V018/V019/V020 assert frozen Contracts behaviour while their names and docstrings claim engine coverage — every rejection in these vectors is thrown by `AggregateIdentity`'s own constructor (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:23-38`), and the asserted 64/64/256-character boundaries are Contracts' limits, not the `1..256 / 1..128 / 1..256` AAD-field bounds that `AadCodec.cs:100-102` implements. The engine's own tenant and domain caps are unreachable through this seam, which is precisely why the AR-20260914-02 constructible maximum is 3,617 while the schema maximum is 3,873 — the difference is exactly `256-64 = 192` plus `128-64 = 64`. Retaining the unreachable codec bounds is correct and amendment-authorized ("do not weaken field bounds to fabricate unreachable values"); the defect is that the test names and docstrings ("V018 binds tenant identity", "V019 … Bounded") advertise coverage that does not exist, and the bare `maximum.Length.ShouldBe(3617)` hides its dependency on Contracts. Fix: correct the docstrings and pin 3,617 to the identity maxima or compute it from them, as V030 already documents its own unreachable cap in place. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:19-60, 366]
- [x] [Review][Patch] Four reachable validation guards have no negative test — (a) `AadCodec.ValidateBeforeMaterial`'s `manifestCommitment.Length != 32` is never exercised: every `TestFixture.Aad(...)` call passes a 32-byte commitment and a project-wide search for a short commitment returns nothing; (b) the `Enum.IsDefined(context.PayloadKind)` guard is never exercised, because no test passes an undefined `(PayloadProtectionPayloadKind)` cast value; (c) V033 covers `/items/0`, `/items/10`, `/items/00`, `/items/-` and `/items/+1` on an eleven-element array but never an index equal to the element count (`/items/11`), so an off-by-one that accepted an out-of-range index would pass; (d) `EnvelopeCodec.Write`'s own `Ciphertext.Length is 0 or > CiphertextBytes` check has no rejecting test — no `Should.Throw` surrounds any `EnvelopeCodec.Write` call site, and `EnvelopeTests.cs:141-143` exercises only the accepted maximum. Fix: add the four negative cases. [src/Hexalith.EventStore.PayloadProtection/AadCodec.cs:79-84, EnvelopeCodec.cs:163-175; tests/…/AadPathTests.cs:426-440]
- [x] [Review][Patch] The snapshot wire format's only oracle is the implementation's own output — `CryptographyTests.cs:60-69` pins `manifestHex`, `aadHex`, `envelopeHex` and `envelopeBase64Url` as literals produced by the code under test, in a test that reads as a sibling of the fixture-backed V001. A repo-wide search for the manifest hex `4858504d010000000100000000`, the AAD commitment `4da57a91925670ffad09e327b661781b` and the envelope tag `759edbbfd849a1872ae9c55c7b49b3e4` hits only this file. The event path is genuinely double-sourced — `g-001.json` is hash-pinned through `manifest.json` by `PayloadProtectionV2ContractTests.cs:827-852` in the required lane, the two `scripts/payload-protection/verify-golden-vectors.{py,mjs}` verifiers are pinned alongside it, and `PayloadProtectionV2ContractTests.BuildAad` re-implements the AAD encoding independently — but neither verifier mentions snapshots and `g-001.json` is event-only. Future regressions are caught; an original snapshot encoding error would not be. Fix here is to label them explicitly as story-owned pins so a later reviewer does not read them as G-001-class frozen evidence; an independent snapshot oracle is deferred below. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:56-69]
- [x] [Review][Patch] The snapshot type-id grammar is bounded on one side only — `AadCodec.ValidateSnapshotTypeId` bounds the id to 16-128 bytes. `SnapshotTypeId_UsesCanonicalLowercaseKebabSuffix` covers the 16-byte minimum exactly (`"hx-snapshot-v1:a"`) but has no 128-byte accept row, no 129-byte reject row, and no non-ASCII row to exercise the `length != value!.Length` ASCII guard. The event-side equivalent (`PayloadTypeId` at 1024/1025) is covered by V021, so the snapshot side is an asymmetric gap in the same grammar. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:77-99]
- [x] [Review][Patch] Wire offsets and format labels are re-hard-coded in tests although named constants exist and are used elsewhere in the same files — `CryptographyTests.cs:473` indexes `encoded[54 + (bit / 8)]` while `EncodeWithUncheckedNonce` two methods away correctly uses `PayloadProtectionWireFormat.NonceOffset`. The same pattern recurs at `EnvelopeTests.cs:36-37` (`AsSpan(12)`, `offset is >= 12 and <= 15` vs `DekVersionOffset`), `:124` (`AsSpan(24)` vs `CiphertextLengthOffset`), `:157-168` (`envelope[28]`, `envelope[30]` vs `KeyReferenceOffset`), `AadPathTests.cs:172-183` (`+ 6`, `, 13` vs `AadFieldHeaderBytes` and `ProtectedSerializationFormatUtf8.Length`) and `LimitsAndConcurrencyTests.cs:630` (`82 +` vs `EnvelopeFixedOverheadBytes`); `LimitsAndConcurrencyTests.cs:36` asserts `SerializationFormat.ShouldBe("json")` rather than `UnprotectedSerializationFormat`. The project has `InternalsVisibleTo`, so there is no access barrier, and this contradicts the story's own delivered claim of "centralized wire constants" and "single-sourced wire constants": a wire-layout change would move the constants and silently desynchronize the tests that duplicate them. `EnvelopeTests.cs` also imports `System.Buffers.Binary` while `CryptographyTests.cs` fully qualifies `BinaryPrimitives` inline. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:473, EnvelopeTests.cs:36-37, 124, 157-168, AadPathTests.cs:172-183]
- [x] [Review][Patch] No wrapper-relocation vector exists anywhere in the project — the realistic attack on a field-level scheme is to move a validly protected wrapper from `/left` into `/right` of the same payload with both envelopes intact, which is exactly what the path and ordinal fields in the AAD exist to defeat. `MixedWrapperKeyIdentity_IsRejectedBeforeLookupAsync` is the nearest case but substitutes the right wrapper's `KeyReference`/`DekVersion` rather than relocating an envelope, and V025/V026 substitute a synthetic AAD instead of mutating the payload. That test already builds the two-field payload the relocation case needs (`{"left":1,"right":2}` protected at `["/left", "/right"]`) and already round-trips `rightWrapper["$pdenc"]`, so the fix is to add a sibling case that assigns the left wrapper's encoded envelope into the right slot and asserts the fail-closed outcome with no plaintext. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:660-692]
- [x] [Review][Patch] V009's two ciphertext-length clauses can never be the deciding check, and its docstring claims bounds it does not isolate — `PayloadProtectionLimits.EnvelopeBytes` (1,048,658) equals `EnvelopeFixedOverheadBytes` (82) plus `CiphertextBytes` (1,048,576) exactly, and `MinimumEnvelopeBytes` is 83. For `ciphertextLength > CiphertextBytes` to decide the rejection, the later length-agreement check would need `value.Length == 82 + ciphertextLength > EnvelopeBytes`, which the first guard already rejects; for `ciphertextLength is 0` it would need `value.Length == 82`, below `MinimumEnvelopeBytes`. Both clauses are therefore unreachable as sole cause — correct defence in depth, but not falsifiable bounds. Every value in the test's invalid set is written into the 101-byte golden envelope, so each is rejected by length disagreement regardless, and the legal maximum 1,048,576 is listed among the rejected values while the docstring calls it the "exact maximum". Fix: correct the docstring and annotate the clause as subsumed defence in depth. [tests/Hexalith.EventStore.PayloadProtection.Tests/EnvelopeTests.cs:115-145; src/Hexalith.EventStore.PayloadProtection/EnvelopeCodec.cs:100-109]
- [x] [Review][Patch] Two unused `using System.Security.Cryptography;` directives — neither file references any symbol from that namespace; the apparent matches are the project's own `PayloadProtectionCryptographicException` and `PayloadCryptography`. The story's own style gate (`dotnet format style --verify-no-changes --severity info`) exits 0, so this is dead code in the diff rather than a broken gate. Fix: delete both lines. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:1, CryptographyTests.cs:1]
- [x] [Review][Patch] V008's only `supported` row re-round-trips the unmutated golden — `[InlineData(4, (byte)0x02, true)]` writes `0x02` to offset 4, which `TestFixture.EnvelopeHex` already holds (`48585032` `02`), so the row exercises no identifier substitution and duplicates the golden round-trip already covered by V001 and V004. It is nonetheless the positive control that amendment AR-20260914-02 requires ("V008 accepts the required HXP2 version byte `02` at offset 4"), and `0x02` is the only supported value, so no genuine alternate spelling exists. Fix: keep the row and comment why it is a same-value control, so a later reviewer does not delete it as dead. [tests/Hexalith.EventStore.PayloadProtection.Tests/EnvelopeTests.cs:78]
- [x] [Review][Patch] The three bit-matrix loops give no failure localization — V010 (96 iterations), V011 (128) and V012 (152) assert with no per-iteration message, so a single-bit regression reports only an exception type with no indication of which offset or bit failed. `EnvelopeTests.cs:32-38` already establishes the right idiom in this very chunk (`$"offset={offset}, bit={bit}"`). [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:463-489, 540-557, 562-579]
- [x] [Review][Patch] V029's unpaired-surrogate assertion is conditional on unrelated theory data — the `"T\ud800"` check sits inside the `else` of `if (valid)`, so it executes only for the single `[InlineData("Té", false)]` row: it would disappear if that row were removed and would run redundantly if another invalid row were added. Fix: hoist it to its own fact or its own row. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:315-331]
- [x] [Review][Patch] `SequenceEntropy` has three latent robustness defects — (a) `FillDataEncryptionKey` uses `checked((byte)Interlocked.Increment(ref _fill))`, so the 256th call throws `OverflowException` instead of supplying a DEK; (b) `CreateKeyReference` calls `Queue.Dequeue()` with no exhaustion check, so one extra generator attempt fails with an opaque "queue empty" error instead of naming the cause; (c) the `Queue<string>` is unsynchronized while the fill counter uses `Interlocked`, so a shared instance driven concurrently would corrupt the queue rather than exercise collisions. All three are latent today — the highest current fill count is 16 (V047) and the one concurrent test uses two separate instances (`LimitsAndConcurrencyTests.cs:118-119`) — but the helper is the seam future concurrency and collision vectors will reuse. [tests/Hexalith.EventStore.PayloadProtection.Tests/SequenceEntropy.cs:28-43]
- [x] [Review][Patch] `RecordingBufferObserver.Observed` is appended under `_sync` but read without it — assertions enumerate the list directly. Latent today because every concurrent read happens after `Task.WhenAll` (`LimitsAndConcurrencyTests.cs:854, 1411`), leaving no live writer, but the half-applied locking is a trap for the next genuinely concurrent case. Fix: expose a locked snapshot accessor and assert through it. [tests/Hexalith.EventStore.PayloadProtection.Tests/RecordingBufferObserver.cs:11-21]
- [x] [Review][Patch] V007's strict base64url matrix omits the empty string — `Base64UrlCodec.Decode("")` returns zero bytes and does not throw, because `DecodeCharacters` rejects only `length % 4 == 1` and the character loop never runs. Adding `[InlineData("")]` to the existing theory would fail it, so the case must be split: the reader path is still fail-closed (0 bytes is below `MinimumEnvelopeBytes`), which is the assertion worth pinning. [tests/Hexalith.EventStore.PayloadProtection.Tests/EnvelopeTests.cs:65-79]
- [x] [Review][Patch] The ULID spelling matrix covers one of four excluded letters and one of two length errors — V024 and `WireKeyReference_NoncanonicalSpellingsAreRejectedBeforeLookupAsync` exercise only `'I'` of the Crockford-excluded set (`L`, `O`, `U` untested) and only the 25-character short form, never a 27-character over-length source, although `CanonicalUlid.IsValid` enforces `Length != 26` in the same expression. The accept side of the first-character bound (a valid reference beginning `7`) is also untested, so an over-strict bound would silently reject valid references. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:176-190]
- [x] [Review][Patch] The configured write-ceiling test reads as the opposite of its intent and never exercises the ceiling's own range — `Snapshot_ConfiguredMaximum_IsEnforcedBeforeMaterialCreation` builds the over-limit payload as `new string('x', maximum - 1)`, which is correct only because the two enclosing quote characters add the remaining two bytes; the literal reads as maximum-minus-one. Separately, `maximumProtectedValueBytes` is never passed as zero, negative, or above `CiphertextBytes`, so the parameter's own range handling is unexercised for both writers. Fix: compute the boundary expressions or comment them, and add the ceiling-value cases after confirming the intended behaviour at that seam. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:236-265]
- [x] [Review][Patch] V003's only third-party known-answer test is the degenerate empty case, and it ignores four fields of the fixture it reads — the test reads `keyHex`, `ivHex` and `tagHex` from `nist-gcm-256-count0.json` but hard-codes `[]` for plaintext, ciphertext and AAD, never consulting the fixture's own `plaintextHex`, `aadHex`, `ciphertextHex` or `profile` block. Nothing asserts that the declared profile matches what is fed to `EncryptAesGcm`, and `ciphertextHex` is never compared, so cross-implementation parity is proven only for zero-length input. Fix within this story: assert the declared plaintext/AAD/ciphertext are empty and the profile matches. Adding a non-empty CAVP count would introduce a frozen fixture, which Story 8.2 owns — deferred below. [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:441-459]
- [x] [Review][Patch] `vector-execution.json` carries an unvalidated field in a second, incompatible format — `laterStory` is expressed as range strings (`"V049-V134"`, `"V137"`) while `owned` enumerates individual identifiers, and the manifest consumer validates only `inherited`, `owned` and `normativeDigest` against the frozen `vector-ownership.json`. So `laterStory`, `story` and `schemaVersion` can drift from or contradict the frozen registry with no guard. Fix: either enumerate `laterStory` in the same form and assert that `inherited` ∪ `owned` ∪ `laterStory` expands to V001-V138 exactly once, or drop the field. [tests/Hexalith.EventStore.PayloadProtection.Tests/Fixtures/vector-execution.json:13]
- [x] [Review][Defer] An independent snapshot golden and a non-empty AES-GCM known-answer vector both require Story 8.2 fixture ownership [tests/Hexalith.EventStore.PayloadProtection.Tests/CryptographyTests.cs:56-69, 441-459] — deferred: pre-existing scope boundary. Story 8.2 froze the fixture set and this story's constraints forbid changing fixtures or verifiers, so neither an authority-owned snapshot golden nor an additional CAVP count with non-empty plaintext and AAD can be added here. The in-scope halves (labelling the snapshot pins, asserting the NIST fixture's declared fields) are filed as patches above.

#### Rejected (third pass, chunk 4a)

- [Rejected][false] An empty selected-path set is never protected — `AadPathTests.cs:121` (`EmptySelection_InvalidEventContext_IsRejectedWithoutMaterialCreation`) drives `[]` with an invalid context and asserts zero material calls, and `LimitsAndConcurrencyTests.cs:742` drives `[]` with a valid context at the exact payload maximum.
- [Rejected][false] The selected-path-resolves-to-JSON-null branch is never exercised — `V041_ZeroSelectedValues_ReturnsOriginalBytes` (`LimitsAndConcurrencyTests.cs:18-38`) protects `{"value":null}` at `["/value"]` and asserts `ProtectedPathCount` 0, format `json`, byte equality, non-aliasing and zero material calls.
- [Rejected][false] Valid tilde escapes are never decoded, so swapping the `~0`/`~1` mappings would stay green — `JsonTransformTests.cs:392-393` pins `a~b` -> `/a~0b` and `a/b` -> `/a~1b` positively, and `:432-434` adds the escaped decoded-equivalent forms.
- [Rejected][false] Ancestor/descendant path overlap is never rejected — `JsonTransformTests.cs:350` covers V036 order-independent overlap and `:373` `V036_InterposedSiblingCannotHideAncestorOverlap` uses exactly the interposed set the spec names, `["/a", "/a-foo", "/a/b"]`.
- [Rejected][false] Already-protected input is never re-protected — `JsonTransformTests.cs:174` rejects every pre-existing reserved wrapper member before encryption and `:194` covers the snapshot writer with no material request.
- [Rejected][false] A payload containing zero `$pdenc` wrappers is never unprotected — 30 `BytesMetadataMismatch` assertions exist across the project, including `AadPathTests.cs:207`.
- [Rejected][false] The event reader is never exercised with a null or wrong-length resolver DEK, unlike the snapshot reader — `Event_KeyOutcome_IsClosedAndClearsTransferredMaterialAsync` (`LimitsAndConcurrencyTests.cs:1142-1160`) is the exact event mirror, asserting `MissingKey` and `ConsistencyMismatch`, and `DiagnosticsTests.cs:199-202` drives both through `TestFixture.UnprotectAsync`.
- [Rejected][false] A Shouldly failure inside `RecordingBufferObserver.BufferCleared` escapes into the operation under test and can mask the expected `PayloadProtectionAuthenticationException`/`PayloadProtectionFormatException` — all four production call sites wrap the callback in `catch { }`, so nothing escapes. The real defect is the opposite (the assertion can never fail) and is filed as a patch above.
- [Rejected][false] The blocking lane cannot detect drift in the frozen Story 8.2 fixtures, because the csproj links them with `PreserveNewest` and no test in this project reads `manifest.json` — `PayloadProtectionV2ContractTests.cs:828` reads `manifest.json` and hash-gates `g-001.json`, `nist-gcm-256-count0.json` and `vector-ownership.json`, and it runs inside the required `ci / build-and-test` lane. Fixture drift is therefore caught by a blocking check; that this project's own lane does not duplicate the guard is not a gap.
- [Rejected][false] `vector-execution.json` leads a reader to believe V137 is covered by Story 8.3 — the manifest correctly lists `V137` under `laterStory`, and the runtime trait enumeration contains exactly V001-V048, V135, V136 and V138. Any overclaim lives in commit and story prose, which is chunk-5 evidence scope.
- [Rejected] The spec's Code Map and Verification section still cite the 263-case minimum and still name `.github/workflows/ci.yml` as the blocking lane, both superseded by the chunk-3 patches (268, `payload-protection.yml`) — the fix is to edit the spec under review, and the narration belongs to chunk 5.
- [Rejected] V030's 4,096-byte total-AAD cap is knowingly unfalsifiable — explicitly authorized by amendment AR-20260914-02, which retains the defensive cap while fixing the executable boundary at the 3,617-byte constructible maximum, and the test documents this in place.

### Review Findings (fifth pass, group 1 of 6, 2026-09-17)

Independent four-layer review of group 1 (wire codecs / manifest / envelope): 11 files,
+1556 / −0, baseline `e8886ec4`…`3a438f74`. Blind Hunter and Edge Case Hunter returned
findings; Verification Gap Reviewer and Acceptance Auditor returned empty result lists, so
those two layers are recorded as failed/empty and this group review may be incomplete.
Remaining groups: 2, 3, 4a, 4b, 5, 6.

- [x] [Review][Patch] `Base64UrlCodec` zeros staging `char[]`/`byte[]` with `Array.Clear`, which the JIT may elide, leaving envelope ciphertext or decoded carrier bytes in the heap after encode/failed-decode — RESOLVED 2026-09-17: every byte staging array now uses `CryptographicOperations.ZeroMemory`, and character staging arrays are zeroed through their byte spans with the same non-elidable primitive. [src/Hexalith.EventStore.PayloadProtection/Base64UrlCodec.cs:60,109,205,211]
- [x] [Review][Patch] `ProtectedSerializationFormat` and `ProtectedSerializationFormatUtf8` are independent literals of `json+pdenc-v2`, so the AAD format field can drift from the JSON format label Core compares — RESOLVED 2026-09-17: the string label is derived once from the canonical UTF-8 wire span, leaving one literal source for both representations. [src/Hexalith.EventStore.PayloadProtection/PayloadProtectionWireFormat.cs:15,112]

#### Rejected (fifth pass, group 1)

- [Rejected][false] `CanonicalText.Encode` can leave a partial UTF-8 buffer on `EncoderFallbackException` — `GetByteCount` already ran the same strict encoder on an immutable string, so `GetBytes` cannot throw that exception on the production path.
- [Rejected][low] `AadCodec.Write` does not zero stackalloc DEK-version/ordinal/sequence spans or checkpoint every 256 AAD bytes — the stack frame dies on return, and the copy is capped at 4,096 bytes after `Validate` already checkpointed path decode; chunked AAD write machinery is disproportionate.
- [Rejected][false] Snapshot AAD accepts any `fieldOrdinal` in `0..4095` when the path is empty — `PayloadProtectionCore` hard-codes ordinal `0` on snapshot protect/unprotect (`PayloadProtectionCore.cs:387,803-817`) and `Snapshot_NonZeroFieldOrdinal_IsRejectedBeforeLookupAsync` already rejects a non-zero snapshot envelope before lookup.
- [Rejected][false] `WriteField` does not enforce type/length pairing or map `OverflowException` — the private helper is only called with the closed pairs (type 2/4 bytes, 3/8, 4/32), and `Validate` sizes the destination so `Slice`/`CopyTo` cannot overflow on that path.
- [Rejected][false] `EnvelopeCodec.Write` never writes flags/reserved and `Encoding.ASCII.GetBytes` can substitute `?` — `new byte[length]` zero-fills offsets 22–23, `Read` rejects nonzero flags, and `ValidateFields` requires a canonical ULID before the ASCII copy.
- [Rejected][false] `EnvelopeCodec.Read` allocates `CanonicalText.Decode` before nonce copies and `CanonicalUlid.IsValid` is only a charset check — section 6.3 forbids variable ciphertext allocation before header/key-ref validation, not the 26-byte key-ref string; Crockford alphabet plus first-character `<= '7'` is equivalent to parse-and-re-encode for this closed alphabet.
- [Rejected][low] `EnvelopeCodec.Read`/`Write` have no `CancellationToken` while copying up to 1 MiB — the bound is the everyday envelope ceiling; adding chunked copy cancellation is the same class of complexity previously rejected for the 16 MiB snapshot snapshot-copy.
- [Rejected][low] `ProtectedPathManifestCodec.Create` discards `JsonPointer.Decode` tokens and never asserts `offset == encoded.Length` — overlap detection already runs on encoded bytes, the length accumulator matches the write loop, and a byte-oriented pointer validator would add branches rather than a direct correction.
- [Rejected][low] Manifest checkpoint callbacks mix enumeration counts, sort comparisons, and byte indexes — spec 7.2/re-derivation requires a check at least every 256 units of those kinds, not one shared unit; splitting counters would add seam complexity without a demonstrated missed cancel.
- [Rejected][false] `JsonPointer.Decode` never applies `ParseArrayIndex`, so `/00` enters HXAD/HXPM — array-index canonicity is only knowable against JSON; `BoundedJsonDocument` calls `ParseArrayIndex` on array traversal, and V033 rejects `/items/00` there. Applying it in `Decode` would reject a legitimate object member named `00`.
- [Rejected][false] Envelope/manifest/wrapper records store mutable arrays without defensive copy or `IDisposable` — ownership transfer to the caller is the specified cleanup model; `PayloadProtectionCore.ClearEnvelope` zeros after use, and `ToString` already suppresses payload dumps.
- [Rejected][low] Private codec helpers lack XML `<param>` docs — CS1591 is not generated for this non-packable project, and the required XML surface is public/internal types already documented.
- [Rejected][false] `EnvelopeCodec.Read` skips the ordinal-nonce check `Write` enforces — V010 and the re-derivation require post-auth nonce validation; `PayloadCryptography.HasExpectedNonce` runs after decrypt (`PayloadProtectionCore.cs:636,867`). Hostile nonces must parse so authentication can fail closed.
- [Rejected][false] `Base64UrlCodec.Encode` lacks the Decode oversize guard — production callers only encode `EnvelopeCodec.Write` output, which is already capped at `EnvelopeBytes`; unbounded `Encode` is not a reachable carrier path.

### Review Findings (independent chunk 1 codec/wire-format, 2026-09-17)

Independent four-layer review of chunk 1 after the fifth-pass codec patches: 8 files,
+1,614 / −0, baseline `e8886ec4`…`629168e3`. Blind Hunter, Edge Case Hunter, and
Verification Gap Reviewer returned findings; Acceptance Auditor returned none. Remaining
groups for follow-up runs: 2, 3, 4a, 4b, 5, 6.

- [x] [Review][Patch] The inert-normalizer fail-closed path in `CanonicalText.GetByteCount` never runs in the suite — RESOLVED 2026-09-17: V029 now exercises `ProtectEvent` and `AadCodec.Write` in both normal and inert-normalizer processes; GitHub/local lanes run the invariant process explicitly, and the required Contracts guard binds that step. Removing `_normalizationIsFunctional` was mutation-verified to fail the dedicated invocation. [src/Hexalith.EventStore.PayloadProtection/CanonicalText.cs:46]
- [x] [Review][Patch] Event AAD UTF-8 field ceilings are pinned only with ASCII — RESOLVED 2026-09-17: V021 and V022 now reject NFC values whose UTF-16 lengths remain within their limits while strict UTF-8 reaches 1,026 payload-type bytes and 2,049 path bytes. Replacing the byte-count comparison with `value.Length` was mutation-verified to fail both focused cases. [tests/Hexalith.EventStore.PayloadProtection.Tests/AadPathTests.cs:69]

#### Rejected (independent chunk 1, 2026-09-17)

- [Rejected][false] `Create(allowEmpty: true)` hashes a count-0 HXPM that must never become AAD field 11 — `ProtectEvent` uses that flag only to snapshot the requested path set, then returns unprotected pass-through when `Paths.Count == 0` before `AadCodec.Write`; AAD field 11 is written only from a later non-empty `Create` of resolved non-null paths.
- [Rejected][low] `JsonPointer.Decode` runs `CanonicalText.GetByteCount` over the whole pointer before the 256-byte checkpoint loop — the first inspection is capped at 2,048 path bytes and is already bracketed by cancellation checks; adding mid-scan checkpoints inside `GetByteCount` would add token/callback parameters for a bound previously treated as everyday-negligible.
- [Rejected][false] `PayloadProtectionWireFormat` omits HXAD field ids, HXPM header offsets, SHA-256 size, and the snapshot type prefix — magics, versions, field count, header sizes, and format labels are already centralized; sequential `WriteField` identifiers and the snapshot grammar prefix are not a second conflicting constant table, and the emitted bytes match those constants.
- [Rejected][false] `CanonicalUlid` has no `ReadOnlySpan<byte>` validator, so `EnvelopeCodec.Read` must `CanonicalText.Decode` the 26-byte key reference first — section 6.3 forbids allocating variable ciphertext before header/key-ref validation, which already happens; the 26-byte ASCII ULID string is not that allocation.
- [Rejected][false] The HXPM writer never checks that the final write cursor equals `encoded.Length` — `totalLength` and the write loop accumulate the same per-path `4 + encodedPath.Length`, so the buffer is fully written; a hypothetical accounting bug was not shown.
- [Rejected][false] `EnvelopeCodec.Write` never assigns the flags bytes and encodes the key reference with `Encoding.ASCII.GetBytes` — `new byte[length]` zero-fills offsets 22–23, `Read` rejects nonzero flags, and `ValidateFields` requires a canonical 26-character Crockford ULID before the ASCII copy.
- [Rejected][low] `Base64UrlCodec.DecodeCharacters` can walk up to `EnvelopeTextCharacters` with no cancellation checkpoint — that is the closed carrier ceiling; adding a token through encode/decode is the same class of chunked-copy complexity previously rejected for the 1 MiB envelope and 16 MiB snapshot copies.
- [Rejected][false] `AadCodec.WriteField` copies without slicing to `value.Length` and can overwrite later field slots — `CopyTo` writes exactly `value.Length` after the header records that length; `Write` sizes the destination from the same `Validate`+`Encode` counts, so remaining-capacity `ArgumentException` is not reachable on this path.
- [Rejected][low] `Create`'s primary `checkpoint` still mixes enumerated path counts, pointer bytes, copy indices, and overlap bytes — sort/encode/hash already have dedicated callbacks; splitting the remaining mixed counter would add parameters without a demonstrated missed cancel.
- [Rejected][false] `JsonPointer.Decode` never caps token count against `JsonDepth` — depth is a document bound enforced by `BoundedJsonDocument.Resolve` before material creation or AAD write; a 65-segment pointer that fits in 2,048 bytes is still a well-formed RFC 6901 string.
- [Rejected][low] `Create(paths, true)` is positional snapshot mode rather than `allowEmpty` — the only production call uses the `allowEmpty:` named argument; separate entry points would add API surface for an internal helper.
- [Rejected][false] `JsonPointer.ParseArrayIndex` throws `NullReferenceException` on a null segment, and `GetUtf8ByteCount` treats a low surrogate as 0 bytes — `Decode` never yields null tokens, and unpaired surrogates are rejected by `CanonicalText` before the decode loop; a valid surrogate pair is 4 + 0 UTF-8 bytes, which is the correct checkpoint accounting.
- [Rejected][false] `EnvelopeCodec.Read` accepts a wire nonce `Write` would reject — V010 and the re-derivation require post-auth nonce validation; `PayloadCryptography.HasExpectedNonce` runs after decrypt, and tests write unchecked nonces on purpose so authentication can fail closed.
- [Rejected][false] `ParseArrayIndex` lacks a null guard (edge-case layer) — same unreachable null as above; `BoundedJsonDocument.Resolve` only passes `Decode` tokens.
- [Rejected][false] `Base64UrlCodec.Encode` length math can `OverflowException` for a huge span — production callers only encode `EnvelopeCodec.Write` output, already capped at `EnvelopeBytes`; the `checked` overflow is not a reachable carrier path.
- [Rejected][low] `AadCodec.Write` does not re-check cancellation between `Validate` and the AAD allocations — the remaining work is the already-validated 4,096-byte record; chunked AAD write cancellation was rejected on the previous group-1 pass as disproportionate.

### Review Findings (chunk 1 codec/wire-format, 2026-09-17)

Independent four-layer review of chunk 1 (wire codecs / manifest / envelope): 11 files,
+1,673 / −0, baseline `e8886ec4`…`652be5dd`. Blind Hunter, Edge Case Hunter, and
Verification Gap Reviewer returned findings. Acceptance Auditor returned an empty
result list and is recorded as failed/empty. Remaining groups for follow-up runs:
2, 3, 4, 5, 6.

- [x] [Review][Patch] Prefix-sharing sibling selections have no acceptance test — RESOLVED 2026-09-17: `V036_PrefixSharingSiblingSelection_IsAccepted` protects `{"a":{"b":1},"a-foo":2}` at `["/a", "/a-foo"]`, asserts `Create` returns two paths, and pins `ProtectedPathCount == 2`. Treating a sorted predecessor as a raw byte-prefix overlap (instead of exact equality) was mutation-verified to fail this case while V036's reject rows stay red. The focused floor is now 292. [tests/Hexalith.EventStore.PayloadProtection.Tests/JsonTransformTests.cs:393]

#### Rejected (chunk 1, 2026-09-17)

- [Rejected][false] `JsonPointer.Decode` always materializes discarded `string[]` tokens — selected paths are already caller-owned managed strings; the byte-oriented index rule belongs to `BoundedJsonDocument`, and Decode is the closed RFC 6901 validator for that existing string.
- [Rejected][low] `Create` feeds one `checkpoint` to enumeration, pointer walking, post-sort copy, and overlap compares — sort/encode/hash already have dedicated callbacks; splitting the mixed remaining counter would add parameters without a demonstrated missed cancel, and everyday callers do not key on the integer.
- [Rejected][false] `CompareUpperBound` substitutes `'0'` with no named constant — the current overlap detector rejects `/a`+`/a/b` with `/a-foo` interposed; the silent-accept outcome does not occur, and V036 already pins that rule.
- [Rejected][low] `ProbeNormalization` only checks that U+00C5 is not Form D — globalization-invariant mode already fail-closes through that probe and V029; a Form-C-always-true stub is not a supported runtime, and a second probe would not change everyday identity validation.
- [Rejected][false] `Base64UrlCodec.Encode` does not reject `value.Length` above `EnvelopeBytes` — production callers only encode `EnvelopeCodec.Write` output, already capped at `EnvelopeBytes`; unbounded `Encode` is not a reachable carrier path.
- [Rejected][false] Envelope/manifest/wrapper records expose mutable `byte[]` without `Clear` — ownership transfers to the caller by spec; `PayloadProtectionCore.ClearEnvelope` zeros after use, and `ToString` already suppresses payload dumps.
- [Rejected][false] `AadCodec.WriteField` can leak `ArgumentOutOfRangeException` on a length-accounting bug — `Write` sizes the destination from the same `Validate`+`Encode` counts and then asserts `offset == result.Length`; the non-taxonomy exception is not reachable on this path.
- [Rejected][false] `WriteField` does not enforce section 7.1 type/length pairing — the private helper is only called with the closed pairs (UTF-8, 4, 8, 32), and `Validate` already requires a 32-byte commitment.
- [Rejected][false] `JsonPointer.Decode` never rejects empty reference tokens — RFC 6901 allows them, section 7.2 does not forbid empty member names, and a slash-only pointer is still bounded by `PathBytes` before JSON depth is applied at resolve.
- [Rejected][false] `GetUtf8ByteCount` returns 4/0 for a surrogate pair — `CanonicalText.GetByteCount` already rejected unpaired surrogates; a valid pair is 4 + 0 UTF-8 bytes, which is the correct checkpoint accounting.
- [Rejected][low] `EnvelopeCodec.Write` omits the Read/Create ownership remarks — Core already zeros successful write buffers; adding remarks does not change a runtime cleanup contract developers already follow at the caller.
- [Rejected][false] `Create` maps every `OverflowException` to format failure and never asserts SHA-256 returned 32 bytes — 4096-path sort/hash arithmetic cannot overflow the checked counters, and `IncrementalHash` SHA-256 always yields 32 bytes.
- [Rejected][low] `AadCodec.Write` does not re-check cancellation after `Validate` — the remaining work is the already-validated 4,096-byte record; chunked AAD write cancellation remains disproportionate for that bound.
- [Rejected][false] `CanonicalUlid.IsValid` and `ParseArrayIndex` have no span overloads — section 6.3 forbids allocating variable ciphertext before header/key-ref validation, not the 26-byte key-ref string; array-index parsing runs on `Decode` tokens that are already `string`.
- [Rejected][false] `EnvelopeCodec.Read` accepts a nonce `Write` would reject — V010 and the re-derivation require post-auth nonce validation; `PayloadCryptography.HasExpectedNonce` runs after decrypt at `PayloadProtectionCore.cs:636,867`.
- [Rejected][false] `Base64UrlCodec.Encode` length math can overflow for a huge span — same unreachable carrier path as the oversize-guard claim; `checked` overflow is not a production `Write` output.
- [Rejected][low] Cancellation after `AadCodec.Validate` can still complete AAD encoding — same 4,096-byte remainder as the Blind Hunter write-path claim; adding a post-Validate checkpoint is not a direct correction worth the extra branch.
- [Rejected][low] A Form-C-always-true normalizer would accept decomposed identity — the documented inert-normalizer threat is already fail-closed; the hypothetical half-stub is not an everyday platform.
- [Rejected][false] `ParseArrayIndex` throws `NullReferenceException` on a null segment — `Decode` never yields null tokens, and `BoundedJsonDocument.Resolve` is the only production caller.
