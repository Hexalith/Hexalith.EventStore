# 6.1-P2 query security and projection capability: owner handoff

Status: **draft, not accepted**. Observed locally on 2026-10-01. P2 remains open.
This record does not authorize publication, change a pin, select a rollback triple,
or close Story 6.1. The Projects sprint status remains unchanged.

## Candidate coordinates

| Coordinate | Local observation | Acceptance state |
| --- | --- | --- |
| EventStore source | `6dededdecd62dd6dc6d1f15810108d860ec70c8f` plus the uncommitted test changes recorded in the evidence manifest | Local candidate; not owner-accepted |
| Builds source | Clean root-declared sibling HEAD `21ce044ab465ccb2adab58b3d66e394ffbecf3c2` | Observation only; no accepted P0 runner or P2 pin |
| Evaluated package version | `3.110.0`, from `dotnet msbuild src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj -getProperty:Version -p:Configuration=Release` | Local evaluation; not a published or accepted candidate |
| Published package/source pin | None selected or published for this run | Owner approval pending after green gates |
| Projects consumer pin | Unchanged | Owner selection pending |
| Rollback triple | Unset | Owner selection and procedure approval pending |

The [machine manifest](evidence/6-1-p2-local-2026-10-01/manifest.json) binds
capability and test source bytes to full repository revisions and retained
command/results files. These are local source-build results. They are not a
published-package certification or a G-4 acceptance packet.
The preserved `manifest-before-rebuild-proof-correction.json` captures the earlier
handoff. The current manifest records the final test bytes and distinguishes the
focused follow-up from results captured before the test-only correction.

## Public capability surface

The [compiled signature snapshot](evidence/6-1-p2-local-2026-10-01/public-signatures.txt)
records exact constructors, optional parameters, and deconstruction signatures
from the locally built assemblies; all eight `dotnet-inspect` queries passed.

- `QueryEnvelope` preserves its 9-, 10-, and 15-parameter constructors and adds
  the 16-parameter constructor ending in `string? delegationId = null`.
  `[DataMember] public string? DelegationId { get; init; }` follows the prior
  members. Actual legacy JSON and DataContract payloads deserialize with null
  identity evidence and false delegation defaults.
- `SubmitQuery` preserves its 11- and 17-parameter constructors and
  17-member deconstruction. Its 18-parameter constructor and deconstruction
  carry `string? DelegationId`; the constructor defaults that member to null.
- `DualPrincipalIdentity` preserves its five-parameter constructor and
  deconstruction beside the six-member form ending in
  `string? DelegationId = null`. This type is compiled in the Gateway assembly.
  Only one bounded, valid RFC 8693 `act.sub` claim supplies the identifier.
  Missing, malformed, oversized, duplicate, or ambiguous evidence stays unknown.
  Both actor and handler query-router chains preserve the value separately from
  actor, workload, Tenant, scopes, audience, and correlation.
- `ProjectionEventDto` preserves its eight-parameter constructor/deconstruction
  and adds the ninth member `long GlobalPosition = 0`.
  `public long GlobalPosition { get; init; }` rejects negative values, including
  through a `with` expression. Zero is legacy/unknown. The wire builder forwards
  each exact persisted position, including allocation gaps.
- `public QueryCursorScope AddProjectionWatermark(long? watermark)` writes
  `watermark:<positive invariant decimal>` and rejects null, zero, and negative
  values. The value must come from the durable read-model write. It represents
  the highest positive persisted position in the applied slice and makes no
  claim of contiguous global consumption or allocator progress.
- The existing opt-in safe-denial router preserves its reviewed design:
  forbidden and both not-found forms converge to the canonical not-found shape;
  routes that have not opted in retain their existing behavior.

## Local implementation and matrix evidence

This run added explicit legacy-query-payload deserialization tests and strengthened
[the live process proof](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessRestartTests.cs).
The proof persists event positions `[101, 104, 109]` through Dapr/Redis. Three
separate worker processes now use `DomainProjectionDispatcher` and the supported
named projection/rebuild handler. The first delivery commits the read model and
watermark together, the second process returns `AlreadyCompleted` for the same
dispatch, and its persisted state remains unchanged. After that duplicate check,
the parent commits an isolated stale state of one applied event and watermark 101
through a separate `ReadModelBatch` dispatch and asserts its `Completed` receipt
and durable readback. The third process first asserts that stale state, then
calls `DomainProjectionDispatcher.RebuildAsync` for a coordinated full replay.
A no-op rebuild cannot satisfy the final three-event, watermark-109 assertions.
The complete serialized persisted event history remains identical to the seeded
history. The cursor still decodes under the identical persisted scope and fails
under another Tenant. The new named-handler helper follows Allman brace style.

A separate test-fixture correction uses the existing explicit
`__HEXALITH_...__` placeholder vocabulary in the reminder readiness tests. It
changes no production behavior and does not weaken the tracked-secret guard.

| Matrix scenario | Passing local evidence | Limit |
| --- | --- | --- |
| Delegated query and unknown malformed evidence | `server-p2.ctrf.json.gz`, `query-routing.ctrf.json`, and `contracts-p2.ctrf.json.gz`: claim parsing, controller extraction, both router paths, serialization, and legacy payload defaults | Unit/controller identity evidence; protected-consumer denial for unknown/malformed delegation and authenticated G-4 fixture remain unproven |
| Exact persisted gapped projection positions | `server-p2.ctrf.json.gz` and `projection-rebuild.ctrf.json`: wire replay, mixed legacy positions, same-write read model and watermark | Live persistence also proved by process lane |
| Cursor scope, tamper, stale watermark, and Tenant replay | `client.ctrf.json.gz`, `projection-rebuild.ctrf.json`, and `rebuild-proof-process-restart-rerun.ctrf.json` | Current source codec; no accepted published package pin |
| Forbidden, missing, and cross-Tenant safe denial | `server-p2.ctrf.json.gz`: canonical router-result equivalence; `admin-denial.ctrf.json`: unchanged default admin denial behavior | Cross-Tenant router negative control; authenticated persisted external 404 equivalence remains a G-4 gate |
| Duplicate delivery, restart, and full rebuild | `projection-rebuild.ctrf.json` and `rebuild-proof-process-restart-rerun.ctrf.json`: persisted duplicate receipt, asserted stale state, recovered state/watermark, three distinct worker processes | Event history is seeded through Dapr; not a G-4 composition fixture |

## Verification

Commands, UTC times, exits, counts, and raw results are retained under
[evidence/6-1-p2-local-2026-10-01](evidence/6-1-p2-local-2026-10-01/manifest.json).
Each lane's JSON contains the complete argv, including filters and result paths.
Large reports are archived as byte-identical gzip files; the lane JSON names the
archive and the manifest hashes its retained bytes.
Tests were invoked through built xUnit v3 assemblies with single-dash filters.

| Command/lane | Result |
| --- | --- |
| `dotnet restore Hexalith.EventStore.slnx -m:1 /nr:false` | Exit 0; refreshed stale cached assets |
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:q` | Initial complete candidate build: exit 0, zero warnings/errors. Final rerun also passed: exit 0, zero warnings/errors. |
| Focused Contracts project build | Exit 0, zero warnings/errors |
| Focused LiveSidecar project build | Earlier build passed. Final test-only correction: `rebuild-proof-build-rerun`, exit 0, zero warnings/errors, 4.90 seconds. |
| P2 Contracts classes | 119 passed, zero failed/skipped |
| Full Client assembly | 838 passed, zero failed/skipped |
| Full QueryRouting assembly | 19 passed, zero failed/skipped |
| P2 Server classes | 263 passed, zero failed/skipped |
| Watermark rebuild integration class | 3 passed, zero failed/skipped |
| Live three-process restart class | Earlier proof: 1 passed. Final strengthened proof: `rebuild-proof-process-restart-rerun`, exit 0, 1 passed, zero failed/skipped, 5.532 seconds. |
| Unchanged admin query-denial class | 36 passed, zero failed/skipped |
| Full Contracts assembly | Exit 1: 2,170 total, 2,137 passed, 31 failed, 2 skipped. All failures are in `Packaging.*`. |
| Final reminder fixture and secret guard reruns | 17/17 and 47/47 passed, respectively. |
| Final full Server rerun | Exit 0: 3,483 total, 3,458 passed, zero failed, 25 existing skips. |
| `git diff --check` | Earlier check and final `rebuild-proof-diff-check` both exit 0 |
| `dotnet tool run hexalith-module test --profile reads --filter Story=6.1-P2` from Projects | Exit 1: `Cannot find a tool in the manifest file that has a command named 'hexalith-module'.` |

Only the LiveSidecar project and focused restart class were rerun after the
test-only rebuild-proof correction. The solution builds and other suite results
above predate that correction and are retained with their original timestamps;
they were not repeated against the final test bytes. The initial follow-up
`rebuild-proof-process-restart` failed while injecting stale state through a
single-record write to batch-managed storage. Its raw failure is retained. The
final setup uses the supported batch-store seam, and the focused rerun passed.
The updated local matrix is `matrix-tests-after-rebuild-proof-correction.json`;
the original matrix and process result remain historical artifacts.

The initial full Server run found the existing reminder fixture literal. Its
failure and the focused correction runs are retained separately from the final
Server lane. Full Contracts failures are recorded separately and are not repaired
by this P2 change: missing nested Builds files, pinned release-evidence/provenance
checks, package-ownership/consumer-authority checks, and shared commit-message
guidance. The two skipped packaged-contract checks are not acceptance evidence.
No nested submodule was initialized or updated.

The baseline command
`aspire run --detach --isolated --no-build --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`
exited 2: the plain AppHost required missing nested Tenants host projects and its
child exited 134. The dedicated Dapr live-sidecar fixture did run successfully.

## Residual gates and owner decisions

No G-4 fixture, TRX, JSON summary, authenticated persisted cross-Tenant negative
control, or exact published-candidate packet exists for this run. Local xUnit
results are labelled local and do not substitute for those artifacts. Full
Contracts is not green. Protected-consumer denial with unknown or malformed
delegation and authenticated persisted external 404 equivalence remain unproven.
P2 therefore remains open; P3, P4, and Story 6.1 remain
blocked on their separately owned prerequisites. No sprint-status transition,
release, source/package pin update, or rollback selection was performed.

After the missing gates pass, the repository owners must accept an exact
EventStore source/package coordinate, Builds runner revision, Projects consumer
pin, the G-4 packet, and the rollback triple. Those approvals are absent.

## Rollback procedure proposal — not approved or executable yet

The rollback triple and trigger remain unset. The following procedure is prepared
for owner review and must be bound to exact approved coordinates before execution:

1. On an owner-declared trigger, return P2/P3/P4/Story 6.1 to blocked through the
   Projects planning authority guard and disable dependent protected read routes.
2. Restore the owner-selected EventStore source/package, Builds runner, and
   Projects consumer coordinates together through their owning repositories.
   Restore/build in package mode and verify the approved dependency graph.
3. Preserve existing event history, global positions, read models, and allocator
   gaps. Perform no deletion, renumbering, history rewrite, or data down-migration.
4. Run the retained legacy-read and authenticated persisted G-4 fixture against
   the restored triple; require earlier data to remain readable and repeat the
   cross-Tenant negative controls and denial-equivalence checks.
5. Stop issuing watermark-bound cursors when an authoritative watermark is
   unavailable. Reject cursors under an unknown or changed restored scope; never
   retry them with weaker unbound validation. Require captured rollback evidence
   for that fail-closed behavior before dependent routes can be re-enabled.
6. Record the applied exact coordinates, owner decisions, UTC time, raw command
   outputs, hashes, and failed/passed gates. Reopening any dependent gate requires
   a new accepted candidate and successful evidence; this draft grants none.
