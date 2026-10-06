# Story 6.6 local page admission and observed capability loss

Story 6.6 remains **in progress**. This run completes the scoped local changes
below; every M1–M8 task and activation obligation remains open. The
[capture](page-preflight-capability-loss-2026-10-06/capture.json) records the
owned source delta, selected inputs, compiled assembly hashes, exact commands,
exit codes and full logs. It grants no production admission or readiness.

The execution revision is `a6fc951e2c01c1c816aed1d03c851c0639f12cdd` plus
the captured working-tree changes. The parent spec's canonical baseline stays
`1329b35e52852952ecb2c94aabf100674e9691e3`; the much broader preserved historical
baseline is distinct from this run's source delta. Existing sealed captures and
concurrent P1R tools, tests, specification, qualification evidence and the
external deferred-work ledger are preserved and excluded from the owned delta.

## Local implementation

An internal atomic loss control makes an observed trusted-code policy violation
sticky. Default registries and manifest candidates share the control in the
admitted Client load context; explicit internal scopes support host composition
and isolated qualification fixtures. Upcast, downserialization, registered
schema/identity validation, runtime-options callbacks, current deserialization
and logical result assembly check loss before and after their applicable
callbacks and at ownership transfer. Range admission, domain copying and final
range return check cancellation and the same loss control. Original internal
constructors and range signatures remain available through overloads.

These checks provide subsequent refusal when an observer reports a violation.
They do not integrate or qualify managed/native/reflection/late-load observers,
authenticate catalogs, bind execution to immutable artifacts, undo earlier
effects or prove universal before-effect confinement. A separately loaded Client
assembly needs the qualified host boundary to share the affected-process loss
scope; the default shared static alone does not establish that qualification.
Loss has no reset or readiness path.

Logical pages now prepare every addressed source before any registered schema
validator or upcaster runs. Preparation checks the address, V1/V2 tuple, alias,
format, digest, complete locally bound chain, independent stored/readable limits
and private readable ownership. It snapshots source metadata under the existing
512 KiB admission reservation, then retains the measured conservative metadata
charge instead of retaining 512 KiB for every tiny row. The resolver consumes
the already charged readable owner, avoiding a redundant copy.

The page checks head, retained floor and ETag after preparation and again after
catalog callbacks. It checks every prepared stored payload hash before callbacks
and at the final page boundary, including when a later callback mutates an
earlier source through a closure. Platform reads and effective output copies
leave original actor payloads unchanged. Detection cannot roll back a callback's
external mutation. Provider plaintext, prepared inputs, effective payloads and
reservations clear or release on refusal. Successful views retain metadata
charges; a longer-lived range takes those charges exactly once before disposing
its pages, then releases them on range disposal or later-page refusal. Detached
charges have a guarded append so an allocation failure cannot strand ownership.

## Discriminating controls

The retained controls cover pre-observed loss without allocation or catalog
callbacks, schema/identity/options/deserializer boundaries, every downserializer
stage, cancellation between callbacks, loss observed while an upcaster awaits,
and existing/new executors sharing one loss scope. Owned zero-hop resolution
works within a two-byte payload budget and clears on loss/cancellation.

Page controls prove that a missing, wrongly addressed, unregistered, digest-invalid,
unreadable or head/floor/ETag-invalid later row prevents the first schema/upcast
callback. A later validator's mutation of an earlier source refuses the whole
page. A separate single-page theory keeps the first two metadata observations
identical and changes only the third head, floor or ETag: both schema callbacks
and the upcaster run, then `SourceHeadChanged` refuses the final result, original
actor bytes remain unchanged and the injected live budget returns to zero.

A 256-event tiny page fits below 1 MiB of measured private charges; the existing
258- and 768-event range boundaries remain covered. New 258-event range controls,
with and without domain copies, prove that metadata stays charged after page
disposal and reaches zero on range disposal or later-page refusal. Range
pre-observed loss refuses before actor reads; simultaneous pre-cancellation
preserves the original token. No artificial mid-copy observer hook or observer
qualification was introduced. An empty page also checks observed loss at its
final readback boundary.

## Verification

The capture indexes exact commands and full logs, including unsuccessful and
superseded runs. Every dependent final assembly run starts after its Release
build completes.

| Final check | Result |
| --- | --- |
| Release solution, package references, warnings as errors | Passed; zero warnings/errors |
| Full Release Client assembly | 1,127 passed; zero failures/skips |
| Focused final Release Client, five affected classes | 55 passed; zero failures/skips |
| Focused final Release Server, both logical-reader classes | 85 passed; zero failures/skips |
| Full final Release Server assembly | 3,807 total; one failure and 25 existing skips |
| Current Dapr-only source preflight with timed mutations | Passed; all 11 prohibited mutations rejected |
| Historical AD-13 approval preflight with mutations | Passed; original digest, 20 obligations and 47 open follow-ups preserved |
| Deferred-work checker | Passed with existing advisory diagnostics |
| Compiled source/PDB and assembly identity checks | Passed; all 21 owned source/test files match SHA-256 documents in the four paired Release assemblies/PDBs |
| Whitespace and story invariants | Passed; frozen intent/baseline unchanged, status in progress, eight tasks open |

The first page test run retained seven obsolete expectations for metadata reads,
charges and callback timing. These were corrected to assert the new admission
and ownership behavior. One earlier full-suite run overlapped its build and used
the previous Server test assembly: 3,786 total, eight failures and 25 skips. That
run is retained and is not final-source qualification. Subsequent completed-build
runs separately recorded 3,798 and 3,802 totals with the same sole content-guard
failure. The first new range-loss fixture accidentally observed the default
process control and caused 19 focused failures; the observing fixture now uses
an explicit isolated control, preserving the sticky production behavior. Its
failed output and the final corrected run are retained.

The baseline-required Aspire start/describe/stop ran before runtime edits. The
local security container was unhealthy and dependent resources waited; the
owned app was stopped. This supplies no live Dapr or production qualification.
No new package publication, deployment or live production lane ran; prior
compatibility captures retain their original scope.

## Exact Server gate remains blocked

```text
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -noLogo
```

The final command exits 1. Its sole failure is
`SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`,
reporting only
`_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff:2590`.
The parent separately reproduced this broad guard failure before these changes.
The external SHA-bound evidence stays intact; no guard exception, exclusion or
suppression was added. The 25 existing DW1 ATDD skips remain reported and do not
satisfy Story 6.6's required qualification lanes.

## Next dependency and remaining scope

`DomainServiceRequestRouter.ProduceWireResultAsync` still falls back to
`DomainServiceWireResult.FromDomainResult` when no bounded producer is registered.
Replacing that compatibility fallback coherently requires the real per-domain
payload types and once-only bounded serializers, exact legacy aliases/formats,
immutable serializer options and identities, measured maximum payload bytes and
internal token/scratch bounds, and complete aliases accepted by existing callers.
`AddEventStoreBoundedV1DomainSerialization` and
`BoundedV1DomainSerializerProfile.Add<TPayload>` already provide the declaration
surface, but no application registrations were found in inspected source or
samples. A capped output stream alone does not establish bounded serializer
internal allocations. This run does not invent those declarations, remove the
compatible fallback without them, or report M2 complete.

The root `.gitmodules` declares `references/Hexalith.Platform`; that submodule
owns production composition. Read-only ordinary documentation at
`d77aa059c1ccbad43f5936c1c06747d89d2e8b86` identifies the composition owner in
`README.md`, assigns the EventStore-ratified template plus Platform environment
and provider inventory in
`_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:237`,
and lists shared runtime/profile qualification as owned work in
`_bmad-output/specs/spec-platform/sequencing.md:109`. Its checked-in
`DaprComponents/statestore.yaml` and `pubsub.yaml` explicitly describe local Redis
Development assets. No authoritative production event-evolution manifest,
registry/upcaster inventory or bounded serializer declarations were found in the
inspected allowed Platform inputs. The authoritative Platform qualification
inventory and its shared runtime/profile evidence remain unavailable in these
inputs. EventStore's absent local deployment file is expected under Platform
ownership and supplies no evidence of a missing Platform production configuration.
This run creates no EventStore-owned deployment and does not promote Development
assets to production evidence.

Complete authoritative per-domain catalogs, options, dependency roots/edges,
immutable execution binding, qualified runtime observers and the ratified
profile remain prerequisites. Dapr control ownership/ETag/transaction recovery,
complete consumer routing, replay/query/projection/subscription/effect/admin
integrations, compatibility and two-host fleet/crash evidence also remain
implementation or qualification work under M1–M8. V2 and proof-dependent
operations stay fenced. No parent task, O-row or readiness obligation closes.

The [parent spec](../../spec-6-6-event-versioning-and-upcasting-implementation.md)
records this scope without changing its frozen intent or baseline. This run
performs no staging, commit, branch, push, dependency or remote mutation.
