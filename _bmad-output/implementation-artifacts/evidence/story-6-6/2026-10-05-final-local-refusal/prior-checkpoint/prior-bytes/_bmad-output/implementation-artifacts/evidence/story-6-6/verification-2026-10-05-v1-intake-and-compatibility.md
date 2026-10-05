# Story 6.6 V1 intake, memory ownership and compatibility repair

This is local preparation and refusal evidence for the requested parent spec,
not Story 6.6 acceptance or activation. The canonical baseline remains
`1329b35e52852952ecb2c94aabf100674e9691e3`; the observed repository HEAD when
this evidence was assembled was `c447573fe4613fc3f9035dc4c5d001a89c0b5fbe`.
Concurrent commits and user edits were preserved. No staging, commit, push,
dependency update, deployment or global runtime configuration change occurred.

## Implemented scope

- The bounded V1 response path admits the complete event reference snapshot
  before emitting bytes. It captures `Count` once and enforces count, payload,
  escaped metadata, encoded result and Unicode limits before rendering. The
  renderer only iterates the admitted snapshot. This does not qualify the
  ordinary legacy producer fallback or provide authoritative serializer bounds.
- The logical resolver transfers its admitted private payload owner into the
  upcast executor. The reader avoids duplicate no-op protection copies and
  shrinks a provider output reservation only after a successful return proves
  its actual length and alias. Distinct provider arrays remain retained and
  cleared through `finally`. No-op 22 MiB and 64 MiB sources and a distinct
  22 MiB provider output now fit the composed budget. Metadata refusal occurs
  before provider callbacks; admitted metadata cannot be replaced by callback
  mutation.
- Compatible nullable provenance members were added to projection, replay and
  subscription DTOs with exact normative names and null omission. The exact
  subscription `VerifiedEffectiveEvent` hint is present. Existing positional
  constructor/deconstruction APIs remain intact. Every supplied unverified
  evolution hint is refused on reached legacy replay, full/named/staged and
  shared-rebuild projection routes before handler/store resolution. Subscription
  refusal occurs before markers and maps to retryable HTTP 503. No verified
  evolution processing is enabled.
- `UnpublishedEventsRecord` again provides its immediate prior twelve-member
  constructor and `Deconstruct`, while retaining `CausationId` JSON round trips.
  A consumer compiled against the prior synthetic Server reference is executed
  without recompilation against the current Server DLL. The verifier captures
  the copied runtime DLL hash immediately before execution and checks it after
  execution. Release CI uses this narrow fixture with both removed-member
  negative controls; local configuration defaults to Debug.
- The current-amendment source verifier rejects comment/string fence spoofs,
  conditional V2 fences and disabled conditional-compilation fences. Its ten
  subprocess mutations pass. It remains a conservative source check rather than
  semantic runtime or activation qualification. Historical approval evidence was
  left unchanged.
- A test-only explicit `EVENTSTORE_TEST_DAPR_HOT_RELOAD=false` override writes a
  private Dapr runtime config for both owned sidecars, including restart. An
  absent override preserves the default profile; invalid input fails before
  directory writes. Authored public/internal types and members, including new
  and changed tests, have XML documentation.

## Verification

Raw commands, logs and structured results are retained in
[2026-10-05-v1-intake](2026-10-05-v1-intake/). Build and test commands ran from
the owning repository. These scopes are separate; their counts are not a claim
that every Story 6.6 acceptance lane ran.

| Check | Result and retained scope |
| --- | --- |
| Four affected Debug test-project builds, `-p:UseHexalithProjectReferences=true -m:1` | Zero warnings/errors; [builds.json](2026-10-05-v1-intake/builds.json) |
| Focused Contracts / Client / DomainService / Server suites | 2 / 111 / 29 / 62 passed, no failures or skips; [focused.json](2026-10-05-v1-intake/focused.json) |
| Full Client assembly | 927 passed, no failures/skips; [log](2026-10-05-v1-intake/full-client.log) |
| Full DomainService assembly | 378 passed, no failures/skips; [log](2026-10-05-v1-intake/full-domainservice.log) |
| Full Server assembly before the final three metadata controls | 3,711 total, 3,686 passed, 25 existing skips, no failures; [log](2026-10-05-v1-intake/full-server.log) |
| Final reader class after those three test-only additions | 23 passed, no failures/skips; [log](2026-10-05-v1-intake/server-metadata-controls.log) |
| Full Contracts assembly without package inventory | 2,241 total, 2,239 passed, two package-fixture skips, no failures; [log](2026-10-05-v1-intake/full-contracts.log) |
| Existing package fixtures with final inventory | Both previously skipped fixtures passed without skips; [log](2026-10-05-v1-intake/package-fixtures.log) |
| Release solution build, `-m:1 -warnaserror -p:UseHexalithProjectReferences=false` | Zero warnings/errors; [log](2026-10-05-v1-intake/release-build.log) |
| Final Server Release test build after metadata test additions | Zero warnings/errors; [log](2026-10-05-v1-intake/build-server-release-final-tests.log) |
| Current amendment verifier `--mutations` | All ten controls pass; [log](2026-10-05-v1-intake/amendment-preflight.log) |
| Historical `6-5-integration/verify.py --mutations` | Passed with unchanged approved digest; 20 obligations / 47 follow-ups remain open; [log](2026-10-05-v1-intake/historical-preflight.log) |
| Deferred-work checker and `git diff --check` | Exit zero; checker retains its existing advisory warnings; [commands](2026-10-05-v1-intake/preflights.json) |

The complete command lists for the broad assembly runs and release gates are in
[full.json](2026-10-05-v1-intake/full.json) and
[release-gates.json](2026-10-05-v1-intake/release-gates.json). The last changes
after the Release solution build were the three reader regression tests and XML
documentation; production behavior did not change after that build.

### Package and already-compiled consumer evidence

`python3 scripts/pack-release-packages.py /tmp/story-6-6-intake-packages
0.0.0-ci-test` produced all fourteen standard release-manifest packages locally.
The existing script normalizes the requested test version to
`999.0.0-ci-test`; [package-inventory.json](2026-10-05-v1-intake/package-inventory.json)
records every package hash. Nothing was published.

With `EVENTSTORE_PACKAGE_CONTRACT_DIR=/tmp/story-6-6-intake-packages`, the built
Contracts assembly ran `TrustedEffectPackageContractTests` and
`PackagedReminderApiTests`. Both passed. This is the existing standard package
consumer scope, separate from the narrow drain-record ABI fixture.

The ABI verifier ran with `--configuration Release --mutations` against both
the Release build output and the DLL extracted from the final Server package.
The extracted entry `lib/net10.0/Hexalith.EventStore.Server.dll` and copied
runtime DLL both hash to
`5bc3ed0a97344f8c26330217c5c83445e53510fd1bd755ab2d0a6ba839c3501d`.
The already-compiled consumer hash is
`a61fdb07391174a0f8bb44534e8f905f2f63f2343ebb991b03dcb6a1acf90883`.
It executed the old constructor and `Deconstruct`; each independently removed
member produced `MissingMethodException` in a timed fresh process. See
[package input](2026-10-05-v1-intake/compiled-package-input.json),
[packaged execution result](2026-10-05-v1-intake/packaged-compiled-consumer/result.json)
and its adjacent compile/execution/mutation logs. This synthetic prior-reference
fixture proves that exact prior twelve-member ABI against consumed current
bytes. It does not close the complete Story 6.6 API/wire/fleet compatibility
matrix or broad compiled-consumer obligation.

### Development live profile

The first native Development run failed one of eleven tests because Dapr
HotReload could not allocate a watcher (`no space left on device`). The failed
[profile](2026-10-05-v1-intake/native-profile-failed.json) and
[test log](2026-10-05-v1-intake/native-test-failed.log) are preserved. Disk had
ample free space; the observational `/proc` scan counted 1,048,544 watch lines
against a 1,048,576 user-watch limit. The scan was not deduplicated and omitted
inaccessible/exited processes, so watcher exhaustion is an inference consistent
with the error, not an exact quota proof. See
[watcher-observation.json](2026-10-05-v1-intake/watcher-observation.json).

Dapr documents the supported per-process feature configuration in
[Component hot reloading](https://docs.dapr.io/operations/components/component-updates/).
The explicit private config `spec.features: [{ name: HotReload, enabled: false }]`
was supplied with `--config` for the fallback. Five configuration tests passed.
The fallback native Development run passed all eleven live tests with two
application hosts and sidecars, retry/cached command handling, primary restart,
public Dapr actor-state GET and preserved payload/digest/message ID. Its
[profile](2026-10-05-v1-intake/native-profile-hotreload-disabled.json),
[test log](2026-10-05-v1-intake/native-test-hotreload-disabled.log) and
[runner](2026-10-05-v1-intake/run-native-live.py) are retained. This profile
difference is explicit; no global sysctl limit was changed and no user-owned
process was stopped. Owned Aspire/native processes were cleaned up.

This is Development V1 loopback evidence. It does not qualify production,
crash-after-commit/ambiguous acknowledgement, broker delivery, mixed fleets,
control holds or evolved-event processing.

## Remaining requirements and exact inputs

The spec status stays `in-progress`, every M1–M8 acceptance checkbox stays open,
and V2/proof-dependent activation remains fenced.

| Required input or qualification | Dependent work still open |
| --- | --- |
| Authoritative per-domain D/V/A/E/F/S/G manifests, exact gateway pins/fingerprints and measured bounded serializer profiles | M1 registry/codec closure and M2 negotiated bounded production invocation; ordinary legacy fallback remains unqualified |
| An approved enforceable managed/native loader boundary and trusted catalog closure, including identity/schema/callable validator binding | M1–M2 trusted registration and hops; the existing dedicated `AssemblyLoadContext` document remains a proposal |
| Dapr-compatible route/page/state proof binding and durable replay/query session design | M2 authenticated source reads and M4 consumer integration; typed ingress/protection allocation qualification remains incomplete |
| Real control/hold participants and a chosen qualified Dapr owner/transaction/ETag boundary | M3 and M5–M7 atomic control, checkpoints, projection generation, query visibility, recovery and holds |
| Production profile `deploy/dapr/production-profile.yaml`, AD-26 ratification and required production/component harness inputs | M8 production provider, crash/ambiguous-ack, broker/effect, fleet and full compatibility qualification; the production-profile file is absent |

M5 verified projection generations/checkpoints, M6 authenticated carrier/membership
and effect receipts, and M7 resume/hold/capture/export diagnostics still require
their specified runtime work and qualification. Missing owner inputs prevent
acceptance/activation, while the local repair above is concrete and reviewable.

The final source and evidence byte inventory is recorded in
[sealed-inventory.json](2026-10-05-v1-intake/sealed-inventory.json). It records
uncommitted authored/changed paths and retained evidence, excluding itself.
