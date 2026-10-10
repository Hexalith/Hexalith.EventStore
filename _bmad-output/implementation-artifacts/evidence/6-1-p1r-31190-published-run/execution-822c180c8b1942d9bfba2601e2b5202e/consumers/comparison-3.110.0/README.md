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

The selected logical-event-evolution addition remains incompatible. The live
Domain readiness observation proves this fixture registers its bounded V1
CLR-name serializer and has no registered logical evolution manifest. Legacy
hydration, unknown metadata refusal and unchanged application-envelope readback
remain measured, but cannot qualify registered logical alias or upcast execution.
The fixture does not create an authoritative gateway pin or readiness decision.

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
