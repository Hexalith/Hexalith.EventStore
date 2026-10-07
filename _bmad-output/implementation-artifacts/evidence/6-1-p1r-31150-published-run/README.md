# EventStore 3.115.0 published P1R qualification run

Status: **approved and stopped on 2026-10-07; spec ready for development**.
The [owner-inputs document](owner-inputs.json) records the accepted candidate,
Builds, rollback, Redis/Dapr profile, additions and Test-owner instrumentation.
No qualification packet or owner acceptance has been created. P1R usability
remains false.

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

The executor/source checkout is `98da5a04e6df33ba026cbaae46d1777acdca7a21`,
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
and 3.110.0 comparison-only packages. Comparison archive identities and their
receipts still require real downloads/execution; the comparison selection does
not authorize either version as rollback. The user selected **Approve and stop**:
the implementation spec is `ready-for-dev`, its approved intent is frozen, and
implementation/qualification execution awaits a fresh build session.

## Acceptance decisions still required after execution

- EventStore owner: exact published candidate/source capability dispositions and supported package scope.
- Builds owner: candidate provenance/alignment at `ba4ca78`, including its untagged identity; any later release/pin transition has separate scope.
- Solution owner: exact supported capability and recovery envelope, with same-baseline conformance and explicit incompatible historical operations.
- Test owner: completed independent measured package, persisted effects, tenant preservation, restore/append/restart and cleanup evidence for the selected envelope.

These are acceptance decisions, distinct from planning authorization to run
tests. Their absence must keep `decisions_complete=false` and `qualified=false`.
The separate coordinated Projects acceptance/pin/guards/sprint transition and
independent readiness gates remain pending.
