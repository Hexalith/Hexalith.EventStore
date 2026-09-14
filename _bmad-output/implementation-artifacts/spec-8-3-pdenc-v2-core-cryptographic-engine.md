---
title: 'pdenc-v2 core cryptographic engine'
type: 'feature'
created: '2026-09-14'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-8-context.md'
  - '_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 8.2 froze the portable `pdenc-v2` bytes and authorized Story 8.3, but EventStore has no provider-neutral implementation that can produce and authenticate them safely.

**Approach:** Add a non-packable core library containing internal codecs, JSON/path transformation, AES-256-GCM, bounds, cancellation, diagnostics, and buffer cleanup; prove it against the frozen goldens and every Story 8.3 vector without wiring Server, storage, or a provider.

## Boundaries & Constraints

**Always:** Revalidate packet `AR-20260914-01`, digest `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, and all Story 8.2 hashes before editing. Implement sections 5-8, 14-15 and V004-V048/V135-V136/V138 exactly; keep output byte-stable, failures bounded, and owned sensitive buffers zeroed.

**Never:** Change the frozen authority, Contracts, fixtures/verifiers, Server/no-op hooks, package manifest, `.slnx`, domain/Parties code, topology, persisted data, or external resources. Do not add Azure/DAPR/domain/UI dependencies, public contracts, automatic registration, packability, release claims, durable lifecycle behavior, or claim V127/V128 and later-story evidence.

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
- [ ] `_bmad-output/implementation-artifacts/evidence/story-8-3/` -- bind clean HEAD, authority/8.2 hashes, Contracts API, 14-package baseline, NIST recheck, and inventory; halt on drift.
- [ ] `src/Hexalith.EventStore.PayloadProtection/` -- create the Contracts-only, `IsPackable=false` project and one documented type per file for strict envelope/base64url, AAD/manifest/RFC6901, JSON transformation, AES-GCM, bounds/cancellation, safe diagnostics, and buffer ownership; cite the digest and normative sections in material files.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/` -- create a runnable xUnit v3/Shouldly project with internal access, linked fixtures, deterministic test-only seams, and a separate execution manifest.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/{Envelope,AadPath,Cryptography,JsonTransform,LimitsAndConcurrency,Diagnostics}Tests.cs` -- execute inherited V001-V003 and owned V004-V048/V135-V136/V138, including every named mutation, exact/max+1 boundary, cancellation checkpoint, zeroing/no-leak assertion, and bounded hostile-load observation.
- [ ] `_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md` -- bind hashes, commands, counts, limitations, and review state; authorize only 8.4/8.5 after exact approval.

**Acceptance Criteria:**
- Given activation, when preflight runs, then every authorized digest/hash matches current bytes and any mismatch blocks source work.
- Given canonical inputs, when the real core protects or authenticates, then G-001/NIST and all assigned vectors produce their exact bytes or frozen typed outcome.
- Given hostile, concurrent, cancelled, or maximum-bound work, when each path exits, then work remains bounded, no partial plaintext escapes, and every engine-owned sensitive buffer is observed zeroed without mutating caller-owned or transferred buffers.
- Given dependency and preservation scans, when verification completes, then the core is provider-neutral/non-packable, old behavior and 14-package output remain unchanged, and no later-story capability is claimed.

## Implementation Notes

## Spec Change Log

## Review Triage Log

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
