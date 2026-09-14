---
title: 'pdenc-v2 core cryptographic engine'
type: 'feature'
created: '2026-09-14'
status: 'in-progress'
baseline_commit: 'e8886ec4c277460de3d3208b3fc0b9c261c4967d'
route: 'dispatch'
review_loop_iteration: 2
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

**Never:** Change the frozen authority, Contracts, fixtures/verifiers, Server/no-op hooks, package manifest, `.slnx`, domain/Parties code, topology, persisted data, or external resources. Do not add Azure/DAPR/domain/UI dependencies, public contracts, automatic registration, packability, release claims, durable lifecycle behavior, or claim V127/V128 and later-story evidence.

## Reapproved Story 8.3 Constructibility Amendments

Approval packet `AR-20260914-02` reapproves the following evidence-backed Story 8.3 interpretations. They supersede contrary registry wording only for this core-engine story; the shared authority bytes and digest remain unchanged.

- V008 accepts the required HXP2 version byte `02` at offset 4 and rejects `03`/`ff` there; `02`/`ff` remain the unsupported representatives for the other closed identifier and flag bytes.
- V016 performs exact substituted-record lookup and returns authenticated mismatch for a valid DEK-version substitution; an ordinal-only substitution is rejected locally before lookup because manifest/ordinal consistency is mandatory.
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
- `src/Hexalith.EventStore.PayloadProtection/BoundedJsonDocument.cs` -- keep the JSON index proportional to payload bytes and node count: do not retain a full concatenated path or decoded managed member-name string per node. Preserve otherwise-valid unselected JSON even when its eventual pointer would exceed the selected-path cap; enforce the 2,048-byte path bound only when a selected or discovered protected path is materialized. Reconstructed event and snapshot plaintext must reject any residual or emergent `$pdenc` member before transfer.
- `src/Hexalith.EventStore.PayloadProtection/ProtectedPathManifestCodec.cs` -- reject after at most 4,097 enumerated paths, accept cancellation, checkpoint bounded work, and detect overlap from adjacent sorted UTF-8 paths without quadratic string allocation.
- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs` -- validate all context/AAD sources before pass-through, material creation, or key lookup; bound null/invalid material and non-caller cancellation; implement the root-manifest snapshot crypto seam without Server integration; revalidate protected and reconstructed output byte/depth/node limits plus cumulative plaintext before transfer; observe cancellation during and after transformation.
- `src/Hexalith.EventStore.PayloadProtection/{Base64UrlCodec,PayloadProtectionCore,PayloadProtectionDiagnostics,PayloadCryptography}.cs` -- reject oversized string carriers before scanning or copying; reject configured snapshot oversize before JSON parsing; preserve and clear ownership when cancellation wins after material creation; recheck cancellation after snapshot AAD validation and before lookup; make sensitive-buffer cleanup allocation-free before zeroing; prevent diagnostic listeners from changing operation outcomes; and classify unsupported AES-GCM as a bounded cryptographic failure rather than malformed input.
- `src/Hexalith.EventStore.PayloadProtection/{PayloadCryptography,PayloadProtectionDiagnostics,CryptographicPayloadProtectionEntropy}.cs` -- retain full-path V010 authenticated-mismatch semantics with post-auth nonce/ordinal validation, map encryption failures and typed read outcomes to closed diagnostics, and remove reliance on Contracts' transitive `Hexalith.Commons.UniqueIds` compile surface.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- add snapshot positive/tamper and key-outcome/cleanup tests, mutable-input/path isolation, exact output/reconstruction maxima, full-path V010, genuine gated V138 concurrency plus checkpoints 1/256/512/768 and zero-backend/CPU observations, exact discovered vector-trait membership, malformed wire-key zero-lookup cases, zero-material-call assertions for every locally invalid JSON/path selection, a complete 4,096-wrapper read, missing/wrong-length keys, one material factory call per payload, exceptional generator cleanup, post-factory cancellation cleanup, diagnostic-listener failure isolation, unsupported-AES classification where constructibly testable, and observer-failure cleanup.
- `.github/workflows/ci.yml` and `scripts/ci-local.sh` -- run the focused project as a blocking direct test lane with the current complete 159-case minimum (updated when the suite changes) while preserving V138 as observation-only, and without adding either project to `Hexalith.EventStore.slnx` or changing release packaging.
- All changed C# must satisfy the tracked Allman-brace and XML-documentation rules; verify whitespace formatting as well as analyzer/style diagnostics.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/evidence/story-8-3/` -- preserve preflight, then bind the re-derived source/test/CI inventory, authority/8.2 hashes, Contracts API, 14-package baseline, NIST recheck, and exact review results; halt on drift.
- [ ] `src/Hexalith.EventStore.PayloadProtection/` -- re-derive the Contracts-only, `IsPackable=false` byte-oriented event/snapshot core and every bounded validation, cancellation, diagnostic, exception, dependency, and buffer-ownership rule in Review Re-derivation Requirements; cite the digest and normative sections in material files.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- re-derive the runnable xUnit v3/Shouldly project with internal access, linked fixtures, deterministic test-only seams, exact execution manifest, and all review-regression cases.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/{Envelope,AadPath,Cryptography,JsonTransform,LimitsAndConcurrency,Diagnostics}Tests.cs` -- execute inherited V001-V003 and owned V004-V048/V135-V136/V138, including every constructible named mutation, all reapproved interpretations, snapshot crypto, full-path outcomes, true concurrency, exact/max+1 reconstruction, cancellation, zeroing/no-leak, and exceptional-exit assertions.
- [ ] `.github/workflows/ci.yml`, `scripts/ci-local.sh` -- add a blocking direct-project PayloadProtection test lane while preserving the frozen `.slnx` and 14-package release inventory.
- [ ] `_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md` -- replace superseded hashes/results with the re-derived commands, counts, limits, review state, and authorization boundary; authorize only 8.4/8.5 after exact approval.

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
- The six required test areas contain 51 unique vector traits and execute 159
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

## Review Triage Log

- Independent review not yet run. Requirement renegotiation and human approval
  are recorded in `AR-20260914-02`; the remaining test task and Story 8.3 may
  close after independent review confirms the implementation matches it.

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

## Design Notes

Keep types internal until a frozen later-story seam requires otherwise. V046-V048 use bounded test seams without implementing Story 8.5's durable SPI. V138 records counts, bounds, cancellation, memory and latency without inventing a performance gate. Validate directly because Story 8.8 owns solution/package integration.

## Verification

**Commands:**
- Story 8.1 normative digest plus packet-bound `sha256sum` preflight -- expected: all approved identities match.
- `node scripts/payload-protection/verify-golden-vectors.mjs` and `python3 scripts/payload-protection/verify-golden-vectors.py` -- expected: V001-V003 pass unchanged.
- `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release --minimum-expected-tests 48` -- expected: all core vectors pass with no skip/unrun.
- `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Release -m:1 -nodeReuse:false -p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true` and `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -nodeReuse:false` -- expected: zero warnings/errors.
- Existing release pack and both package validators in a temporary directory -- expected: exactly 14 archives; the new project is excluded.
- `git diff --check` -- expected: no whitespace errors.

**Observed results (2026-09-14):** both independent V001-V003 verifiers passed;
focused Release tests passed 159/159 with no skips; code-style verification and
the LF-normalization scan, the AOT/trim-analyzed core Release build, the complete
solution Release build, and `git diff --check` passed; release packing plus both
validators produced exactly 14 archives. Stories 8.4/8.5 remain unauthorized
pending independent review and exact approval.
