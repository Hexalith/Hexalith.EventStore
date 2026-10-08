---
title: 'Fix CI timestamp decay and publish the verified release'
type: 'bugfix'
created: '2026-09-04'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'fcafc59464efd2f97347a97f19a1d48ad340f10c'
context:
  - 'docs/ci.md'
  - '_bmad-output/implementation-artifacts/spec-postgres-image-governance.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI runs `33497282544` and `33864785022` fail only in Contracts because commit `f152995794337a929c0a1ec2242eff9a5a3a1c44` changed Story 4.15 validator/test dates without renewing content-bound evidence. Its fixed “future” timestamps also decay as the wall clock advances.

**Approach:** Restore approved chronology, make future-time tests deterministic without weakening UTC/order/hash/authority checks, and preserve completed evidence through an additive successor. Then push the repair, require green exact-source CI, run the protected release, and verify GitHub and NuGet outputs.

## Boundaries & Constraints

**Always:** Keep Story 4.15 v1/v2 historical artifacts verifiable; use an additive successor for evolved validator/test bytes; retain the reviewed PostgreSQL index, source-only authority, full Contracts lane, warnings-as-errors, 14-package inventory, exact-source proof, protected environment, immutable publisher pin, and collision/post-publish gates. Validate the exact commit message. Bind the release tag to the pushed SHA and validate both publication channels by ID, version, count, and package contract.

**Never:** Skip or soften OQ8 tests; rewrite historical evidence or published objects; change inventory, PostgreSQL identity, destinations, credentials, protection, or publisher pin; use release bypass when ordinary CI succeeds; reuse a partially published version.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved chronology | Historical closures plus the active successor | Contracts passes on later calendar dates | Fail on hash, schema, order, or authority drift |
| Future mutation | Timestamp after captured current UTC | Reject as later than current UTC | Assert the specific diagnostic |
| Publication | Green exact-source `main` | Stable release with 14 GitHub assets and 14 NuGet packages | Stop on denial, collision, missing output, or partial failure; never reuse the version |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py` -- date mismatch, UTC/future validation, binding, and successor selection.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- four expiring future mutations and successor coverage.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v2/**` -- completed historical closure; verify but do not rewrite.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/**` -- additive successor: identity, pre-review results, subject, receipts, handoff, manifest.
- `.github/workflows/ci.yml` -- exact failing Contracts restore/build/test commands; behavior is preserved.
- `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json` -- protected release and 14-package authority; verify, do not broaden.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-oq8-platform-evidence.py` -- parse exact UTC seconds generically, reject real future time, validate v2 historically, and require v3 for evolved bytes.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- use runtime-relative future values and cover v2 preservation plus v3 success/drift/future cases.
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/**` -- seal candidate → subject → reviews → handoff → sorted manifest without changing v1/v2.
- [x] `docs/ci.md` -- document the durable timestamp rule and active successor lineage if operator guidance changes.
- [ ] Git/GitHub/NuGet -- validate a `fix(ci): ...` message, commit/push `main`, require exact-source CI, dispatch ordinary Release, then verify tag/source, release assets, NuGet availability, and contents.

**Acceptance Criteria:**
- Given any later current date, when OQ8 closure and full Contracts run, then approved evidence passes and future/order/drift mutations fail for their intended reasons.
- Given the repair commit on live `main`, when blocking push workflows complete, then CI and Commitlint succeed for that exact SHA without bypass or excluded tests.
- Given successful ordinary publication, when GitHub and NuGet are queried, then a stable release targets the repair SHA and all 14 package IDs exist at one valid version.

## Approved Scope Extension — 2026-10-08

The user explicitly approved: “Yes, fix the current CI blockers and release.” This extends the existing goal to today's blocking CI failures while preserving all sealed evidence. The original frozen chronology and publication constraints still apply. The original baseline remains `fcafc59464efd2f97347a97f19a1d48ad340f10c`; current repair investigation began at `4cc77f9554395e84539e173e94b8f0b4df14d643`. Intervening committed work must not be reverted. Other sessions are actively changing this workspace: preserve every unrelated edit and do not stage, commit, push, or mutate remotes during implementation; the parent owns the authorized remote phase.

Current CI run `37740386611` on source `762a745426db66c2846b1af17a10b2a619bf3d95` fails Contracts and two shared test projects. Local reproduction logs are in `/tmp/eventstore-consumer-authority-tests-20261008.log`, `/tmp/eventstore-secrets-tests-20261008.log`, and `/tmp/eventstore-provider-tests-20261008.log`.

### Additional Code Map and Tasks

- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`: retain central package authority for executable consumers while recognizing the exact archived, identity-only planning restore surface under `evidence/6-1-p1r-31150-published-run/preflight/package-observation/`. The five version entries in its `Directory.Packages.props` are the current failure. Preserve every archived byte and use exact-file exemptions, never a directory/glob. Include positive coverage of this evidence and negative coverage of a new adjacent executable override in tracked and untracked fixture discovery.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`: fix confirmed structural false positives without admitting usable credentials. Failures include public NuGet `Microsoft.IdentityModel.Tokens`/`JsonWebTokens` records in lock/assets graphs and an embedded candidate graph, structural `null` fields in captured JSON, and a C# `new CancellationToken(true)` expression. Keep quoted credential literals, bearer literals including `null`, keys, JWTs, and private keys rejected. Add meaningful positive/negative controls for the revised classifications.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs` and `_bmad-output/implementation-artifacts/evidence/story-5-3-retired-captures.json`: independently inspect the OTLP API-key findings in the historical `story-6-6/json-replay-admission-2026-10-07/aspire-retry-describe.log` without echoing credential values. Preserve immutable raw evidence. If these are actual historical credentials, retire only this exact capture through an additive non-authorizing, exact-hash-bound record and tests proving changed bytes and adjacent files cannot inherit retirement; do not blanket-exempt logs or create current evidence authority. If they are inert values, fix only the demonstrated classification instead.
- [x] `tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs` and relevant `ProviderVerification.Tests` files: determine and fix why the matching per-run credential yields `interaction.contract-failed` in `RealKestrelPactTests.ProductionPipeline_AcceptedPactRequiresMatchingPerRunCredential`. Keep the production HTTP/authentication/validation pipeline and unchanged FrontComposer pact; use only bounded provider-state test dependencies at legitimate external-service seams. Preserve the wrong-credential rejection and cleanup assertions. Do not weaken runtime behavior or revise the expected accepted contract to match the failure.

Additional acceptance: Given the current source and recorded evidence, when the exact Contracts CI lane and shared deterministic test lanes run, then they pass without weakening package governance, secret protection, OQ8 authority, or the provider contract. Given an adjacent executable package override, a usable secret, altered retired bytes, or an unmatched provider credential, when the relevant checks run, then the intended rejection remains observable.

Implementation must not change any sealed OQ8 v1–v5 packet or gate-bound input. The active validator passes today, and no OQ8 source evolution is needed for these repairs. Prefer the existing precise classification/exemption mechanisms over new broad suppression. Run the affected test projects and report exact commands and counts. The parent will run wider verification, review the repair, validate the exact Git message, commit only task-owned files, push under the repository's actual branch rules, require green ordinary exact-source CI/Commitlint, and dispatch/verify the protected release.


### Current Integration — 2026-10-08, upstream `3600d196`

These instructions supersede the provisional five-file implementation and review-loop requirements above. Another session independently landed `d87c969b` and `3600d196799b7bbc5398a6d126e918171f07a2d3` while this build was running. Work from the isolated owning checkout `/tmp/eventstore-ci-release-20261008`, whose verification base is now `b830d9829af70536d2a3fd21c5e2a23b2ca2f256`; use its repository guidance and recorded root submodules. Do not modify the shared primary checkout. The original `baseline_commit` stays unchanged. Preserve every newly landed test and independent improvement unless it directly conflicts with the specific repair below. No Git/remote mutations or dependency changes during implementation.

The landed source already fixes package observations with exact-file sealed-content checks, JSON null/NuGet metadata with a single structural parse and public package identities, and accepted provider authentication through a harness policy override. It also redacts the runtime capture and records original/sanitized SHA-256 values. Preserve those current capture/manifest/sanitization bytes. The original capture remains in immutable Git object `4cc77f9554395e84539e173e94b8f0b4df14d643:<capture path>` with hash `920af53b3660885236289c92e78c8841a64b1f0df978a8e35d8b53dab991e2e7`; the parent verifies this without displaying credentials. Do not restore credentials or add the now-obsolete current-path retirement record. No sealed OQ8 packet or gate-bound input changes are permitted.

Integrate only the remaining useful corrections in these four files:

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`: KEEP the upstream exact exclusions, SHA256SUMS consistency check, and all adjacent executable/props cases. Validate the bytes from the actual validator `root` (including fixture roots), rather than only from `repositoryRoot`, before granting each new planning exemption. Include positive tracked/untracked LF/CRLF fixtures and same-path changed-file rejection. No additional archive exclusions are needed. Preserve all three archived planning files unchanged; do not edit their contents or seals.
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`: KEEP the upstream single-pass JSON metadata implementation, library/package identity/hash schema checks, strict known public package names, all existing controls, and exact-hash XML capture exemption. Add only precise C# boolean `CancellationToken` constructor recognition for `.cs`/`.razor`, simple/full/global-qualified names and optional `canceled:` argument, with unsafe operand/other-type/adjacent-literal negatives. Retain the known NuGet grammar while directly rejecting malformed/empty/reversed intervals in the existing private helper; add relevant controls without introducing a second metadata parser or dependencies. Do not add a new XML classifier or retirement logic: those provisional implementations are superseded, and the landed exact-hash exemption already prevents changed output from inheriting it.
- `tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs`: replace only the newly landed `PostConfigure<AuthorizationOptions>` default-policy override with `PostConfigure<JwtBearerOptions>` named Bearer forwarding to the existing bounded per-run handler. This retains the unchanged production default authorization policy and substitutes authentication at its existing external fixture seam.
- `tests/Hexalith.EventStore.ProviderVerification.Tests/RealKestrelPactTests.cs`: integrate the stable real HTTP matching/wrong-credential preprobe, message/correlation/location/Retry-After assertions, cleanup/state events, and immutable pact hash assertion. Keep the original accepted/wrong credential pact expectations and port cleanup.

The stable provisional implementation is available as ordinary task source (not a skill) in `/tmp/eventstore-ci-release-stable-pre-integration/` for the four matching paths; use its pertinent provider assertions and test controls as a reference, while preserving the landed algorithms and tests. The previous re-derivation passed package governance 42/42, scanner 95/95, Server 3,952 plus 25 existing skips, and real HTTP/Pact 3/3. Its full Provider result was 82/83 solely because another session's AppHost edit failed runtime provenance; the current isolated checkout removes that interference.

Acceptance remains green ordinary exact-source Contracts/shared lanes, unchanged central authority and production auth policy, retained rejection of adjacent/same-path overrides and usable credentials, wrong-key rejection and cleanup, verified immutable historical Git evidence, and protected publication of the same 14 packages. Run affected Release/package builds and focused classes; the parent owns full clean-checkout CI, independent review, Git/PR merge, exact-source push gates, and protected release.


## Implementation Notes

- Current four-file integration preserves the landed structural scanner, exact archived-file seals, and current capture redaction. It checks the actual scanned root, rejects malformed NuGet intervals, recognizes exact cancellation constructors, and forwards named Bearer authentication while retaining production authorization policy. Direct HTTP/Pact assertions cover response identity, headers, matching/wrong keys, state and port cleanup, and immutable pact bytes.
- Focused integration checks on source `3600d196` passed: package governance 45/45, scanner 100/100, real HTTP/Pact 3/3; all three affected Release/package builds had zero warnings/errors. Parent verification now uses source `b830d982` and its exact recorded dependency commits. The original and sanitized runtime capture hashes were independently verified; sealed OQ8 remains unchanged and passes.

- Preserved the v1/v2 evidence bytes and bound historical v2 validation to completed closure commit `83b32fcfad7bb608098aebccdc15002636ffb431`; v3 carried the original repair; the subsequently landed v5 is the current selected successor.
- Aligned `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs` with the authoritative shared-policy wording introduced when root commit `fcafc59464efd2f97347a97f19a1d48ad340f10c` updated `references/Hexalith.AI.Tools` to `5f93d2ec8239494852c97032c819cb1689939e36`. The shared instruction changes themselves were preserved.
- Focused implementation, full clean-checkout tier-1 verification, and exact hosted Contracts verification are complete; final-source publication remains open. The Git/GitHub/NuGet task remains open because push, workflow dispatch, and publication require the post-review remote phase.

## Spec Change Log

- 2026-10-08 current-base verification: Rebased the isolated source onto `b830d982`, preserving its newly committed SDK/dependency/AppHost evidence and identical prior spec/ledger snapshots. Removed superseded provisional implementation instructions; final repair remains the four named C# files. KEEP all logged preservation constraints and the original baseline.
- 2026-10-08 concurrent integration: Main advanced to `3600d196` with overlapping repairs and a documented current-capture redaction. Preserve that committed work and the immutable original Git blob; remove the obsolete provisional retirement plan. Reuse the landed structural scanner and sealed exclusions, strengthen actual-root binding, preserve production authorization policy through named Bearer forwarding, and retain real HTTP/Pact controls. KEEP all sealed OQ8 evidence, original baseline, upstream tests, package inventory, release protections, and unrelated edits.
- 2026-10-08 review loop 1: Required canonical-byte binding for the new archive exclusions and retired LF log; replaced loose JSON/XML ancestry and npm version assumptions with explicit demonstrated NuGet/xUnit structures; required one null-token parse per content. This avoids same-path executable exemption, lookalike metadata suppression, valid NuGet false positives, and quadratic scans. KEEP all previously passing provider behavior, adjacent override controls, non-authorizing retirement, sealed evidence, original baseline, and unrelated work.
- 2026-10-08: The user approved extending the resumed timestamp-and-release goal to current package-governance, secret-scan, and provider-contract CI blockers. Added their exact code paths, rejection controls, and preserved-evidence constraints. KEEP the landed timestamp fix, immutable OQ8 lineage, protected ordinary release, 14-package inventory, immutable publisher pin, and all unrelated session changes.

## Review Triage Log

| Reviewer | Finding | Verdict / route | Evidence |
| --- | --- | --- | --- |
| blind-hunter 1 | npm lifecycle scripts run before signature audit | medium / defer | `release.yml` executes `npm ci` before `npm audit signatures`; npm lifecycle execution adds privileged dependency-code execution before provenance verification. This was introduced by intervening trusted-publishing work, not this resumed repair. |
| blind-hunter 2 | npx can install a missing release executable | maybe-false / defer | The command permits implicit installation, but successful locked `npm ci` supplies semantic-release in the normal path. A reachable missing-executable state and its credential-bearing invocation would settle the unverified medium risk. |
| blind-hunter 3 | standalone workflows do not block release | false / reject | The ordinary gate intentionally selects `ci.yml`; CI itself includes the compiled event-evolution lane, while local mutation guards and P1R preparation explicitly grant no production authority. The spec requires CI and Commitlint, not every separately triggered workflow. |
| blind-hunter 4 | heavyweight container tests lack hosted replacement | medium / defer | Contracts excludes two HeavyweightContainerPublish methods; no replacement hosted lane was found. This is a prior committed CI policy, with no OQ8 exclusion introduced by this repair. |
| blind-hunter 5 | local-guard CI uses Debug and source references | medium / defer | `event-evolution-local-guards.yml` restores Debug with `UseHexalithProjectReferences=true`, contrary to the shared CI baseline. This belongs to intervening event-evolution work. |
| blind-hunter 6 | consumer probes have no subprocess deadline | medium / defer | `validate-consumer-package-references.py` invokes subprocesses without a timeout before shared test shards; a stalled probe blocks those shards until the job timeout. This predates the resumed repair. |
| blind-hunter 7 | compiled-consumer logs disappear with runner | medium / defer | The CI compiled-consumer job writes to runner.temp and has no upload step. A failure can leave only a reference to an unavailable diagnostic file. This is intervening event-evolution work. |
| blind-hunter 8 | compiled-consumer timeout omits partial log | medium / defer | Its run helper writes captured output only after subprocess completion, so TimeoutExpired skips the write. This is intervening event-evolution work. |
| blind-hunter 9 | timed-out helper can leave descendants | maybe-false / defer | The replay-routing helper bounds only its immediate process. A process-tree reproduction showing surviving compiler/runtime descendants would settle the unverified medium claim; no such reproduction was supplied. |
| blind-hunter 10 | release assertions do not parse critical YAML fields | medium / defer | TrustedPublishingReleaseTests uses substring and ordering checks for protected fields; comments or misplaced fields could satisfy them. This is a verification weakness in intervening publishing work. |
| verification-gap 1 | local publication freeze has no execution coverage | medium / defer | EventStore tests do not execute its relocated freeze script; Builds tests execute a different workflow. An inverted comparator would authorize an unset/false flag without that coverage noticing. |

The edge-case reviewer returned no findings. Review used the required original-baseline diff (234,216,750 bytes, 6,276 paths); all reported executable changes above are intervening committed work. Current reproduced CI blockers are explicitly authorized by the 2026-10-08 scope extension and will be repaired and reviewed separately within this same build.

| repair blind 1 | retired LF log hash fails after CRLF checkout | medium / patch (moot on loopback) | The capture lacks an LF attribute and simulated CRLF changes its SHA-256. Canonical CRLF-pair normalization preserves Git content; `.gitattributes` is gate-bound and must remain unchanged. |
| repair blind 2 | changed bytes at archived exempt paths remain excluded | medium / bad_spec | The validator passes every exact path directly to ExcludedPath without checking archived identity. A same-path executable or changed pin inherits the exclusion, contrary to preserved identity-only evidence. |
| repair blind 3 | arbitrary nested targets lookalike qualifies | medium / bad_spec | The helper uses `parents.Contains("targets")`; the reflection probe demonstrates exemption beneath unrelated JSON. Require the recorded graph ancestry. |
| repair blind 4 | valid NuGet build/four-component package version fails | medium / bad_spec | The concrete three-component package-parent regex rejects valid NuGet identities. A build-metadata reflection fixture produces a credential finding. |
| repair blind 5 | npm range grammar rejects NuGet intervals and accepts npm prefixes | medium / bad_spec | The helper reuses PackageVersionRangePattern: `[8.23.0,)` fails and npm prefixes qualify. NuGet metadata classification must use its own supported grammar. |
| repair blind 6 | fake test in CDATA receives metadata exemption | medium / bad_spec | The helper parses a fabricated self-closing element from a substring; a reflection fixture confirms that output text qualifies. Complete-document XML parsing must establish the attribute. |
| repair blind 7 | unrelated test names containing known method qualify | medium / bad_spec | Contains does not bind declaring class or exact method identity. The different-class reflection fixture is exempt, although only known rejection-test metadata is intended. |
| repair blind 8 | second known endpoint exemption lacks focused controls | medium / bad_spec | The added test exercises only identity.example.test. The tokens.example.test/oauth/token branch can lose its method/URI guard without a focused test detecting it. |
| repair blind 9 | exact cancellation constructor still fails in Razor | low / patch (moot on loopback) | The scanner treats Razor as C# elsewhere, but the new recognition is `.cs` only. The reflection fixture confirms a false credential report for the same boolean constructor. |
| repair blind 10 | JSON null scanning grows quadratically | medium / patch (moot on loopback) | The helper parses the entire document for each match. The supplied 100/300/1000-field probes show roughly 10/53/546 ms; parse/token offsets once per content. |
| repair edge 1 | many structural nulls repeatedly parse full document | medium / patch (moot on loopback) | Independent tracing identifies the same full-parse-per-match root cause; the blind review's executed scaling probes confirm it. |

The repair verification-gap reviewer returned no findings. All second-round findings were classified separately before grouping. Archive binding, NuGet grammar/ancestry, and XML metadata identity require a non-frozen specification correction and re-derivation; simple patches are retained as requirements for that loopback.

The final current-source review covered all four repair files and their surrounding helpers and tests. The original-baseline diff is now 262,463,576 bytes across 6,392 paths; reviewers traced CI/release context but did not claim an exhaustive review of every intervening committed file. A supplementary 22,386-byte repair diff isolated the new work. The verification-gap follow-up that accidentally received claims was discarded and replaced by a context-free reviewer, who found no repair gaps.

| Reviewer | Finding | Verdict / route | Evidence |
| --- | --- | --- | --- |
| final blind 1 | scalar NuGet grammar admits malformed labels and overflowing components | medium / defer | `IsNuGetVersion` and its regex are unchanged from upstream `b830d982`; malformed scalar examples still satisfy the metadata helper. The [NuGet parser](https://raw.githubusercontent.com/NuGet/NuGet.Client/dev/src/NuGet.Core/NuGet.Versioning/NuGetVersionFactory.cs) validates labels and numeric bounds. This is a pre-existing classification gap, not caused by the new interval-order check. |
| final blind 2 | named-argument stripping removes ternary runtime operand | medium / patch | In `new AccessToken(value: true ? sourceToken : null)`, the new regex removes both `value:` and `sourceToken:`; the runtime-expression branch then sees only literals and reports a false credential. Patched removal at argument-label boundaries; C#/Razor ternary references and boolean-only negatives pass all eight focused cases, and the full scanner passes 102/102. |
| final blind 3 | heavyweight container tests lack hosted replacement | medium / defer | carried: the same `ci.yml` exclusion and two container tests remain as previously logged; do not defer again. The local full Contracts run includes them. |
| final blind 4 | separate local mutation workflow does not block release | false / reject | carried: ordinary release selects `ci.yml`, which contains the authoritative compiled-consumer lane; local mutation guards grant no production authority. The previously checked contract is unchanged. |
| final blind 5 | local mutation CI uses Debug and source references | medium / defer | carried: the same workflow and invoked scripts retain the previously logged build mode; do not defer again. |
| final blind 6 | compiled-consumer logs disappear with runner | medium / defer | carried: the same runner.temp output has no artifact upload; do not defer again. |
| final blind 7 | compiled-consumer timeout loses captured output | medium / defer | carried: the same subprocess helper writes logs only on normal completion; do not defer again. |
| final blind 8 | AppHost smoke token request has no deadline | medium / defer | `request_apphost_smoke_token` invokes Aspire without `timeout`, unlike topology discovery; a stalled connection can hold the operator's smoke command indefinitely. This unchanged earlier committed script is outside the current-source repair. |
| final blind 9 | loopback smoke curl can use an intercepting HTTPS proxy | medium / defer | The unchanged curl options use `-k` without `--noproxy`; HTTPS proxy configuration without a loopback exclusion can route credential-bearing local requests through that proxy. The demonstrated configuration is a pre-existing operator-tool gap. |
| final blind 10 | package-consumer subprocesses have no deadline | medium / defer | carried: the same consumer validator subprocess path remains unbounded; do not defer again. |
| final blind 11 | release step-order assertion accepts missing first step | medium / defer | The unchanged `IndexOf` ordering assertion accepts `-1` when the revalidation step name is deleted; no presence assertion covers that name. This distinct deletion-regression gap predates the four-file repair. |
| final edge 1 | CI excludes two container tests despite acceptance wording | medium / reject | The existing CI exclusion is real and already logged separately; the full local Contracts lane covers those tests. This claim proposes only reconciling this build's spec, which the review workflow explicitly rejects; no source change is authorized by that finding. |
| final verification-gap 1 | publication freeze has no behavioral check | medium / defer | carried: the same relocated EventStore freeze block lacks execution coverage while Builds tests execute a different workflow; do not defer again. |

The context-free replacement verification-gap reviewer found no additional gap in the four-file repair. All final findings were classified separately after all layers reported, before grouping. The ternary regression is the only current-change patch; no bad-spec or intent loopback is required.

## Design Notes

The validator and closure test are content-bound, so their evolution belongs in v3 rather than hidden inside v2. The manifest binds exact approved timestamps; generic parsing plus comparison with current UTC avoids encoding “today” in source.

## Verification

Historical timestamp-repair results: focused OQ8 375/375 and Contracts 1,896/1,896 passed on the earlier source. These are preserved historical results, not the current verification counts.

Current-source results on `b830d982` plus the four-file repair: active v5 and historical v1/v2 validators pass; focused package governance 45/45, scanner 102/102 after the ternary patch, and real HTTP/Pact 3/3 pass. Clean Release build has zero warnings/errors. Full Contracts passes 2,290 with two package-inventory skips, including both heavyweight container-publication tests. Tier 1 passes all 20 projects plus payload-protection lanes: 12,502 passed, zero failed, 53 existing/package-inventory skips. Server passes 3,959 with 25 existing skips; Provider passes 83/83. The exact hosted Contracts restore/build/test command passes 2,286 with two package-inventory skips; its Release build has zero warnings/errors. Final exact-source push CI and publication remain pending. The initial exported dependency-mode flag interfered with intentional matrix fixtures and was removed before the successful full Contracts run.

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: historical v1/v2 and active v5 current-source closure pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all closure cases pass.
- Exact `.github/workflows/ci.yml` Contracts command, then `./scripts/ci-local.sh --tier 1` -- expected: local gates pass.
- `npx commitlint --edit <candidate-message-file> --verbose` and `git diff --check` -- expected: exact message and diff pass.
- `gh run watch <exact-source-ci-run> --repo Hexalith/Hexalith.EventStore --exit-status` -- expected: successful push CI for live `main`.
- `gh workflow run release.yml --repo Hexalith/Hexalith.EventStore --ref main -f bypass-validation=false` and `gh run watch <release-run> --exit-status` -- expected: protected ordinary release succeeds.
- GitHub tag/release inspection and `python3 tools/validate-release-packages.py <assets> <version>` -- expected: repair SHA and 14 valid assets.
- NuGet flat-container download plus the same validator -- expected: all 14 public packages exist and validate.
