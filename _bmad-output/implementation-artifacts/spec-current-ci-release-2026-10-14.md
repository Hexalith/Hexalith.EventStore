---
title: 'Repair current CI and publish a verified EventStore release'
type: 'bugfix'
created: '2026-10-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e74408c46e70750a7767091be87d1107ba8d66c4'
context:
  - 'AGENTS.md'
  - 'docs/ci.md'
---

<frozen-after-approval reason="User explicitly authorized CI fixes, push and release with human validation bypassed; local tests are prohibited">

## Intent

**Problem:** Current main fails CI because named projection substitutes advertise ordinary delivery as disabled and new gateway authority code contains source forms and credential fixtures rejected by the existing secret scanner. This prevents a clean release of the latest source.

**Approach:** Correct the stale substitutes and remove literal credential fixtures, preserving production capability admission and secret detection. Then commit and push the reviewed repair, observe GitHub Actions, publish a new semantic release and confirm every declared NuGet package is available with the correct source identity.

## Boundaries & Constraints

**Always:** Preserve user changes and published/historical evidence bytes. Keep the existing secret scanner, capability checks, rebuild assertions, release manifest, publisher pin and protected environment unchanged. Generate test credentials at runtime and compare requests with those exact generated values. Use the current main branch. Validate exact commit messages with pinned commitlint before committing. The user permits automated approval of this release through the authenticated required-reviewer account; do not remove environment protections.

**Never:** Run local tests or a local test executable. Disable CI jobs, filters or security rules to hide failures. Update dependencies or nested submodules. Print actual credentials. Change production dispatch or rebuild behavior merely to satisfy substitutes. Reuse or overwrite a published release version. The implementation agent must not stage, commit, push, dispatch workflows or approve deployments; the parent owns remote publication.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Versioned named delivery | Substitute representing an ordinary handler | Explicitly enables delivery; original upcast assertions are retained | Runtime capability denial remains intact |
| Rebuild delivery | Two named handler substitutes matching issued metadata | Fingerprint agrees with admitted ordinary routes; existing convergence and lifecycle assertions remain | Existing injected store, handler and fence failures remain covered |
| Gateway credential acquisition | Required configured credentials and a valid JSON token | Same form, bearer and caching behavior using explicit dynamic reads | Empty token, wrong type, expired token and rejected renewal retain their behavior |
| Gateway fixture security | Fresh generated password, client secret and access tokens | Source contains no usable literal credential; assertions compare against exact per-handler values | Rejected response must never reuse an expired bearer |
| Publication | Reviewed source pushed to main | Successful CI and release; all 14 manifest IDs published at one version | Report actual failures and source drift; never invent passing evidence |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/sprint-status.yaml`: restore only the two guarded comment ranges deleted in `77514130b2a863537991c4cf6b554fccd599ef04`, using its parent version; preserve every current YAML value and date.

- `tests/Hexalith.EventStore.DomainService.Tests/VersionedProjectionDispatchTests.cs`: `DispatchAsync_RenamedV1EventReachesNamedHandlerWithCurrentPayload` lacks `OrdinaryDeliveryEnabled.Returns(true)`. NSubstitute returns false instead of invoking the interface default implementation.
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionHarness.cs`: `CreateNamedHandler` has the same missing property configuration; catalog and dispatcher compute inconsistent ordinary-route fingerprints.
- `src/Hexalith.EventStore.DomainService/GatewayAuthorityAccessTokenProvider.cs`: acquire ClientSecret with existing `Required`; parse token string into an ordinary local, check nonempty, then assign the cache. Preserve validation and deadlines.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreGatewayAuthorityTransportTests.cs`: handler variables called token, literal bearer values and literal realm credentials trigger the scanner. Rename HTTP-handler references, generate secrets per handler, record issued values and retain exact request/form/renewal assertions.
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`: authoritative scanner; inspect but do not edit or add exemptions.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: parent-owned remote publication and fourteen-package inventory.

## Tasks & Acceptance

**Execution:**

- [x] Restore the two guarded sprint comment ranges from their exact historical source, without changing any YAML values.

- [x] Configure ordinary delivery on both affected projection substitutes.
- [x] Make gateway provider dynamic reads recognizable without changing token validation or credential requirements.
- [x] Generate authority transport credentials and access tokens at runtime; preserve exact cached-token and rejected-renewal assertions.
- [x] Inspect the diff and compile affected projects without executing tests. Report any local build blocker exactly; the parent may verify compilation through GitHub instead.
- [x] Parent: review, validate the commit message, commit/push and monitor exact-source CI.
- [x] Parent: dispatch Release, automate its required-reviewer approval, verify tag/source and all fourteen GitHub and NuGet packages.

**Acceptance Criteria:**

- Given the revised substitutes, when GitHub runs existing projection suites, then delivery and rebuild proofs pass without assertion removal or runtime admission changes.
- Given fresh authority fixtures, when transport tests and the unchanged scanner run in GitHub, then credentials are generated, expected requests remain precise and scanning passes.
- Given a released version, when each manifest package is downloaded from NuGet, then its ID, version and repository commit match the release source and corresponding GitHub asset.

## Implementation Notes

Parent initiated the repository-required AppHost baseline through `aspire start` before implementation. Local tests remain prohibited; GitHub runs the configured CI suites on push. User authorization supersedes interactive skill approval checkpoints.


Both affected test projects compiled with SDK 10.0.401 in Debug source mode, zero warnings/errors; no local tests executed. The exact form comparison was also recompiled successfully. Logs are retained at `/tmp/eventstore-ci-repair-domain-build.log`, `/tmp/eventstore-ci-repair-server-build.log` and `/tmp/eventstore-ci-repair-domain-form-build.log`. AppHost startup did not become ready and was cancelled through its owning session, followed by exact-target `aspire stop`.

GitHub run 38585796898 additionally exposed three Contracts failures caused by two guarded sprint comment blocks removed before this repair. The blocks were restored from the exact parent of that deletion; a comparison of every non-comment YAML line proved all current status values and dates were preserved.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence and disposition |
|---|---|---|---|
| BH-1 invalid-token coverage | low | reject | The refactor preserves the old missing/non-string/blank-token denial and never reaches cache assignment on invalid input. The suggested new fixture branches address pre-existing coverage, with no demonstrated changed outcome. |
| BH-2 exact encoded form | medium | patch | Substring assertions accept suffixes/duplicate fields. Changed to exact complete encoded-form comparison, including username and generated credentials; compilation passed. |
| BH-3 cached-to-expired transition | low | reject | The one-second fixture deliberately exercises an already expired token under the existing fifteen-second skew. The separate happy case verifies cache reuse; controlled-time infrastructure adds complexity for unchanged behavior. |
| BH-4 container acceptance wording | low | reject | The finding proposes editing this build's spec. Release already requires container publication and smoke checks; the parent observes that workflow and its evidence before completion. |
| BH-5 recovery wording | low | reject | The finding proposes editing this build's spec for a hypothetical publication failure. Existing publisher preflight/collision controls and the no-reuse constraint remain unchanged. |

Edge-case review returned no actionable findings; verification-gap review returned no gaps. User authorization permits continuation without a human checkpoint.

## Verification

- Inspect `git diff --check` and the complete repair diff.
- Compile affected test projects with .NET 10.0.401; do not invoke tests.
- Parent validates full commit message using `node_modules/.bin/commitlint --edit` and retains passing output in a temporary evidence file.
- Parent monitors pushed-source CI and Commitlint, Release and the production approval endpoint, then downloads all manifest NuGet packages and compares package identity, version, repository commit and payloads with GitHub release assets.

## Publication Outcome

Release v3.122.0 completed from `1dac529c4690910c89d4090784a7d46cb9433a9d`. All seven push workflows and Release succeeded; all fourteen NuGet packages were downloaded and matched their GitHub payloads, version and source identity. Production approval was automated through the authenticated required reviewer, while environment protections stayed intact. The workflow supported validation-bypass input was used; CI completed successfully in parallel. Both OCI platforms and smoke results passed. No local tests ran. See [verification evidence](evidence/ci-release-2026-10-14/README.md).

This final documentation-only completion record uses `[skip ci]` to avoid repeating configured CI suites after the released source has already passed.
