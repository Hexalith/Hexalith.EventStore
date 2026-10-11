# Qualification, Ownership, and Recovery

## Authority boundaries

Jerome approved the October 6 planning handoff. The [repository-local runtime scope](../../implementation-artifacts/spec-6-1-p1r-remediation-runtime.md) records source implementation approval and completion; its scope prepares qualification. Neither approval selects new candidate/rollback coordinates, grants publication/deployment, changes pins, nor accepts a new exact tuple.

Architecture IDs are repository-scoped. The handoff's repository ownership, mutation freeze/forward recovery, dual-principal authority, and truthful release evidence refer respectively to **Projects AD-6, AD-17, AD-20, and AD-30** in the adopted Projects Architecture Spine. EventStore has different AD-6/AD-17 decisions; its AD-17 governs command-status Location and is not this recovery policy. EventStore architecture governs its own implementation and production/release gates.

| Owner | Required decision or deliverable |
| --- | --- |
| EventStore Owner / Developer | Repository-local runtime scope, exact tested source and baseline inputs, safe metadata/replay/capability implementation and source/package treatment of every family. |
| Builds Owner | Own local catalog/tool/publication scope and candidate alignment/provenance; coordinate any later exact catalog/pin changes. |
| Platform and Identity/Security owners through P2 | Complete authenticated transport, admission, exact persisted watermark, recovery semantics, and refusal of incapable routes. |
| Solution Architect | Supported capability/migration/rollback envelope, same-baseline conformance, and any later exact Stack rebinding. |
| Test Architect | Independent source/package, successful effects, restore-plus-append, tenant, cleanup, preservation, and required assertion evidence; exact candidate/rollback decision. |
| Release Engineering | Separately authorized immutable publication and later operational/release drills. |
| Product Owner / Projects maintainer | Existing epic/story inventory and independently gated downstream planning/acceptance; no source proof silently advances readiness. |

## Qualification sequence

1. Preserve historical inputs and unrelated changes; record repository-local scopes and exact tested source/Builds/configuration. Reproduce all seven families and distinguish effective fixes, remaining source defects, and immutable old-package incompatibilities.
2. Enforce the supported authority/watermark/API boundaries through EventStore/P2. Define the supported capability inventory, including independently qualified Reminder or later additions selected into the envelope.
3. Prepare green source/capability/recovery evidence, then obtain separately bound owner publication authority for an explicit candidate and any proposed capable rollback family. Do not guess a dependency version or rebuild an existing published version.
4. Independently verify the actual immutable published inputs in isolated Release/package lanes. Bind exact EventStore candidate/rollback source, package, tag, archive repository metadata, Builds, assets/lock graphs, and physically loaded identities. Package provenance differing from tag provenance remains explicit.
5. Execute the applicable finite compatibility matrix and real persisted operational recovery/cleanup lanes. Record execution outcome and compatibility disposition separately; every required supported lane must pass.
6. Obtain exact EventStore/Builds/Solution/Test decisions and same-baseline conformance. Only then prepare a validated coordinated fixed-record-v1/guard/catalog/Stack/consumer-pin transition under its owners. Use the unchanged planning guard and focused negative controls; preserve frozen scope and historical acceptance/evidence.
7. Return independently to P0/P2/P3, same-baseline architect sign-off, P4/spec readiness, and an independent assessment returning exactly READY. P1R alone cannot start Story 6.1 or complete Story 8.11.

The current source packet satisfies a bounded source-remediation slice. Its recorded `published_candidate=null`, `capable_published_rollback=null`, `qualified=false`, and `p1r_usable=false` keep the later gates open.

## Measured 3.119.0 support boundary

The fixed case table is `tools/p1r_support_boundary.json`. It names all 163 cases in the 19 scenario/addition receipts of sealed packet `packet-677345025f7b42449cbf8ce4afb43813`, whose `packet.json` SHA-256 is `1cb9d427c34db61ad250173b13016c69de7e13db522c4fa45cf478fa2d468b4c`. The table binds each original operation, expected and actual outcome, disposition, failed assertion count, assertion ID inventory, and receipt hash. Its source pin is `19892532995447336389fe98daaef19c44636942b3a33b791e13b2f534aab7e3`. The sealed receipts and every failed result remain unchanged and visible. The table applies only to the measured 3.119.0 candidate with the 3.70.1 and 3.110.0 comparisons. A different candidate needs a separately measured boundary and owner decision.

All cases marked `supported` must still pass their measured effect or refusal, with positive assertions and compatible disposition. This includes every passing legacy-principal query direction, passing replay and mixed-client call, the candidate's failing stale-fence check, and all three failing logical-event-evolution checks. `containment-only-backup-restore` remains an explicitly incompatible, observed containment case; it never proves writable rollback or RPO 0. With `rollback=null`, Projects AD-17 mutation freeze and forward recovery remain mandatory.

The following 39 historical cases may be treated as bounded refusals only after a new receipt proves admission at the incapable published endpoint. The original failed or incompatible case is retained in the fixed table; its new execution records an `unsupported/refusal/incompatible` case rather than claiming the old binary gained the capability.

| Lane | Exact refused case IDs | Admission requirement |
| --- | --- | --- |
| legacy-metadata | `3.70.1-pascal-floor-None`, `3.70.1-web-floor-None` | Refuse authoritative metadata access when the loaded type lacks `RetainedFloor`. |
| metadata-read | `3.70.1-pascal-floor-1`, `3.70.1-pascal-floor-5`, `3.70.1-web-floor-1`, `3.70.1-web-floor-5` | Refuse before reading a protected floor through the floor-losing type. |
| metadata-write | `3.110.0-to-3.70.1`, `3.119.0-to-3.70.1` | Refuse at the 3.70.1 writer before actor dispatch, append, or retained-floor mutation. A typed metadata read is never writer proof. |
| invalid-evidence | `3.110.0-invalid-floor`, `3.110.0-protected`, `3.110.0-unknown-version`, `3.70.1-invalid-floor`, `3.70.1-protected`, `3.70.1-unknown-version` | Refuse unsafe historical package identities before invalid persisted evidence is applied. Candidate invalid-evidence checks remain supported. |
| query-wire | `3.110.0-3.70.1-json-dual`, `3.110.0-3.70.1-xml-dual`, `3.110.0-3.119.0-json-dual`, `3.110.0-3.119.0-xml-dual`, `3.70.1-3.110.0-json-dual`, `3.70.1-3.110.0-xml-dual`, `3.70.1-3.119.0-json-dual`, `3.70.1-3.119.0-xml-dual`, `3.119.0-3.110.0-json-dual`, `3.119.0-3.110.0-xml-dual`, `3.119.0-3.70.1-json-dual`, `3.119.0-3.70.1-xml-dual` | Refuse at the incapable endpoint before JSON or DataContract serialization can drop original actor, workload, delegation, scopes, audience, or admission proof. |
| projection-wire | `3.110.0-3.70.1-json-positive`, `3.70.1-3.110.0-json-positive`, `3.70.1-3.119.0-json-positive`, `3.119.0-3.70.1-json-positive` | Refuse at 3.70.1 before global position 987 can be lost or used as an authoritative cursor. |
| mixed-api | `3.110.0-status`, `3.110.0-stale-fence`, `3.70.1-status`, `3.70.1-cursor-scope`, `3.70.1-fenced-effect`, `3.70.1-stale-fence`, `3.70.1-trusted-effect`, `3.70.1-unauthorized-effect`, `3.70.1-retained-floor` | Refuse before unavailable status fields, cursor scope, actor methods, or unproved 3.110.0 stale fencing can present a protected effect or completion. |

For each refusal, the receipt must bind the exact case ID, lane, physically loaded old version, literal `Probe.dll admit` command, output hash, positive measured assertion, and a typed `incapable-published-endpoint` denial at `before-protected-route`. The admission result must state `protected_access=false`, `effect=false`, and `write=false`; command execution, check witnesses, and equal before/after case digests are independently validated. Since admission performs no state read or write, those equal digests are a no-dispatch control, not persisted-state proof. Omitted/substituted cases, opaque errors, invented refusals, missing assertions, or later-stage denial do not satisfy the table. This bounded fixture admission does not grant production P2 authority or candidate qualification.

## Lossless recovery envelope

- Maintain a single writer. Fence and drain incompatible ingress before any transition; no old/new dual command writes.
- Writable rollback requires the actual rollback reader and writer to understand retained metadata and all new supported events/authority/APIs. Restore floor 5/head 12 with snapshot 9 into a fresh owned database, rehydrate, append 13, preserve floor/old event hashes/tenants, and restart/replay. Successful hydration alone is insufficient.
- Until a capable published rollback is independently qualified and selected, enforce the approved Projects AD-17 mutation freeze and forward recovery. An incapable old endpoint cannot receive a protected operation or imply completion.
- Keep pre-upgrade restoration as an explicit containment-mechanics observation. It cannot satisfy RPO 0 by discarding later committed writes. Do not accept data loss or shrink the supported feature envelope by inference.
- Read-routing rollback retains its own deterministic equivalence gate; it does not establish safe writable package downgrade.
- Historical PostgreSQL/Dapr 1.18.2, current Redis/Dapr 1.18.4, and any newly selected runtime/backend/profile require their own bound evidence. A backend or package change cannot inherit an old pass merely because source code is similar.

## Evidence requirements

Retain per-case exact argv, working directory, start/end times, exit status, measured assertion count, fixture identifiers, configuration/source/package hashes, archive/signature provenance, resolved assets and lock graphs, output hashes, physically loaded assembly names/paths/hashes, and persisted inventory hashes. Unexpected EventStore source dependencies invalidate a package lane. Source Debug/project references never substitute for Release/published package consumption or local packaging smoke for actual archive proof.

Assert domain state, sequence, retained floor, snapshot/tail fold, original event bytes/hashes, and tenant isolation. Failed hydration can write documented command-status/dead-letter bookkeeping; count it separately instead of asserting zero infrastructure writes. Real successful fenced/trusted effects and stale/unauthorized negative controls are required; null dispatch, compilation, mock counts, and API status alone are insufficient.

Build affected test projects individually. Local source uses `-c Debug -p:UseHexalithProjectReferences=true -m:1`; invoke built xUnit v3 assemblies with single-dash class/method filters and run affected suites. Restore again when switching dependency modes. Before future code edits, follow the repository Aspire baseline workflow. Record exact blockers separately from focused checks without weakening gates. Use only root-declared repositories; preserve unrelated working-tree changes.

Each execution is `passed`, `failed`, or `unavailable`; compatibility is independently `compatible`, `incompatible`, or `unverified`. A passing negative control can demonstrate incompatibility. Missing, failed, skipped, zero-assertion, unavailable, or incompatible required lanes remain nonpassing. Unmeasured assertion totals remain null/unmeasured and cannot satisfy instrumented published qualification. Existing broad-suite skips remain visible even when focused source regressions pass.

Use a new evidence directory and unique receipt names; do not overwrite an existing invocation. Keep safe diagnostics, counts, and hashes. Credentials and database dumps stay in invocation-owned scratch and are destroyed after hashes/restore results are recorded. Cleanup proves exact process/container ownership and labels, no remaining owned resources, and unchanged shared container IDs/image IDs/start times/running state. Startup failure, timeout/cancellation, and repeated cleanup require real retained drills; mocked controls alone are insufficient. Continue all owned cleanup attempts after an individual failure and retain a nonpassing summary and raw failure receipts.

## Preserved prerequisite and release gates

The fixed October 1 P1R acceptance, P0 Stage 1, DW-35, DW-68, and completed original investigation retain their decisions. Current P1R usability stays false; P0/P2/P3/P4, G-6, NOT_READY, blocked Story 6.1, and downstream states are independent. G-6 source/runtime acceptance does not qualify P1R packages. Story 6.7 reversible reads and Epics 7–8 consume the applicable capability/recovery proof without inheriting unsafe old methods.

Story 8.11 remains the terminal release decision with complete Projects AD-30 evidence and dated Jerome and John acceptance, alongside EventStore's applicable publication/production gates. Candidate publication or evidence validation alone grants no release availability, promotion, traffic, or consumer migration. The spec neither changes those gates nor performs their decisions.
