# Published P1R consumers

The executor copies these consumers into a fresh invocation, uses exact central
package versions, `CI=true`, a private `NUGET_PACKAGES`, lock files and Release
outputs. Its source comparison uses a separate Debug/project-reference graph.
There are no source references in a published lane. Every consumer loads the
selected assemblies and reports their physical paths and SHA-256 hashes.

The Counter domain emits bounded V1 JSON through the actual published domain
SDK. It has no provider client or persistence implementation. Actor commands,
signed fences, trusted-effect inboxes, reminders and application-envelope readback execute
through the published packages and Dapr. PostgreSQL v1 administration is restricted to
invocation-owned fixture provisioning, stopped-writer fixture diagnostics,
logical `pg_dump` custom-format backup/fresh-container restore and ownership-verified cleanup.
The tracked actor-state component is rendered with an invocation-private connection
string in scratch; retained configuration contains the redacted tracked template and
the SHA-256 of the rendered file. Redis remains a separate private pub/sub dependency.
Container discovery requests only IDs, image IDs, names, running/start state
and the invocation ownership label; environment and credential fields are
never read into retained command output.

The explicitly enrolled 3.121.0, proposed 3.122.1 and proposed 3.125.0 consumers register the pinned qualification-only
logical evolution manifest and bounded V1 serializer. Live Domain readiness,
legacy alias hydration, unknown metadata refusal and unchanged application-envelope
readback measure the addition. Historical comparisons retain their own bounded
fixtures. The fixture does not create an authoritative gateway pin or readiness decision.

The 3.121.0, proposed 3.122.1, proposed 3.125.0 and distinct Debug/source consumers enroll the same finite Counter
folded-snapshot limits on both hosts. The domain measures detached state
round-trip, canonical increment bytes and Apply parity before enrollment. The
Counter has no automatic snapshot processor. Snapshot fixtures commit nine real
events, invoke the published manual snapshot API, stop the writer and retain the
persisted version-one envelope and folded count nine, then append three real
events. Original event and snapshot hashes must survive those appends. Historical
comparison fixtures retain their original stopped-writer snapshot diagnostic;
they cannot establish genuine successor snapshot creation. Enrollment is a finite
tooling allowlist; later versions receive no successor compiler definitions or
snapshot fixture sources, and an unenrolled successor's genuine seed refuses.
The refusal occurs before fixture state reset or writer start. The selected
candidate's restore writer must use that exact candidate version. The 3.125.0
operational importer also requires the final private isolation and cleanup journal.
The strict witness checks bind each required case to its selected candidate or
source writer, including the selected version for post-upgrade restore. They
require retained shared fixture source, physical consumer binaries, owned launches
and the stopped-writer provider envelope; missing or substituted proof is refused.

The historical `snapshot-nine` diagnostic replaces the entire snapshot `state`
with the folded Counter object `{"count":9}` and sets its covered sequence to
nine. Earlier diagnostics added `count` to a `DomainServiceCurrentState` object,
leaving `currentSequence` and `events` in that object; the production snapshot
inspector correctly refused this ambiguous DTO collision. The corrected fixture
preserves the remaining envelope, protection metadata, original events, retained
floor and second tenant. Active writers still prohibit diagnostic mutation.
This fixture correction does not alter production snapshot creation or relax
the corruption, protection or replay-floor guards.

The Host declares a bounded Test fixture semantic adapter and effect admission
policy. The invocation generates private keys and credentials; they are never
returned in responses or written into retained receipts. This authority allows
the real signed package methods to produce measurable persisted effects. It
does not qualify production identity, authenticated transport, P2 acceptance or
deployment. Reminder intents are explicit fixture inputs; Dapr owns reminder
witnesses, scheduling and persisted target effects.
Reminder checks observe sequence 12 before the positive restart and poll the
read-only actor sequence until natural scheduler delivery commits sequence 13.
Only afterward do they inject duplicate or stale callbacks; injected callbacks
cannot establish the positive scheduler delivery assertion.

The invocation uses private loopback endpoints and a scheduler broadcast address
bound to its published port. Dapr HotReload and .NET configuration reload are
disabled only in these consumers, avoiding the shared host's exhausted inotify
watch budget. The rendered component and discovery files are retained with
hashes; these settings do not change the selected runtime/backend profile.

The separate source consumer imports the owning source central package graph,
including its transitive pins. Its operational domain routes use the current
SDK's real JWT issuer and validation with a private Development symmetric key,
exact audience and caller. The source domain returns its physical identity from
the SDK-permitted anonymous readiness route, preserving its route guard.
This bounded authority is source comparison evidence;
production identity and P2 acceptance remain pending.

Every Boolean probe check has a stable ID, including failures. Counters are
derived exclusively from the retained check records. Historical unsupported
methods are observed before dispatch, and remain incompatible. Published
comparison inputs never become rollback selections. All four owner decisions,
same-baseline conformance and the separate usability transition remain pending.

Corrected qualification retains predicate operands and their source bytes once
per lane receipt, and binds each probe check to its literal command output.
Independent validation rederives domain inventory hashes, exact replay
coordinates and sidecar configuration paths/content. Every supported metadata
append restarts its writer and rehydrates sequence 13. Each shared Debug/source
case executes the corresponding selected-package case, with normalized domain
outputs and inventory coordinates and explicit semantic deltas.

Only the existing specific fence/gateway-proof denial qualifies security
refusal. Undisclosed remote exceptions, missing audit and infrastructure errors
remain failed execution. The natural Reminder proof additionally requires
initial/restart convergence to submit zero direct effects and rejects read-only
observations completing after their bounded deadline.

Run published observations/execution through `tools/p1r-published-executor.py`;
its `observe` and `run` commands now enter the isolated runner automatically.
The sibling `<out>-isolation/` directory retains wrapper evidence. Inventory
subcommands invoked by the executor inherit the fixed private endpoint.
The immutable input guard still requires the selected tag and source HEAD to
match. A dirty tooling repair requires a separately scoped verification command;
it cannot use that guard to claim immutable package qualification.

For an explicitly scoped repair verification, run from the EventStore root:

```sh
python3 tools/p1r-isolated-runner.py \
  --out _bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/isolation-UNIQUE \
  -- python3 "$PWD/_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/verify-and-audit.py" \
  --attempt "$PWD/_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/attempt-UNIQUE" \
  --repo "$PWD"
```

Both output directories must be new and inside the mounted workspace. The whole
measurement attempt freezes all three executed scripts, refuses reuse, retains
separate plan/summary/audit files, and verifies script hashes again after audit.
The measurement audit leaves overall evidence validity pending the final outer
controller audit, including its final cleanup and actual owned absence.
The driver receives the existing workspace at
its original absolute path, allowing Git provenance and adjacent root-declared
source references to retain their identities. The outer endpoint is captured
once; `--outer-host` can select it explicitly. No outer Docker configuration,
credentials or socket is mounted into the runner. The runner publishes no ports,
uses no host network, and shares only the private daemon's network/PID namespaces
and an invocation-owned socket/data volume. The qualification daemon still
observes its complete shared-container inventory and requires exact equality;
unrelated outer-daemon activity never becomes an exclusion rule.

The Linux/amd64 glibc SDK 10.0.401, Docker 29.8.2 daemon/CLI and Python 3.12.12
tool images are pinned by manifest digest in `p1r_isolation.py`. Python and the
Docker CLI are copied into the ephemeral SDK runner; no Dockerfile, apt install
or repository dependency update is involved. Dapr 1.18.2, the pinned PostgreSQL
profile and the selected package versions remain executor inputs. Private
execution retains daemon IDs, executable hashes, source binding, actual namespace
links, literal command/exit, and repeated process cleanup. `isolation.json`
retains exact outer identities and two ownership-checked container/volume cleanup
attempts on success, command failure or interruption. Any failed attempt remains
nonpassing even when a retry removes its resources.
SIGINT and SIGTERM request orderly interruption of the exact registered private
supervisor before guarded outer teardown. Unexpected remaining private containers
or unavailable private inventories retain the daemon and volume without removing
those containers. An unavailable final supervisor cleanup receipt also preserves
the SDK runner. These refusals retain nonpassing evidence for later inspection.


The supported controller profile requires a local Unix Docker endpoint
(`unix:///absolute/socket/path`). TCP, TLS and SSH contexts are refused before
provisioning, with a retained refusal that omits endpoint credentials. Ambient
Docker TLS settings and client configuration never enter retained proof. Private
daemon start and readiness probes share one 30-second monotonic deadline; each
readiness probe has at most two seconds, and late successful probes still fail.

Proposed 3.122.1 and 3.125.0 receipt import requires the complete controller journal. It binds
all three pinned image OS/architecture/digest/image-ID measurements to literal
role creation and inspection, the Python/Docker copy chain, the prelaunch SDK
toolchain measurement and actual `_inner` launch. Every command has a distinct
ordered ID and contained timestamps; unrelated Docker mutations are refused.
The exact executor arguments select a physical input path and raw file hash.
Private supervisor, root and owned process identities include PID, start ticks,
PGID and session, with two distinct timed cleanup observations. Docker client
and server versions and Docker/dotnet executable hashes must agree with the
prelaunch measurements. Successful private execution and both outer cleanup
attempts precede final receipt attachment through the real controller caller.
Private, outer or finalizer failure remains nonpassing. Historical imports retain
their original contracts, and these tooling controls confer no package qualification.

Source repair verification and immutable package qualification remain distinct.
After independent repair review/verification, propose an immutable successor
publication with exact approved version, source/tag, Builds identity and archive
hashes, then obtain approval and qualify those newly published bytes under the
unchanged runtime/backend profile. The finite 3.122.1 enrollment is a proposal,
contingent on an authorized fix landing and repeated canonical release and registry
availability analysis. It does not select packages, change production snapshot
enrollment or qualify unpublished bytes. Frozen source/tag and package identities,
Builds construction/execution identities, successful source CI, publication authority,
signed public archives and full published qualification remain outstanding gates. All
four role acceptances, same-baseline conformance and Solution Architect
disposition remain pending; P1R is unusable and Story 6.1 remains blocked.

The source landing closes retained findings B1/E1 (socket exposure), B2 (root and
unsupported workspace mounts), B4 (early scratch cleanup), B5 (non-UTF-8 failure
receipts), E2 (inventory executable binding), E3 (shell-mediated provider writes)
and V1 (real Host/Domain registration coverage). Socket alias enumeration fails
closed on unreadable subtrees. Genuine seed journals permit only the finite
direct execution profiles; their exact provider query remains read-only.
`test_p1r_snapshot_registration.py` builds and executes the real fixture programs,
checks their actual service providers at HostBuilt, and proves both refuse missing
enrollment. It stops before external resources start and grants no qualification.
B3 remains an ordinary follow-up: root-created output ownership needs a separate
UID/volume-access design and real-container cleanup validation. The existing
qualification and cleanup requirements still apply before relying on a run.

Historical captures remain at their original locations under the external
`/home/administrator/projects/hexalith/p1r-evidence-disposition-20261016/_bmad-output/implementation-artifacts/evidence/6-1-p1r-evidence-disposition-20261016/evidence-retention-manifest.json`
(SHA-256 `52c47102136221f03876ae07e4b18bade4c7d4e21f2c9b4f44360831e1099fa0`).
This source change adds no captures and changes no scanner, retirement record or seal.

Read-only analysis of canonical main `74b8a6711400fc2853acf6e63b9ea4c751078d58`
with the pinned commit analyzer proposes 3.123.0 from reachable v3.122.0 plus this
fix. Published v3.123.0 points to `f78916d9534a72ba1515d97cfd51408e6eb809f1`,
outside that main ancestry. Publication therefore needs an owner decision about
release history before finite enrollment can be reconciled to an available
proposal; that historical analysis did not select a package.
No version is reserved or selected. Source CI, genuine same-run one-use authority,
protected production approval, published qualification and existing acceptance
gates remain required. The source handoff is the normal PR and its CI results.

## Current-source successor readiness, 2026-10-17

The finite tooling fixture now also selects the capability, logical-evolution
and folded-snapshot paths in project evaluation for proposed 3.125.0. This is
a readiness enrollment
for the current EventStore authority/recovery source, not a published package
selection. The 3.124.0 published family lacks those current-source changes.
No 3.125.0 archives, tag, registry signature, lock graph, loaded Release package
identities or source/tag equality have been verified here. A future published
attempt must select the exact tagged source and Builds revision, verify all five
signed archives, use isolated Release package graphs, and run the full strict
qualification matrix and private resource cleanup. The pinned Dapr 1.18.2
PostgreSQL profile remains the required published comparison; source-only
Debug/project-reference checks cannot satisfy it.

Rollback remains null. Drain and freeze incompatible ingress, then recover
forward until a separately qualified capable rollback is selected. Publication,
consumer pins, P1R acceptance, production/story gates and Story 6.4 remain open.


## Exact 3.125.1 enrollment, 2026-10-18

The finite qualification fixture additionally enrolls published 3.125.1 in the
same capability, logical-evolution and folded-snapshot compiler paths, genuine
seed requirements, retained fixture source, strict physical-writer witnesses and
final isolation/cleanup attachment and import checks. Existing 3.125.0,
historical and Debug/source paths retain their contracts. A selected 3.125.1
restore writer must be exactly 3.125.1; either cross-version substitution with
3.125.0 refuses before provider I/O or writer start. Unenrolled 3.125.2 and
3.126.0 receive no successor compiler paths and genuine seeds refuse before
fixture reset.

The focused executor, qualification, registration and isolation suites verify
these tooling boundaries. Registration evaluates Host, Domain and Probe for
both enrolled versions and both unenrolled controls, and separately executes
the real source Host/Domain programs at HostBuilt with missing-enrollment
negative controls. The 3.125.1 witness class executes the existing strict
snapshot, physical binary, provider, private isolation, timing, cleanup and
recovery import corpus. These checks execute no published P1R qualification.
Exact commands, results and preservation proof are retained in
`../../_bmad-output/implementation-artifacts/evidence/6-4-p1r-31251-enrollment/`.

The released v3.125.1 source is
`55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`. The approved tooling repair adds
an optional `tooling: {commit, builds_commit}` block to planning observations and
owner inputs. Both are full commits selected after review. `observe` and `run`
require the tooling checkout and its actual qualification Builds checkout to
match those pins before restore or resource creation. Without the block, the
existing selected-tag/source/tooling equality guard remains in force.

The separate-root path accepts only exact 3.125.1. Both `--source-root` and
`--source-builds-root` are required: source HEAD and v3.125.1 must resolve to the
release commit above, and source Builds must match release gitlink
`4629ff2d3bd66376b29a553663c241d3a7b87853`. Both paths must be inside the
isolated workspace mount. All tracked inputs must be clean; added code and
configuration, including ignored compiler inputs, refuse. Generated `bin`/`obj`
outputs remain excluded. There is no bypass flag.

Scripts, fixtures and witnesses retain tooling attribution. Debug source
ProjectReferences and the PostgreSQL component template use the verified release
root; existing MSBuild import properties select its verified Builds catalog and
the effective runtime version 3.125.1. Preprocessed imports, evaluated versions
and the complete source-reference closure are checked before restore and retained
in the checkout receipt for independent import validation. Release consumers
continue to restore the selected packages without source references or source
catalog overrides. No release source or catalog edit is needed.

The existing source binding retains its tooling inventory and qualification
Builds identity, with `runtime_source` recording the source inventory and source
Builds path/identity. Both roots are rechecked through private isolation,
execution, receipt import and packet validation, including after owned cleanup.
Removing or swapping runtime evidence remains a refusal after receipt hashes are
recomputed. `candidate.builds` continues to describe package construction; it
does not select the qualification Builds checkout.

The tooling and qualification Builds commits remain unselected. Named owners
must still select the exact published tuple and prove archive/restore/lock/loaded
graph agreement before a separately authorized published attempt. Tooling tests
run no published qualification and change no production snapshot policy, pins,
rollback=null, AD-17 drain/freeze/forward recovery, acceptance or Story 6.4 hold.
