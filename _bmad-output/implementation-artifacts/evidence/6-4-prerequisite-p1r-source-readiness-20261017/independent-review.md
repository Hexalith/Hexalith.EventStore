# Independent tooling review

An independent review agent inspected the uncommitted EventStore diff against
the approved prerequisite spec and its context. The agent made no edits.

## Finding and resolution

The reviewer found that `seed("source", 10, ...)` could use an unenrolled
selected successor because the prior guard checked the writer string `source`
but not the selected candidate. The executor now requires the selected
candidate to be one of 3.121.0, 3.122.1 or proposed 3.125.0, and a package
writer to equal that selection, before stopping nodes, truncating state or
starting a writer. Negative tests exercise an unenrolled 3.126.0 selection
with a source writer and an older package writer with selected 3.125.0.

The reviewer rechecked the revised guard and reported that this finding was
closed. No further code defect was identified in the reviewed finite
enrollment, strict witness, isolation, or version-bound restore changes.
The [final executor result](test-executor-final.log) ran after the correction:
128 tests passed. The [stable isolation hardening result](test_p1r_isolation_hardening-stable.log)
also passed 24 tests.

## Verification limit raised in review

At review time the two opt-in Client live recovery tests were skipped for
missing isolated Dapr endpoints. A later source-only run supplied an owned
Redis container and private Dapr sidecar; both tests passed and exact owned
resources were removed. Its [result](test-client-pending-recovery-isolated.log)
and [ownership record](client-live-recovery-ownership.json) close the local
source verification limit. The pinned Dapr 1.18.2/PostgreSQL published
qualification remains open.
