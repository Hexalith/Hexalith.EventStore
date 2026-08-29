---
title: 'Bump .NET SDK, Builds, and effective packages'
type: 'chore'
created: '2026-08-29'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '62d28510f3c11904b6b2ce22edc075d55878924b'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore pins SDK `10.0.302`, which cannot resolve when only current SDK `10.0.400` is installed. The parent already points at latest Builds `main`, but its Roslyn 5.9, NBomber 6.6, and xUnit 4 families remain unreconciled with EventStore consumers and live documentation.

**Approach:** Pin SDK `10.0.400`, retain the exact latest reachable Builds `main` and its audited catalog, migrate affected consumers, and synchronize only live snapshots. Prove Release package mode with focused package, test-runner, generator, load-test, AppHost, and container checks.

## Boundaries & Constraints

**Always:** Resolve Builds `main` immediately before implementation and preserve its full SHA. Keep Builds as sole package authority, `rollForward: latestPatch`, `net10.0`, package mode, coupled adaptations, unrelated work, and historical evidence.

**Ask First:** A newer Builds head introduces an uninvestigated package family; compatibility requires editing Builds rather than EventStore; a latest family fails after reasonable consumer migration; or a real container check requires publication/registry credentials rather than a local archive.

**Never:** Add local versions/overrides, downgrade or suppress a catalog family, initialize nested submodules, use recursive/remote updates, change TFMs, rewrite frozen evidence/history, publish, commit, push, or weaken warnings/auditing.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current upstream | Gitlink, checkout, and remote `main` all equal `18742168...` | Leave gitlink/catalog unchanged; update SDK and consumers | Record the no-op identity proof |
| Upstream advanced | Remote `main` resolves to a newer reachable commit | Advance only the root Builds checkout/gitlink and reassess its catalog delta | Stop on unrelated/local submodule changes |
| Latest family breaks a consumer | Warning-as-error or test failure is caused by Roslyn, NBomber, or xUnit | Apply the supported EventStore API/config migration and rerun the family lane | Ask before shared-catalog rollback or expansion |

</frozen-after-approval>

## Code Map

- `global.json:3-4` -- sole EventStore SDK selector; change `10.0.302` to `10.0.400`, preserving `latestPatch`.
- `references/Hexalith.Builds` -- root gitlink/check-out authority; currently remote-equal at `18742168b0bcdc40e5223f7573b1dcca441d781f`.
- `references/Hexalith.Builds/Props/Directory.Packages.props:196-201,242,252,317-320` and `Tools/package-version-audit.json` -- latest families and matching 285-row audit; consumer-read-only.
- `perf/Hexalith.EventStore.LoadTests/Program.cs:34-36` -- replace obsolete aggregate `NodeStats.AllFailCount` with scenario/step failure statistics.
- `tests/Hexalith.EventStore.{Server.LiveSidecar.Tests,IntegrationTests,Server.Tests}/AssemblyInfo.cs` -- replace xUnit 4-obsolete `CollectionBehavior(DisableTestParallelization=true)` with assembly `Parallelization(Mode=ParallelMode.None)` while preserving serialized execution.
- `src/Hexalith.EventStore.RestApi.Generators/` and its test/DomainService consumers -- Roslyn 5.9 compatibility surface; change only compiler-proven incompatibilities.
- `CONTRIBUTING.md`, `docs/getting-started/prerequisites.md`, `docs/guides/{deployment-azure-container-apps,deployment-docker-compose,deployment-kubernetes,troubleshooting}.md`, `docs/brownfield/*.md`, `_bmad-output/project-context.md`, and `_bmad-output/planning-artifacts/architecture.md` -- live SDK/package snapshots only.
- `Directory.Build.targets:21-102` and `CorrectiveOciProvenanceReleaseTests` -- SDK-internal container-label workaround requiring real 10.0.400 archive/provenance regression proof; comments are historical observations.

## Tasks & Acceptance

**Execution:**
- [ ] `global.json` and Builds gitlink -- pin SDK `10.0.400`; re-resolve latest Builds and change the gitlink only if upstream advanced.
- [ ] Package consumers above -- migrate NBomber/xUnit APIs and address only demonstrated Roslyn 5.9 compatibility failures.
- [ ] Live documentation surfaces -- synchronize current SDK, Roslyn, NBomber, and xUnit values without touching historical/frozen evidence.
- [ ] Validation lanes -- run Builds governance, package-mode restore/build, focused consumers/tests, AppHost checks, and SDK container provenance/archive proof.

**Acceptance Criteria:**
- Given the approved dependency refresh, when identity preflight runs, then EventStore selects SDK `10.0.400`, Builds checkout/gitlink equals the latest reachable remote `main`, and its catalog exactly matches the deterministic audit.
- Given the latest catalog, when Release package-mode consumers build and focused tests run, then Roslyn 5.9 generators compile, NBomber uses supported statistics, xUnit 4 preserves required serialization, and no warnings/errors or stale dependency assets remain.
- Given SDK `10.0.400`, when container regression validation runs, then both local target architectures retain exact provenance labels and no internal-target/type-load regression occurs.
- Given current documentation, when stale-snapshot checks run, then live SDK/package claims match effective pins while historical and checksum-bound evidence remains byte-unchanged.

## Spec Change Log

## Verification

**Commands:**
- `dotnet --version && dotnet --info` -- expected: SDK `10.0.400` resolves from the repository.
- Builds deterministic catalog/audit/exception/Dapr/consumer-authority validators -- expected: all pass against the exact checkout.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` then serialized `dotnet build ... --no-restore --configuration Release -warnaserror -m:1` -- expected: clean.
- Build/run LoadTests, generator, DomainService, three serialized xUnit projects, focused Contracts packaging classes, and AppHost tests individually -- expected: all pass.
- Local multi-RID container archive/provenance check, `bash scripts/check-doc-versions.sh`, changed-doc lint, `git diff --check`, and status/submodule inspection -- expected: exact labels/docs, no whitespace errors, no unintended submodule or evidence changes.
