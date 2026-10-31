---
title: Retain existing durable fences for actual Direct notifier RPCs
status: locally-verified-awaiting-protected-ci
baseline_commit: bb3dad5b17ff3f5264a62a09e048d6103c3b4cbf
---

# B8 Direct notifier transport completion

Both real Direct DaprProjectionChangeNotifier overloads await an unwrapped
IETagActor.RegenerateAsync. No actor proxy decorator enrolls remote uncertainty.
The real EventStoreProjection base discards its notification task and catches
failures, so an outer exception cannot supply a quiescence guarantee. Wrap only
the two actual RPC awaits in the existing RouteCatalogRemoteCall boundary.
Validate cancellation before enrollment with the existing notifier token.
Preserve constructor ABI, publication/broadcast behavior, source contracts,
authority/eligibility, caller async lifecycle, budgets and all existing fences.

## File Scope

- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` - two actual RPC boundaries only
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionChangeNotifierLeaseTests.cs` - real ambient durable lease, both overloads and genuine fire-and-forget projection lifecycle
- `_bmad-output/implementation-artifacts/spec-direct-notifier-remote-fences.md` - exact scope, lifecycle contract and proof

## Verification

Run the focused new tests before the source patch to prove the actual gap, then
run those and existing notifier/SignalR tests after the two-line correction.
Use separate artifact outputs and no root/private runtime or producer changes.
Verify timeout/cancelled remote completion retains the fence despite a catch;
known success before scope disposal releases; pending at disposal retains even
after eventual success under the established owner-verified-quiescence contract;
already-cancelled tokens dispatch nothing. Exercise the actual base Project
method and its deliberately discarded task rather than a modeled replacement.
Full protected PR CI/Integration rerun is mandatory before normal squash/release.

## Cross-Tenant Negative Evidence

No new tenant authority or scope is introduced. The same admitted durable lease
continues to block replacement after ambiguous or still-pending actual RPCs;
wrong scope/retirement semantics remain under existing unchanged catalog guards.

## Concrete result

The actual new12cases before the source correction produced9failures and3
successes, zero skips. Lost/canceled responses, pending scope disposal and the
real fire-and-forget Project path exposed missing fencing; pre-canceled tokens
also proved that Direct transport previously ignored cancellation before RPC.
After changing exactly two awaits, all31new/existing notifier and SignalR cases
pass with zero skips. Both Release builds have zero warnings/errors.

The genuine projection base discards its NotifyAsync Task. Tests invoke that
real Project implementation with an existing SDK projection fixture: synchronous
known success before outer scope disposal releases; pending RPC at disposal
retains its durable fence, including after eventual RPC success. That latter
conservative behavior is the existing contract, not an added background drain,
changed caller lifetime or automatic timeout. Actual full Root concurrent/persisted
runtime proof remains required and must not remove empty-lease assertions.

Build output is isolated under artifacts/direct-notifier-fence; original shared
producer outputs and Root files are not rebuilt. Exact focused commands:

```sh
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release -m:1 --nologo --artifacts-path artifacts/direct-notifier-fence
dotnet test artifacts/direct-notifier-fence/bin/Hexalith.EventStore.Server.Tests/release/Hexalith.EventStore.Server.Tests.dll --nologo --filter 'FullyQualifiedName~DaprProjectionChangeNotifierLeaseTests|FullyQualifiedName~DaprProjectionChangeNotifierTests|FullyQualifiedName~DaprProjectionChangeNotifierSignalRTests'
```

Logs:/tmp/es-direct-notifier-fence-red.log (12cases/9fail),
/tmp/es-direct-notifier-fence-build.log (0warnings/errors),
/tmp/es-direct-notifier-fence-green.log (31pass/0skip).
No constructor ABI, source contract, authority, eligibility, broadcast failure
policy, budget or public asynchronous lifecycle changes. SDK merge/publication
must use fresh exact-head protected checks after this correction lands in PR376.
