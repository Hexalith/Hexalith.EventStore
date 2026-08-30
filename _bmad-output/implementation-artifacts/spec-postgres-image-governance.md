---
title: 'Govern PostgreSQL image identity'
type: 'bugfix'
created: '2026-08-27'
status: done
baseline_commit: '10051a68eb1db322a4f7fa91934d880ce1409687'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The live-sidecar workflow and `Oq8PostgresqlFixture` independently name the mutable `postgres:18.4` tag, so their identities can drift and a later tag replacement can change tested bits without a reviewed source change.

**Approach:** Pin the reviewed multi-platform PostgreSQL 18.4 index as `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, enforce exact workflow/fixture agreement with deterministic regression tests, and document a rotation procedure that re-verifies the upstream index before coordinated updates. Preserve the Story 4.15 v1 handoff as immutable historical evidence and add a versioned v2 successor that becomes the only active current-source authority for the changed workflow and fixture.

## Boundaries & Constraints

**Always:** Preserve the fixture's fail-closed prerequisite inspection and TCP readiness probe; use the cross-platform index digest rather than the amd64 child manifest; keep the Story 4.14 and Story 4.15 v1 evidence directories, review subjects, receipts, checksum manifests, and `4-8-eventstore-oq8-platform-evidence.yaml` byte-for-byte unchanged; add new governance tests in a new source file because the existing OQ8 workflow guardrail test is itself hash-bound. The additive v2 successor must bind the v1 landed source commit `5e8f175b2ced4715f7c6f765386812cc1001dbb4` and subject SHA-256 `26a0afd67c14befc3d7b5045c13c1532b27663e3409026d6f5d5e8fc5b3b5e6f`, the exact reviewed PostgreSQL index, the before/after identities of the workflow and fixture, the successor validator/tests/documentation, and fresh architecture, security, and test receipts issued after the successor subject is frozen.

**Block If:** The v2 successor is absent, does not link exactly to the v1 commit and subject above, omits either changed source identity or any successor gate input, lacks any fresh content-bound architecture/security/test receipt, rewrites v1 bytes, or attempts to make the historical v1 handoff alone authorize the changed current source.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`; rewrite retained Story 4.14/4.15 v1 evidence, review subjects, receipts, checksum manifests, or top-level handoff packet; weaken OQ8 closure validation; treat v1 as current-source authority after either bound source changes; grant release, package, registry, deployment, runtime-pin, consumer-migration, external-repository, Folders-final-closure, or final-consumer authority; use the historical Docker image/config ID as the registry pin; hide the fixture change through generated-source, MSBuild, PATH, or local-tag indirection.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reviewed image | Workflow and fixture use the reviewed index | Both pull/run the identical digest-pinned image | No error expected |
| Mutable or malformed identity | Tag-only, child digest, missing digest, or invalid SHA-256 | Governance test rejects the identity | Focused deterministic failure identifies the violated contract |
| Drift | Workflow and fixture references differ | Governance test rejects the mismatch | Failure reports both extracted identities |
| Ambiguous workflow | Pull step is missing or has duplicate image pulls | Governance test rejects the workflow | Fail closed instead of selecting one reference |
| Historical v1 handoff | Immutable v1 evidence is valid but current workflow or fixture differs from v1 | Preserve v1 as historical evidence; require the valid v2 successor for current-source authority | V1 alone must not authorize current source |
| Missing or incomplete successor | V2 predecessor link, bound identity, review receipt, or gate input is missing or changed | Reject current-source closure | Name the missing or drifted successor field without falling back to v1 |
| Overstated successor authority | V2 claims authority beyond the existing EventStore source-only boundary | Reject the successor | Preserve every v1 external-authority exclusion |

</intent-contract>

<frozen-after-approval>

## Owner Decision: Phase-Separated Story 4.15 v2 Successor

Create exactly one additive successor under `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/`. The v1 directory and top-level `4-8-eventstore-oq8-platform-evidence.yaml` remain immutable historical evidence. Current-source closure requires both valid v1 historical evidence and the valid v2 successor; v1 alone never authorizes the evolved live files.

The v2 directory contains exactly these artifacts, with no aliases or additional authority files:

- `source-artifact-identity.json` using schema `hexalith.eventstore.story-4-15-successor-source-identity/v2`.
- `limitations.json` using schema `hexalith.eventstore.story-4-15-successor-limitations/v2`.
- `validator-sha256.txt` containing the SHA-256 of the evolved `tools/validate-oq8-platform-evidence.py`.
- `pre-review-execution.json` using schema `hexalith.eventstore.story-4-15-successor-pre-review-execution/v2`.
- `review-subject.json` using schema `hexalith.eventstore.story-4-15-successor-review-subject/v2`.
- `reviews/architecture.json`, `reviews/security.json`, and `reviews/test.json`, each using schema `hexalith.eventstore.story-4-15-successor-review-receipt/v2`.
- `source-only-handoff.json` using schema `hexalith.eventstore.story-4-15-successor-source-only-handoff/v2`.
- `closure-sha256.txt`, a path-sorted SHA-256 manifest of every preceding v2 file and never of itself.

Assemble and validate v2 in this order:

1. Evolve the workflow, fixture, governance test, validator, closure test, and documentation; then write `source-artifact-identity.json`, `limitations.json`, `validator-sha256.txt`, and the receipt-independent `pre-review-execution.json`.
2. Freeze `review-subject.json`. It binds the v1 predecessor commit `5e8f175b2ced4715f7c6f765386812cc1001dbb4` and subject SHA-256 `26a0afd67c14befc3d7b5045c13c1532b27663e3409026d6f5d5e8fc5b3b5e6f`; the completed-v1 closure snapshot commit `17e47a390fdfecafba84dce14779ad13b97be339`; the reviewed index `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`; the exact candidate-input hashes; and the source identity, limitations, validator record, and pre-review execution hashes. It does not bind its own hash, a final Git commit/tree containing itself, any review receipt, `source-only-handoff.json`, or `closure-sha256.txt`.
3. Issue the three fresh review receipts only after the subject is frozen. Each receipt binds the SHA-256 of that exact subject and the SHA-256 of `limitations.json`; issuing a receipt must not mutate the subject or any bound candidate input.
4. Write `source-only-handoff.json` after all receipts exist. It binds the v1 predecessor commit and subject, the v2 subject hash, limitations hash, and the exact architecture/security/test receipt hashes, and repeats every v1 external-authority exclusion.
5. Write `closure-sha256.txt` last. Closure fails for a missing, additional, reordered, malformed, symlinked, or digest-drifted v2 artifact.

`source-artifact-identity.json` and `review-subject.json` both record the following source transitions:

- `.github/workflows/integration.yml`: predecessor SHA-256 `343163fd164bb49252ad2ec67c7fbc90aa2f3aaecafa4d4d51640ccc39e7b777` with image `postgres:18.4`; successor SHA-256 is computed from the frozen candidate and its image is the reviewed index.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs`: predecessor SHA-256 `7f29993a470d179288a367c8d877e01b7f0f7be4206faf329f5d889b6171cae6` with image `postgres:18.4`; successor SHA-256 is computed from the frozen candidate and its image is the reviewed index.
- The successor gate-input map contains exact path and candidate SHA-256 pairs for `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs`, `tools/validate-oq8-platform-evidence.py`, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs`, and `docs/ci.md`.

V1 validation is historical: validate immutable v1 packet, subject, receipts, handoff, manifests, and their internal bindings byte-for-byte. Preserve `5e8f175b2ced4715f7c6f765386812cc1001dbb4` as v1's declared landed-source identity, but resolve v1 bindings that historically targeted then-current live paths or validator bytes against completed-v1 closure snapshot commit `17e47a390fdfecafba84dce14779ad13b97be339`, not against the evolving working tree. After an evolution explicitly bound by v2, do not compare v1's historical live-path or validator hashes to current working-tree bytes. V2 alone validates the current workflow, fixture, gate inputs, and current validator identity; using v1's `validator-sha256.txt` as the current-validator identity is forbidden.

</frozen-after-approval>

## Code Map

- `.github/workflows/integration.yml` -- evolved OQ8 orchestration path; its named pull step currently contains `docker pull postgres:18.4` and may safely change to the reviewed digest.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- runtime authority currently declares private `PostgresImage = "postgres:18.4"`; this file is also one of 24 byte-frozen Story 4.15 capability paths.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/**` and `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` -- immutable v1 predecessor; read and validate, but do not edit or reseal.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/**` -- exact additive successor location and file set defined by the frozen owner decision; freeze candidate inputs and subject before receipts, then assemble the handoff and manifest.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- new, unbound deterministic guardrail location; extract the named workflow step and fixture constant, assert one exact match, digest shape, and negative mutations.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- read-only evidence; do not add coverage here because Story 4.15 hash-binds it as `workflowGuardrailTests`.
- `tools/validate-oq8-platform-evidence.py` -- evolve the active entry point so it still proves v1's immutable historical integrity but requires the v2 successor for current-source closure after the bound workflow/fixture change; it must not silently remove a path from v1 or accept v1 alone.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- evolve the blocking contract to prove v1 remains historically intact, v1 alone cannot authorize the changed current source, and only a complete content-bound v2 successor restores current-source closure.
- `docs/ci.md` -- unbound operational documentation location for digest discovery, review, coordinated rotation, focused governance validation, and full live-sidecar validation.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- explicitly read-only; orchestration owns resolution recording.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/integration.yml` and `Oq8PostgresqlFixture.cs` -- replace the mutable tag with the reviewed multi-platform index reference after the OQ8 successor/reseal decision is supplied.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- add positive agreement/digest-shape coverage and negative tag-only, mismatch, missing, and duplicate-pull cases.
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/**` -- add the exact immutable v2 successor described above without modifying any v1 artifact; assemble it in the frozen subject → receipts → handoff → manifest order.
- [x] `tools/validate-oq8-platform-evidence.py` and `Oq8PlatformClosureTests.cs` -- replace v1's current-HEAD authority with the fail-closed v2 successor gate while retaining explicit historical v1 integrity validation and negative proof that v1 alone cannot authorize the changed source.
- [x] `docs/ci.md` -- document registry index inspection, upstream/version/platform review, coordinated literal rotation, and required validation.

**Acceptance Criteria:**
- Given the integration workflow and fixture source, when deterministic governance tests run, then they prove the named pull step and `PostgresImage` contain one identical `postgres@sha256:<64 lowercase hex>` reference.
- Given the reviewed PostgreSQL 18.4 multi-platform index, when the live-sidecar lane runs, then prerequisite inspection, container startup, and captured runtime metadata all use `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
- Given the existing Story 4.15 source-only handoff, when repository closure validation runs after implementation, then every v1 artifact remains byte-for-byte valid as historical evidence, v1 alone is explicitly non-authorizing for the changed current source, and the complete v2 successor with fresh content-bound architecture/security/test receipts is the only gate that restores current-source closure.
- Given a missing, drifted, partially reviewed, predecessor-mismatched, or authority-overstating v2 successor, when either focused or full closure validation runs, then validation fails closed and never falls back to v1 or silently exempts the changed workflow/fixture.
- Given v2 assembly, when the candidate subject is frozen, then it binds only predecessor/current source identities and receipt-independent gate inputs; each later receipt binds that unchanged subject, the handoff binds the three receipts, and the path-sorted checksum manifest closes the fixed file set last without any self-reference.
- Given an accepted v2 evolution, when v1 historical integrity and current-source closure are validated, then v1 retains landed-source identity `5e8f175b2ced4715f7c6f765386812cc1001dbb4`, its historical live-path and validator bindings resolve against completed-v1 closure snapshot `17e47a390fdfecafba84dce14779ad13b97be339`, and v2 identities resolve against current candidate bytes and the evolved validator.

## Spec Change Log

- 2026-08-28: Owner selected the versioned Story 4.15 successor resolution: preserve v1 byte-for-byte, bind the digest-pinned workflow/fixture and successor gate in v2, require fresh architecture/security/test receipts, and make v2 the only active current-source authority.
- 2026-08-28: Owner refined the selected resolution to an exact phase-separated `v2/` contract: freeze receipt-independent candidate inputs and subject first, issue subject-bound receipts second, bind them in the handoff third, and close the fixed artifact set with a manifest last; validate v1 against its landed historical snapshot and v2 against current bytes.
- 2026-08-30: Review hardening bound semantic PostgreSQL declarations, non-symlink ancestors, bounded single-read snapshots, canonical typed verification evidence, strict UTC phase ordering, the fresh digest observation path, the fourth rotation literal, and exact final test-receipt results; v2 was resealed in the approved phase order.

## Review Triage Log

- [x] Required v2 to extract exactly one PostgreSQL declaration from each current workflow/fixture snapshot and reject a coherently resealed tag-only semantic source.
- [x] Rejected symlinked v2, source-transition, and gate-input ancestors/components, including ancestor escape mutations.
- [x] Added 64 KiB artifact and 512 KiB bound-source limits before reads, plus one cached snapshot for hashing and semantics with a deterministic post-snapshot drift probe.
- [x] Fixed the pre-review command set and exact JSON integer/boolean types; added the receipt-independent live-sidecar result and kept the final closure command in the later test receipt.
- [x] Rejected future timestamps and enforced pre-review execution before freeze, receipts after freeze, and handoff after every receipt with boundary/order/future mutations.
- [x] Made fresh observation validation pass the reviewed digest explicitly and proved a runtime-only `postgres:18.4` drift is rejected while historical observations retain their explicit tag expectation.
- [x] Added `PostgreSqlImageGovernanceTests.ReviewedPostgresImage` to the coordinated rotation literals in `docs/ci.md`.
- [x] Bound and validated exact governance 18/18, closure 368/368, and live-sidecar 115/115 command results in the test receipt, including fail-closed name/command/count/failure/skip/type mutations.

## Design Notes

The registry inspection performed on 2026-08-27 returned index digest `sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636` and amd64 child manifest `sha256:4cc13dede823cab4e05290c7fb3350fb4e599ecabd9b07e6706b5d5e8f5bc929`. The index is the correct pin because the fixture is not architecture-specific. Retained evidence's `sha256:3a82...` value is a Docker image/config identity, not a registry manifest digest.

## Verification

**Commands:**
- `docker buildx imagetools inspect postgres:18.4 --format '{{json .Manifest}}'` -- expected: the reviewed index digest and platform manifests are visible before rotation.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.PostgreSqlImageGovernanceTests -noColor` -- expected: all governance cases pass.
- `actionlint .github/workflows/integration.yml` -- expected: no findings.
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: immutable v1 historical integrity and complete v2 current-source closure both pass; v1 alone cannot authorize the changed source.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: v1 preservation, v2 successor, missing/drifted receipt, predecessor mismatch, source drift, and authority-boundary cases all pass.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release -p:UseHexalithProjectReferences=false` -- expected: the complete live-sidecar suite passes with the digest-pinned image already pulled.

**Recorded results (2026-08-30):**
- Upstream inspection returned OCI index `sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, including amd64 child `sha256:4cc13dede823cab4e05290c7fb3350fb4e599ecabd9b07e6706b5d5e8f5bc929` and the reviewed platform set.
- Release Contracts build passed with 0 warnings and 0 errors; `actionlint` and Python syntax validation reported no findings.
- PostgreSQL governance passed 18/18; final OQ8 closure passed 368/368 with 0 failures and 0 skips; live-sidecar passed 115/115 with 0 failures and 0 skips.
- Full v1+v2 validation and explicit historical-v1-only validation passed; v1 remained non-authorizing for current source.

## Suggested Review Order

**Current-source closure**

- Start with the fail-closed v2 authority, snapshot, chronology, and receipt validator.
  [`validate-oq8-platform-evidence.py:2610`](../../tools/validate-oq8-platform-evidence.py#L2610)

- Review the frozen subject that binds predecessor, sources, gates, and limitations.
  [`review-subject.json:1`](evidence/story-4-15-successors/v2/review-subject.json#L1)

- Confirm exact governance, closure, and live-sidecar results are content-bound.
  [`test.json:14`](evidence/story-4-15-successors/v2/reviews/test.json#L14)

**Runtime image authority**

- Verify CI pulls the reviewed multi-platform index before live-sidecar execution.
  [`integration.yml:74`](../../.github/workflows/integration.yml#L74)

- Verify the runtime fixture uses the identical immutable image reference.
  [`Oq8PostgresqlFixture.cs:29`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L29)

**Fail-closed verification**

- Check deterministic image agreement, shape, drift, and ambiguity guards.
  [`PostgreSqlImageGovernanceTests.cs:43`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs#L43)

- Inspect successor mutation coverage across identity, evidence, paths, and chronology.
  [`Oq8PlatformClosureTests.cs:146`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L146)

**Operations and portability**

- Follow the coordinated four-literal digest rotation and validation procedure.
  [`ci.md:79`](../../docs/ci.md#L79)

- Preserve LF bytes for every hash-bound v2 successor artifact.
  [`.gitattributes:34`](../../.gitattributes#L34)
