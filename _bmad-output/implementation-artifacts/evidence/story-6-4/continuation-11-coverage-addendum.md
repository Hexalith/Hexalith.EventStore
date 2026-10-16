# Story 6.4 current local qualification addendum

This addendum records refreshed local qualification of the corrected source73
candidate after the authorized five-package servicing update. It supersedes the
pre-update qualification disposition, not the historical evidence or approved
specification. The [historical coverage index](continuation-10-coverage-index.md)
remains the executable test-selector map for W0–W6, all twelve section 6 durable
boundaries and section 7 preservation; its source65 runs are historical.

Preserved baseline: `e17e7294f540ebba42fe25192998689f0ecd89c1`.
Actual current HEAD: `008b83f0443c151052275fd291b0b88ac3aa996c` plus the recorded
working-tree changes. The [current gate packet](continuation-11-final-gates.json)
retains all 296 `frozenSourceFiles73` byte/hash identities, clean shared-catalog
commit/hashes, effective closure comparison, exact commands, actual per-run
DLL/deps/package identities, intermediate failures and corrections. Actual compiled
informational versions confirm this HEAD; timestamps are timezone-qualified.
Normative SHA-256 remains
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.

## Fresh final gates

| Complete individual assembly | Debug selected / passed / declared skips | Release selected / passed / declared skips | Current packet keys |
| --- | --- | --- | --- |
| Server | 5102 / 5077 / 25 | 5102 / 5077 / 25 | `fullServerDebug2`, `fullServerRelease2` |
| DomainService | 904 / 904 / 0 | 904 / 904 / 0 | `fullDomainDebug2`, `fullDomainRelease1` |
| Client | 1562 / 1562 / 0 | 1562 / 1562 / 0 | `fullClientDebug2`, `fullClientRelease1` |
| Contracts with actual 14-package inventory | 2358 / 2358 / 0 | 2358 / 2358 / 0 | `fullContractsDebug3`, `fullContractsRelease1` |
| LiveSidecar | 192 / 191 / 1 | 192 / 191 / 1 | `liveFullDebugFinal2`, `liveFullRelease1` |

Each configuration selected **10,118**, passed **10,092**, and retained exactly
26 declared skips, with zero failures/errors/not-run. The 25 Server skips match
the existing DW1 set; the live skip is the existing parent-launched worker helper
whose owning restart parent passes. Exact section 10 focused Server 548,
Domain 110 and Live 40 pass; the additional owning authority 64 passes. No new skip
was introduced. Source73 fixture corrections rename an operation identity and
generate a logging sentinel; the tracked-secret scanner remains unchanged.

The actual exact Release/package restore/build and 14-package manifest pack pass
with **zero warnings and zero errors**. All package bytes, normalized CI version
`999.0.0-ci-test`, nuspec dependencies and isolated consumer-probe receipts are
retained under `packageInventory2`. Debug and Release Contracts both execute the
real package probes. The five authorized versions remain 10.0.13. SignalR.Common
uses Update because the clean shared catalog already declares it; the owning
strict validator/negative fixtures enforce that current action. No shared
reference file, audit policy, extra central package or version was changed.

## Current W0–W6 and preservation bindings

Every Server/Domain/Client/Contracts selector named by the historical index ran
again in the fresh full Debug and Release assemblies above. The complete suites,
including scheduler classes and wider rebuild regressions, provide the current
closure; focused filters alone are not used to claim it.

| Frozen requirement | Current executable assertions and fresh evidence |
| --- | --- |
| W0 prescribed heads/pages/R | `GuardedCurrent_ProductionActorsAndStoresMeetExactLogicalReadAndZeroMutationBudget` and live `SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface`. Fresh focused/full Debug and full Release packets each contain all 15 heads `[1,256,257,9999,10000]` × pages `[1,16,256]` at R256. The separate fresh [R4096 packet](continuation-11-live-current-r4096-release1-vectors.json) contains the same exact 15 combinations, five passed theory cases, exact eleven metadata reads, zero event/effect work and independent proof/read-byte bounds. |
| W0 absent/touched/erased/drift | Actor source bootstrap/inspection/erasure, empty qualification and production rebuild selectors from the index pass both complete configurations; fresh live empty vector retains Unknown and creates no receipt or delivery effects. |
| W1 long small tail / three routes | Production actor/store/handler cost and three-route seven-pass selectors pass both configurations. Fresh focused/full live cost packets include delta1 at H10000 without prior-history reads. |
| W2 delta771 / P1,P256 | Production tail costs and actual256-event retry-worker range profile pass both configurations; fresh live P256 cost vector checks canonical model/count and original-work cleanup. |
| W3 count/byte framing | Production delta0/256/257/512/513 and actor8190/8191/8192 framing/lookahead selectors pass both configurations; fresh live typed oversized/native/callback vectors retain separately charged failures. |
| W4 mixed/protected/64MiB±1 | Actual lookahead/decode/sizing, provider readability and exact source/wire pass limits run in both full Server configurations and reject over-limit work before effects. |
| W4 working bytes | `ProjectionProofWorkingMemoryTests` and every `ProjectionEventWorkingMemoryTests` assertion run in both configurations. Historical detailed phase packets remain bound to their original measurements; current complete-suite assertions are the refreshed qualification. |
| W4 scheduler/cold/CAS | All scheduler Costs/CasRaces partials,64×64 plus 128 shard, refused-CAS/cold migration, and actual N9 worker assertions run in both configurations. Fresh live cold/warm and native-attempt vectors retain separately measured control work. |
| W5 legacy/named/generic fallback | Complete-prefix H0/256/257/771/10000, lost-response/original-manifest recovery, worker bounded loader and wider configured >64MiB regressions pass both configurations. Each fresh full live packet includes twelve actual generic recovery attempts at four prescribed heads, with one original preparation. |
| W6 A8/A32,N8/N9 | Owning transition-cap exhaustion and ninth activation retain original IDs/proof/reservation and later converge through fenced recovery in both configurations. |
| W6 R/R+1,replicas,source/owner changes | Exact retention 256/4096 and complete ID digests, older identities, source/generation races and gateway fences pass both configurations; whole live suites run real replica/original-receipt recovery. |

All twelve section 6 boundaries mapped in the historical index ran on current
source73 in both full Server configurations: pre-source/admission/decryption;
claimed lease before reservation; reserved before handler; partial event/detail/
index/candidate/part; durable effect with lost response/cancellation; missing or
changed receipt/generation/freshness; verification-source failure; pending CAS/
lifecycle release; completion CAS before reread; completed row with failed
cleanup; fallback promotion/cancel/resume/release; restart/fleet/restore/retention.
Owning live SDK and generic recovery rows also ran in each fresh complete live
suite. A direct finalizer unit remains identified as such; it does not replace
an owning durability or live assertion.

The section 7 preservation mapping remains intact: source codec/atomic append
ambiguity, duplicates/fences, raw identities/position/provenance, full digest
horizons, bounded pages and repeated passes, complete-prefix/empty equivalence,
staging/promotion, handler barrier/cancellation/public ABI, shared ownership and
tenant scope, fleet/migration/downgrade/erasure, and payload-free telemetry. Actual
full Domain trust-boundary/anonymous-policy, transport-body/private-context,
descriptor-absent golden and older-actor compatibility tests all reran.

## Actual live and build identities

Fresh sanitized 49-observation packets are retained for [focused Debug](continuation-11-live-focused-debug2-vectors.json),
[whole Debug](continuation-11-live-full-debug5-vectors.json) and
[whole Release](continuation-11-live-full-release1-vectors.json). Each binds all
15 Current,12 recovery,7 tail-cost and remaining source/native/cold/empty/control
observations to its actual source, DLLs, catalog and environment. The separate
R4096 run is Current-only and is not represented as another whole live suite.
Wrapper/test/owned-control exits are zero, OQ8 owning scratch/process cleanup is
verified and its PostgreSQL container is absent.

SDK 10.0.401/runtime 10.0.12 were used. Regular fixtures explicitly pin Dapr 1.18.2
with wrapper-owned controls; unchanged OQ8 uses installed 1.18.7/shared controls.
HotReload and the existing fixture profile were preserved. Retained Redis host
forwarding/scheduler and inotify failures are unsuccessful historical attempts.
The temporary inotify capacity adjustment is separately restored or an external
change preserved in the packet; unrelated processes were never terminated.

Contracts/package publication fixtures rewrite host outputs. The four completed
Release assemblies are truthfully bound to retained `releaseBuild4` assets.
After those fixtures, exact restore/build5 produced the coherent outputs used by
whole Release Live and R4096. Both builds use source73/current catalog and byte-
identical test deps, but Server.dll and Gateway.dll have different SHA256 values.
The old DLL bytes could not be recovered to establish the precise PE difference;
no binary or CIL equivalence is asserted. Their per-run hashes remain separate.
No source/configuration/closure change is evidenced between these two builds.
The unchanged solution still generates the transitive Gateway in Debug during
its exact Release command; that observed configuration is disclosed.

The final concept/runbook qualification wording is a documentation-only delta
from the two retained source73 documents. `finalDocumentationDelta` binds both
before/after hashes without rewriting runtime snapshots or earlier gate records.
The [closing audit](continuation-11-final-audit.json) verifies current unchanged
source73 files, that explicit documentation delta, per-gate/runtime/package
bindings, exact Current matrices, text/privacy hygiene and released owned controls.

## Remaining acceptance boundary

Local W0–W6, crash/preservation, Debug/Release, live-sidecar, package and zero-warning
qualification is complete for these recorded candidates and actual profiles.
Parent step-03 acceptance and subsequent independent step-04 review remain open;
wrapper and sprint status stay in progress. This is tool evidence, not new human
approval or AD-12 approval. Authenticated exact-head sealed CI and the formal
AD-12/NFR8 transition are not performed by these local runs. No production
qualification, cutover, activation, deployment, package publication, source commit
or overall production readiness is claimed. Existing environments require their
own inventoried maintenance/backup/restore/qualification and operator approval.
