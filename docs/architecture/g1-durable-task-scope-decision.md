# G-1 Durable Task scope decision

On 2026-11-25 the EventStore Owner approved the G-1 Durable Task design, implementation, and isolated qualification scope requested by the Projects 7.1-P2 owner proposals.

The scope covers generic admission, single-use Confirmation Artifact consumption in the admission transaction, committed task status and owner receipt lookup, worker fencing, and recovery. The original actor, tenant, request and operation remain bound across retries. Review EventStore's existing checkpoint and receipt primitives for reuse.

The delivery checks require clean-checkout persisted tests for equivalent retry, failed and ambiguous admission, lease takeover, lost replies, dependency failure and cancellation, and an exercised rollback or forward recovery on the retained store. Record the exact source, runtime, commands, native results, and owner acceptance after those checks.

The inspected EventStore source was `ae1143bdc831ca6f2cf61726648a3efe946d68a2`. The proposal is `Hexalith.Projects/_bmad-output/implementation-artifacts/7-1-p2-owner-scope-proposals.md` at `fdc8ca69ebe0a656f88d55f9e32c8de71b1262f6`. This decision authorizes work in the isolated qualification scope. Capability acceptance is pending.

## Admission candidate

`DurableTaskAdmission` uses the same conditional state interface as `ProcessCheckpointStore`. One row binds the Tenant, original actor, versioned operation and idempotency key. The row holds the opaque token hash, normalized request hash, independently authorized interactive binding digest and expiry. First admission creates the task and marks the token consumed by that task in one conditional write. Equivalent retries reauthorize the original actor and recover the committed ID. A changed request, foreign scope, stale binding or revoked actor cannot recover or consume that admission. The owner of the authenticated host supplies the current authorization and interactive binding.

The existing `ProcessCheckpointStore` fences a source process around an enumerated event stream. A Durable Task has its own admission and execution lifecycle. The next slice needs durable discovery of admitted tasks, authoritative state transitions, work leases and fences, checkpoint and owner receipt retention, cancellation, and task-bounded recovery. Each effect needs one stable task-step key, authoritative lookup after an ambiguous outcome, and recorded original-actor authority. The admission API has no transport registration.

## Admission verification

From the EventStore checkout with Platform beside it, run the source-mode build and both focused suites.

```bash
run-with-temp bash -c '
set -e
project=tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
dotnet build "$project" --configuration Debug --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=true -m:1
tests="$TMPDIR/artifacts/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll"
dotnet "$tests" -class "Hexalith.EventStore.DomainService.Tests.DurableTasks.DurableTaskAdmissionTests"
python3 ../Hexalith.Platform/eng/durable-task-qualification/probe_state_store.py --native "$tests"
'
```

On 2026-11-25 the build completed with zero warnings and errors. Four focused and three live tests passed with zero skips. The live tests exercised competing task-only admissions and inspected the committed admission and artifact state across two sidecars after failures before and after commit. The fixture injected a lost write response at the storage boundary and used scoped test authority.
