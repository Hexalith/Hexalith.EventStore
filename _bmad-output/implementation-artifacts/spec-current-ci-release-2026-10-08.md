---
title: 'Repair current main CI and publish a verified release'
type: 'bugfix'
created: '2026-10-08'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4cc77f9554395e84539e173e94b8f0b4df14d643'
context:
  - 'docs/ci.md'
  - '_bmad-output/implementation-artifacts/spec-gh-33497282544-fix-ci-cd-and-release.md'
---

<frozen-after-approval reason="User selected fixing current main and publishing a new release on 2026-10-08">

## Intent

**Problem:** The original timestamp repair shipped as v3.102.0. Current main has three later blocking CI failures: archived package observations are treated as live package-version overrides, the secrets guard reports NuGet metadata and captured diagnostics, and the real Kestrel Pact acceptance test fails with its matching per-run credential.

**Approach:** Fix the current guard and test-harness incompatibilities while keeping real credentials and live dependency overrides rejected. Preserve historical evidence bytes, add an explicit non-authorizing retirement for any credential-bearing immutable capture, and verify the full affected lanes. Then commit/push the repair to main, require successful exact-source CI and Commitlint, dispatch ordinary protected Release, and validate GitHub and NuGet publication.

## Boundaries & Constraints

**Always:** Preserve unrelated workspace edits, every historical evidence byte and OQ8 predecessor, UTC/order/hash checks, full blocking test lanes, warnings-as-errors, current 14-package inventory, publisher pin, protected production environment, destinations, collision and post-publication gates. Preserve positive and negative authentication coverage. Validate the exact full commit message before committing. Bind the release tag, source, IDs and contents to one version and SHA. Use an isolated checkout for implementation because the shared workspace has concurrent edits.

**Never:** Bypass CI; exclude failing tests; invent passing evidence; rewrite published objects or frozen evidence; reuse partially published versions; update dependencies, credentials, inventory or protection. A capture containing credentials can be retired only by an exact path and hash with authoritative=false; its preserved bytes remain provenance only, and no broad evidence-directory exemption is permitted.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Historical package observation | Exact archived MSBuild file outside the live build graph | Pass inventory guard without changing the capture | Reject new sibling overrides and changed exempt bytes |
| Public NuGet metadata | Dependency package IDs and versions in lock/assets JSON | Pass secret guard after structural classification | Reject actual credentials in the same document and malformed metadata |
| Captured diagnostics | Genuine bare JSON null or escaped inert source strings; hash-bound retired OTLP capture | False positives pass; exact retired bytes remain non-authorizing | Quoted null, usable literals, altered capture and near-miss paths fail |
| Pact authentication | Per-run accepted credential vs different credential | Real command pipeline fulfills accepted Pact only with matching credential | Keep mismatch denial and bounded host/process cleanup |
| Publication | Exact green main SHA | New stable release with 14 valid GitHub assets and 14 NuGet packages | Stop on protection denial, collision or partial publication; never reuse version |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`: exact hash-bound archived `Directory.Packages.props` and companion `Identity.csproj` exemptions, with drift and sibling override regression controls.
- `.gitattributes`: preserve LF checkout bytes for those exact observations and the retired Aspire capture so their hashes are stable across platforms.
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`: structured NuGet metadata, bare null, captured source/report interpretation, and exact hash-bound retirement enforcement.
- `_bmad-output/implementation-artifacts/evidence/story-5-3-retired-captures.json`: additive retirement of the immutable `story-6-6/json-replay-admission-2026-10-07/aspire-retry-describe.log` capture; do not change that log or its existing manifest.
- `tests/Hexalith.EventStore.ProviderVerification/ProviderVerificationHost.cs` and focused test helpers: investigate the real response mismatch and repair only the verification harness to supply current pipeline prerequisites; retain consumer Pact bytes and runtime security behavior.
- `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.releaserc.json`, `tools/release-packages.json`: preserve publication and test contracts; use them for remote verification.

## Tasks & Acceptance

- [x] Guard the exact archived package-observation exemption with a content hash and negative controls for changes and new sibling overrides.
- [x] Correct secret-scanner structural false positives with positive/negative controls; add exact hash-verified retirement without rewriting historical artifacts or skipping arbitrary reports/logs.
- [x] Reproduce and repair matching-credential Pact acceptance in the test harness, preserving mismatch denial; run the full ProviderVerification test project.
- [x] Run full Contracts and Server suites, OQ8 evidence validator, and a Release solution build. Add a current-source verification record under `evidence/current-ci-release-2026-10-08/` containing commands, results and any blockers, without credential values.
- [ ] Independently review the current repair, resolve findings, validate a full `fix(ci): ...` message, commit/push a review branch and merge its required squash PR, then require exact merged-main blocking CI and Commitlint success.
- [ ] Dispatch ordinary protected Release, verify tag/source and all 14 GitHub and NuGet packages at one stable version, and record remote run identities and hashes. Do not claim completion while a gate or publication check remains open.

**Acceptance Criteria:**
- Given preserved evidence, when affected tests run, then legitimate archived/structural data passes and real-secret, drift, override and authentication-negative controls reject for their intended reasons.
- Given the repair on live main, when blocking push workflows complete, then CI and Commitlint succeed for the exact SHA with no bypass or test exclusion added.
- Given ordinary protected publication, when GitHub and NuGet are queried, then the tag targets the repaired source and all 14 manifest package IDs validate at one new stable version.

## Implementation Notes

The parent performs Git and remote publication after local implementation and review. Live main rules require a squash PR and seven status checks; use that route without bypassing protections. Publication ownership is awaiting the user's reply because another active session is editing the same repair in the shared checkout. The implementation subagent must not commit, push, dispatch workflows, or alter the original shared checkout. Existing broad review findings about v5 governance, release install/freeze behavior and unrelated scanner bypasses predate this continuation and are handled separately in the parent review ledger. Focus fixes on demonstrated current CI failures and preserve their negative controls.

## Spec Change Log

- 2026-10-08: The user explicitly selected “Fix current main and publish a new release” after historical v3.102.0 publication was verified. This continuation records a new exact baseline without rewriting the original spec's baseline or historical repair evidence.
- 2026-10-08: Full Contracts verification exposed the archived companion `Identity.csproj` as another evaluated historical consumer. Its exact preserved path/hash joins the observation exemption with tracked/untracked drift controls. Live GitHub main rules require a squash PR before the exact-main CI and publication gates.

## Review Triage Log

| Finding | Verdict | Route | Evidence and disposition |
|---|---|---|---|
| BH-C01 | high | patch | The parent reproduced zero violations for invalid NuGet ID api:token with a literal version-shaped value. The dependency-string branch validates its parent identity but never the matched dependency ID; apply the existing ID rule to that field. |
| BH-C02 | high | patch | The parent reproduced exemptions for array-valued targets and lock dependencies. Contexts discard object/array kind, allowing malformed structures to inherit credential exemptions; require object ancestors with targeted controls. |
| BH-C03 | medium | patch | The parent reproduced an exemption with contradictory duplicate schema/type fields. JsonDocument.TryGetProperty selects the last duplicate; reject duplicate properties before structural exemptions and retain malformed-input controls. |
| BH-C04 | medium | patch | The parent reproduced caret acceptance and valid NuGet range/build-metadata rejection. The reused npm regex causes both outcomes; separate NuGet identity/resolved-version and range validation without changing dependencies or npm policy. |
| BH-C05 | medium | patch | The parent reproduced exemption despite a matching libraries entry declaring project. Ordinary targets do not consult an available library declaration; reject conflicting/malformed declarations when present, preserving supported minimal metadata. |
| BH-C06 | low | reject | Each matching assignment reparses the document, so the synthetic 1,000/2,000 repeated IdentityModel dependency benchmark scales quadratically. Current full secrets verification takes about 12 seconds across the entire tracked repository; the benchmark shape is unusual in ordinary changes. A cache/index refactor adds complexity beyond a direct correction, so this performance optimization is rejected under the review low-severity rule. |
| BH-C07 | medium | patch | The verification record points complete output exclusively into temporary paths. Retain credential-free passing output and hashes under the new evidence directory so subsequent reviewers can inspect it after the temporary execution files disappear. |
| BH-C08 | low | patch | The historical package summary records hashes/content parity but omits verifier commands and download origins. Add that provenance and explicitly label signature inspection as presence only. No cryptographic signature-verification claim was made; the boolean field is accurate. |
| BH-C09 | false | reject | The baseline ledger already contains many flat source_spec/summary/evidence entries, and bmad-build explicitly requires that exact append format. The finding identifies no consuming rule or failed lifecycle operation caused by these additions; changing the mandated format is unsupported. |
| EC-C01 | medium | patch | The valid range/build-metadata rejection is independently reproduced by the parent and shares BH-C04's npm-versus-NuGet validation root cause. |
| EC-C02 | high | patch | The invalid dependency-ID exemption is independently reproduced by the parent and shares BH-C01's missing matched-ID validation. |
| EC-C03 | medium | patch | JsonDocument accepts an unpaired escaped surrogate, while Utf8JsonReader.GetString can throw InvalidOperationException. The helper catches only JsonException; return a violation for that demonstrated malformed input instead of aborting scanning. |
| EC-C04 | high | patch | The claims finding combines the confirmed valid-range false positive and invalid-ID exemption. Both are genuine new classifier outcomes; it shares BH-C04 and BH-C01 and requires code/test corrections rather than editing the claim. |
| VG-C01 | high | patch | Pre-verified regression gap: deleting the fixed OTLP pin leaves all 69 tests passing because byte and manifest mutations are tested separately. A coordinated changed capture plus matching changed manifest must still reject against the immutable original pin. |
| VG-C02 | medium | patch | Pre-verified regression gap: removing the SHA-512 content-hash predicate leaves all 69 tests green and exempts malformed lock metadata. Cover invalid Base64 and wrong decoded length for both supported lock schemas. |

All three review layers completed before triage. The confirmed patch groups and artifact provenance/retention findings are resolved. Parent reflection probes confirm malformed structures and unpaired Unicode produce findings, while legitimate NuGet ranges/build metadata pass. Coordinated re-pinning and bad content hashes have executable rejection controls. Focused secrets verification passed 129/129; post-review full Contracts, Server, ProviderVerification, OQ8 and the Release build all passed with the disclosed existing skips. No new finding requires a loopback or deferral. The separate upstream historical-capture rewrite conflicts with frozen intent and awaits the user's source/publication choice.

## Design Notes

Archived consumer files do not select live package versions. Metadata exemptions require structure; source excerpts require their actual source semantics rather than filename alone. Credential-bearing historical captures use the existing retirement model, with an exact path/hash and no active authority.

## Verification

- Release builds use `-warnaserror -m:1 -p:UseHexalithProjectReferences=false`.
- Build test projects individually, then run their built xUnit assemblies directly; focused filtering uses single-dash `-class` or `-method`.
- Run `python3 tools/validate-oq8-platform-evidence.py`, full Contracts, full Server and full ProviderVerification suites, plus the Release solution build.
- Preserve complete test output locally and report exact command, exit code, counts and failures. Do not print credential values.
- Parent remote gates: pinned commitlint preflight, exact-source push CI/Commitlint, ordinary `release.yml` dispatch with `bypass-validation=false`, tag/source identity, then both channels' 14-package contracts and nuspec source commits.

## Current Outcome

The isolated implementation is reviewed and locally verified. See `evidence/current-ci-release-2026-10-08/post-review-verification.json` and its retained output. Contracts ran sequentially after an unchanged Dapr process test twice hit its five-second deadline in parallel runs; all 2,281 cases finished with zero failures and two existing inventory-dependent skips. Server finished 4,011 cases with zero failures and 25 existing skips; ProviderVerification passed 83/83; OQ8 and warnings-as-errors Release build passed. The exact pinned commitlint candidate is validated but unused.

No commit, stage, push or Release dispatch has been performed by this session. The other session pushed a different green repair that sanitizes a historical capture, then advanced main again. Its first publication attempt left tag v3.116.0 and stopped before NuGet push due source drift; that version must remain untouched. A replacement release run is observed separately in `upstream-coordination.json`. The user is choosing whether to retain frozen capture bytes and publish this reviewed repair, accept the sanitized upstream source, or let the other session own publication. Keep the spec in review until that choice and final publication checks are complete.

- Observed publication outcome: the other session's ordinary protected Release run 37750172086 succeeded from b830d9829af70536d2a3fd21c5e2a23b2ca2f256 as v3.117.0. Its exact-source push CI and Commitlint passed; bypass-validation=false is verified in run logs. The tag matches source, and all 14 manifest packages validate in GitHub and NuGet with identical uncompressed contents except NuGet signatures and matching nuspec source commits. Publication verification is complete, while acceptance of the upstream historical-capture rewrite and disposition of this uncommitted reviewed alternative remain the user's pending choice.
