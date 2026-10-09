# EventStore 3.115.0 published P1R qualification run

Review iteration 1 is executed and its [permanently retained packet](packet-c497b88cb2e5436ebddb37e33f1fcb64/packet.json)
is independently valid. The [corrected execution](execution-c497b88cb2e5436ebddb37e33f1fcb64/README.md)
records 163 cases, 5,522 checks, 5,412 passed and 110 failed across all seventeen
scenarios/seven families and both additions. Technical qualification, the four
owner decisions, same-baseline conformance and P1R usability remain false/pending.

[Retained-path preparation](review-iteration1-final-verification/prepare.json)
and [separate independent validation](review-iteration1-final-verification/validate.json)
both exit 0. Package checks pass 267/267; the eleven paired Debug/source cases
pass 704/704; strict restore passes 12/12; repeated owned cleanup passes 7/7;
Reminder checks pass 131/131. [Focused verification](execution-c497b88cb2e5436ebddb37e33f1fcb64/verification/focused-tests.json)
passes 115 tests. [Eleven negative controls](review-iteration1-final-verification/negative-controls.json)
are refused at both import and independent validation, with matching refusal
reasons. The actual security errors, wire losses, historical incompatibilities
and missing registered logical evolution remain nonpassing.

The complete execution's sealed packet copy retains its original preparation
receipts and validation result. Its process-control receipts bind the original
working directory, so validation of that relocated copy correctly refuses
[the changed location](review-iteration1-final-verification/relocated-copy-validate.json).
The authoritative packet linked above was prepared directly at its permanent
path, importing only the unchanged receipts from the successful fresh execution.
No source binding or existing seal was rewritten. [Final review verification](review-iteration1-final-verification/README.md)
records the retention correction and exact artifact hashes.

The source closure is `04ca7c61e729ff7154472bd1a6a9fcdc4b4073b940db2aa3f4530abd6f1e6a38`.
Candidate/comparison/runtime/Builds selections, rollback=null, Projects AD-17
mutation freeze/forward recovery and all independent downstream gates remain
unchanged. Three failed invocations and the complete execution with refused
preparation remain separately sealed historical diagnostics; none supplies
receipts to the corrected execution.

## Historical execution before review iteration 1

Status: **executed on 2026-10-07; fresh packet valid; technical qualification false**.
The [final execution index](execution-325e3c9327df4810941981b5882e3a70/README.md) records all seventeen canonical
scenarios/seven families and both selected additions: 163 cases, 4,909 checks,
4,819 passed and 90 failed. [Independent validation](execution-325e3c9327df4810941981b5882e3a70/validate.log) exits 0
and recomputes `valid=true`; technical qualification, all owner acceptance,
same-baseline conformance and P1R usability remain false/pending.

The [final inputs](execution-325e3c9327df4810941981b5882e3a70/owner-inputs.json),
[actual planning/package observations](execution-325e3c9327df4810941981b5882e3a70/planning-observations.json) and
[sealed packet](execution-325e3c9327df4810941981b5882e3a70/packet/packet.json) bind actual candidate 3.115.0 plus
comparison-only 3.70.1/3.110.0. Real package evidence passed 267/267; the distinct
Debug/source comparison passed 329/329, strict restore 12/12, cleanup 7/7,
natural Reminder delivery 125/125, and focused Python verification 92 tests.
Historical incompatibilities, pre-upgrade containment loss and the missing
registered logical alias/evolution path remain nonpassing. Rollback is null;
Projects AD-17 mutation freeze/forward recovery and all independent downstream
gates remain unchanged.

The planning/preflight documents below retain their earlier observations.
They do not substitute for the new source-bound execution evidence.

The [approved run spec](../../spec-6-1-p1r-31150-published-run.md) describes the executor
and the settled selections. Historical sealed packets and the completed
harness spec are unchanged.

## Retained planning observations

| Evidence | Result |
| --- | --- |
| [Package/source/Builds identities](planning-observations.json) | Five actual 3.115.0 archives, their SHA-256 and NuGet content hashes; all nuspec repository commits equal `283b07a52c9c70e1c940164a7011ee8c3ad98b2d`. Candidate tag binds Builds `ba4ca78c3868a4757cb92d912a54c8a237871b54`; its version is an untagged git-describe identity. |
| [Isolated restore command](preflight/package-observation/restore.json) and [output](preflight/package-observation/restore.log) | Exit 0. `CI=true`, fresh `/tmp/p1r-31150-restore-planning-s6svg_uh/packages`, nuget.org only, Release/package properties. This identity-only project was not built or executed. |
| [Actual package archives and restore metadata](preflight/package-observation/archives/) | Five downloaded signed archives and copied `.nupkg.metadata` documents. Downloads and independently restored archive hashes agree. |
| Signature commands/output | `preflight/package-observation/verify-Hexalith.EventStore.{Client,Contracts,DomainService,Server,ServiceDefaults}.{json,log}`; all five `dotnet nuget verify --all ... --verbosity minimal` commands exit 0 and report repository signatures. |
| [Restore assets](preflight/package-observation/project.assets.json) and [lock](preflight/package-observation/packages.lock.json) | Real planning restore graphs; no physically loaded consumer evidence and no package-lane qualification. |
| [Aspire start receipt](preflight/aspire-baseline/aspire-start.json) and [output](preflight/aspire-baseline/aspire-start.log) | `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --isolated --format Json --non-interactive` exits 2. Build succeeds with zero warnings/errors; startup refuses absent nested Tenants projects, AppHost exit 134. |
| [Preparation receipt](preflight/preparation/prepare.json), [validation receipt](preflight/preparation/validate.json) and [failed packet](preflight/preparation/packet/packet.json) | Fresh `python3 tools/p1r-qualification.py prepare --out /tmp/p1r-31150-preparation-h2jvlo7x/packet` and `python3 tools/p1r-qualification.py validate /tmp/p1r-31150-preparation-h2jvlo7x/packet` both exit 2, reporting `preparation observation incomplete`. Packet error: `required Builds input unavailable`. |
| [Shared container observations](preflight/shared-containers.json) | IDs, image IDs, start times and running state captured for the four pre-existing Dapr/Redis/Zipkin containers. No qualification containers started. |

The retained preflight executor/source checkout was `98da5a04e6df33ba026cbaae46d1777acdca7a21`,
which contains the sealed remediation capture secrets-scan exemption. This is
different from both the published candidate source and the prompt's `af2892e8`
checkout. No missing exemption commit was cherry-picked.

The workspace-root Builds checkout is
`397c94a4e246c90b21cf408790fa0d55bf32d795` (4.30.0). It remains unchanged and is
not the candidate's build provenance. EventStore's nested Builds and Tenants
directories are uninitialized; neither was initialized during preflight.

The proposed Redis backend identity observed from `docker image inspect redis:6`
is `redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`.
The Dapr container registry identity is
`daprio/dapr@sha256:68bb6057abbd3cc1267ad895a73415426ba77f2b451de99693aac54b45ea7d0e`.
Installed versions: .NET SDK 10.0.401, Aspire CLI 13.6.0, Dapr CLI 1.18.2 and
Dapr runtime 1.18.4. The candidate's Dapr SDK dependencies are 1.18.10; CLI,
runtime and package SDK identities remain distinct.

Signature verification receipts:

| Package | Command receipt | Output |
| --- | --- | --- |
| Client | [receipt](preflight/package-observation/verify-Hexalith.EventStore.Client.json) | [output](preflight/package-observation/verify-Hexalith.EventStore.Client.log) |
| Contracts | [receipt](preflight/package-observation/verify-Hexalith.EventStore.Contracts.json) | [output](preflight/package-observation/verify-Hexalith.EventStore.Contracts.log) |
| DomainService | [receipt](preflight/package-observation/verify-Hexalith.EventStore.DomainService.json) | [output](preflight/package-observation/verify-Hexalith.EventStore.DomainService.log) |
| Server | [receipt](preflight/package-observation/verify-Hexalith.EventStore.Server.json) | [output](preflight/package-observation/verify-Hexalith.EventStore.Server.log) |
| ServiceDefaults | [receipt](preflight/package-observation/verify-Hexalith.EventStore.ServiceDefaults.json) | [output](preflight/package-observation/verify-Hexalith.EventStore.ServiceDefaults.log) |

## Accepted planning selections

The user accepted all six recommendations on 2026-10-07: the five observed
3.115.0 packages from nuget.org; candidate Builds at untagged `ba4ca78`; no
capable rollback and AD-17 freeze/forward recovery; invocation-owned Redis/Dapr
1.18.4; both additions with `p1r-executed-checks-v1`; and narrowly bound 3.70.1
and 3.110.0 comparison-only packages. At that handoff, comparison archive identities and receipts still required
real downloads/execution. The fresh execution above now records them; neither
version is authorized as rollback. The original **Approve and stop** planning
choice produced the frozen ready-for-development spec; the later implementation
session executed that approved scope without changing its frozen intent.

## Acceptance decisions still required after execution

- EventStore owner: exact published candidate/source capability dispositions and supported package scope.
- Builds owner: candidate provenance/alignment at `ba4ca78`, including its untagged identity; any later release/pin transition has separate scope.
- Solution owner: exact supported capability and recovery envelope, with same-baseline conformance and explicit incompatible historical operations.
- Test owner: completed independent measured package, persisted effects, tenant preservation, restore/append/restart and cleanup evidence for the selected envelope.

These are acceptance decisions, distinct from planning authorization to run
tests. Their absence must keep `decisions_complete=false` and `qualified=false`.
The separate coordinated Projects acceptance/pin/guards/sprint transition and
independent readiness gates remain pending.

## Development evidence and safe retention

These earlier trials are diagnostics and are never imported into the final
packet. Original sealed bytes and invocation artifacts remain in private
storage outside the repository with directory 0700/file 0600 permissions
(executable files 0700). New safe derived copies exclude broad Docker inspection
output, retain original file/output hashes and explain altered outer bindings.
The final executor selects only public preservation fields and its ownership
label before capture. No credentials or database dumps are retained here.

| Safe derived evidence | Purpose |
| --- | --- |
| [Development trials](development-trials-safe/RETENTION-NOTE.md) | Initial full and interrupted executions, fixture/ownership corrections and refreshed input observations. |
| [Source graph trial](source-graph-trial-safe/RETENTION-NOTE.md) | Complete intermediate run with the distinct Debug dependency-graph defect. |
| [Correction smokes](correction-smokes-safe/RETENTION-NOTE.md) | Real source route/authentication corrections, all selected operations, restore and failure drills. |
| [Scheduler trial](scheduler-coverage-trial-safe/RETENTION-NOTE.md) | Owned interruption before adding completed natural Scheduler observation. |
| [Natural Reminder smoke](natural-reminder-smoke-safe/RETENTION-NOTE.md) | Completed natural sequence 13 before injected duplicate/stale callback in all three cases. |
| [Intermediate capture](execution-627a9059adee4dcfa35d15afaabd9346-safe/RETENTION-NOTE.md) | Abandoned pre-final capture after broad discovery output was found. |
| [Privacy discovery trial](privacy-discovery-trial-safe/README.md) | Owned interruption, repeated cleanup, safe derived receipts and original hashes. |

The final [source/configuration/direction negative controls](execution-325e3c9327df4810941981b5882e3a70/negative-controls.json)
refuse substitutions at both import and independent validation. Each newly
retained directory has its own SHA256SUMS; historical originals were preserved
without rewriting their seals.
