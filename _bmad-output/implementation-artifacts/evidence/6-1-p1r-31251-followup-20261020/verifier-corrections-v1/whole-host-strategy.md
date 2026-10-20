# Whole-host preservation strategy for a separately named qualification

This follow-up prepares future verification. It neither repeats qualification
nor grants owner acceptance, conformance, P1R usability or Story 6.4 acceptance.

Use the proposed run name `p1r-31251-q5-post-verifier-corrections`, with a new UTC
start timestamp and invocation UUID appended to its evidence directory. Do not
reuse V4, this diagnostic directory or any prior attempt's receipts. The selected
candidate remains 3.125.1 / v3.125.1 /
55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b; it must receive a separately sealed
verifier closure and independently observed execution Builds.

Before provisioning, bind the corrected verifier in an isolated ordinary checkout
or otherwise secure exclusive ownership of its repository for the complete
run. Concurrent commits in this follow-up changed the shared repository HEAD;
none were issued by this task. A moving verifier invalidates its source closure.

The operator must secure either a dedicated disposable
host or an exclusive host window covering preflight, execution, audit and final
cleanup. The window must cover all users and automation that can create,
remove, restart or upgrade containers. Recording this prerequisite is not a
claim that such a lease exists. No coordination messages or shared service
changes are authorized or performed by this follow-up.

Record the complete unfiltered `docker ps -aq --no-trunc` result and safe inspect
records for every ID, including stopped containers. Retain full container ID,
image ID, StartedAt and Running. Capture the selected local daemon ID and the
exact CLI binary. Fail preflight if the inventory is incomplete. Use the existing
private daemon, socket, network and PID namespace runner for fixture resources;
never expose the host Docker socket to a consumer.

After every attempt, finish repeated exact-identity and invocation-label-owned
cleanup; retain process PID/start ticks, private-container and private-volume
absence proofs, scratch absence and any failed cleanup attempt. Then inventory
ALL outer containers again. Compare the complete before/after maps with no
exclusions. Owned absence and complete outer equality are two independent
requirements. A changed or missing unrelated ID, image ID, start time or running
state fails preservation even when task cleanup succeeds. Never remove that
container from the comparison, restore/restart an unrelated container, or assign
an actor without evidence.

If the window is violated, retain the entire attempt as nonpassing. Investigate
outside this qualification and schedule a new separately named run after the
operator secures a suitable host. No repeat can turn the earlier failed attempt
into a pass. A quiet bounded diagnostic on today's host is not an exclusive lease
or proof that a longer qualification will preserve the host.

The future run additionally requires successful cold and restart/reuse
two-tenant seeding under the exact Dapr 1.18.2 / PostgreSQL v1 image profile,
coherent retained-floor/proof inputs, truthful paired prerequisites, a fixed
verifier revision, all 17 scenario lanes plus Reminder and evolution, recovery
and failure drills, independent strict import and genuine owner decisions. Keep
rollback null and Projects AD-17 drain/freeze/forward recovery. The newer
verified-origin API needs its own explicitly selected successor package/source
and consuming Story 6.4 qualification; it is absent from 3.125.1.
