# G-1 Durable Task scope decision

On 2026-11-25 the EventStore Owner approved the G-1 Durable Task design, implementation, and isolated qualification scope requested by the Projects 7.1-P2 owner proposals.

The scope covers generic admission, single-use Confirmation Artifact consumption in the admission transaction, committed task status and owner receipt lookup, worker fencing, and recovery. The original actor, tenant, request and operation remain bound across retries. Review EventStore's existing checkpoint and receipt primitives for reuse.

The delivery checks require clean-checkout persisted tests for equivalent retry, failed and ambiguous admission, lease takeover, lost replies, dependency failure and cancellation, and an exercised rollback or forward recovery on the retained store. Record the exact source, runtime, commands, native results, and owner acceptance after those checks.

The inspected EventStore source was `ae1143bdc831ca6f2cf61726648a3efe946d68a2`. The proposal is `Hexalith.Projects/_bmad-output/implementation-artifacts/7-1-p2-owner-scope-proposals.md` at `fdc8ca69ebe0a656f88d55f9e32c8de71b1262f6`. This decision authorizes work in the isolated qualification scope. Capability acceptance is pending.

## Admission candidate

`DurableTaskAdmission` uses the same conditional state interface as `ProcessCheckpointStore`. One row binds the Tenant, original actor, versioned operation and idempotency key. The row holds the opaque token hash, normalized request hash, independently authorized interactive binding digest and expiry. First admission creates the task and marks the token consumed by that task in one conditional write. Equivalent retries reauthorize the original actor and recover the committed ID. A changed request, foreign scope, stale binding or revoked actor cannot recover or consume that admission. The owner of the authenticated host supplies the current authorization and interactive binding.

The existing `ProcessCheckpointStore` fences a source process around an enumerated event stream. A Durable Task has its own admission and execution lifecycle, described under the execution and lifecycle candidate below. The admission API still has no transport registration, and each effect still needs recorded original-actor authority. The remaining work is listed under Still open.

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

## Execution and lifecycle candidate

`DurableTaskExecution` keeps the lease fence, checkpoint, pending step and owner receipts on the admission row. Each owner step uses the stable key `{taskId}/step/{n}` and resolves an ambiguous outcome through the owner lookup before invoking the owner again. An unresolved partial effect enters `NeedsAttention` and fences automatic execution.

The committed lifecycle has the states `Pending`, `Running`, `WaitingForDependency`, `NeedsAttention`, `Succeeded`, `Rejected`, `Failed` and `Cancelled`. Each transition is one conditional write on the admission row, and a persisted state outside the graph fails closed. Rows persist the state by name. An owner failure records `WaitingForDependency` and keeps the step pending. Each attempt at a pending step is charged against the runtime's dependency failure budget when it starts, so an owner call that outlives its lease or a worker that abandons the step still consumes it. A committed receipt or a refusal resets the charge, and an attempt the caller cancels is refunded. A reported owner failure on the last allowed attempt, or an attempt beyond the budget, moves the task to `NeedsAttention` and releases its lease without calling the owner again. Within the budget, the next attempt's lookup recovers a lost reply. The owner reports a lookup or call as `Committed` with its receipt, `Unresolved`, `Rejected` or, for a lookup only, `Absent`. `Rejected` is an authoritative refusal with no effect for the step key, and the owner then refuses every later execution for that key. It ends the task as `Rejected` and clears the pending step, and a refused irreversible step keeps its irreversible marker. Inconsistent owner evidence, a missing receipt, a refusal of a step with a committed receipt, or a receipt that differs from the committed checkpoint moves the task to `NeedsAttention` before the error is returned. The lease holder records a terminal outcome only when no step is pending, and terminal states are immutable. Cancellation clears the lease and retains committed receipts. It is refused once an irreversible step may have reached its owner, and while another step is in flight unless the task is in `NeedsAttention`. A refused or late cancellation returns a conflict with the committed status.

Admission reserves a task-identifier index row before the admission commit. Status and cancellation by task ID reauthorize the original scope and return no task for an unknown identifier or a denied caller. A worker acquires its lease from a scheduled task ID. A reservation whose admission row names another task resolves to no task.

`ResolveStepAsync` resolves a delegated step's task ID and canonical step key to the committed admission, with the current fence, status and the receipt once committed. It returns no binding for an unknown task, a noncanonical key, a step the task has not started or a refused step. Acceptance is fence-based: the binding accepts execution for a pending step of a `Running` or `WaitingForDependency` task under the presented fence, so an owner can refuse a worker whose lease was taken over. It also accepts a worker whose lease expired before another worker took over, and the step key's idempotency and the next lease's lookup recover such a call. An owner refuses a binding that does not accept the presented fence by failing the call, without reporting `Rejected`. The lookup grants no authority. The owner checks the target it derives from the step and re-evaluates the original actor's current authority.

Still open:

- The authenticated host and routes, including the route that exposes the committed step lookup to authenticated owner workloads and confines each owner workload to the steps of its own operation.
- Separate authorization for status, cancellation and admission, including confirmation for cancellation.
- The scheduler and durable discovery.
- The dependency failure budget value, which belongs to the Platform operating envelope.
- Reconciliation from `NeedsAttention`. The row does not persist the cause, an exhausted budget or an unresolved partial effect, which reconciliation needs.
- Retention of admission and index rows.
- The selected rollback, including the compatibility of rows written by the previous candidate and of new rows read by it. New rows add the attempt count, which the previous candidate drops when it rewrites a row. The previous candidate reads a refused irreversible step as inconsistent and fails closed.
- Cancellation races are verified only against the in-memory store, not against the Dapr and Redis store.

## Lifecycle verification

From the EventStore checkout with Platform beside it, build the test project, run the focused classes and run both native phases. The evidence directory keeps the native xUnit receipts after the temporary directory is removed.

```bash
run-with-temp bash -c '
set -e
evidence="$HOME/.local/state/hexalith-eventstore/g1-lifecycle"
mkdir -p "$evidence"
project=tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
dotnet build "$project" --configuration Debug --artifacts-path "$TMPDIR/artifacts" -p:UseHexalithProjectReferences=true -m:1
tests="$TMPDIR/artifacts/bin/Hexalith.EventStore.DomainService.Tests/debug/Hexalith.EventStore.DomainService.Tests.dll"
dotnet "$tests" -class "*DurableTaskAdmissionTests" -class "*DurableTaskExecutionTests" -class "*DurableTaskLifecycleTests" -class "*DurableTaskStepBindingTests" -class "*DurableTaskCancellationRaceTests" -class "*DurableTaskFailureBudgetTests" -failSkips -result-xml "$evidence/focused.xml"
python3 ../Hexalith.Platform/eng/durable-task-qualification/probe_state_store.py --native-execution "$tests" --evidence-directory "$evidence"
'
```

On 2026-11-25 the build completed with zero warnings and errors. The focused run passed 83 tests: 4 in `DurableTaskAdmissionTests`, 4 in `DurableTaskExecutionTests`, 50 in `DurableTaskLifecycleTests`, 13 in `DurableTaskStepBindingTests`, 5 in `DurableTaskCancellationRaceTests` and 7 in `DurableTaskFailureBudgetTests`. The probe passed 5 tests in `DurableTaskAdmissionLiveTests` and 1 test in each of the two `DurableTaskExecutionLiveTests` phases, with zero skips across two sidecars and a Redis restart. The new live case resolves delegated step bindings across both sidecars, including a committed step older than the latest checkpoint, refuses a stale worker's fence, exhausts the failure budget into a cancellable `NeedsAttention` task and records an owner refusal as `Rejected`. The fixture uses scoped test authority and a test owner.

## Original-actor binding (2026-11-25)

The EventStore Owner selected the committed admission lookup for delegated owner steps. A delegated step presents its task ID and stable step key. The owner reads the committed admission row through an authenticated EventStore lookup that binds the Tenant, original actor, operation and request hash. It checks that the step and target match, then re-evaluates the actor's current authority at the operation. No task-bound bearer credential is introduced. Platform and Folders recorded the same selection. `DurableTaskAdmission.ResolveStepAsync` implements the store lookup. Its authenticated route is still open.
