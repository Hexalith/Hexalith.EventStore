---
title: 'pdenc-v2 core cryptographic engine'
type: 'feature'
created: '2026-09-14'
status: 'in-review'
baseline_commit: 'e8886ec4c277460de3d3208b3fc0b9c261c4967d'
route: 'dispatch'
review_loop_iteration: 0
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

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-8-3/` -- bind clean HEAD, authority/8.2 hashes, Contracts API, 14-package baseline, NIST recheck, and inventory; halt on drift.
- [x] `src/Hexalith.EventStore.PayloadProtection/` -- create the Contracts-only, `IsPackable=false` project and one documented type per file for strict envelope/base64url, AAD/manifest/RFC6901, JSON transformation, AES-GCM, bounds/cancellation, safe diagnostics, and buffer ownership; cite the digest and normative sections in material files.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- create a runnable xUnit v3/Shouldly project with internal access, linked fixtures, deterministic test-only seams, and a separate execution manifest.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/{Envelope,AadPath,Cryptography,JsonTransform,LimitsAndConcurrency,Diagnostics}Tests.cs` -- execute inherited V001-V003 and owned V004-V048/V135-V136/V138, including every constructible named mutation and the reapproved V008/V016/V023/V030/V038/V039 interpretations, exact/max+1 boundary, cancellation checkpoint, zeroing/no-leak assertion, and bounded hostile-load observation.
- [x] `_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md` -- bind hashes, commands, counts, limitations, and review state; authorize only 8.4/8.5 after exact approval.

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
- The six required test areas contain 51 unique vector traits and execute 137
  passing cases after the Step-3 exact-coverage audit. Approval packet
  `AR-20260914-02` accepts the evidence-backed constructible interpretations
  for V008/V016/V023/V030/V038/V039 without weakening bounds or importing
  Story 8.5 behavior. Exact covered cases and the mathematical proof are
  recorded in `evidence/story-8-3/verification.md`.

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

## Review Triage Log

- Independent review not yet run. Requirement renegotiation and human approval
  are recorded in `AR-20260914-02`; the remaining test task and Story 8.3 may
  close after independent review confirms the implementation matches it.

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
focused Release tests passed 137/137 with no skips; code-style verification and
the LF-normalization scan, the AOT/trim-analyzed core Release build, the complete
solution Release build, and `git diff --check` passed; release packing plus both
validators produced exactly 14 archives. Stories 8.4/8.5 remain unauthorized
pending independent review and exact approval.
