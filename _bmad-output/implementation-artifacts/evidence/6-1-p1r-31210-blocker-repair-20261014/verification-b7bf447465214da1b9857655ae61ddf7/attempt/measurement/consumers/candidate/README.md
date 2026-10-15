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

The explicitly selected 3.121.0 consumer registers the pinned qualification-only
logical evolution manifest and bounded V1 serializer. Live Domain readiness,
legacy alias hydration, unknown metadata refusal and unchanged application-envelope
readback measure the addition. Historical comparisons retain their own bounded
fixtures. The fixture does not create an authoritative gateway pin or readiness decision.

The 3.121.0 and distinct Debug/source consumers enroll the same finite Counter
folded-snapshot limits on both hosts. The domain measures detached state
round-trip, canonical increment bytes and Apply parity before enrollment. The
Counter has no automatic snapshot processor. Snapshot fixtures commit nine real
events, invoke the published manual snapshot API, stop the writer and retain the
persisted version-one envelope and folded count nine, then append three real
events. Original event and snapshot hashes must survive those appends. Historical
comparison fixtures retain their original stopped-writer snapshot diagnostic;
they cannot establish genuine successor snapshot creation.

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

Source repair verification and immutable package qualification remain distinct.
After independent repair review/verification, propose an immutable successor
publication with exact approved version, source/tag, Builds identity and archive
hashes, then obtain approval and qualify those newly published bytes under the
unchanged runtime/backend profile. No successor version is selected here. All
four role acceptances, same-baseline conformance and Solution Architect
disposition remain pending; P1R is unusable and Story 6.1 remains blocked.
