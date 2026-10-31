---
title: Bind live-sidecar fixtures to explicit catalog authority
status: in-progress
created: 2026-10-31
baseline_commit: c02afa0fce310ccdb696232f18829df96cba088e
---

# Bind live-sidecar fixtures to explicit catalog authority

Integration workflow 41187538252 passes published-compatibility but fails live-sidecar job 124497785317: 278 selected, 268 passed, six failed and four existing process-worker skips. Hosted gateway/API composition omitted the common catalog registration. Legacy admission/process qualifications also lack the independently bound catalog authority now required by public runtime entry points.

## Scope and Constraints

Correct only owning live-sidecar test composition and its runner/fixture contracts. Preserve production catalog trust, public constructors, admission and dispatch lease validation, authentication, metadata checks, persistence assertions, full CI selection and existing timeouts. Never use a null-authority seam, suppress catalog guards, manufacture Development authority for another environment, or initialize nested submodules.

The parent explicitly permits a strict test-owned immutable catalog at the public extension seam, bound to exact fixture schema/digests, revision, run, finite tenant scope and host identity. Such authority must reject arbitrary routes, wrong identity/profile/revision and every lifecycle mutation. It does not qualify production trust or catalog lifecycle; the separate real hosted-catalog tests retain those runtime checks.

Allowed files: this spec; `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RouteCatalogHostedLiveTests.cs`; necessary fixture/catalog/negative-control files under `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/`; the two admission test callers under Actors/Events; the existing `tools/qualify-internal-actor-admission.py` runner and focused runner tests if required. No production `src/` edits, dependency versions, selectors, root producer output writes or unrelated changes.

## Observed Failures

- Hosted catalog plain: missing IRouteCatalogAuthority during independently captured gateway observation; mTLS: owned sidecar exits with stderr-only diagnostics.
- P1R signed-fence persistence and primary-removal admission: catalog local identity/profile missing before admission.
- Actor callback process qualification: worker exits one; its diagnostic result is retained only in private scratch.
- OQ8 PostgreSQL matrix: all twelve initial writers receive HTTP503 before authority/replay assertions.

## Verification

Use detached owner worktree `/tmp/hexalith-eventstore-integration-c02afa` and the existing root-declared Builds catalog path for isolated compilation; do not build or mutate original producer outputs while parent final-cohort checks run. Run focused negatives and the six failing cases only. Record exact commands/counts and cleanup here. Parent owns any required independent review; exact full commit message must pass pinned commitlint before any commit or push.

## Checkpoint 2026-10-31 10:47 UTC

Parent requested a safe checkpoint to free this agent slot for three independent reviewers. This task remains **in progress**; no commit or push has occurred.

Implemented only owner tests/tools:

- Hosted fixture now registers common catalog services and the real HTTP security inventory/human policy; its actual bound-owner workload scheme/policy is declared for the controller's existing extra endpoint. Sidecar early-exit diagnostics retain stdout as well as stderr.
- Added immutable `LiveAdmissionCatalogBinding`, scope, startup buffering, registration and authority fixtures. The private launch binding covers run, host, app/consumer/receiver identities, finite exact tenants, declared schema/descriptor digest and key revision. Each tenant has a separately validated schema3 snapshot; lifecycle mutations refuse. Public admission/router/receiver and the real dispatch-lease type stay installed. This fixture does not qualify production trust or distributed catalog lifecycle; the real hosted tests continue to do so.
- Existing live adapter is now explicitly declared; its raw payload is canonically represented as a base64 string in the declared semantic descriptor, with test-only policy revision live-test-v2. Existing target and payload distinctions remain.
- Dapr in-process admission callers select their exact finite scope. OQ8/actor-process launchers provide private bindings and UI surface claims on their human test JWTs. OQ8 restarts all three hosts on catalog/key revision changes and retains the persisted receiver boundary counter.
- Added nine focused immutable-authority negatives and a Python token surface control. No production source, version or gitlink edits; parent's native Sample/SDK work is separate.

Successful checks so far:

- Isolated Release build with warnings as errors: zero warnings/errors, most recent `/tmp/es-live-catalog-isolated-build-7.log`.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*LiveAdmissionRouteCatalogTests' -result-xml /tmp/es-live-catalog-unit.xml`: 9 passed, 0 skipped, 0.465s.
- Exact two original in-process admission/persistence failures via repeated `-method` selectors: 2 passed, 0 skipped, 13.596s; `/tmp/es-live-catalog-admission.{log,xml}`.
- Python original focused runner checks: 8 passed. The added ninth UI-claim assertion reran in the hosted-3 shell; inspect `/tmp/es-live-catalog-python-2.log` for result.

Active/incomplete probes and exact sessions:

- Hosted real catalog plain+mTLS rerun: session **90124**, `/tmp/es-live-catalog-hosted-3.log`, planned XML `/tmp/es-live-catalog-hosted-3.xml`. At checkpoint it has reached actual shared authority operations; no failure line yet. Poll before any isolated producer rebuild.
- Callback process second rerun: session **73639** completed failure (needs poll to close). Private evidence `/tmp/actor-admission-evidence-34aff91042eb4f3097f53668b8f1038d` reports a JsonReaderException during actor invocation, after catalog/authorized surface succeeded; cleanup_passed=true. The authority incorrectly tried parsing binary Dapr actor arguments before using the typed actor-id tenant.
- OQ8 second rerun: session **93213** completed failure (needs poll to close), `/tmp/es-live-catalog-oq8-2.{log,xml}`; 11 writers succeeded and one returned500 from that same actor-body selection path. Cleanup occurs in fixture finally.
- A bounded correction is now saved **only in original owner** `Fixtures/LiveAdmissionRouteCatalog.cs`: select the exact typed actor-id tenant first; parse JSON only for a body-scoped request, and translate malformed unbound JSON into catalog refusal. This edit is **not yet copied/built/tested**. Add a focused binary actor-body scope control and rerun the callback+OQ8 failures after the hosted probe releases its isolated outputs.

Isolated worktree: `/tmp/hexalith-eventstore-integration-c02afa`, detached c02afa; original owner producer outputs untouched. Build command:

```sh
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Release --disable-build-servers -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:Hexalith1BuildPackageProps=/home/administrator/projects/hexalith/parties/references/Hexalith.Builds/Props/Directory.Packages.props -warnaserror
```

Copy only this task's `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` files and two actor-runner Python files into the worktree. Do not copy parent's native Sample/SDK changes until coordinated. Remaining: finish exact six live failures, strengthen the new selection/counter controls only as necessary, preserve compact tracked outcome summary next to this spec, parent combined independent review, pinned commitlint21.2.4, then authorized owner commit/push after parent review. Never declare this checkpoint complete.

## Default discovery correction

Parent explicitly extended this owner correction to resolve default CI transport risk. Existing assembly `ParallelMode.None` and OQ8 nonparallel collection already serialize tests; no serialization change is warranted. The default fixture still used unscoped mDNS with globally reused production app IDs. Existing sanitized invocation diagnostics proved HTTP500 `ERR_DIRECT_INVOKE` before any Sample receiver execution. The exact OQ8 matrix then passed with all original assertions using the existing complete private override group (23.246s, cleanup=true), proving a discovery-specific failure.

Use the existing owned SQLite discovery configuration and unique per-run namespace by default as well, preserving configured control planes, production application IDs, PostgreSQL profile, explicit overrides, timeouts and all assertions. Extend allowed scope only to `Actors/IdempotencyAdmissionOq8PostgresqlTests.cs` to assert actual private registry plus namespace/config arguments at startup, rotation and reader retirement. Evidence records the actual namespace/discovery configuration and the added test-only catalog/body-buffering seams.

## Focused verification complete — awaiting combined independent review

The checkpoint's remaining actor-body issue is corrected and covered by real binary actor-path/JSON-rewind negatives. All six original failed cases now pass; no test selection, timeout, persistence, authorization, lease, catalog, or cleanup assertion was removed.

| Selected checks | Passed | Failed | Skipped | Seconds |
| --- | ---: | ---: | ---: | ---: |
| Hosted catalog, plain and mTLS | 2 | 0 | 0 | 87.226 |
| Signed fence persistence and primary-host removal | 2 | 0 | 0 | 12.907 |
| Actor callback process replacement | 1 | 0 | 0 | 38.815 |
| OQ8 default discovery, complete PostgreSQL matrix | 1 | 0 | 0 | 25.128 |
| Immutable catalog, counter restart, discovery/override negatives | 31 | 0 | 0 | 0.498 |
| Python actor runner and signed UI-surface control | 9 | 0 | 0 | 0.720 |

The isolated Release build uses the exact command above, with zero warnings/errors (`/tmp/es-live-catalog-isolated-build-10.log`). Direct DLL selectors, exact argument arrays, execution environment, counts and XML/log SHA256s are preserved in `live-sidecar-catalog-fixture-verification.json`, an explicitly allowed compact tracked summary beside this spec (not an evidence folder). It contains all 19 changed test/tool input hashes, runtime/assembly hashes, all original OQ8 matrix observations and explicit scope limits.

Summary SHA256: `988058f94268e32e47e354e3f4b22f1fef3367b0760b04a150c302b7590f3e03` (16,245 bytes). Baseline remains `c02afa0fce310ccdb696232f18829df96cba088e`; this work is uncommitted. No original producer outputs, production source, dependency version or gitlink was changed by this task.

Default OQ8 was verified without any member of the six-variable override group. Exact final environment additions were `HEXALITH_OQ8_EVIDENCE_DIRECTORY=/tmp/es-live-catalog-oq8-default-final-evidence`, `HEXALITH_OQ8_DIAGNOSTICS_PATH=/tmp/es-live-catalog-oq8-default-final-diagnostics.json`, and `HEXALITH_OQ8_CLEANUP_PATH=/tmp/es-live-catalog-oq8-default-final-cleanup.json`; the selector was `-class '*IdempotencyAdmissionOq8PostgresqlTests' -result-xml /tmp/es-live-catalog-oq8-default-final.xml`. Ten actual sidecar-start observations all used one run-private namespace/SQLite registry, including failover, key rotation and reader retirement. The private PostgreSQL container and all recorded sidecar PIDs are absent; processes stopped and scratch removed. The callback qualifier completed all 47 checks, with independent process/container/scratch cleanup passed. The extra private override diagnostic probe also cleaned its three exact labeled containers.

Pinned commitlint CLI21.2.4 validated the fixture-only exact candidate stored in `/tmp/es-live-catalog-commit-message.txt`: zero problems/warnings, evidence `/tmp/es-live-catalog-commitlint.log`. No commit or push has occurred; a combined native Sample/SDK commit needs its own final exact message validation.

Parent owns the combined three independent reviews and any final broad hosted verification/publication. This test-only immutable authority intentionally does not claim production enrollment/trust or distributed lifecycle qualification; the separate real hosted tests retain those responsibilities. Original failing probes remain recorded above to distinguish diagnosis from final passing evidence.
