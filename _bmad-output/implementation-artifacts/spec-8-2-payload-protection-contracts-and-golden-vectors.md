---
title: 'Payload-protection contracts and golden vectors'
type: 'feature'
created: '2026-09-13'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'dfc0ac557c43363159b55bffb4d40feceab1f787'
context:
  - '_bmad-output/implementation-artifacts/epic-8-context.md'
  - '_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - 'docs/brownfield/development-guide.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 8 lacks additive public contracts and independently reproducible vectors for one byte-exact `pdenc-v2` protocol. Story 8.1 authorizes this work only after exact digest and source-compatibility preflight.

**Approach:** Reopen Story 8.1 to approve the missing signatures; then add the contracts/defaults and synthetic fixtures, verify them with Node.js, Python, and .NET, and bind the evidence.

**Decision:** Amend Story 8.1 before source work to freeze the snapshot-carrier, completion-context, and default-overload API and obtain approval for its replacement digest.

## Boundaries & Constraints

**Always:** Treat digest `0f841d5a72a0d0b10fa42a7e765b7282a810f3a5a2aa2b41da2001d17a054ae7` as superseded only after replacement approval; block source until then. Preserve provider behavior, cancellation, v1 contracts, neutral dependencies, documented types, stable fixtures, bounded diagnostics, vector ownership, and exact evidence.

**Never:** Implement engine/backend/Server behavior; change topology, packability, the 14-package manifest, Parties, external resources, persisted data, or G5; expose sensitive data; invent a public signature; retain or claim authorization from the superseded digest after normative bytes change.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Existing provider | Implements today's abstract members | Compiles/runs unchanged | Cancellation and conservative typed fallback remain |
| Context-aware call | Trusted identity and approved context | Override receives context losslessly | Invalid context cannot claim v2 |
| Golden/vector input | G-001, NIST, mutation, or future-owned case | Three toolchains agree; owned cases are exact and future cases stay gated | Drift, leak, or simulated proof fails |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` -- immutable authority: sections 1, 6–9, 14–16, Appendix A, and approval packet.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs:10` -- preserve all current members; add only authorized defaults.
- `src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata*.cs` and `UnreadableProtectedDataReason.cs` -- unchanged v1/fail-closed contracts to reuse.
- `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs` and `src/Hexalith.EventStore.Testing/Fakes/FakeUnreadableProtectionService.cs` -- unchanged compatibility probes.
- `tests/Hexalith.EventStore.Contracts.Tests/Security` -- home for API, serialization, default-method, cancellation, vector/hash, mutation, and no-leak tests.
- `scripts/validate-consumer-package-references.py` and `tools/release-packages.json` -- existing package-only gate; manifest stays byte-identical.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-8-2/` -- bind pre-edit HEAD/worktree, digest/approval, Contracts hashes/API inventory, baseline diff, and manifest hash; halt on incompatible drift.
- [x] Story 8.1 authority and sprint artifacts -- freeze the missing APIs, recompute the digest, supersede old approval, and block until replacement approval.
- [x] `src/Hexalith.EventStore.Contracts/Security/*.cs` -- after renewed approval, add the approved policy, erasure, occurrence, snapshot, and completion contracts, one documented type per file.
- [x] `IEventPayloadProtectionService.cs` -- add authorized context defaults without changing old members or dropping v2 context.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Security/Fixtures/PayloadProtectionV2/` and `scripts/payload-protection/` -- add atomic/derived fixtures, V001–V138 ownership, regeneration, and independent verifiers.
- [x] `PayloadProtectionV2ContractTests.cs` -- prove API shape, bounds, round trips, goldens, mutations, cancellation, compatibility, and no-leak behavior.
- [x] `_bmad-output/implementation-artifacts/8-2-payload-protection-contracts-and-golden-vectors.md` -- bind hashes/commands/results/limitations and keep Story 8.3 blocked until named approvals.

**Acceptance Criteria:**
- Given the frozen ADR and source, when preflight runs, then digest, authorization, compatibility, API inventory, and manifest checks pass before source edits.
- Given old providers, when source/package builds run, then behavior is unchanged and public differences are additive.
- Given approved context, when new APIs run, then overrides receive it exactly and defaults never claim v2, fabricate identity, suppress cancellation, or leak.
- Given checked-in fixtures, when three toolchains run, then exact goldens/hashes pass and future-owned vectors remain gated.
- Given completion, when all focused and package gates pass, then evidence is content-bound and Story 8.3 stays blocked pending explicit authorization.

## Implementation Notes

- Preflight evidence is captured under
  `_bmad-output/implementation-artifacts/evidence/story-8-2/`; current
  Contracts/Security bytes match approved historical source and the release
  manifest remains the exact 14-package baseline.
- Story 8.1 is reopened. Its replacement normative digest is
  `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`
  at EventStore source `dfc0ac557c43363159b55bffb4d40feceab1f787`.
- The amendment freezes `ProtectedSnapshotPayloadV2`, completion context and
  persistence outcome, event/snapshot write wrappers, and six additive service
  defaults. Legacy defaults delegate only non-v2 operations; v2 cannot pass
  without an overriding implementation and exact completion context.
- `AR-20260801-01` is superseded for the new bytes. Jérôme Piquot confirmed
  `AR-20260913-01` at `2026-09-13T12:31:46Z` in all seven mandatory roles for
  the exact replacement digest/source, accepted the additive API and documented
  residual risks, and reported no open material finding. Source work is now
  authorized within the frozen Story 8.2 boundary.

## Spec Change Log

- 2026-09-13: Completed pre-amendment source/digest/manifest inventory and
  prepared the approved Option A normative amendment; implementation paused at
  the replacement content-bound approval gate.
- 2026-09-13: Recorded `AR-20260913-01`; Story 8.1 returned to done and Story
  8.2 implementation resumed against the unchanged replacement digest.
- 2026-09-13: Added the approved additive contracts and service defaults,
  synthetic golden/control fixtures, V001–V138 ownership ledger, and independent
  Node.js, Python, and .NET verification. All focused, full Contracts, solution
  build, 14-package, and isolated-consumer gates passed. Story 8.2 is ready for
  independent review; Story 8.3 remains explicitly predecessor-gated.
- 2026-09-13: Triaged all 36 independent-review findings individually: 30 were
  patched, 4 were disproved, one low wording concern was rejected because its
  proposed fix contradicted authority/edited the reviewed spec, and one
  pre-existing diagnostic-format issue was deferred. Reverification passed 31 focused and
  2,018 full Contracts tests, the solution build, both independent verifiers,
  all 14 package checks, and all isolated consumers.
- 2026-09-14: Recorded Jérôme Piquot's final acceptance of the exact reviewed
  Story 8.2 evidence and explicit authorization for Story 8.3 as
  `AR-20260914-01`. Story 8.2 moved to done; Story 8.3 remains unstarted and
  later successors remain predecessor/evidence gated.

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| blind-01 | high | The replacement packet omitted the section 17.5 evidence-reference field, which made authorization traceability incomplete. Patched the detached record with content hashes and packet references. |
| blind-02 | high | The replacement packet named the roles but omitted the re-review method and prior independent toolchain output it re-bound. Patched it to record the exact method, versions/output hash, unchanged-vector basis, and explicit no-rerun qualification. |
| blind-03 | medium | A null `eventTypeName` bypassed the optional ordinal comparison and could reach a legacy provider. Patched both context-aware event defaults to reject null/whitespace before occurrence validation. |
| blind-04 | low | The protect defaults do call old providers before rejecting a v2 result, but this is the exact approved section 9 algorithm; unlike unprotect, the input format/carrier is not specified as a pre-delegation protect guard. Rejected because the proposed change would contradict authority and the remaining wording concern would edit the spec under review. |
| blind-05 | medium | G-001 inputs and outputs were mutually editable within one fixture. Patched all three implementations to pin the approved fixture SHA-256 and recompute/assert the approved normative digest. |
| blind-06 | medium | V002 decrypted locally assembled fields instead of parsing the fixture envelope. Patched all three implementations to canonical-decode, parse, validate, and decrypt the envelope fields. |
| blind-07 | medium | The NIST fixture's source/profile/reference values were not independently pinned. Patched all three implementations with the approved fixture hash plus exact source, profile, key, IV, and tag assertions. |
| blind-08 | medium | Manifest iteration accepted omissions, duplicates, extras, and escaping paths. Patched exact path allowlists, uniqueness, and repository/output containment checks. |
| blind-09 | medium | Numeric coverage alone did not bind future ownership metadata. Patched the full ownership-file hash plus digest, registry, bounded ranges, and exact executed/predecessor-gated state checks; the hash pins owners, tiers, sections, and tokens too. |
| blind-10 | medium | The built output proved the `RecursiveDir` target duplicated source directories and tests read the checkout. Patched flat fixture targets, copied verifier/spec inputs, and made .NET tests consume only `AppContext.BaseDirectory` copies. |
| blind-11 | medium | The reflection test located six methods without excluding extra context members or pinning return/parameter/default/nullability shape. Patched exact discovery, signature, optional-token, and nullability assertions. |
| blind-12 | medium | Several new public contract shapes were only compile-probed. Patched constructor/property/interface method and default-member reflection inventories. |
| blind-13 | medium | Cancellation precedence covered only event unprotect. Patched one test to exercise all six defaults with canceled tokens and otherwise invalid arguments, while proving no legacy call occurs. |
| blind-14 | medium | Combined v2 signals masked loss of one recognition branch. Patched format-only, scheme-only, carrier-only, and snapshot result/metadata-only matrices. |
| blind-15 | medium | The no-leak scan omitted `PayloadErasureStateRequest` and used unrelated occurrence canaries. Patched distinct request/key and occurrence/type canaries. |
| blind-16 | medium | The recorded full test used `--no-build` without a directly tied preceding build. Patched verification to build and run through one `dotnet test --project ... --no-restore` command and refreshed evidence after execution. |
| blind-17 | false | Standard CI invocation is not a Story 8.2 acceptance requirement: the two independent commands are explicit story gates and were executed. The separately valid clean-environment dependency concern is tracked as verification-other-01 and patched. |
| verification-01 | false | The search correctly proves normal CI does not execute the scripts, but the frozen acceptance criterion requires the three toolchains to run as explicit story commands, not integration into the repository's standard lane. |
| verification-02 | medium | Fixture-to-fixture digest agreement did not bind the approved authority. Patched Node/Python digest recomputation and .NET copied-authority recomputation; every fixture digest is asserted against the approved value. |
| verification-03 | medium | Non-executed ownership states could be renamed while the 135 count stayed green. Patched exact state assertions for every bounded assignment. |
| verification-04 | medium | Combined v2 inputs left each fail-closed predicate independently unproved. Patched a signal-isolation matrix for event and snapshot reads and snapshot protection results. |
| verification-05 | medium | Legacy event delegation tests did not capture arguments. Patched identity, event object, type, byte-array, format, result, and cancellation-token preservation assertions. |
| verification-06 | medium | `PayloadErasureStateRequest` was absent and occurrence fields lacked own canaries in diagnostic tests. Patched both paths with distinct sensitive values. |
| verification-07 | medium | Five cancellation-first defaults were uncovered. Patched all six defaults in a single precedence test with invalid inputs. |
| verification-other-01 | low | Python `cryptography` was not reproducibly declared. Patched a `requirements.txt` pin for validated version `46.0.5`, documented installation, and included its hash in the manifest. |
| edge-01 | medium | Same null event-type defect as blind-03; independently retained and patched in both event defaults. |
| edge-02 | false | The public occurrence contract is intentionally constructed by trusted EventStore storage context; constructor visibility cannot establish caller trust, and Story 8.3 owns cryptographic use validation. |
| edge-03 | medium | `PayloadProtectionWriteResult` allowed v2/context disagreement despite the explicit section 9 invariant. Patched construction to require a context exactly for v2 format/scheme results. |
| edge-04 | medium | `SnapshotProtectionWriteResult` had the same invariant gap for carrier/scheme results. Patched construction and all four valid/invalid combinations. |
| edge-05 | medium | Same future-state gap as verification-03; independently retained and covered by exact state/hash assertions. |
| edge-06 | medium | Same normative-binding gap as verification-02; independently retained and covered by digest recomputation and approved literal checks. |
| edge-07 | medium | Same manifest-shape gap as blind-08; independently retained and covered by exact unique contained paths. |
| edge-08 | low | An extreme or overflowing ownership range could hang/exhaust a verifier before coverage comparison. Patched `1 <= first <= last <= 138` guards before expansion in all three implementations. |
| edge-09 | medium | Same NIST source/profile gap as blind-07; independently retained and covered by exact profile/reference assertions. |
| edge-10 | false | The approved contracts specify value semantics and assign parser/engine enforcement to Story 8.3; Story 8.2 must not invent constructor guards. Exact constructor/property/interface shapes and enum bounds are now pinned. |
| edge-11 | medium | Logging an unreadable outcome can expose its retained sensitive-by-default metadata key alias. The generated formatting root cause predates Story 8.2 and changing existing v1 contracts is excluded, so appended a deferred-work entry for a separately authorized compatibility fix. |

## Design Notes

Baseline `b200305978577530ee2e6ba9e92b886d26dc6f6f` and HEAD `dfc0ac557c43363159b55bffb4d40feceab1f787` have identical `Contracts/Security` source; the manifest hash remains `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`. Later epic wording introduced the missing signatures; the selected route resolves them in a newly approved normative amendment before implementation.

## Verification

**Commands:**
- Story 8.1 section 1.2 digest command before and after amendment -- expected: baseline first, then a distinct replacement digest with LF/no-BOM checks and matching renewed approval.
- `node scripts/payload-protection/verify-golden-vectors.mjs` -- expected: exact atomic/derived hashes and NIST control pass.
- `python3 scripts/payload-protection/verify-golden-vectors.py` -- expected: independently reconstructed bytes/hashes match.
- `dotnet test --project tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore --minimum-expected-tests 2018` -- expected: rebuild tested content and pass all 2,018 tests.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1` -- expected: warnings-as-errors build passes.
- Release pack plus NuGet/package-consumer validators in a temporary directory -- expected: exactly 14 provider-neutral packages validate.
