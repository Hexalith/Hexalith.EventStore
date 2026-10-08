---
title: 'Update Packages and Aspire to Latest Compatible Releases'
type: 'chore'
created: '2026-08-28'
updated: '2026-10-08'
status: 'done'
route: 'dispatch'
baseline_commit: '4cc77f9554395e84539e173e94b8f0b4df14d643'
builds_baseline_commit: 'af20682ac8fc420068a731ecb87cff84727a3d53'
review_loop_iteration: 0
context:
  - '_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The August upgrade is superseded. Live discovery on 2026-10-08 finds newer compatible candidates while EventStore uses Aspire 13.6.0.

**Approach:** Apply latest validated releases by coupled rollback group, including Aspire 13.6.1, then verify package-mode builds/tests and running topology. The user requested this fresh upgrade on 2026-10-08.

## Boundaries & Constraints

**Always:** Use only the Builds catalog. Preserve BOM/CRLF, previews, conditional pins, family alignment, history, auditing, warnings-as-errors, package-mode CI, and concurrent edits. Validate consumers before acceptance; retain failed families with evidence. Prepare an uncommitted patch before requesting Git authorization.

**Never:** Add PackageReference/wrapper versions; downgrade unresolved packages; alter tooling, global.json, unrelated APIs, or frozen evidence; initialize nested submodules; suppress gates; commit, publish, push, or edit external consumers without authorization.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure handling |
|---|---|---|---|
| Candidate | Listed compatible release | Advance complete dependency-constrained group | Retain whole group on failed validation |
| Aspire | 13.6.1 available | SDK/hosting 13.6.1; Keycloak/Kubernetes 13.6.1-preview.1.26506.6 | Roll back complete group on compile/runtime failure |
| Preview | Dapr preview remains latest channel | Retain 13.6.0-preview.1.261001-0243 and Folders' stable conditional | Never downgrade to older stable |
| Incomplete family | Three Parties rows unresolved | Retain complete Parties 1.1.1 family | Record recheck trigger |
| Compatibility | OpenApi 3.x conflicts with runtime gate | Retain latest proven 2.x | No validator weakening/API migration |
| Provenance | Changed catalog is uncommitted | Preserve authoritative audit; capture discovery separately | Finalization waits for catalog-commit authorization |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- 304 rows; property-backed and conditional declarations.
- `references/Hexalith.Builds/Tools/` -- audit generator, validator, and `package-version-audit.json` bind committed catalog/consumer bytes; changed uncommitted catalogs cannot finalize.
- `references/Hexalith.Builds/Tools/package-version-exceptions.json` -- ten expected SDKs equal hosting; verify actual consumers separately.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` -- SDK pin; preserve CLI bundle policy. Aspire library/AppHost tests provide compatibility checks.
- `docs/reference/nuget-packages.md`, `_bmad-output/project-context.md`, `_bmad-output/planning-artifacts/architecture.md` -- current version snapshots.
- `_bmad-output/implementation-artifacts/evidence/latest-packages-2026-10-08/` -- prior spec, discovery, baseline, decisions/results.

## Tasks & Acceptance

**Execution:**
- [x] Dated discovery evidence -- complete live 304-row/146-family audit, leaving authoritative audit intact.
- [x] Builds catalog -- validate/apply 23 candidates: Aspire 13.6.1/previews; FrontComposer 4.6.0; AngleSharp 1.8.4 with existing bUnit; coupled Swashbuckle/SwaggerUI 10.3.0; coupled Verify/Verify.XunitV3 33.3.2. Retain unresolved/incompatible families.
- [x] AppHost and exception inventory -- align SDK expectations; inspect actual consumers and FsCheck/xUnit/Dapr dependency floors.
- [x] Documentation/evidence -- record accepted pins, exceptions, exact checks, and blockers.
- [x] Authoritative audit -- finalize against committed catalog bytes; preserve history and record any owning-consumer classification limitation.

**Acceptance Criteria:**
- Given discovery, when decisions are recorded, then every row has candidates, selection/disposition, family/evidence, and exception trigger.
- Given accepted changes, when governance runs, then applicable gates pass and pre-existing failures are reported separately.
- Given aligned Aspire pins, when package-mode restore/build and focused tests run, then they pass and the evaluated hosting graph is aligned to 13.6.1.
- Given upgraded topology, when Aspire starts/describes it, then required resources are Running/Healthy without binary failures; stop afterward.
- Given committed catalog bytes, when audit regeneration/validation runs, then selections and provenance match. Without commit authorization, completion remains pending.

## Implementation Notes

- Compatibility-validated 21/23 candidate rows: Aspire 13.6.1 and aligned previews, FrontComposer 4.6.0, AngleSharp 1.8.4, and paired Swashbuckle 10.3.0. Catalog BOM/CRLF and Dapr/Folders conditional pins were preserved. Authoritative classification is recorded separately below.
- Retained Verify/Verify.XunitV3 together at 33.3.1: candidate 33.3.2 and retained 33.3.1 both fail the same unconfigured SponsorCheck SC021 consumer gate. Retained whole Parties 1.1.1 and OpenApi 2.12.2 with explicit recheck triggers.
- All 304 row decisions and 146 families, exact commands, dependencies, isolated version-free consumer sources, and six executed matrix checks are recorded in [dated evidence](evidence/latest-packages-2026-10-08/README.md).
- Final package-mode Release restore/build: zero warnings/errors. AppHost 145/145, Admin.UI/bUnit 1,082/1,082, focused Contracts governance 65/65, and isolated consumer 20/20 passed. Catalog, Folders, Dapr, exception, and documentation gates passed. The earlier configured consumer-authority run used 16 existing exact exclusions; the review follow-up derives the current 15 exclusions, verifies both sealed-file digests first, and passes for 82 projects. Concurrent exclusion edits were preserved.
- Final explicit Aspire startup/described topology: all 30 resources Running/Healthy; eight required services awaited, no binary load/member failures, inputs stable, and successful stop. Earlier Tenants source failure and exact 13.6.0 recheck are retained separately; external checkout movements were preserved and claims are bound to recorded current revisions/hashes.
- Actual external FrontComposer, Memories, and Tenants SDKs remain at 13.6.0; six inventory owners are unavailable here. Only EventStore's actual SDK was changed. Inventory expectations are separate from external consumer acceptance.
- An external commit incorporated the tested catalog/inventory bytes at Builds `520abb5898ad44b30c0744e707b53cd94741e6b1`. Authoritative regeneration and default-path validation now pass for all 304 packages/146 families; selections/provenance match and all prior history is preserved. After local installation/validation, external activity committed the exact audit bytes at observed Builds `ad52c5bdd4361c59eedf12a16620150006403584`. The earlier dirty-catalog rejection at `5dd29d2c596c1e93c63977b098ad61407d20560a` remains historical evidence.
- Aspire and FrontComposer have authoritative accepted classifications. AngleSharp/bUnit and both Swashbuckle families retain their current externally committed selections with explicit compatibility evidence and retained classifications: owning-Builds committed direct-consumer discovery has no representative for those families. No consumer provenance was fabricated or validator weakened. No commit, publication, push, external consumer edit, or nested submodule initialization was performed by this task.

## Spec Change Log

- August history remains in the dated evidence's `prior-august-spec.md`.
- 2026-10-08: User requested latest releases. Rebased on current tree/live evidence. KEEP: authority, previews, atomic rollback, compile/runtime proof, no implicit commits.

## Verification

- Builds catalog, Dapr, exception, and consumer-authority validators; authoritative audit validator after authorized catalog commit.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false`; `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 -p:UseHexalithProjectReferences=false`.
- Individual AppHost, Admin.UI/bUnit, focused Contracts governance tests, and isolated consumers of unused changed packages.
- Explicit AppHost path with `aspire start --isolated --non-interactive`, `aspire wait eventstore`, `aspire describe --format Json`, `aspire stop`; `bash scripts/check-doc-versions.sh`.

**Baseline:** Restore/build: zero warnings/errors; AppHost: 145/145; required runtime services: Healthy. Contracts: 2,272 passed, two skipped, one existing failure. Consumer authority rejects five PackageVersion rows in the newer `6-1-p1r-31150-published-run/preflight/package-observation/Directory.Packages.props` evidence fixture; preserve its bytes and the gate.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-1 | medium | patch | The current overview still lists Aspire 13.6.0/older previews and SwaggerUI 10.2.3. Correct those version cells; EH-2 shares this cause. |
| BH-2 | medium | patch | The packet hashes raw JSON/text bytes and has no dedicated Git attributes. Preserve original captured bytes across autocrlf checkouts. |
| BH-3 | false | reject | sdk-consumer-inventory.json explicitly labels three external-drift and six not-present owners; the spec notes distinguish inventory consistency from external acceptance. External edits are forbidden by the authorized intent, so no global rollout success was claimed. |
| BH-4 | maybe-false | defer | Assembly loading and local topology proof do not establish Azure service/publishing behavior, but no failing supported consumer path was reproduced. A concrete credential-free resource-registration or publishing-model regression would settle this unverified compatibility concern. |
| BH-5 | medium | patch | The isolated probe assets/runtime dependency graph was not retained, leaving selected version maps unbound to the executable. Preserve the resolved graph and assembly digest with a fresh narrow probe run. |
| BH-6 | medium | patch | Exception counters have no retained runtime-console input. Capture sanitized console output and bind the counters to it during an explicit healthy startup/stop. |
| BH-7 | medium | patch | The matrix checker reads health summaries rather than the retained resource list. Check actual resource names/states/health and required services. |
| BH-8 | medium | patch | All seven recorded runtime input hashes match the current checkout, but the checker does not enforce that comparison. Check current bytes and before/after equality so later drift fails closed. |
| BH-9 | medium | patch | The script uses Python assertions for verification; optimization removes those guards. Use explicit validation failures and exercise malformed evidence with optimization enabled. |
| BH-10 | medium | patch | The recorded direct authority command supplies exclusions without the C# sealed-file preflight. Add the existing sealed-file digest checks to the evidence command sequence before invoking the unchanged validator, using the current exact exclusion inventory. |
| EH-1 | false | reject | The CR-only XML mapping finding belongs to concurrent user test edits in the snapshot. Current SecretsProtectionTests.cs contains no BuildXmlLineStarts/XML report parser, so the cited failure cannot occur in the current source. No unrelated test source was changed. |
| EH-2 | medium | patch | The overview version cells are stale; verified at lines 47 and 52. Grouped with BH-1 after its independent verdict. |

### Review resolution

- BH-1/EH-2: corrected the two current overview version cells.
- BH-2: added a dated-packet `-text` rule; verified unchanged Git blob bytes with autocrlf enabled.
- BH-5: retained the exact resolved assets/deps manifests in a deterministic two-member ZIP with archive/entry hashes and the passing assembly digest; repeated consumer validation passed 20/20.
- BH-6/BH-7/BH-8: retained sanitized console and actual describe resources, enforced all seven current runtime input digests, and verified eight required-service waits, 30 healthy resources, zero binary failures, and successful stop.
- BH-9: replaced removable assertions with explicit failures; optimized validation passes and rejects 13 malformed packet copies without changing retained captures.
- BH-10: recorded the current sealed-file preflight before the unchanged authority validator, which passes for 82 projects with 15 exact exclusions.
- BH-4: appended the unverified Azure resource/publishing concern to the deferred-work ledger. Rejected findings remain recorded above.

The existing secrets scanner passed for all 123 stored packet files using its current compiled source. Earlier raw-manifest metadata findings remain recorded; exact manifest bytes are retained as a standard binary ZIP under the existing artifact policy. The primary agent independently reran the six-row matrix, optimized malformed-packet exercise, documentation check, and authoritative audit validation. Current checkout movement included external root commits; no Git mutations were performed by this task.
