---
title: 'Dapr crypto backend error outcomes (Story 8.7 gate 2)'
type: 'bugfix'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '02873c9d4d3da23a53037cb3bda811081d9cd575'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Dapr.Client 1.19.0 `EncryptAsync`/`DecryptAsync` rethrow gRPC failures on a fire-and-forget task and return empty output. The `DaprKubernetesCrypto` backend therefore reports every provider failure as `ConsistencyMismatch`: outages get no retry, the executor breaker almost never opens, and the rethrown `RpcException` is a latent unobserved task exception. This is gate 2 of Story 8.7 (`deferred-work.md`, "Deferred from: build of spec-8-12-dapr-kubernetes-cryptography.md").

**Approach:** Under amendment revision 3, which changes only §2, call the generated `Dapr.Protos` 1.19.0 `EncryptAlpha1`/`DecryptAlpha1` client on the calling task. Each call's gRPC status then reaches the unchanged §7 mapping. The wire format, key addresses, records, one attempt per call and the excluded-API list are unchanged.

## Boundaries & Constraints

**Always:**
- Apply amendment §7 exactly, by gRPC status only, never by message text.
- Keep everything else as it is:
  - one attempt per call and the combined budget;
  - round-trip wrap verification and the 32-byte unwrap check;
  - rejection of oversized, empty and prefix-less wraps;
  - buffer clearing;
  - transport-free Reconcile → `Missing`.
- `WrappedDekV1`/`V2` and envelope bytes stay unchanged. The request fields are the ones Dapr.Client 1.19.0 sends.
- Resolve the endpoint and API token exactly as Dapr.Client 1.19.0 does (`DaprDefaults`).
- One type per file. Check each new guard with a mutation in a scratch copy, never in the tree.

**Never:**
- Package references or central versions (Builds owns them), `Dapr.Cryptography`, Subtle APIs, message-text classification, background tasks or backend retries.
- 8.7 host admission, Enabled activation, Server/DI wiring, the other 8.12→8.7 obligations, Azure 8.6, the capacity deferrals, 3.21, the `epics.md` refresh and production qualification.
- The ambient kube context, the user's installs, any daprd other than the 1.19.1 lock, and new proof or evidence models.

**User decisions (2026-11-10, recorded at HEAD `02873c9d4d3da23a53037cb3bda811081d9cd575`):**
- **Option A; amendment revision 3 approved as drafted.** Only the §2 "Client calls" bullet changes. The approved bytes are bound by normative `14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881` and effective authority `29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44`; paired frozen intent `b36bbe08…` is unchanged. Option B (no fixed `Dapr.Client` exists) is recorded only. Option C (`Dapr.Cryptography`) is rejected: it needs a Builds change and reverses the 8.12 exclusion.
- **Denied ServiceAccount: accept.** daprd 1.19.1 returns `Internal` for a Kubernetes 403, so under §7 it stays `ConsistencyMismatch`. `Denied` is proven on the lane by an unauthenticated call against a per-run API token. A deferred-work entry records the daprd limitation.
- **Keep the full spec** (about 2,900 tokens).
- **Legacy envelope.** The lane creates the 666-byte envelope through `Dapr.Client` 1.19.0 `EncryptAsync`; nothing is committed, because decrypting a committed envelope would need committed key material.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Wrap / unwrap | Healthy sidecar | 666-byte envelope with no `"k"`; exact 32-byte DEK | Unchanged |
| Legacy envelope | Envelope written by Dapr.Client 1.19.0 `EncryptAsync` | New backend decrypts it to the exact DEK; a new envelope decrypts through the legacy SDK | — |
| Unauthenticated | Missing or wrong `dapr-api-token` | `Denied` | No retry; nothing persisted |
| Outage | Sidecar stopped or unreachable | `Unavailable`; the executor retries; repeated failures open the breaker | Open breaker: no transport attempt |
| Provider error | Denied ServiceAccount (daprd `Internal`), tampered envelope, missing Secret, `NotFound`/unknown status | `ConsistencyMismatch` | Partial output discarded and cleared |
| Leak channel | Any failure | Nothing reaches `TaskScheduler.UnobservedTaskException` | — |

</frozen-after-approval>

## Code Map

Engine paths are under `src/Hexalith.EventStore.PayloadProtection/`.
- `DaprCryptographyPayloadProtectionBackend.cs` — the `_client.EncryptAsync`/`DecryptAsync` calls at :111/:117/:140 go to the new transport. Keep `RunAsync`, `Classify` and `Clear`. The `DAPR_CRYPTOGRAPHY` suppression at :5 goes: the generated methods are not `[Experimental]`.
- Generated client: `Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient(CallInvoker)` from `Dapr.Protos` 1.19.0, a direct dependency of `Dapr.Client` with no package change. The SDK request is `EncryptRequestOptions { ComponentName, KeyName, KeyWrapAlgorithm = "RSA-OAEP-256", DataEncryptionCipher = "aes-gcm", OmitDecryptionKeyName = true }` and `DecryptRequestOptions { ComponentName, KeyName }`, with options on the seq-0 message. Payloads are 4,096-byte blocks, so ours are one message.
- `DaprDefaults` (Dapr.Common): `GetDefaultGrpcEndpoint(config)`, `GetDefaultDaprApiToken(config)`. The SDK builder adds `ThrowOperationCanceledOnCancellation = true` and the header `dapr-api-token`.
- `PayloadBackendExecutor.cs:27-130` — three attempts only for `Unavailable`. Five Unavailable/Ambiguous results within 30 s open the breaker. While it is open, `attempted` is never invoked (the lane's zero-attempt check). Unchanged.
- Digest pins to move: the constants in `PayloadProtectionAuthorityProfile.cs:10,13`; header comments in 14 engine files and 8 test files; `KeyRecordAuthorityTests.cs:25`; the canary at `DaprCryptographyBackendTests.cs:366`. `git grep -n -E "2c7c5099|e31a6b1d" -- src tests` lists them all. Key records carry no authority. Fences, deployments and leases bound to `2c7c5099` are refused, as in revision 2, and exist only in disposable lanes.
- Tests:
  - `DaprCryptoTestSidecar.cs` (NSubstitute `DaprClient`) becomes a fake `CallInvoker` that keeps the envelope logic.
  - Its users are `DaprCryptographyBackendTests.cs` and `KeyRecordAuthorityTests.cs`.
  - The §7 theory is at :139-162.
- Live:
  - `KubernetesCryptoFixture.cs:149-178` starts daprd and builds `Client`.
  - `PayloadProtectionKubernetesCryptoLiveTests.cs` `AssertFailureAsync` :383-397 asserts `ConsistencyMismatch` for every case today.
  - The workflow pins `--minimum-expected-tests 12` (:82).
- Do not change: `WrappedDekV2`/codec/schema bytes, the `PayloadBackendExecutor` logic, the §3–§8 normative text, the 8.12 spec frozen block, predecessor approvals, the 1.18.7 lifecycle lane.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md` — write the approved revision 3 bullet from Design Notes (the marker range must hash to `14eb081c…`). Move the frontmatter digests and keep the previous ones. Add a dated revision note outside the markers.
- [x] `DaprCryptographyGrpcTransport.cs` (new) — the generated client over a `CallInvoker`, plus an optional token header. `Create(IConfiguration?)` uses `DaprDefaults` and owns and disposes its channel.
  - Each call writes one seq-0 message, completes the request stream and reads to the end on the calling task.
  - Output is capped at 2,048 bytes (encrypt) or 33 bytes (decrypt).
  - A write failure surfaces the call's status.
  - Protobuf buffers are cleared.
  - There is no gRPC retry policy.
- [x] `DaprCryptographyPayloadProtectionBackend.cs`, `PayloadProtectionAuthorityProfile.cs` and the pinned headers — the constructor takes the transport, the SDK calls and the suppression are removed, and the revision 3 digests are bound.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/` — a fake transport covers:
  - every §7 status, ended at write, at read, and mid-stream after partial output;
  - the exact request fields and single message;
  - the token header with and without a token;
  - `Create` resolving the endpoint and token from configuration;
  - no unobserved exception;
  - the digest test.
- [x] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`, `.github/workflows/payload-protection-kubernetes-crypto.yml` — the fixture uses a per-run API token (daprd environment, state client and `Create`). The matrix cases above are added, plus the breaker zero-attempt check, a legacy-envelope test in both directions (test-only suppression), an unobserved-exception listener and the bumped minimum count.
- [x] `docs/ci.md`, `deferred-work.md` — update the limitation sentence, mark the 8.12 SDK-outcome entry resolved, and append the daprd `Internal`-for-403 limitation.

**Acceptance Criteria:**
- Given the approval, when the code lands, then the constants and `KeyRecordAuthorityTests` bind the revision 3 digests, predecessor approvals and fixtures are byte-unchanged, and no production source suppresses `DAPR_CRYPTOGRAPHY`.
- Given the unit lane, when it runs, then everything passes, and each new guard's scratch mutation fails at least one test.
- Given the owned kind lane on daprd 1.19.1, when it runs with `--explicit only`, then every case passes with zero skips and no cluster survives.
- Given the final diff, when `*.csproj` and `*.props` are inspected, then no package reference or version changed.

## Spec Change Log

## Review Triage Log

### Review pass 1 (2026-11-10)

Layers: blind-hunter (14), edge-case-hunter (8), verification-gap (3 gaps). There are no intent_gap or bad_spec entries, so the pass applies patches and one deferral.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| VG1 | The backend's clearing of transport-returned arrays is no longer tested | medium | Pre-verified gap. `Outputs` now records only protobuf chunk arrays, which the transport clears itself, so deleting `Clear(verified)`/`Clear(plaintext)` leaves plaintext DEK copies and every test green. | patch |
| VG2 | The transport's failure-path zeroing of its accumulation buffer is untested | medium | Pre-verified gap. Deleting `if (!returned) ZeroMemory(output)` leaves partial envelopes, or the whole DEK after a write failure followed by OK, in an unreferenced array; the MidStream, write-then-OK and over-cap tests still pass. | patch |
| VG3 | A `Denied` wrap through the reservation coordinator is untested on the Dapr profile | medium | Pre-verified gap. No test asserts `provider-denied` on the Create audit; dropping the `Denied => ProviderDenied` arm would turn Denied wraps into Ambiguous intents with every test green. | patch |
| BH1+BH2 | The revision 3 note does not say how unchanged §7/§8 wording reads under revision 3 | low | §7 still says "inside the SDK exception" and "SDK-returned arrays", and §8 lists "API-token authentication" as blocked while the lane proves Denied with a self-hosted token. A sentence outside the markers fixes it, and the digest is unchanged. The DaprException-wrapping unit test stays: `Classify` still walks inner exceptions. | patch |
| BH3 | The deferred-work entry is marked done before review and commit | false | The ledger edit lands in the same commit as the code, so it cannot be published without it, and a commit cannot cite its own SHA. | reject |
| BH4+BH5 | The 8.12→8.7 obligations entry was not updated for revision 3 | low | It still lists unobserved-exception scrubbing, which is now moot for the crypto path, and it omits the new transport lifetime duty: create once through `Create`, share it as a singleton, dispose it at shutdown. One note line. | patch |
| BH5b+ECH3 | A disposed transport's `ObjectDisposedException` is audited as consistency-mismatch | low | It needs a call after the host disposes singletons, which happens after it drains work. The fix adds a branch. | reject |
| BH6 | `docs/ci.md` and the 8.12 spec still point at the old SDK finding | low | The row ends "Results are recorded in the Story 8.12 spec", and 8.12's Implementation Notes read as current. Direct pointer fixes. | patch |
| BH7 | The transport claims the request is exactly what Dapr.Client sends | low | Dapr.Client also adds `User-Agent` and trace-context headers (`DaprClientGrpc.CreateCallOptions`); only the message matches. A wording fix. | patch |
| BH8 | Nothing pins the resolved `Dapr.Protos` 1.19.0 version | low | This is pre-existing: revision 2's `Dapr.Client` 1.19.0 pin had no guard either. A Builds Dapr bump is a reviewed change that also moves the runtime lock, and the fix adds a guard. | reject |
| BH9+ECH6 | The no-suppression guard scans only `.cs` files | low | A `<NoWarn>` in a csproj or props file, or an analyzer-config severity line, would pass it. Extending the globs is a direct correction. | patch |
| BH10 | The "status not recorded" branch of `Failed()` never runs in unit tests | false | An exception thrown inside an exception filter makes the filter evaluate as false, so the catch has no observable effect: the OCE reaches `Classify` as `Unavailable`, the §7 outcome for a non-caller cancellation, either way. | reject |
| BH11+ECH8 | A write failure on a call that stays open burns the budget | low | No reachable path is shown. The only write failure is local serialization of a fixed message under 2 KiB, which cannot fail; every other write failure ends the call. The fix adds branches. | reject |
| BH12 | Response `Seq` is not checked | low | This is hardening at parity with the SDK, and a guard on unverified runtime semantics could fail valid calls. | reject |
| BH13 | The live unobserved-exception check has no positive control | low | The empty-queue assertion is the only real Grpc.Net.Client evidence for the matrix's leak row, and it can pass vacuously. Adding a control is a few lines of test code. | patch |
| BH14 | No test feeds roots bound to the revision 2 authority | low | The refusal is the generic codec digest check (`PayloadProtectionStateCodec.cs`, lines 338-346), which existing authority tests already cover with foreign digests. | reject |
| BH15 | The `docs/ci.md` row omits the ungranted Secret | low | The live test and the new ledger entry both cover it. A wording fix. | patch |
| ECH1 | A `dns:` endpoint is accepted where Dapr.Client refuses it | false | Grpc.Net.Client 2.80.0 `GrpcChannel` throws `InvalidOperationException` for any non-http(s) address without `Credentials` ("Unable to determine the TLS configuration…"), and the transport sets none. | reject |
| ECH2 | A response message over 4 MiB maps to a retried Unavailable | low | It needs a sidecar that answers a 32-byte request with more than 4 MiB. A smaller limit still yields `ResourceExhausted`, so the fix needs a new branch. | reject |
| ECH4 | A long-lived transport may stay Unavailable after a sidecar restart (reconnect backoff) | maybe-false | It is not shown: the lane recreates the transport after a restart. Dapr.Client built the identical channel, so this is pre-existing. A live case that keeps the pre-stop transport across a restart would settle it. If true, medium. | defer |
| ECH5 | A queued unobserved exception fails every later live test | low | The failure is still loud; only attribution suffers. The fix adds snapshot logic. | reject |
| ECH7 | The live pragma comment still cites a production suppression | low | `PayloadProtectionKubernetesCryptoLiveTests.cs:350` says "production suppression stays in the backend file". A direct fix. | patch |

**Pass 1 resolution (2026-11-10).**
- **Applied by the step-03 agent.** All nine patch entries.
  - An internal allocator seam lets tests observe the transport's output buffers.
  - The reservation `provider-denied` fact was added.
  - The suppression guard now also scans project and analyzer-config files.
  - The live unobserved-exception check has a positive control.
  - The wording fixes, the amendment note outside the markers (the digest is still `14eb081c…`), the obligations-entry note, the `docs/ci.md` pointer and the 8.12 superseded pointer are in.
- **Mutation checks, in a scratch copy:** 7 of 7 caught.
- **Deferral:** ECH4 was appended to `deferred-work.md`.
- **Parent re-verification:**
  - Debug builds have 0 warnings: unit, LiveSidecar, Azure Key Vault and Server.
  - Release `-warnaserror` `UseHexalithProjectReferences=false` builds are clean: unit and LiveSidecar.
  - `PayloadProtection.Tests` passed 1,244/1,244, 0 skipped. Azure Key Vault passed 192/192.
  - The Kubernetes lane (`--explicit only --fail-skips on --minimum-expected-tests 14`) passed 14/14, 0 skipped, in about 2 minutes. No kind cluster survived.
  - `git diff --check` is clean.

## Design Notes

| | A: generated client (recommended) | B: newer Dapr.Client | C: `Dapr.Cryptography` 1.19.0 |
|---|---|---|---|
| Fixes it | Yes; the status is read on the calling task | No release exists. 1.19.0 is the latest, and `master` (`1f8f6c37`) still swallows the error | Yes (`DuplexStreamProcessor` completes the channel with the error) |
| Cost | Revision 3 (§2), digest move in about 22 files, about 80 lines of transport | Nothing now. A future fix still needs a revision (§2 names 1.19.0), a Builds bump and a live re-run | Revision 3, a new package and a Builds version in another repository, and reversing the 8.12 exclusion (§2 Excluded list, 8.12 Never list) |

A keeps the dependency graph and needs no Builds change. B is recorded here and changes nothing.

**Approved revision 3 §2 bullet.** Between the markers, this text replaces the four-line revision 2 "Client calls" bullet, and nothing else changes. The result must hash to `14eb081c…`.

```markdown
- **Client calls.** EventStore calls only the generated gRPC client of `Dapr.Protos` **1.19.0**, the package `Dapr.Client` 1.19.0 already depends on. It calls no `Dapr.Client` cryptography method and starts no background task:
  - `EncryptAlpha1`, with the component name, the key address as key name, key wrap `RSA-OAEP-256`, data cipher `aes-gcm` and `omitDecryptionKeyName` set. This is the request that `Dapr.Client` 1.19.0 `EncryptAsync` sends.
  - `DecryptAlpha1`, with the component name and the recorded key address as key name.
  - Each call writes its whole input as one message with sequence 0, completes the request stream, and reads the response stream to its end on the calling task. The call's gRPC status is its outcome. Output received before a non-OK status is discarded and cleared.
  - The sidecar gRPC endpoint and API token are resolved as `Dapr.Client` 1.19.0 resolves them: `DAPR_GRPC_ENDPOINT` or `DAPR_GRPC_PORT`, and `DAPR_API_TOKEN`, sent as `dapr-api-token`. No option field supplies them.
  - Envelopes written through `Dapr.Client` 1.19.0 `EncryptAsync` keep their meaning and decrypt unchanged.
  - No production source suppresses the `DAPR_CRYPTOGRAPHY` diagnostic.
```

## Verification

**Commands:**
- `dotnet build` of `tests/Hexalith.EventStore.PayloadProtection.Tests` and `…LiveSidecar.Tests` csproj — expected: 0 warnings, 0 errors.
- The PayloadProtection.Tests DLL in place with `--fail-skips on --minimum-expected-tests 324` — expected: all pass, at least 1,196 plus the new tests.
- The Azure Key Vault test DLL in place — expected: 192 pass.
- `dotnet test --project …LiveSidecar.Tests.csproj --filter-trait Lane=KubernetesCrypto --explicit only --fail-skips on --minimum-expected-tests <new>` — expected: all pass, 0 skipped.

**Results (2026-11-10, uncommitted tree on `02873c9d4`):**
- Amendment revision 3 is applied. The marker range hashes to `14eb081c…`, the derived authority is `29ec8546…`, and the paired frozen intent `b36bbe08…` is unchanged. The 32 pins under `src/` and `tests/` now use the revision 3 digests, and no `2c7c5099`/`e31a6b1d` reference remains there. No `*.csproj`, `*.props` or `*.targets` file changed.
- Builds have 0 warnings and 0 errors: the engine, Server (which links the engine files), the PayloadProtection unit and LiveSidecar test projects in Debug and in CI-mode Release `-warnaserror` with `UseHexalithProjectReferences=false`, and the Azure Key Vault tests.
- `PayloadProtection.Tests` (`--fail-skips on --minimum-expected-tests 324`): 1,243/1,243 passed, 0 skipped (47 new). Azure Key Vault unit: 192/192.
- Kubernetes lane (daprd 1.19.1 per lock, kind 0.31.0, `--filter-trait Lane=KubernetesCrypto --explicit only --fail-skips on --minimum-expected-tests 14`): 14/14 passed, 0 skipped, about 2 minutes, run twice. No kind cluster or container survived.
  - A missing or wrong API token is `Denied`, with one attempt per call.
  - A stopped sidecar is `Unavailable`. Attempts per call are 3, 2 and 0: the breaker opens on the fifth failure, and the open breaker allows no transport attempt.
  - A denied ServiceAccount, an ungranted Secret and a missing Secret are `ConsistencyMismatch`, with one attempt per call.
  - Legacy envelopes open in both directions, at 666 bytes with no `"k"`.
  - The fixture's listener recorded no unobserved task exception.
- Scratch-copy mutations (never in the tree) each failed at least one test: the write failure propagating; an undelivered request with an OK status trusted; the output cap removed; the decrypt cap widened; the locally ended call's status ignored; protobuf buffers not cleared; the token header dropped; the endpoint not read from configuration; `omitDecryptionKeyName` unset; the request stream not completed; sequence ≠ 0; an SDK-style background reader (the leak test alone fails); a production `DAPR_CRYPTOGRAPHY` suppression; the digest constant changed.
- Implementation notes:
  - Grpc.Net.Client cancels its own reader when a call fails locally, for example on a refused connection. The transport then raises the call's recorded status, so that status still reaches §7.
  - Dapr.Client's builder refuses non-http(s) endpoints. `GrpcChannel` already refuses them with the same `InvalidOperationException`, so no separate check was added.
  - The transport's own partial-output buffer is cleared on failure, but no test can observe it.
