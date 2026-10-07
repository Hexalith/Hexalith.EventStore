# Story 6.6 — continued local preparations, 2026-10-07

This continues the [replay-router prerequisite](verification-2026-10-07-replay-router-admission.md).
Original frozen intent and baseline `1329b35e52852952ecb2c94aabf100674e9691e3`
are preserved; observed HEAD is `40c92e085d8a6463d469c1b340c410fec84a690f`.
All M1–M8 tasks/O01–O20 obligations remain open, with V2 and unavailable
proof-dependent routes fenced. No deployment, publication, retained-data
migration, staging or Git-history mutation occurred. Concurrent PRD/UX and
Platform/Memories changes are excluded from this run's owned scope.

## Implemented and observed

The optional managed observer now reconciles every process assembly and catches
unlisted startup/late/reflection/dynamic contexts. Context sets, binding tuple
keys and live-context presence use reference identity, so custom virtual value
equality cannot admit another loader context. The retained
[before control](local-preparations-2026-10-07/managed-equality-before/control-value-equal-unlisted.log)
observed the defect (`refusal=False, callbacks=1`); the final process fixture
refuses that context before its callback. An in-flight callback that violates
policy is observed once, then its uncommitted success is refused. The observer
cannot undo those effects. Native observation and immutable executed-image
binding remain unqualified.

Greeting now registers its exact empty-marker V1/json writer in the Sample host.
Tests compare the actual `/process` route to independent legacy serialization,
retain null version metadata, check foreign payload refusal and count 1000/1001.
Counter's existing six declarations remain intact.

Both processor bases have an additive optional `DetachedStateCapture<TState>`
hook. Its finite owner-declared graph charge precedes the copy callback, and the
SDK rejects a null/shared/wrong-type root. Known Counter/Greeting scalar copies
are supplied. Snapshot capture precedes tail Count/enumerator callbacks for
declared state, including nested wrappers; undeclared legacy state keeps its
reference behavior and admission order. Actual Apply/Handle failures and
cancellation mutate only observed detached state, preserving source state/bytes.
Getter/copy cancellation preserves the originating token. A caller Count and
enumerator deliberately change the source to 40/80; reconstruction still uses
the earlier captured value. This is ordered capture, not an atomic read of
concurrently mutated input. The owner remains responsible for graph independence
and its allocation declaration. No generic cloning, canonical state serializer,
authenticated command proof or full M4 isolation is claimed.

The dormant `EventEvolutionProofFraming` owner preflights exact approved outer
framing, route count/1 MiB route claim, strict UTF-8 key, 64-byte signature,
mandatory prefix, discriminator and trailing bytes before private allocation.
It rechecks the private copy and retains symmetric claim/key/signature slices.
Command pages always reject checkpoints. Complete valid frames at 2 MiB−1 and
2 MiB pass structural ownership, 2 MiB+1 returns `ProofLimit`; an individually
legal 64 MiB page returns `ScratchLimit` because source+copy+ownership exceed the
shared 128 MiB budget. Private proof bytes clear on disposal, source bytes stay
unchanged and budget.LiveBytes returns to zero. **Claims/signatures are opaque
and unverified**; no production consumer or signer accepts this owner.

The [additive workflow](../../../../.github/workflows/event-evolution-local-guards.yml)
uses existing pinned actions/SDK and root-only submodule preparation. Dependency
restore is outside each lane's 60-second build/execution deadline. Hosted CI is
not observed. OQ8-bound `ci.yml` and its seals remain unchanged.

## Final verification

Exact argv, original `/tmp` log paths, archived logs, 48 owned source SHA-256
facts, context hashes and all 14 local package hashes are retained in
[commands-and-source.json](local-preparations-2026-10-07/commands-and-source.json).

| Lane | Observed result | Retained log/result |
| --- | --- | --- |
| Client Debug/source build and full assembly | Exit 0; 0 warnings/errors; 1327 passed | [build](local-preparations-2026-10-07/client-build.log), [tests](local-preparations-2026-10-07/client-full.log) |
| DomainService Debug/source build and full assembly | Exit 0; 0 warnings/errors; 517 passed | [build](local-preparations-2026-10-07/domainservice-build.log), [tests](local-preparations-2026-10-07/domainservice-full.log) |
| Sample final Release assembly | Exit 0; 186 passed; 0 failed/skipped/not-run | [tests](local-preparations-2026-10-07/sample-final-release-full.log) |
| Required Release solution build | Exit 0; 0 warnings/errors | [build](local-preparations-2026-10-07/release-build.log) |
| Local CI package pack / package-only consumers | Exit 0; 14 packages; all 3 consumers passed | [pack](local-preparations-2026-10-07/local-package-pack.log), [consumers](local-preparations-2026-10-07/package-consumers.log) |
| Router guards | Control 22 passed; 5 compiling mutations detected | [result](local-preparations-2026-10-07/router-guards/result.json) |
| Managed process guards | 9 isolated cases passed; 6 compiling mutations detected | [result](local-preparations-2026-10-07/managed-guards/result.json) |
| Detached snapshot guards | Control 15 passed; 5 compiling mutations detected | [result](local-preparations-2026-10-07/snapshot-guards/result.json) |
| Opaque framing guards | Control 26 passed; 5 compiling mutations detected | [result](local-preparations-2026-10-07/framing-guards/result.json) |
| Current-amendment preflight | Exit 0; 22 timed controls; all 20 obligations open | [result](local-preparations-2026-10-07/current-preflight.log) |
| Additive workflow lint / dependency warm-up | Exit 0 | [lint](local-preparations-2026-10-07/workflow-actionlint.log), [Client restore](local-preparations-2026-10-07/ci-client-restore.log), [DomainService restore](local-preparations-2026-10-07/ci-domainservice-restore.log) |
| Deferred checker | Exit 0; existing historical advisories | [result](local-preparations-2026-10-07/deferred-check.log) |

The Release command is exactly
`dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false`.
Packages use `999.0.0-ci-test` only and were not published. Consumer checks are
current package-only builds, not the full historical compiled-consumer/fleet
matrix. The earlier Client run exposed test-fixture aggregate discovery
contamination; making that fixture generic excluded its definition from the
assembly scanner. The [intermediate failure](local-preparations-2026-10-07/intermediate-discovery-fixture-failure.log)
is retained separately; final 1327-test source execution passes.

Each isolated mutant compiles successfully before its named control fails. Router
admission/prefix mutations trigger deliberate forbidden keyed-provider sentinel
`InvalidOperationException`s; three router kills are Shouldly assertions. Managed
mutations fail named fixture assertions, including identity collisions/unloaded
context; they are not native prevention controls. All ten snapshot/framing kills
are Shouldly assertions (some observe a deliberate forbidden-callback exception
through their expected-message check). Exact diagnostics and per-lane logs are
retained, rather than inferring an assertion merely from `[FAIL]`.

Managed final observations cover 203–204 declarations and 38,618–38,821 manifest
bytes, with measured admission/operation 101–138 ms and LiveBytes=0. These are
small real-runtime fixtures, not maximum 64 MiB/65,536-row qualification.

## Concrete next inputs and retained candidates

The [candidate index](source-candidates-2026-10-07/index.md) links the source
catalog, Release artifact/peer-pin preparation, production-profile proposal and
[logical claim design](source-candidates-2026-10-07/dapr-logical-claims.design-candidate.md).
Candidates were regenerated after the final Release/pack outputs. All three
content hashes and 128 nested source/artifact size/hash facts were checked;
every authority flag is false.

The next semantic codec/integrated reader needs a distinct Dapr logical evidence
schema/carrier/purpose/trust choice: historical `StoredDigest` binds provider/raw
evidence, while current `EventLogicalDigest` binds selected application metadata
and payload bytes. They cannot share an authority label. The new proposal makes
the field differences and one coherent actor/range source binding explicit.
Actual immutable managed/native execution-image/catalog closure and durable
control-owner/participant choices also remain missing. The
[current inventory](source-candidates-2026-10-07/remaining-tasks-current.md)
distinguishes those design inputs, additional dormant implementation, and later
external qualification. No missing production authority is used as a blanket
reason that dormant work cannot continue.

`test -f deploy/dapr/production-profile.yaml` returned exit 1 with no output:
the canonical production profile is absent, separately from passing local lanes.
Final diff/staging/frozen-baseline and candidate audit results are retained in
[final-checks.json](local-preparations-2026-10-07/final-checks.json).
