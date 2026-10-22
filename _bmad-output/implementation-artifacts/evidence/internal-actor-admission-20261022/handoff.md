# EventStore internal actor admission handoff

**EventStore repair and narrow qualification: PASS. Folders Task 36 strict acceptance: still open.**

The EventStore-owned correction is ready for Folders strict-lane requalification. No Folders production source, dependency pin, historical evidence, publication, staging, commit, branch or push was changed by this task. The existing inherited-owner replay implementation remains byte-identical. The process qualification below exercises production EventStore/Sample hosts; it is not a new Folders real-provider pass.

## Causal finding and correction

The required Folders packet is `/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/evidence/story-12-2-inherited-owner-requalification-20261022T084922Z/`. Its six requested inputs are hash-bound in [causal-inputs.json](causal-inputs.json). The log first records HTTP429 at line 9495, then `ValidateAuthorityAsync` failures at lines 9517/9521/9525 while executing ConfigureBranchRefPolicy, and `MarkRecoveryAsync` at line 9531. Recovery cannot persist the unknown outcome; the command returns 500 and retries fail closed with `idempotency_admission_unavailable`. All 294 limiter warnings identify unknown/anonymous. Sustained rejection suggests the shared tenant window, but the original packet has no successful-callback count or rejecting-lease metadata to prove which quota rejected first. No original terminal admission state is inferred.

The owning Aspire helper did not supply EventStore's app-channel token in Folders composition. Runtime callbacks therefore shared anonymous public quota; one shared verified-sidecar bucket would merely relocate cross-actor exhaustion. The final-script real-Dapr **red** reproduces this second issue even with a valid token: actor A saturates the shared bucket, then unrelated actor B's AdmitAsync fails with internal 429. See [live-final-v2-red](live-final-v2-red/result.json).

The fix wires the generated per-run token through `AddHexalithEventStore`, and separates configured-token-verified callbacks by actor type/id with structurally distinct actor/public cache keys. Existing numeric limits, windows, segments, queues, override lookups and429/Retry-After behavior remain. No limiter was disabled and no public budget was raised. Workload JWT issuer/audience/operation checks, admission proofs/fences, conflict decisions, recovery and completion algorithms are unchanged.

Folders has its own `Hexalith.Folders.EventStore` Program and calls raw `MapActorsHandlers`. The SDK therefore recognizes the actual matched Dapr delegate's declaring assembly/type plus exact route/verb metadata, as well as its standalone private marker. A lookalike path, copied claims, or a non-Dapr delegate with copied metadata cannot establish the partition. No consumer persistence workaround or host edit is needed for quota isolation. Standalone mappings also require the channel policy. Raw custom-host route authorization remains the host's existing responsibility; fallback recognition itself verifies the configured token before granting an isolated quota and does not rewrite consumer authorization policies.

The related standalone AppHost's duplicate token-generation call was removed because ownership moved into the helper. No unrelated AppHost was edited or operated.

## Source and binary identity

Source root: `/home/administrator/projects/hexalith/eventstore`. Starting HEAD was `c66a0a3345ad97b137959179a0583faf81d59e4b`; external Git recording advanced it to `70b2206b531a71c883fbe2e4749047b399d26447` during the task. The production repair remains uncommitted, so HEAD alone does not identify it. [final-source.patch](final-source.patch), [source-inputs.json](source-inputs.json), [handoff-inventory.json](handoff-inventory.json), and the frozen [handoff-runtime](handoff-runtime/) are the correction identity.

Of the preceding packet's 3751 source/configuration inputs, seven existing files changed and all seven configuration inputs stayed identical; seven new source/test/tool files are also frozen, giving 3758 inputs. [prior-handoff-source-delta.json](prior-handoff-source-delta.json) lists the exact delta. The inherited replay coordinator SHA-256 remains `23f82c1898318e7d9a1fc0e6892586d210a5d48debab58087f2c9c2176b1cbe9`. Its identity, continuity, CAS/epoch, and100-operation/1048576-byte enforcement code was not changed. Historical replay/process/bounds evidence is preserved rather than relabeled as newly executed.

| SDK module | DLL SHA-256 | MVID | PDB SHA-256 |
|---|---|---|---|
| Hexalith.EventStore.Admin.Abstractions.dll | `88F5DB9B73BAF69F5DB34E90EF3004C5399227D1E9EAC4BE1E3F10418424A9AC` | `a4d68224-6c8f-4050-8a06-cd582b854c97` | `82C52AB9C6979C9B0569F213939ED216BA6B1A5BD42117088036EB2B6B69E49A` |
| Hexalith.EventStore.Aspire.dll | `9040588CED86CFCEE0F723F389CDF704D0FFB73711EE993625329B0DB3889B7A` | `07308b69-e723-44a6-8d80-827c5086f17d` | `6736B086DBF26BDA618FD33FEF7D4BD761C6D85B02E54EC6690D4727181C19EA` |
| Hexalith.EventStore.Client.dll | `320CE0A8B5210783E9C9BD68AA361F9859D74E716E7013FF6F140D3CDC8050E2` | `e559c561-ea73-42c2-aa51-93f8fb5bec16` | `7A95D396F3A82465C706661A498E60E3BCCC9F19B5A020F329153FAFA6F2B42D` |
| Hexalith.EventStore.Contracts.dll | `383CE30F1E8FD47116E69CE15A75905F29777128A4BCF8CE7757ABF4FFED8240` | `ab0bafc6-848e-4899-9d34-3e3fcc291355` | `9305C373426F11587D1EED277EF703E9396D5B668E09DF84CE2747AEF9FF8DA1` |
| Hexalith.EventStore.DomainService.dll | `576E65A296500368B0C8465E77F8899284FD6603CFEE67F197E4B60C03C6B09D` | `832d4856-900d-4ffc-a933-4a2fc39db04e` | `C2BF2BA681ED7EA08C5C602C9F3135B89078192E046AE06BFCCFEE52599622B6` |
| Hexalith.EventStore.Gateway.dll | `2281AC2E2BF697BCC7BDDFB4EF5BBFE5C615376A45E1CD2A430D590A35059CA2` | `236e02bd-308e-4088-bc53-4275ce0f058f` | `88E5E2E81275DF4AB6629C90467894AA818956731DF2199E19B5C2F464E0861E` |
| Hexalith.EventStore.Server.dll | `4B925BFCAE275D99DDA8D9138FA1AAE9F7E210B4544972DBC968A7BFA6968A6D` | `67300c4c-f4a6-4f23-89dc-2967b0395c1d` | `C6E32011E28D15944828E03CE8241DA02F2E3F41C9CC8C702044F3944BF6D218` |
| Hexalith.EventStore.ServiceDefaults.dll | `AEEF471B16F0ED3CB45BD7B795DAC7C057981F29DECBB54F97484AB06A980F2E` | `077cb560-c32d-4ae9-9c9a-f4bd8005a153` | `E16E2F24DED239D1E212513F97AB8CA280B6C8BC86137F4ED8A0A087149D521B` |
| Hexalith.EventStore.dll | `36EB99FA3DCB1D843E0557FB03DAC178C0A034BA097DD90CEEFDFB5F68BABD3E` | `e96ea7ed-4015-47d9-8b78-ab4c4d8dec29` | `AA752EC4A38D603C11227E96D7D7165131E7DD95CDE422867F0D16FCE15E6E32` |

The handoff contains 27 frozen files, including matching Commons/ULID dependencies and runtime manifests. [source-binding](source-binding/summary.json) verifies 11 PE/PDB pairs, including the test hosting shim and Commons. [qualification-source-binding-final](qualification-source-binding-final/summary.json) additionally binds the actual Sample host and Counter contracts: 13 PDBs, 1869 ordinary documents, 60 generated documents, zero errors and zero generated-disk differences. PE/PDB GUID/stamp/age checks pass. [bound-source-index.json](bound-source-index.json) maps 1929 document copies to 1907 content-addressed source archives, including the uncommitted correction.

[mapped-handoff-audit.json](mapped-handoff-audit.json) matches all 27 actual `/proc` mapped SDK/shim observations before/after recovery to the frozen DLL/PDB hashes. MVIDs are read from those bound PE files. One earlier Sample Contracts image variant was detected, retained in [mapped-handoff-pre-normalization.json](mapped-handoff-pre-normalization.json), and corrected by selecting one SDK graph in both private process outputs. Only `live-final-v2` is the final acceptance run. Earlier diagnostics and the pre-review graph remain retained.

## Commands and results

The process qualifier targets this Linux/Docker environment. Commands ran from the EventStore root. [main-command-receipts.json](main-command-receipts.json) and named red/green build/test receipts preserve exact argv, results and XML timing where available. Debug builds used project references:

```bash
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false
```

Final live command:

```bash
HEXALITH_ACTOR_ADMISSION_EVIDENCE_DIRECTORY=/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/live-final-v2 dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.ActorCallbackAdmissionProcessTests -showLiveOutput -result-xml _bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/live-final-v2.xml
```

| Evidence | Result |
|---|---|
| Initial production pipeline and topology reds | Expected failures retained for missing actor isolation/channel authorization/token wiring. |
| Copied-subject red | Both cases fail before structural actor/public key separation. |
| Final-script frozen-runtime red | Expected actor B admission429; exit1; cleanup succeeds. Same script hash as green. |
| `final-admission-auth-rate.xml` | 547 passed, zero failures/errors/skips; current admission/authentication/rate regressions. |
| `review-server-attempt4.xml` | 21 passed; includes unmarked custom-host DELETE/PUT/reminder/timer provenance, forged delegates and copied identities. Overlaps the 547; do not add counts. |
| `token-green.xml` | 18 topology/trust-model tests passed; its Aspire output matches the frozen Aspire DLL. |
| `review-python-cleanup` | 4 passed, including failed removal, already-absent resources, success/cleanup separation, real SIGTERM and owner-loss child cleanup. |
| `live-final-v2.xml` | 1 discoverable live test passed, zero skips; 47/47 assertions, 37.6 seconds of process qualification. |
| Source/PDB/mapped audits, selector preflight, frozen check, diff check |Pass. |
| Broader public integration matrix | 30 passed; 3 health tests fail expecting 200 but receiving 503 with absent external health dependencies at localhost:3500. Retained as a failed broader gate, not hidden by the isolated throttle pass. |
| Full server projection suite |Pre-review diagnostic interrupted with SIGINT/SIGTERM before rebuilding for review corrections; no complete full-suite success claimed. Final 547 relevant tests ran afterward. |

## Persisted recovery and public controls

[qualification.json](qualification.json) and the phase snapshots in [live-final-v2](live-final-v2/) retain actual Redis data written through production Dapr paths. Diagnostic reads use only the disposable provider. No provider-side application writes or mock persistence were used. The existing test-only OQ8 hosting startup supplies a Counter intent adapter, clock and boundary counter; production authentication, command routing, domain invocation, actor state, event publication and command status stores remain in use.

The qualification config is deliberately stricter than production defaults: tenant100/60s and consumer100/60s, zero queue. Actor A continues returning 429 immediately before/after the compared phases. A separate admission-control row moves Reserved→Pending→UnknownProviderOutcome, persists state 4/fence 1, survives process replacement, rejects Execute authority and a stale reconciliation fence, and completes only under its current fence. Authenticated inspection and full durable-row equality bracket each refusal. Re-admission returns Replay with the entire original result and both execution identities. This is a synthetic admission reconciliation control, not a real-provider side-effect claim.

A separate real Counter command traverses public authenticated HTTP, EventStore, authenticated Sample workload invocation and real Dapr/Redis. It retains exactly one event, aggregate sequence 1, command statusCompleted(4), and admissionTerminal(5). EventStore and sidecar PIDs change **35032/35033→39378/39379**. Public retry retains the original message identity and durable result, invokes the actual domain process once total, and appends no second event. The HTTP correlation ID correctly belongs to the retry attempt. Changed intent returns 409 and leaves the original event/admission unchanged. No conflict is acknowledged.

Tenant and consumer controls each prove exactly 100 authenticated 404 status reads followed by 429, correct rejecting tenant/consumer identities and Retry-After. Saturated actors also remain bounded. Missing/wrong channel credentials and channel-only workload-effect submissions are denied in the production standalone host. SDK quota classification additionally rejects duplicate/unconfigured credentials and forged endpoint provenance in focused tests.

Actual sidecar is Dapr 1.18.7 (binary hash, arguments, private SQLite discovery and components retained); the .NET actor SDK inspected is 1.18.10. Owned Redis 7 Alpine image is `sha256:8b81dd37ff027bec4e516d41acfbe9fe2460070dc6d4a4570a2ac5b9d59df065`. Owned placement/scheduler use `daprio/dapr:1.18.7`. Full identities are in the per-run infrastructure receipts.

## Folders strict-lane requalification recipe

The following is a handoff recipe, not an executed Folders pass. Use a new evidence directory; keep the existing packet unchanged. The externally produced `story-12-2-strict-continuation-20261022T100829Z` packet records two newer failures during fixture initialization, before consumer commands: Dapr health timed out after SIGHUP during sidecar startup. The signal sender remains unclassified. Investigate that owned startup sequence before launching another strict attempt; this actor-quota correction does not establish a startup repair. Its README also identifies the actual Tenants launcher that the final selector now includes. See [external-additional-inputs.json](external-additional-inputs.json). First verify current inputs and packet bytes:

```bash
cd /home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022
sha256sum -c SHA256SUMS
python3 verify-handoff.py
```

From `/home/administrator/projects/hexalith/folders`:

```bash
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -m:1 -nr:false

actor_packet=/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022
folder_strict_evidence="$PWD/_bmad-output/implementation-artifacts/evidence/strict-actor-admission-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir "$folder_strict_evidence"
python3 "$actor_packet/select-folders-runtime.py" --consumer-root "$PWD"
python3 "$actor_packet/select-folders-runtime.py" --consumer-root "$PWD" --apply --receipt "$folder_strict_evidence/runtime-selection"

HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests -showLiveOutput -result-xml "$folder_strict_evidence/strict-live.xml"
```

The selector's final default preflight passed all ten required Debug output paths, including the actual Tenants executable and its Server library; see [folders-selector-preflight-final.json](folders-selector-preflight-final.json). The earlier nine-path preflight is retained as a diagnostic. `--apply` is deliberately unexecuted here. It verifies frozen inputs, preserves every replaced output by hash, selects matching DLL/PDB dependencies and generated Commons/ULID runtime probes, and records a receipt. It never changes source, package pins or historical evidence. The consumer's own `Hexalith.Folders.EventStore.dll` stays its own entry point; corrected Gateway and Aspire images are both necessary. Capture the live mapped modules, per-node token wiring (without logging secrets), persisted strict command/event/admission end-state and cleanup again; version text, a passing build or the old SDK image selection is insufficient.

## Preservation, external Git and cleanup

[preservation-final.json](preservation-final.json) verifies all 39775 historical evidence files and all preexisting user edits unchanged. [external-git-observation.json](external-git-observation.json) distinguishes the packet-reported Folders HEAD `f65dfb08600b44a24d2db4dc57a129da96313a82` from the first directly observed `0a693eb48b37faed255c9f560ecbba691e3f1bf1` and final `460f7b036a526ac08d563f2e69bd3a017fea54f6`. Folders external commits are timestamped 2026-10-22T12:04:49+02:00 and 12:53:10+02:00. EventStore external commit `70b2206b531a71c883fbe2e4749047b399d26447` is timestamped 12:49:20+02:00 and records the preexisting planning/sprint edits. All qualified source/binary bytes still verify after these advances. No task staging/commit/push or external publication is inferred.

Final red and green cleanup independently prove all owned application/sidecar processes and labeled containers absent and runtime directories removed. Cancellation controls also pass. [temporary-cleanup.json](temporary-cleanup.json) records removal of the identity-checked verifier, review temporary file and baseline runtime; [baseline-runtime.tar.gz](baseline-runtime.tar.gz) preserves the 154-file red graph. All failed attempts remain retained. The unrelated Tenants AppHost remains running at PID 67730; this task issued no lifecycle command against it. Final resource inventory is recorded separately.

Task 36 closure still requires the fresh strict real-provider consumer pass and its own retained state evidence. Package/PD10, SM4 acknowledgement performance, broader suite gates, and whole Story 12.2 acceptance remain separate and open. No package or release qualification is claimed.
