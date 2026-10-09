# Explicit older logical snapshot replacement verification

The dormant internal `ReplaceLogicalSnapshotAsync` entry now admits and replaces
a complete, strictly older logical snapshot pair under
`dapr-actor-logical-snapshot-replacement-v1`. The existing initial issuer still
holds nonidentical pairs. Both completed origins, exact prior bytes and the
pinned canonical codec are checked inside one supplied serialized owner
decision; both images share the actual aggregate save and independent paired
readback. The [companion model](../../../story-6-6-dapr-logical-snapshot-replacement-model.md)
defines this local policy. Existing snapshot encoding and ordinary v1 prefix
tags are unchanged, with no production registration or authority.

Final current-byte isolated Debug/source and Release/packages runs each passed
360 controls and killed all 25 compiling timed mutations. Every control has
errors, failures, skipped and not-run zero. All 96 sequential commands in each
run retained unchanged dynamic copied-input, imported-helper and executed-DLL
sets. Debug's recorded whole-root set drifted in exactly three unrelated paths:
`Server/Security/InteractionOccurrenceRegistryActor.cs`,
`Client.Tests/Streams/DirectoryAtomicAppendTests.cs` and
`Server.Tests/Security/InteractionOccurrenceRegistryActorTests.cs`.
Release's recorded whole-root set also drifted in exactly three non-owned paths: `Server/Actors/AggregateActor.cs`, `Server/Security/GovernanceScopeGuardReducer.cs` and `Server.Tests/Security/GovernanceScopeGuardTests.cs`. The private Release copy retains the pre-drift aggregate main file; the parent's current-root build must cover its arriving source-publication fence change. Exact before/after SHA pairs are in the receipt summaries;
these runs qualify the isolated copied-input and unchanged owned-byte scope,
without a stable whole-workspace claim. The full
receipts, exact commands, logs and per-command SHA-256 sets are retained in
[Debug](final-debug/summary.json) and [Release](final-release/summary.json), with
their compressed original JSON/log inventories beside them.

| Control group | Passed cases per configuration |
| --- | ---: |
| New older-pair replacement | 51 |
| Existing Client logical model | 16 |
| Existing snapshot candidate | 66 |
| Existing reconstruction | 53 |
| Existing ordinary source operation | 39 |
| Existing anchor preparation and initial issuer | 56 |
| Existing ordinary command state | 76 |
| Actual anchored tail and head zero-tail | 3 |
| Total | 360 |

The new controls exercise actual ordinary completed replay owners and the
actual aggregate partial through Begin, page commit, paired stages, save and
fresh readback. They cover ineligible/mixed/torn pairs, actual prior history,
canonical roundtrip, full private witness substitution, post-stage prior and
desired history loss, exact idempotent retry, save acknowledgement uncertainty,
no repeated pending save, original cancellation and foreign-token callback
failures, retained decoding/graph/SDK capacity and captured array clearing.
Valid ledger substitution during the last pair readback withholds Proven in
fresh, pending and saved routes. The final ordering is pair readback followed
by source/desired-origin and applicable prior-origin fences, under the required
common serialized owner; no later readback reopens that history window.

Pending replacement recovery freshly proves the retained prior before that
call can return Proven. If current serving proof fails after independent
durable classification as Proven, conclusive cleanup withholds that call's
result and releases its pending images. A later fresh call may independently
admit the exact desired pair through its entire desired completed history,
fresh canonical read/write proof and final readback/fence. Tests explicitly
cover this third-call success and third-call noncanonical refusal; it does not
reuse the lost prior proof or release the failed call's result.

Each mutation builds with warnings as errors before its named behavioral
killer executes, within a 60-second combined build/test lane. Compile failures,
timeouts, missing discovery, skipped/not-run cases and surviving mutations
cannot qualify as kills. The current killers observe forbidden stage/save or
callback counts, durable bytes, retained charges, array clearing, no-repeat-save
and bounded owner-decision completion. The full-private-witness mutation removes
the two overlapping witness guards together to test their shared requirement.
Affected established initial issuer, paired-save/readback, private-pin,
materialization, cache-release and owner-decision controls run again at these
bytes; unrelated historical guard receipts retain their original scopes.

The harness copies every runtime source file from Client, DomainService,
Server, Contracts and ServiceDefaults, plus UniqueIds; it excludes no external
runtime path and makes no runtime source substitution. It builds narrow
generated Client/Server test projects with explicit fixture copies instead of
claiming the full test solution. Exact fixture and source inventories, generated
project/build wrappers and root .editorconfig/.gitattributes/nuget.config,
global.json and imported build inputs are retained in
[source-inputs.json](source-inputs.json) and its source archive. The guard
receipt records every executed assembly directory's before/after DLL set and
SHA; intermediate DLL bytes overwritten by later mutations are not archived.
This verifies the selected actual runtime composition in a private test copy,
not the complete current-workspace solution or all affected root tests.

The pre-edit Aspire start/describe/stop commands exited zero, with all 30
resources Running; the AppHost was stopped. The
[sanitized baseline](aspire-baseline.json) retains only allowlisted resource
fields and raw local artifact paths/hashes, omitting dashboard login material.
[Narrow final checks](narrow-checks.json) cover actionlint, Python syntax,
owned-file diff checks, LF/one-type style, unchanged six independent snapshot
vectors and the frozen intent digest. This policy adds no encoding; no new
vector approval is claimed. Earlier attempts, their precise superseded scope
and the four-path external drift are retained separately in
[historical notes](historical/README.md).

[Owned files](owned-files.json) contains the exact eleven-file inventory and
SHA-256 values. [Source snapshot](source-snapshot.json) also binds every
frontmatter context file; [seal](seal.json) binds the packet contents. The
frozen intent remains
`de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d`.
Prior packets and root addenda remain execution-time snapshots.

The parent still must run the full current-root Release/packages solution gate
after this handoff. The [earlier anchored-continuation root build and two actual root methods](../dapr-logical-anchored-continuation-root-2026-10-09/verification.md)
passed at their separately sealed prior bytes; this packet does
not upgrade that result to the replacement bytes.

The next executable local M4 dependency is a distinct proof-qualified
earlier-head logical snapshot rebase policy and its actual owner composition.
This replacement only accepts strictly older coverage under the same fixed
head/source pins. Earlier-head equivalence, automatic activation, restart/token
retention, anchored completed-command/origin issuance, safe legacy buffer
retirement and query transport ownership remain unfinished local work. Real
SDK paired-save ownership and common cross-actor serialization, component/fleet
observations, authoritative catalogs/state declarations, immutable execution,
production keys/profile and Platform authority require concrete evidence or
external authority. Local controls grant none of those. Parent acceptance,
M1–M8 and O rows stay open.
