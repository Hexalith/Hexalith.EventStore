# Story 6.4 continuation 09 recovery

Story remains in progress, incomplete and unqualified. The daemon restarted on
2026-10-16 Europe/Paris while the sole implementation agent was working. Its
changes and temporary logs survived; its agent/session did not. This is a recovery
checkpoint, not final verification or an acceptance waiver. The approved normative
specification controls every disagreement.

Continue the original parent-owned sequential step-03 workflow. Do not render or
restart bmad-build, read step-04, delegate parallel implementation, stage, commit,
push, change dependencies/audit policy, modify submodules or activate a nonfixture
environment. The root remains read-only while the next sole implementer edits.
All final build and assembly runs must be sequential, including dependency builds.

The original render is
`_bmad/render/bmad-build/eventstore-5ec6a32020fe/70d85eb25b5f587c9387/`.
Baseline/HEAD remains `e17e7294f540ebba42fe25192998689f0ecd89c1` on main.
The normative digest and detached approval were recomputed successfully after the
restart: `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`
and `657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f`.
No frozen bytes changed; no story/task/acceptance checkbox is complete.

Read [continuation 08 current state](continuation-08-current-state.md) and
[verification](continuation-08-verification.md) for earlier work. Their statement
that legacy primitives are unwired is superseded by the runtime changes below.

## Implemented since continuation 08

- The real orchestrator now uses bounded full-prefix v1 replay with a durable
  outbox intent, frozen original app/version/head/source/candidate references,
  exact CAS claim/release and preservation of newer activations. Pure preparation
  happens once per original candidate. Projection discovery does not authorize
  guarded Current; catalog removal cannot bypass a guarded/schema2 scope.
- Additive publication/inspection actor and gateway contracts stage, promote,
  read back and compact original replay receipts under an exact unexpired owning
  lifecycle lease. Publication validates scope/source/candidate bindings. Old
  public positional interfaces remain intact. Nested lifecycle snapshots have
  DataContract attributes for typed Dapr remoting.
- Ordinary operator rebuild selection now retains a bounded source/head fence,
  including when no earlier fallback floor exists. An older paused worker cannot
  acquire a later lease and overwrite a newer selected operator model. Rebuild
  still does not advance delivery C. Discard/rollback restores prior ordering.
- Failed/ambiguous saves clear actor pending cache. Tests cover before-commit and
  committed-response-loss cases for new publication and ordinary stage/promote/
  discard/rollback/finalize. Unrelated later saves cannot resurrect dirty state.
- Recovery retains original intent, candidate and reservation identities across
  stage/promote/release/cancellation and active/expired competing workers. Existing
  complete schema1 rows with selected actor state but no new receipt settle using
  read-only compatibility proof; the next head still uses bounded replay.
- Legacy finalization/readback/release/cleanup share one TimeProvider-aware
  30-second deadline. Anchor-free replay performs a third post-publication frozen
  source-prefix check without a retained second DTO array before advancing C.
  Source mutation during promotion retains old C and pending original work.
- Expected legacy failures export only unknown/full_replay bounded status/reason
  measurements. Notification failure on the new publication path exports a
  bounded measurement without raw actor/tenant/provider logging. Raw outbox
  reads/writes, occupied scans and failed CAS attempts now charge actual bytes.
- Guarded finalization source inspection now observes its independent deadline;
  other added guarded source reads honor caller cancellation via WaitAsync without
  changing public actor cancellation contracts. A hung-read deadline test exists.
- Complete wire arrays now size actual one-array framing across source pages,
  including exact 64 MiB/+1 at P=1/256. No discount hides repeated serialization.
- ProjectionWireJsonSerializer directly writes original carrier JSON over a
  Stream using 3,072-byte decoded Base64 segments and FlushAsync. Guard/legacy
  sizing, Dapr request bodies/parts and named mapped measure/upload paths use it.
  Carrier skeletons retain configured naming, escaping, ordering and optional
  values; scalar raw tokens retain DateTimeOffset '+' bytes.

## Changes present but not yet built at interruption

- The serializer conservatively falls back to configured JsonSerializer for
  converters, custom metadata/resolvers, reference handling or derived carriers,
  and charges WireStreamingFallbacks. New custom byte[] converter, renamed/
  ShouldSerialize metadata and ReferenceHandler.Preserve tests must run. This
  fallback preserves compatibility; it does not prove bounded token qualification.
- Guard replay drops sharedReplayEvents and plan DTO-array references after
  publication before decoded verification. Non-null empty arrays preserve replay
  mode markers; immutable admission digests remain. Verify no regression and do
  not claim all-mode simultaneous working-byte acceptance from this lifetime fix.
- Two old tracker tests now mock actual raw persisted rows/markers rather than
  obsolete typed reads, preserving their original assertions:
  ProjectionCheckpointRefusalDiagnosticsTests.PostCutoverWriterProtocolRefusalNamesItselfRatherThanReportingAConcurrencyRace
  and ProjectionCheckpointTrackerTests.ScopedLegacySave_CannotOverwriteCurrentV2DeliveryRow.

## Surviving verification and active process

Temporary files `/tmp/story-6-4-cont09-*.log` retain all intermediate results.
They are not yet copied/sanitized into a complete continuation09 evidence packet.
Full focused invocation arguments have not been recovered for every short log;
do not invent them. Rebuild and record the required exact final commands.

- Build17 passed with zero warnings/errors; legacy6 log: 205 passed, no failures,
  skips, errors or not-run cases, 4.742 seconds. This includes actor/legacy/outbox/
  orchestrator/idempotency, competing claims, original receipt recovery, deadline,
  source-mutation, privacy and actual outbox accounting checks.
- focused1 log: 498 passed, no failures/skips/errors/not-run, 16.732 seconds.
- Build21 passed with zero warnings/errors. Serializer checks reported ten passes,
  including byte-for-byte carriers/options/segment boundaries, 48 MiB decoded
  payload with every actual destination write at most 8 KiB, and cancellation
  charging partial bytes. wire6 log has 30 passing exact-limit/legacy checks.
- Domain build1 passed with zero warnings/errors in 5.62 seconds, but overlapped
  the full Server runner. The overlap was disclosed and must not count as a final
  sequential gate. No Domain full run completed in continuation09 yet.
- Full Server process **PID 3074103** survived restart and was still running when
  recovery was checked. It executes the Debug/net10.0 Server.Tests assembly with
  no class filter and writes `/tmp/story-6-4-cont09-server-full1.log`. Do not start
  another build or runner while it is active. It is an intermediate run on older
  assets, with the disclosed Domain build overlap. At recovery it had the two
  stale-fixture failures listed above, with no final summary yet. Wait for exit,
  retain/sanitize its actual summary and check for other failures before reruns.
- No new full Client/Contracts/live or Release/package gate is established.
  Older gates and the earlier random-secret scanner failure remain as documented
  in continuation07; do not describe them as current clean gates.

## Mandatory work remains

Finish the newly changed fixtures/serialization/lifetime checks and investigate
the full Server result. Generic descriptor-absent named full-route staged
original-manifest recovery remains separate from the wired pure v1 actor route.
Do not count one as the other. Current owner/applied proof recomputation handles
single-domain/all-installed catalogs, but arbitrary proper requested-domain
subsets still need assessment and verified additive context if used by valid
catalog requests; process-local issued-fingerprint memory is never authority.

All remaining approved W0-W6 and section6 interruption vectors, A=8/32,
R/R+1=256/4096, actual N=8/9 scheduler costs, exact source/wire/proof/control/
model/part/fold ceilings, actual simultaneous event/proof working bytes and
proof/hash visits remain blocking. Timing/RSS are diagnostic only; no invented
latency gate is authorized. The streamed writer removes one allocation hazard,
not the entire memory gate. The fixture AES-GCM/Brotli decode path may retain
duplicate plaintext arrays and needs actual lifetime measurement.

Full pinned **Dapr 1.18.2** real actor/handler/store W0-W6, fallback and two-replica
recovery qualification remain. Existing two live source-primitive cases alone
do not qualify the guard. Reuse the verified binaries under
`/tmp/story-6-4-dapr-1.18.2-wpll4ztg/`; inventory/hashes are in continuation06.
Use only isolated owned control-plane/fixture resources, preserve shared Works
infrastructure and never kill processes by generic names. Real live test mappings
need the three production owner/applied/reconcile proof endpoints and guarded
transport endpoints. Counters must include actual native transport/runtime/app
attempts: zero Dapr policy retry activations does not prove zero built-in retries.
Runtime service_invocation req_sent_total counts actual remote attempts. Exact
64 MiB body qualification may need recorded fixture transport headroom for
carrier overhead; do not change production topology or silently raise profile
limits. Existing raw Redis assertions are diagnostic only; owning seams prove
the new vectors. Some old live diagnostics hardcode localhost:6379, so account
for that if isolated EVENTSTORE_TEST_REDIS_PORT differs.

Run every exact section10 focused and full Debug/source command, package inventory
checks and Release restore/build with zero warnings/errors plus all five Release
assemblies sequentially, on final assets. Contracts package inventory requires
EVENTSTORE_PACKAGE_CONTRACT_DIR and the repository's local package tooling; no
publication is authorized. Record actual blockers without changing dependencies
or audit policy. Finish implementation before the parent writes/reads the entire
final baseline diff, audits every frozen matrix row and then reads step04.

Parent-only read-only audit note for later: LegacyReplay sets completed telemetry
before exact outbox cleanup; inspect whether an ensuing cleanup exception leaves
a truthful completed-row/pending-cleanup outcome. No false Current or activation
success may be reported. No root runtime edit was made for this observation.
