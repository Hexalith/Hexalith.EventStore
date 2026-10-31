---
title: 'Story 4.6: Resolve the Existing Successor Handoff'
type: 'chore'
created: '2026-10-31'
status: 'done'
route: 'oneshot'
review_loop_iteration: 1
story_key: '4-6-global-position-sharding-spec-renegotiation'
completion_scope: 'handoff-preparation-only'
original_story_status: 'awaiting-operator'
approval_state: 'approved-for-planning'
planning_authorized: true
approved_by: 'jpiquot'
approver_github_id: 6775094
approval_recorded_at_utc: '2026-10-31T06:24:18Z'
approval_conversation_id: '01a18ded-3ceb-7c92-b5f5-dbf1e38a4686'
authorization_received: true
authorization_state: 'granted-subject-to-evidence-and-design'
authorization_recorded_at_utc: '2026-10-31T06:30:17Z'
evidence_execution_state: 'local-mechanics-validated-production-evidence-pending'
implementation_authorized: false
baseline_revision: '63e92c924244c6ba747fa4336d0018b47f7c7a01'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.6 already has a reviewed successor and no unchecked implementation tasks. Architecture-owner approval, commissioned evidence, and separate implementation authority remain outstanding; this invocation supplies no new scope or records.

**Approach:** Verify the existing candidate and make its handoff concrete before selecting any further work. Preserve the existing specification while the user resolves the next action recorded outside this intent block.

## Boundaries & Constraints

**Always:** Keep frozen v1 authoritative. Bind approval to the exact candidate commit, successor blob, normative digest, reviewed content, and candidate-commit architecture owners. Distinguish validation from approval and production evidence. Record commands, counts, revision, and applicable CI links in story Verification; keep run output in workflow artifacts.

**Never:** Infer approval from this invocation, checks, merges, digests, or status. Change the existing story packet, tracker, ledger, code, contracts, state, topology, deployment, or migration. Recreate removed evidence packets. Stage, commit, push, or contact owners.


**Decision (2026-10-31):** The user selected option 1: continue the existing-candidate handoff. Prepare exact approval details and preserve the reviewed successor. This selection does not supply authenticated architecture-owner approval, production evidence, or implementation authority.

</frozen-after-approval>

## Implementation Notes

The handoff preparation is separate from completion of Story 4.6. The original wrapper, successor, frozen predecessor, and sprint row remain `awaiting-operator` where applicable and are unchanged. That handoff state is later than implementation/review; no sprint synchronization is needed. This follow-up changes only this record and the derived epic context. Concurrent source, test, and submodule changes are preserved. Nothing is staged, committed, or sent to another person.

### Exact approval subject

| Field | Verified value |
|---|---|
| Candidate commit | `411005b4ccb2352249c83330c54967c6a22beba1` |
| Successor | [`spec-dapr-global-event-ordering-v2.md`](spec-dapr-global-event-ordering-v2.md) |
| Successor Git blob | `160331d25451928ff3c3dea2300b65cab4f97c3b` |
| Normative SHA-256 | `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd` |
| Candidate-commit architecture owners | `jpiquot` |
| Approval at handoff preparation | Not established; no authenticated approval reference supplied at that time |

Approval must review the successor bytes at the candidate commit, rather than later mutable content. The full file SHA-256 is `bbec7a16661995849891fae2617cf74c281d3da155086d0e22a39d5a2488f59a`. Existing worktree and index bytes reproduce the committed candidate successor.

### Proposed owner statement — draft, not approval

For the candidate-commit architecture owner to review and publish through an authenticated channel:

> I approve the reviewed content of `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` at candidate commit `411005b4ccb2352249c83330c54967c6a22beba1`, successor Git blob `160331d25451928ff3c3dea2300b65cab4f97c3b`, and normative SHA-256 `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`, in my capacity as candidate-commit architecture owner `jpiquot`. This approval authorizes downstream planning only. It grants no runtime implementation, public-contract change, migration, deployment, topology change, or cutover authority.

Retain the authenticated record's durable reference outside the successor's normative content. An agent-authored copy of this statement does not satisfy approval. If reviewed bytes or any subject identity differ, obtain approval for the replacement identity instead.

### Next owner actions

1. Completed by the human approval recorded below: the user approved the offered exact candidate, and the connected GitHub identity matches the candidate's sole architecture owner, `jpiquot`. Preserve the approval reference and exact subject together.
2. After planning approval, commission a separately reviewed evidence specification. Before any isolated evidence candidate runs, obtain unanimous authenticated evidence-only authority binding the candidate, blob, normative digest, tested implementation artifact, test environment, and exclusive canonical UTC second-precision expiry. Require fencing, draining, and teardown at completion or expiry. This handoff grants none of that authority.
3. Produce every section 7 category: option capacity; position identity; multi-host concurrency; aggregate/event identity; restart/failover; mixed-version consumer safety; cursor/checkpoint/rebuild; inventory/migration; overflow/limits; hot-shard recovery; rollout/rollback; and the Story 4.5 applicability declaration. Every empirical row must bind the approved subject, tested artifact, provider profile/configuration, and topology, and inspect persisted end state. Apply section 7's additional capacity identity/expiry rules and authenticated seam-review rules. Preserve results using the current tests/CI artifact policy; do not recreate removed committed evidence packets.
4. Request separate implementation authority only after the applicable evidence and reviewed implementation design satisfy the successor. Until then, frozen v1 and the single global allocator remain authoritative.

### Verification

Investigation HEAD: `63e92c924244c6ba747fa4336d0018b47f7c7a01`.

- The original wrapper's embedded staged-blob verifier passed all six groups. With `PYTHONOPTIMIZE=1`, it exited 1 with `optimized Python disables verification assertions`, as required.
- `git diff --name-status 1194dfe59bcbc9b235390d1e46a7dfe4ee115d94..411005b4ccb2352249c83330c54967c6a22beba1` returned exactly added wrapper and modified successor.
- `git rev-parse 411005b4ccb2352249c83330c54967c6a22beba1:_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` returned the successor blob above.
- `git show 411005b4ccb2352249c83330c54967c6a22beba1:_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` resolved `architecture_owner` to `["jpiquot"]`.
- Before the approval recorded below, connected GitHub searches for the exact digest and Story 4.6 approval returned no issue matches; related PR 353 had no submitted reviews. External approval could have existed elsewhere; none had been supplied in this conversation at handoff preparation.
- No runtime tests apply to this documentation handoff. Final formatting and protected-file equality checks are recorded after review below.

Run the exact embedded verifier and optimization control from the repository root:

```bash
python3 - <<'PY'
from pathlib import Path
import os
import re
import subprocess
import textwrap

spec = Path('_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md').read_text()
match = re.search(r"  ```bash\n  python3 - <<'PY'\n(.*?)\n  PY\n  ```", spec, re.S)
if match is None:
    raise SystemExit('Embedded verifier not found')
script = textwrap.dedent(match.group(1)) + '\n'
subprocess.run(['python3', '-'], input=script, text=True, check=True)
optimized = subprocess.run(
    ['python3', '-'], input=script, text=True, capture_output=True,
    env={**os.environ, 'PYTHONOPTIMIZE': '1'})
if optimized.returncode != 1 or 'optimized Python disables verification assertions' not in optimized.stderr:
    raise SystemExit('Optimization rejection failed')
print('optimized-execution rejection: PASS')
PY
```

Final results: the command above passed all six original groups and its optimization control. Documentation structure, local links, LF/no-BOM formatting, candidate byte/owner/scope checks, and protected-file equality passed. `git diff --check -- _bmad-output/implementation-artifacts/epic-4-context.md` passed. `git diff --cached --name-only` returned no staged paths. The original wrapper, successor, frozen predecessor, and sprint-status bytes still equal `HEAD`. The derived context contains all 17 Epic 4 stories.

## Review Triage Log

- `low`, patched — "Approval absent" exceeded the available external evidence; the handoff now says approval is not established and no authenticated reference was supplied.
- `low`, patched — the Verification section reported embedded-verifier results without exact invocations; it now includes the runnable extraction, normal invocation, and expected optimization rejection.
- `medium`, patched — the context refresh omitted the resume tuple and ordinal rule; restored `(MessageId, normalized CausationId, CommandType)` and ordinal matching, as required by Story 4.2.
- `medium`, patched — the context refresh omitted the OQ8 production-equivalent topology boundary; restored independent OS processes/sidecars, the PostgreSQL profile, production resiliency, and the supporting-only role of same-process/direct-actor evidence, as required by Story 4.14. Profile changes require requalification.

All four findings were addressed. No findings were deferred. Only this bounded handoff preparation is complete; original Story 4.6 remains awaiting operator action.

## Recorded Architecture-Owner Approval

Recorded at `2026-10-31T06:24:18Z` in Codex conversation `01a18ded-3ceb-7c92-b5f5-dbf1e38a4686`, after the completed handoff identified authenticated architecture-owner approval of the exact candidate as the next action.

The human's verbatim statement was:

> I approve

This statement approves the candidate offered in the immediately preceding handoff. The statement above is the human input; the longer proposed owner statement earlier in this record remains agent-authored draft text.

| Approval binding | Value |
|---|---|
| Approver's connected GitHub login | `jpiquot` |
| Stable GitHub account ID | `6775094` |
| Candidate commit | `411005b4ccb2352249c83330c54967c6a22beba1` |
| Reviewed successor | `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` |
| Successor Git blob | `160331d25451928ff3c3dea2300b65cab4f97c3b` |
| Normative SHA-256 | `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd` |
| Complete successor SHA-256 | `bbec7a16661995849891fae2617cf74c281d3da155086d0e22a39d5a2488f59a` |
| Required candidate-commit owners | `["jpiquot"]` |
| Approval scope | Downstream planning only |

Authentication provenance: the current user's GitHub connector returned `{"login":"jpiquot","id":6775094}` from `github_get_user_login`; `github_get_profile` independently returned the same account ID and nickname. The human approval is preserved in the conversation above, with the connected account supplying its identity context. This is a conversation approval record, not a submitted GitHub review or an externally published signature.

Read-only verification reproduced the exact successor blob and normative digest from the candidate commit, matched candidate bytes to both worktree and index, and resolved the complete architecture-owner set from that candidate's immutable allowlist. All required owners are represented by the verified account. Verification HEAD was `211fe67676a89c969eb8821798b52c7be124dc6b`; unrelated intervening commits do not change the approved subject.

This record supersedes the earlier handoff snapshot's unestablished-approval status for this exact subject only. The original wrapper and successor retain their historical detached status text; no approved bytes, original story status, or sprint row were changed to publish this approval.

Downstream planning is now approved. A separately reviewed evidence specification may be prepared. No isolated evidence candidate may execute until the evidence-only authority binds its tested artifact, environment, expiry, and lifecycle controls. Production evidence and separate implementation authority remain outstanding; frozen v1 remains the runtime authority and Story 4.6 stays `awaiting-operator`.

## Recorded Downstream Authorization

Recorded at `2026-10-31T06:30:17Z` in conversation `01a18ded-3ceb-7c92-b5f5-dbf1e38a4686`. The human's verbatim instruction, following confirmation of candidate approval and the remaining evidence/implementation prerequisites, was:

> I authorize

The GitHub connector was checked again and returned `{"login":"jpiquot","id":6775094}`, matching the sole architecture owner in the approved candidate's immutable allowlist. This instruction commissions the downstream evidence work and authorizes proceeding toward implementation of the same approved semantic candidate once its evidence and design requirements are met. No further general authorization is requested.

The authorized subject remains candidate commit `411005b4ccb2352249c83330c54967c6a22beba1`, successor blob `160331d25451928ff3c3dea2300b65cab4f97c3b`, and normative SHA-256 `995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd`. The preceding approval record identifies the reviewed file and complete-file digest.

This authorization supersedes the earlier statement that a general downstream authorization is outstanding. It does not assert that empirical results exist or waive the approved successor's requirements. The `implementation_authorized: false` projection records that runtime execution is not yet eligible: the evidence and separately reviewed design are not complete. It must not be interpreted as a request for the same human authorization again. Artifact/environment/expiry-specific evidence authority must bind actual qualification inputs; those values are not invented in this record.

### Evidence preflight

At investigation HEAD `211fe67676a89c969eb8821798b52c7be124dc6b`:

- Local prerequisites are available: .NET SDK `10.0.403`, Dapr CLI `1.18.2`, installed Dapr runtime `1.18.7`, and Docker server `29.8.2`. These versions describe this machine; they do not ratify a production profile.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo --verbosity minimal` exited 0 with zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*GlobalPositionActorTests'` exited 0: 3 passed, 0 failed, 0 skipped. These are deterministic v1 baseline tests using an in-memory state manager; they do not satisfy a v2 production-provider evidence row.
- `rg -n 'tenant-domain-v2|composite-tenant-domain-v2|UnsupportedCrossShard|UnsupportedCrossGeneration' src tests perf -g '*.cs'` exited 1, with no matches. The existing allocator still targets actor `global`, and persisted/public positions remain scalar. No current v2 qualification implementation was found.
- `rg --files deploy -g '*production*profile*' -g '*oq8*' -g '*postgres*'` found `deploy/dapr/statestore-postgresql.yaml`; it did not find a canonical production profile. The OQ8 fixture can inform an isolated two-process PostgreSQL lane; broker qualification is a separate disposable experiment, not global-position evidence.
- The load harness documents one implemented synthetic command-load scenario and two stubs. It supplies neither a representative production trace nor an equivalent tenant/domain/composite allocator comparison. No section 7 capacity result can be derived from those scenarios.

### Required qualification work

| Evidence category | Next concrete qualification work |
|---|---|
| Option capacity | Bind the production trace and target profile; compare equivalent tenant, domain, and composite implementations under the same resources and measurement method. Prove composite throughput exceeds both alternatives by at least 20% and the observed production peak stays below 50% of its demonstrated rate. |
| Position identity | Exercise exact v1/v2 wire round trips and reject malformed, overflowed, duplicate, routing-mismatched, or colliding identities. |
| Multi-host concurrency | Run independent hosts and sidecars against the selected shared provider; inspect persisted uniqueness and shard/generation monotonicity. |
| Aggregate/event identity | Prove gapless aggregate sequence, stable message/CloudEvent identity, and unchanged retry results through actual persistence. |
| Restart/failover | Restart each authority component and inspect recovered generations, ceilings, and retry identity. |
| Mixed-version safety | Prove all specified unsupported comparisons and the absence of scalar fallback paths for v2. |
| Cursor/checkpoint/rebuild | Prove committed-source boundaries, before-first migration, stale shard-set handling, atomic progress, and failure recovery. |
| Inventory/migration | Account for every admissible pair and prove initialization, retirement, restore, and partial rollout preserve identities. |
| Overflow/limits | Measure supported bounds and prove failure before mutation or checkpoint advance. |
| Hot-shard recovery | Measure pair isolation and lifecycle-control cost; prove restore resumes above prior ceilings. |
| Rollout/rollback | Prove old-writer fencing and the permanent no-v1 boundary after the first durable v2 allocation. |
| Story 4.5 applicability | Bind an exact reviewed seam diff to the tested candidate; affected append/provider-write seams require the corresponding approved evidence. |

Every empirical row remains unqualified. Before collecting decision-grade evidence, the downstream design must bind the actual tested artifact, provider configuration, topology, environment, evidence expiry, and lifecycle controls to the approved subject. Capacity additionally requires its trace, acceptance limits, measurement method, and validity-profile authority/derivation. Results belong in tests/CI artifacts, with commands and counts retained here.

The outstanding human input is data, not permission: repository paths or artifact links for the representative production trace and the production provider/topology profile. An asynchronous question requested those two inputs while the available baseline checks ran. No synthetic result is substituted for them, and no production runtime or approved specification was changed.

## Development Defaults and Local Candidate

The owner's verbatim answer to the production-trace/profile question was:

> i dont know

Production inputs are therefore unavailable rather than waived. No repeated approval or data request is pending. Development work proceeded with synthetic identities and the existing in-memory state manager, with the repository's PostgreSQL fixture retained as a reference for a later provider lane; it is not silently selected as a ratified production profile.

The [local position mechanics record](spec-4-6-local-position-mechanics.md) captures the isolated test-only candidate, review, exact commands, tested assembly identity, and limits. The Debug build succeeded with zero warnings/errors, and all 34 focused global-position cases passed. The synthetic workload covers 32 shards and persisted reservation ceilings across fixture restarts. All six review findings were addressed.

This completes an independent development step without claiming production capacity, distributed actor guarantees, metadata wire compatibility, or any satisfied section 7 empirical row. The production allocator, approved specification bytes, and original story/sprint status are unchanged. The required production trace/profile and further provider/migration qualification remain explicit future inputs/work; they are not represented as another missing general human authorization.
