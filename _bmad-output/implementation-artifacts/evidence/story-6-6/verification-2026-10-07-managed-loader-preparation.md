# Story 6.6 dormant managed artifact and observation verification

The local managed prerequisite is implemented and verified. Story 6.6 remains
in progress: M1–M8, catalog readiness, production qualification and V2/proof
activation remain open. The implementation is internal and has no application
registration or production startup path.

This record applies the [approved trusted-code amendment](../../story-6-6-trusted-code-amendment.md)
and preserves the [parent spec](../../spec-6-6-event-versioning-and-upcasting-implementation.md)
canonical baseline `1329b35e52852952ecb2c94aabf100674e9691e3`. The observed repository
HEAD during final verification was `98da5a04e6df33ba026cbaae46d1777acdca7a21`.
That revision advanced through concurrent external work; it is neither this
patch's commit nor a replacement story baseline. No Git history, dependency,
publication or deployment mutation was performed by this slice.
The capture records the later independently observed HEAD at final inventory.

[The capture](managed-loader-preparation/capture.json) lists owned paths, exact
source/compiled-artifact hashes, retained command logs and final story checks.
Only the managed-loader notes and change-log entry in the shared spec belong to
this slice. Concurrent planning, sprint-status, Server security and other spec
notes were preserved and are excluded from the authored source inventory.

## Implemented behavior

`EventManagedArtifact` reserves capacity before copying the original exact G row
or allocating the private image. It admits managed bytes and metadata against
that declaration, retains privately owned storage, and loads once into its own
new collectible context from a non-publicly-visible, read-only stream. Image
hashes are checked immediately before and after loading. The owner validates
every execution binding against its actual loaded Assembly object and privately
admitted digest; a same-name Assembly object cannot substitute for it. Disposal,
unload, changed private storage or lost evidence fences subsequent use and clears
retained image/declaration bytes before releasing their reservations. This does
not promise completion of CLR unloading or account for all CLR/JIT allocations.

The existing upcaster, downserializer, current deserializer and validation
wrappers accept optional internal execution bindings. They enforce the same
capability-loss identity as their registry. Upcaster/downserializer bindings
check both the implementing type's assembly and the actual interface target
method's assembly. Inherited code from another assembly therefore cannot obtain
the direct-image claim by presenting only its derived type's assembly. Existing
unbound descriptor semantics and public APIs remain compatible.

`EventEvolutionManagedLoadObserver` admits the supplied pinned graph before
observation and registers AssemblyLoad/unload handlers before reconciling existing
loads. Its context identity is the exact AssemblyLoadContext object, never merely
its Name. File-backed observations compare declared full identity, actual managed
version and chunk-hashed current file bytes. Missing files, unknown identities,
ambiguous declarations, dynamic artifacts and unproved stream-loaded assemblies
refuse with support-safe sticky capability loss. Hashing checks the original token
between bounded chunks. Observation shutdown or context unload also loses the
affected scope.

For privately loaded images, the observer admits only the exact active Assembly
object plus its privately admitted bytes, original exact G declaration and shared
loss scope. The original domain, dependency identity, version and declared
context cannot be relabelled just because another G row has the same hash.
An unknown late load in that same observed context loses the scope; actual
subsequent registry callbacks then refuse.

File-backed Assembly.Location hashing establishes only the current file's
identity. It does not prove the loaded image is identical or prevent file
replacement. The private-image route supplies stronger direct managed-object
provenance. Neither route establishes immutable transitive/framework/native
execution or authenticates a complete catalog or serving-peer inventory.

## Executable controls

The 48 focused tests comprise 21 observer, 16 artifact and 11 actual
registry-callback/composition controls. They cover pre-existing and late explicit
or reflection loads, startup/load concurrency, changed/missing files, missing or
dynamic artifacts, ambiguous context/assembly identity, cancellation, shutdown
and unload, bounded declaration/image ownership, private-image corruption,
foreign or forged binding origin, and callback fencing after capability loss.

Real upcasting, downserialization, validation and deserialization execute from
the admitted private test assembly through existing registry callbacks. Source
file replacement/deletion after private admission does not substitute executed
bytes. Shared-scope refusal controls run zero callbacks. Composed controls accept
declared private images, reject same-hash wrong-row/context evidence and detect
undeclared late loads in the same context before the next callback.

A separate control deliberately loads a declared assembly identity into a
different unlisted runtime context with the same Name. That load is outside this
observer's coverage. This is an explicit boundary control, not whole-process
qualification. AssemblyLoad observations arrive after loading; they cannot undo
initializer or other effects that already occurred. The approved policy grants
no hostile-code confinement or universal refusal before effects.

## Final verification

Each linked log retains the exact invocation and exit code. Checks used the final
LF-normalized source, consistent with tracked `*.cs text eol=lf` attributes.

| Check | Result | Evidence |
| --- | --- | --- |
| Debug Client.Tests build, project-reference mode | Passed, zero warnings/errors | [Build](managed-loader-preparation/debug-build.log) |
| Focused built Debug Client assembly, three classes | 48 passed, zero errors/failures/skips/not-run | [Focused tests](managed-loader-preparation/focused-tests.log) |
| Full built Debug Client assembly | 1,183 passed, zero errors/failures/skips/not-run | [Full Client tests](managed-loader-preparation/full-client-tests.log) |
| Release solution build, package-reference mode, warnings as errors | Passed, zero warnings/errors | [Release build](managed-loader-preparation/release-build.log) |
| Historical approval preflight with mutations | Passed; original digest, 20 obligations and 47 open follow-ups retained | [Historical preflight](managed-loader-preparation/historical-preflight.log) |
| Current Dapr-only preflight with timed mutations | Passed; all 11 prohibited mutations rejected, V2 fenced | [Current preflight](managed-loader-preparation/current-preflight.log) |
| Five timed executable managed-loader mutations | All killed by their owning controls | [Mutation result](managed-loader-preparation/mutation-result.json) |
| Deferred-work checker | Exit 0; 406 legacy advisories, no blocking findings | [Deferred-work check](managed-loader-preparation/deferred-work.log) |
| Owned-path whitespace, story invariants and retained-source hash checks | Passed | [Final checks](managed-loader-preparation/final-checks.log) |

The executable [mutation runner](managed-loader-preparation/verify-mutations.py)
builds an isolated temporary source copy, confirms a passing baseline and applies
one mutation at a time. Builds have 180-second timeouts and tests 60-second
timeouts. The controls kill removal of AssemblyLoad subscription (two failures),
bypassed private-image pin admission (one), bypassed actual Assembly-object origin
(one), bypassed original G-row equality (two), and removed pre-load private-image
rehashing (one). Repository sources are not mutated by those runs.

The first isolated build failed with CS0006 because a symlink-relative Commons
reference assembly was resolved under the temporary tree. Its
[failed output](managed-loader-preparation/baseline-build-symlink-failure.log) is
retained. Selecting the same pinned physical Commons source with the existing
`HexalithCommonsRoot` build property resolved the isolation path; the final
baseline and every mutation build passed. No package or dependency was changed.

These checks are local preparation evidence. The current source preflight does
not certify runtime loader completeness, and the historical preflight does not
approve the current Dapr-only implementation or close its obligations. No new
live-sidecar, package-consumer, production-profile or complete-fleet qualification
is claimed by this slice.

## Remaining code and authoritative prerequisites

The next authority prerequisite is the amendment's Qualification 2: obtain the
actual authoritative per-domain immutable manifests, complete reviewed catalogs,
roots/edges, shared framework dependencies and serving-peer inventory, retaining
the release/review identity and exact artifacts. Existing supplied graph checks
verify consistency of supplied data; they cannot detect a root and its entire
subgraph omitted together. Direct image hashes cannot supply that authority.
Gateway/peer pins and complete D/V/A/E/F/S, handler, adapter, filter, receipt and
codec declarations must come from the reviewed deployment inputs before any
readiness claim or production admission composition.

Qualification 3 still requires native-load observations, full process/context
coverage, complete transitive/framework/native execution binding, dynamic-path
review and measured startup/call/runtime cost at the 64 MiB/65,536-row limits.
The new observer covers only explicitly admitted contexts and declared managed
images. The portable .NET AssemblyLoad event and Assembly.Location do not provide
native loader events or immutable process-wide executable-image evidence. This
slice does not invent a cross-platform native observation mechanism or select a
deployment runtime/profile on behalf of its owner.

Missing local integration remains code work: compose authoritative admission
and qualified observers into the production registry/startup lifetime, then
complete the shared reader and all replay, projection, subscription, query,
control/resume and receipt paths in the spec's dependency order. The four wrapper
seams are available for that later composition; they do not activate it. Full
compatibility and durable cancellation/recovery/fleet matrices remain required.

Qualification 4 and AD-26 still require the separately ratified production Dapr
profile and its evidence. At capture time both designated production-profile and
routing-catalog slots, `deploy/dapr/production-profile.yaml` and
`deploy/dapr/eventstore-routing-catalog.json`, were absent. A healthy local
development host or separate Counter recovery evidence does not supply these
production inputs. These absent authoritative artifacts are distinct from the
remaining integration/qualification code. Until they and the other gates pass,
compatible V1 behavior and V2/proof-dependent fences remain in place. No M1–M8
task or O-row is closed by this record.
