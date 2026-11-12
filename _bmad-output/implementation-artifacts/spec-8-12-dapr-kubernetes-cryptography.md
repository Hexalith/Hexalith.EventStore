---
title: 'Story 8.12: Dapr Kubernetes Cryptography And Wrapped-Key Conformance'
type: 'feature'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'aa16227bd07f0564a3026d619a5abd973e02656f'
story_key: '8-12-dapr-kubernetes-cryptography-and-wrapped-key-conformance'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-5-lifecycle-protocol-amendment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The selected on-premises path (stock Dapr `crypto.dapr.kubernetes.secrets/v1`) has no EventStore backend or key record. `WrappedDekV1` is bound to RSA output ≤ 512 bytes, so Story 8.7 cannot wire the Server.

**Approach:** Approve the exact successor amendment. Then, inside the existing engine, add a gRPC Encrypt/Decrypt wrap backend, a closed `WrappedDekV2` record and a third authority profile with a new reader capability. Qualify them against a real Kubernetes API on runtime 1.19.1.

## Boundaries & Constraints

**Always:** Follow the base spec, the 8.5 lifecycle authority and the approved 8.12 amendment exactly. Keep pdenc-v2 event/snapshot bytes and AAD, and `WrappedDekV1` with its 512-byte bound and RSA meaning, byte-unchanged. Decrypt with the recorded trusted key address, never the envelope's name. Keep one combined budget, owned-buffer clearing and typed outcomes. Assert persisted end state through the Dapr state boundary. One type per file; Builds owns package versions.

**Never:**
- Subtle crypto APIs, a custom runtime, the `Dapr.Cryptography` package or any Builds change, provider HTTP, an application Kubernetes client, exported KEKs.
- Restamping or converting existing roots; migrating V1 records across backends; inferring Missing, Invalidated or deletion from Dapr errors.
- Server host wiring or Enabled activation (8.7); package or inventory change (stays 14); release, deployment, Azure 8.6 or G5.
- The current kube context or any shared cluster; committed key material.
- Edits to frozen fixtures, the base spec or predecessor approvals; new `Vector` traits.

**User decisions (2026-11-10):**
- **Full slice that can close.** EventStore builds the amendment, record, authority, backend, unit tests and an owned kind + daprd 1.19.1 lane (new opt-in CI workflow). Platform-owned in-cluster items are listed as explicitly blocked. 8.12 may close `done` with only the bounded 8.7 handoff.
- **Alpha accepted for this profile only.** The Alpha/experimental posture is recorded as a limitation, and `DAPR_CRYPTOGRAPHY` is suppressed only in the backend file. This acceptance gives no readiness claim and extends to no other profile.
- **One spec**, kept despite its size.
- **Reconcile returns Missing (review pass 1).** Dapr Encrypt and Decrypt create no provider-side state, so Reconcile reports the exact mutation absent. The existing 8.5 recovery can then fail interrupted reservations after their window and restore interrupted rewraps; durable state alone decides recovery. The verified code is kept and patched, not reverted.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Wrap | Fresh 32-byte DEK, current write address | V2 record with exact address, closed algorithm id, ≤ 2,048 decoded bytes | Oversize or malformed → ConsistencyMismatch; nothing persisted |
| Unwrap | Recorded address in the inventory | 32-byte DEK; decrypt overrides the envelope name | Tamper or name mismatch → ConsistencyMismatch; no plaintext |
| Rotation | Write address changed by rollout; old address retained | New wraps use the new address; old records still unwrap; V2→V2 rewrap verified | Address outside the inventory refused before transport |
| Failure | Cancel, outage, denied, missing Secret, lost response | OCE / Unavailable / Denied / ConsistencyMismatch; Reconcile → Missing, because Encrypt mutates nothing provider-side | Wrap, unwrap and transport failures never yield Missing, Invalidated or deletion |
| Mixed readers | Predecessor process meets a V2 record or the V2 capability; V2 process meets a V1 root | Both fail closed with typed outcomes | No conversion or restamping |

</frozen-after-approval>

## Code Map

All engine paths are under `src/Hexalith.EventStore.PayloadProtection/`.
- `PayloadProtectionStateCodec.cs` — 512 bound at :247-249; only `RSA-OAEP-256` accepted at :218; raw schemaVersion `"1"` at :144-146; `FieldsHash` magic `HXKF 01` at :339-353; authority digest check at :271-284; decode defaults to the Predecessor profile at :57.
- `PayloadProtectionStateSchema.cs` (field/element tables, algorithm list :12), `PayloadProtectionStateJsonContext.cs:8`, `DaprPayloadProtectionStateStore.cs:164,175` (`"key"` → `WrappedDekV1`; needs schema dispatch).
- `PayloadBackendResult.cs:16` — 512-byte output cap; make it per-schema.
- `KeyReservationCoordinator.cs:327` — hard-coded `"RSA-OAEP-256"`; take the id from the backend.
- `PayloadProtectionBackend.cs` — SPI. Reconcile must not return Missing without proof.
- `DaprPayloadProtectionBackend.cs` — budget, clearing and cancellation pattern to reuse. Keep it unchanged (8.7 first phase).
- `PayloadProtectionAuthorityProfile.cs:11-13`, `PayloadProtectionOptions.cs:40,53-54` — closed profiles and backends.
- `PayloadProtectionLeaseCoordinator.cs:237-260`, `PayloadProtectionDeploymentCoordinator.cs:32-35,72-73` — capability and read-format gates. Add the reader capability token here and in the codec's read-format list (:202).
- Callers that require an exact backend id and exact version echo: `PayloadKeyResolver.cs:36,104`, `PayloadKeyRotationCoordinator.cs:81-237`, `PayloadRestoreAdmission.cs:77-94`, `PayloadProtectionReconciler.cs:45`. Reuse them as is.
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj:50` links top-level engine files, so new files ship dormant inside Server. `Dapr.Client` 1.19.0 is already a dependency there.
- `Dapr.Client` 1.19.0 `EncryptAsync`/`DecryptAsync` (bytes overloads, `EncryptionOptions(KeyWrapAlgorithm.RsaOaep256)`, `DecryptionOptions`) are `[Experimental("DAPR_CRYPTOGRAPHY")]`. Suppress per file.
- Tests: `DaprPayloadProtectionBackendTests.cs` (mock `DaprClient` pattern), `DaprStateStoreBoundaryTests.cs:142`, `DiagnosticsTests.cs:621-645` (exact `Vector` set), `ReleasePackageManifestTests.cs:1218` (14).
- Live: `PayloadProtectionDaprFixture.cs:37-61` and `OwnedPostgresContainer.cs` are the pattern to follow. That fixture requires 1.18.7, so do not reuse it. Use `deploy/dapr/qualification/runtime-lock-1.19.1.json` (pinned daprd/CLI archives and sha256). kind 0.31.0 and Docker are available locally.
- Do not change: `spec-shared-payload-protection-engine.md`, V2 fixtures and verifier scripts, the `Interaction*` files, `spec-8-7-on-premises-backend-amendment.md`, the existing workflows and their 1.18.7 pins.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md` — drafted at planning and approved with this spec; after that, bind its digests and never edit its normative range.
- [x] `WrappedDekV2.cs`, `PayloadProtectionStateSchema.cs`, `PayloadProtectionStateJsonContext.cs`, `PayloadProtectionStateCodec.cs`, `DaprPayloadProtectionStateStore.cs`, `PayloadBackendResult.cs` — closed V2 schema dispatched by schemaVersion, with its own hash magic and bound; V1 bytes unchanged.
- [x] `PayloadProtectionAuthorityProfile.cs`, `PayloadProtectionOptions.cs`, `DaprCryptographyPayloadProtectionOptions.cs`, `PayloadProtectionLeaseCoordinator.cs`, `PayloadProtectionDeploymentCoordinator.cs` — add the third profile from the amendment's derivation and require the reader capability; options validate the address grammar and the closed inventory.
- [x] `DaprCryptographyPayloadProtectionBackend.cs`, `PayloadProtectionBackend.cs`, `KeyReservationCoordinator.cs` — gRPC wrap/unwrap with the amendment's failure mapping; the backend supplies its algorithm id and record schema.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/WrappedDekV2CodecTests.cs`, `DaprCryptographyBackendTests.cs`, `KeyRecordAuthorityTests.cs` — the matrix, bounds/truncation, mixed-reader refusals, cancellation/clearing and no-leak sentinels.
- [x] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/KubernetesCryptoFixture.cs`, `PayloadProtectionKubernetesCryptoLiveTests.cs`, `.github/workflows/payload-protection-kubernetes-crypto.yml` — an owned kind cluster plus daprd 1.19.1 via `kubeconfigPath`, with runtime-generated RSA-3072 Secrets per version. Cover wrap/unwrap, restart, rotation with retained reads, rewrap, erasure/restore end state and failures, with zero skips.

**Acceptance Criteria:**
- Given approval, when code lands, then constants bind the exact amendment and effective digests, and every predecessor approval and fixture is byte-unchanged.
- Given the owned kind lane on 1.19.1, when the live suite runs, then every selected case passes with zero skips, and the maximum-length-address envelope is ≤ 2,048 bytes. Otherwise 8.12 stays blocked with no fallback.
- Given the existing payload-protection unit, golden, compatibility and lifecycle lanes, when they run, then they pass with unchanged minimum counts and the manifest stays at 14.
- Given closure is requested, when the Verification section is completed, then Platform-owned limitations are listed as explicitly blocked and only the bounded 8.7 handoff is authorized. These are in-cluster RBAC, component scopes, sidecar auth, backup/restore and the rotation rehearsal.

## Implementation Notes

- **Record shape.** `WrappedDekV2` derives from `WrappedDekV1`, which is now unsealed with a virtual `SchemaVersion`. The unchanged resolver, rotation, restore, lifecycle and reconciler callers keep one field shape, and `with` transitions keep the V2 type. The codec selects the schema from `schemaVersion` and the process authority. V2 decodes only under `DaprKubernetesCrypto`, V1 is refused there, and nothing is converted. The Dapr state store's existing `"key"` decode already goes through the codec, so `DaprPayloadProtectionStateStore.cs` needed no edit. A test pins the exact V1 JSON bytes, the `HXKF 01` hash and the 512-byte bound.
- **Authority.** `PayloadProtectionAuthorityProfile.DaprKubernetesCrypto` binds `b11cf767…` with key-record schema 2. A test recomputes it from the amendment's marker range. The `wrapped-dek-v2` token is accepted only under that profile, and there every lease must list it with `json+pdenc-v2`. The lease coordinator refuses a mismatched instance profile at construction, and fence advancement requires the token from every serving member.
- **Backend.** `DaprCryptographyPayloadProtectionBackend` supplies `dapr-crypto-kubernetes-secrets-v1`, `dapr-enc-v1-rsa-oaep-256-aes-gcm` and schema 2. `KeyReservationCoordinator` takes both from the backend and refuses a backend whose schema differs from the lease authority. Backend output is bounded at 2,048 bytes for schema 2 and 512 otherwise.
- **SDK finding.** Dapr.Client 1.19.0 byte overloads never surface gRPC failures. Its stream processor rethrows inside a fire-and-forget task and then completes the output, so the call returns whatever arrived, usually nothing. On 1.19.1 a denied ServiceAccount, an RBAC-ungranted Secret, a missing Secret, a namespace-less name, a tampered or truncated envelope and a stopped sidecar all returned empty output. By the amendment table these are `ConsistencyMismatch`: no executor retry, reason `consistency-mismatch`. `Denied` and `Unavailable` are reachable only from thrown statuses (unit-tested) and budget expiry. Caller cancellation does surface.
- **Wrap verification.** A failed stream can also return a prefix-valid but truncated envelope. A wrap is therefore accepted only after the same address decrypts it back to the exact DEK, within the same single attempt and combined budget. This adds one Decrypt per new key and no retry.
- **Envelope size.** The SDK embeds no key name unless `DecryptionKeyName` is set, so EventStore envelopes carry no `"k"`. The measured envelope is 666 bytes whatever the address length, including the 256-character maximum.
- **Lane isolation.** The live tests are xUnit `Explicit` with `Trait("Lane", "KubernetesCrypto")`. The unchanged lifecycle workflow reports them as not run, which does not trip `--fail-skips on`. The lane therefore needs `--explicit only` in addition to the trait filter. xUnit constructs class fixtures even for unselected explicit tests, so the fixture starts lazily inside the tests. The workflow is `workflow_dispatch` only. It reuses `tools/install-dapr-qualification-tools.py` for the lock-verified daprd and pins kind by version and SHA-256.
- **Suppression.** Among production sources only the backend file suppresses `DAPR_CRYPTOGRAPHY`. Test-only suppressions cover the unit test double, the backend unit tests and one live SDK call that builds a name-embedding envelope.
- **KEK inventory scope.** A KEK inventory spans every scope that uses an address. Live cases that assert inventory counts or run the rotation coordinator therefore use dedicated Secrets.
- **Owner decision on SDK-hidden failures (2026-11-10).** At the step-3 matrix audit, the owner chose "Accept + track". The matrix Failure row is read as the permitted outcome set under amendment §7, unchanged. A denied ServiceAccount, an outage or a missing Secret that the SDK reports as empty output is `ConsistencyMismatch`, and stays fail-closed. Thrown structural statuses still map to `Denied`/`Unavailable` and are unit-tested. Restoring distinct outcomes, and closing the latent unobserved-task-exception channel, is tracked in `deferred-work.md` under this spec as a prerequisite before 8.7 activation. The round-trip wrap verification is kept.
- **Review pass 1 changes (2026-11-10).**
  - **Reconcile.** Revision 2 of the amendment was approved (normative `e31a6b1d…`, effective authority `2c7c5099…`, frozen intent `b36bbe08…`). `ReconcileAsync` now returns `Missing` without transport; this supersedes the earlier Ambiguous notes. Two new unit tests prove recovery through `PayloadProtectionReconciler.RunPassAsync` with the Dapr backend. An interrupted reservation with no key record becomes `Failed`/`not-persisted`. An interrupted V2 rewrap is restored to its prior state and old wrap. `RestoreAbsentAsync` acts only while the key is still `RewrapPending`, so a committed rewrap cannot be reverted.
  - **Digests.** All 21 code and test references moved to the revision 2 digests.
  - **Test envelopes.** The test sidecar's envelopes are now 666 bytes, so the blocking unit lane covers the path over 512 bytes.
  - **Configuration.** Stale `RetainedKeys` children are validated even when unused. The backend copies the inventory, then validates the copy.
  - **Live lane.** The denied case asserts the inventory is unchanged rather than zero. The no-leak test snapshots under its lock. The surviving-cluster guard fails if kind fails.
  - **Documentation (parent session).**
    - `docs/ci.md` has a row for the new lane.
    - The Epic 8 context is corrected and its dropped constraints are restored.
    - The amendment has a post-qualification note recording the fixed 666-byte SDK envelopes with no embedded name.
    - `deferred-work.md` records the breaker consequence and the obligations 8.7 inherits.
- **Review pass 2 changes (2026-11-10).**
  - **Executor.** A transport-free Reconcile `Missing` now releases a held half-open probe without changing breaker state and records no provider-failure metric. New tests cover both. For any backend, a definitive Missing reconcile answer no longer counts as a breaker failure.
  - **Backend.** Dapr Reconcile refuses a non-canonical operation id.
  - **Configuration.** Scalar values at the three backend section keys are invalid.
  - **SPI.** The remark is reworded for backends that keep no provider state.
  - **Test double.** It authenticates its header as AAD.
  - **Disabled-mode test.** The `RetainedKeys:Token` case asserts the generic message.
  - **Documentation (parent session).**
    - The 8.6 diagnostic pointer is restored to the approved narrow scope.
    - The Epic 8 context explains the revision 2 Missing exception.
    - The amendment approval wording is corrected, with its normative digest unchanged.
    - The `docs/ci.md` Test Lanes row is updated.
    - Two deferrals are recorded: the Azure stuck-intent behavior, and refreshing the `epics.md` 8.12 paragraph.
- **Out of scope, unchanged.** Server host wiring and Enabled activation (registration still refuses Enabled), packaging, release, deployment, Azure 8.6 and G5.
- **Superseded 2026-11-10 by amendment revision 3.** The SDK finding and the "Accept + track" notes above describe revision 2. Under revision 3 (`14eb081c…`), the backend calls the generated `Dapr.Protos` client, so `Denied` and `Unavailable` are distinct outcomes. See [spec-dapr-crypto-error-outcomes.md](spec-dapr-crypto-error-outcomes.md).

## Spec Change Log

## Review Triage Log

### Review pass 1 (2026-11-10)

Layers: blind-hunter (16), verification-gap (2 gaps + 1 other), edge-case-hunter (14). Route: **intent_gap** (E1/E2/E10) triggers a loopback to the owner; patch and defer entries below wait on that decision.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| E1 | Interrupted reservation wrap stays pending forever | high | `PayloadProtectionReconciler.ReconcileReservationAsync` returns for any backend reconcile outcome except None/Missing. Frozen matrix + amendment §7 make Dapr Reconcile always Ambiguous, so Started/Ambiguous intents with no key record are never failed or quarantined; work grows with every crash or timeout. | intent_gap |
| E2 | Interrupted rewrap leaves key RewrapPending forever | high | `PayloadKeyRotationCoordinator` rewrap reconciliation records `ambiguous-outcome` on Ambiguous and reaches `RestoreAbsentAsync` only on Missing, so the key never leaves RewrapPending and its KEK cannot retire. Same root cause as E1. | intent_gap |
| E10 | Executor budget expiry on Wrap becomes Ambiguous | medium | `PayloadBackendExecutor` maps a Wrap exception or budget expiry to Ambiguous (pre-existing generic rule). The backend's own budget yields Unavailable as §7 says, but the executor's override feeds E1's stuck intents. Grouped with E1. | intent_gap |
| B12 | 8.7 inherits unenforced obligations | medium | §4 two-phase rollout and retirement proof, Server wiring of the new options, and unobserved-exception scrubbing have no code owner. Amendment §6 and the frozen Never list assign rollout/activation to 8.7. | defer |
| G1 | Over-512-byte envelopes untested in blocking lanes | medium | Pre-verified: the unit test sidecar emits 75-byte envelopes, so dropping `keyRecordSchemaVersion: 2` (backend line 114) passes every push/PR lane. | patch |
| G3 | Live denied test depends on test order | medium | KEK inventory keys are global per backend/provider (`PayloadProtectionStateKeys.KekHead`), and other cases persist keys bound to `fixture.V1`, so `ShouldBe(0ul)` holds only by ordering. | patch |
| B1 | Breaker never opens on SDK-hidden outages | low | `Observe` counts only Unavailable/Ambiguous, and SDK-hidden outages are ConsistencyMismatch. The accepted-limitation deferred entry omits this. | patch (ledger) |
| B5 | Surviving-cluster guard fails open | low | `kind get clusters 2>/dev/null \| grep … \|\| true` passes when kind itself fails. Direct fix. | patch |
| B7+E5 | Stale disabled RetainedKeys children unchecked | low | `PayloadProtectionOptions.Load` checks only top-level keys of the stale section, breaking the "unknown installed-backend fields are invalid" rule. | patch |
| G2 | Stale-disabled detection hidden behind Backend in its test | low | Pre-verified: the test keeps `Backend` set, so deleting the new clause still passes. | patch |
| B15 | Backend validates options before copying RetainedKeys | low | `Validate()` runs on the caller's list and `ToArray()` follows, so the admitted inventory is not the validated one. Direct reorder. | patch |
| E6 | No-leak sentinel spreads the listener list outside its lock | low | `[.. observed]` is read without `gate` while listener callbacks append under it. Direct fix: snapshot under the lock. | patch |
| B8+E12–E14 | Recompiled epic context is stale and lost constraints | low | It still says "proposed" 2,048, "16 KiB" and "689 bytes", and it dropped the irreversible-denial and watermark rules and the 8.6 diagnostic pointer. | patch (doc) |
| B9 | Amendment planning evidence contradicts the live result | low | SDK envelopes carry no `"k"` and are a fixed 666 bytes. A dated note outside the markers leaves the digest unchanged. | patch (doc) |
| B16 | `docs/ci.md` lacks the new lane | low | The workflow table has no row for `payload-protection-kubernetes-crypto.yml`. | patch (doc) |
| B2 | Unobserved exceptions and sidecar-log sentinel gaps | low | The unobserved channel is already tracked by the owner-accepted deferred entry. A daprd warn log over 10,000 lines in a two-minute run is unlikely, and the backend and executor use no ILogger. | reject |
| B3 | Some live failure cases skip persisted-state checks | low | §8 is met lane-wide: persisted state is asserted in the wrap, restart, rotation, invalidation, denied, maximum-length and no-leak cases. Backend-level failures persist nothing, and in-flight cancellation is unit-tested. | reject |
| B4 | No guard test pins the new workflow | low | No requirement names it, and adding one is new test infrastructure. A kind version drift fails the fixture loudly. | reject |
| B6 | Fixture failure messages lack step identity | low | Developer-only diagnosability. The fix adds parameters to the fixture's runner. | reject |
| B10 | Sprint row is in-progress while the spec is in-review | false | Step 5 syncs the row to `review`; the gap is transient by design. | reject |
| B11 | Verification lacks CI link and Release build | low | The fix edits this build's spec, and CI runs need a push, which steps 3–4 forbid. The Release `-warnaserror` build is re-run locally in verification. | reject |
| B13 | Rewrap makes a redundant sidecar round trip | low | Rotation is rare, and skipping the backend's verification would add a branch or parameter. The owner kept wrap verification. | reject |
| B14+E4 | Address grammar admits `.` and `..` data keys | low | The code follows amendment §3 exactly. Such names are unlikely, the failure is fail-closed, and the fix needs a normative grammar change. | reject |
| E3 | Only reservation checks the backend/authority schema pairing | low | Both derive from one `Backend` setting, so the mismatch is unreachable through configuration. The fix adds guards in three constructors. | reject |
| E7 | Cleanup failure masks the original start failure | low | Needs a start failure plus a cleanup failure; the fix adds a branch. | reject |
| E8 | Envelope parse helper throws ArgumentOutOfRange | false | It fails loudly on an envelope shape the SDK never produced; a loud failure is correct. | reject |
| E9 | Released probe port can be taken before daprd binds | low | Rare; the fix adds retry logic. | reject |
| E11 | State store listed in Tasks but unchanged | false | Implementation Notes record that the existing decode already dispatches through the codec. | reject |

**Pass 1 resolution:** the owner chose "Return Missing" and "Keep code, patch", and approved amendment revision 2. E1 and E2 are fixed. E10 is resolved by the same change: Ambiguous wrap intents now reconcile to Failed/`not-persisted`. All pass-1 patch and defer entries are applied.

### Review pass 2 (2026-11-10, delta since pass 1)

Layers: blind-hunter (20), verification-gap (0 gaps + 1 other), edge-case-hunter (9). There are no intent_gap or bad_spec entries.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| R2-1 (BH3, ECH1, ECH2) | Executor treats a transport-free Reconcile `Missing` as a provider failure | medium | When Reconcile holds the half-open probe slot, `PayloadBackendExecutor.Observe` reopens the breaker and doubles its interval; every Dapr reconciliation also emits `ProviderFailure`. Reconciles follow failures, so they can repeatedly hold back write recovery. | patch |
| R2-2 (BH6) | Restored 8.6 pointer widens the approved exception | medium | The approved policy covers only the selected `ManagedIdentityClientId` in events 24/26, and forbids broad Azure-Identity exemptions. The restored text says "Azure-Identity events only" and drops the no-authority disclaimer. | patch (doc) |
| R2-3 (ECH3) | Dapr Reconcile affirms Missing for a malformed operation id | low | No `CanonicalUlid.IsValid` guard, unlike the Azure backend. One-line fix. | patch |
| R2-4 (ECH7) | A scalar backend-section value is accepted silently | low | `Load` allows the three section keys as scalars with no children, and stale detection checks only children. One-condition fix. | patch |
| R2-5 (ECH6) | Test sidecar does not authenticate its header | low | The fake AES-GCM uses no AAD, so tampering the 591 padding bytes still decrypts, unlike Dapr's MAC-authenticated header. Direct fix in the test double. | patch |
| R2-6 (BH19) | Disabled `RetainedKeys:Token` case does not assert the generic message | low | Sibling cases assert the canary-free message; this one checks only the exception type. | patch |
| R2-7 (BH1, VG-other, ECH8) | SPI remark contradicts itself | low | "A missing KEK, query failure or unknowable outcome must not return Missing" sits next to "Dapr always returns Missing". The rule needs stating for stateless backends. The removed Ambiguous sentence was pass-1 text, not a predecessor rule. | patch |
| R2-8 (BH2, VG-other, ECH8) | Azure backend has the same stuck-intent behavior | medium (unverified live) | `AzureKeyVaultPayloadProtectionBackend.ReconcileAsync` is always Ambiguous and `wrapKey` keeps no provider state, so E1/E2 apply to the retained, deferred Azure profile. Pre-existing and outside the selected path. | defer |
| R2-9 (BH3b) | Ledger says the breaker "never opens" | low | Wrap budget-expiry Ambiguous results still count toward opening. Direct wording fix. | patch (ledger) |
| R2-10 (BH5) | Epic context still says uncertain results stay Ambiguous | low | Revision 2 defines Missing for a transport-free Reconcile; the context needs that clarification so 8.7 is not misled. | patch (doc) |
| R2-11 (BH5, BH7) | `epics.md` 8.12 reconciliation paragraph is stale | low | It still says "689 bytes" and "proposed 2,048-byte bound". It is the planning source, so regenerating the context would restore the stale text. A planning artifact is updated through correct-course or closure. | defer |
| R2-12 (BH10) | Amendment approval wording | low | The column header says "(candidate)" under an "(approved)" heading, and "carries the uncommitted tree" is imprecise. | patch (doc) |
| R2-13 (BH16) | `docs/ci.md` Test Lanes table not updated | low | The lifecycle row does not say the explicit `Lane=KubernetesCrypto` cases are excluded there. | patch (doc) |
| R2-14 (BH15) | Unit-lane Release `-warnaserror` build not run | false | The parent session ran it: `PayloadProtection.Tests` Release `-warnaserror` `UseHexalithProjectReferences=false`, 0 warnings, outputs newer than all sources. | reject |
| R2-15 (BH4) | E10 disposition not recorded | low | Recorded in the pass 1 resolution above. The Ambiguous phase that a budget expiry produces is the one the new interrupted-reservation test drives. | reject |
| R2-16 (BH8) | Spec Design Notes keep the planning-time envelope estimate | low | The fix edits this build's spec. Implementation Notes and the amendment record the measured 666 bytes. | reject |
| R2-17 (BH9) | Spec Change Log empty after the frozen change | low | The change log is for bad_spec loopbacks; the intent decision is recorded in the frozen block and in the pass 1 resolution. | reject |
| R2-18 (BH11) | Guard test cannot detect an unapproved revision | low | The test binds constants to the documents; approval is a human step. Detecting it would need new approval metadata. | reject |
| R2-19 (BH12) | Interrupted-reservation test covers one pass and one phase | low | `Started` uses the same reconciler branch, and the later quarantine is generic logic covered by V049 and `DueStartedCreate…`. | reject |
| R2-20 (BH13) | Barrier rewrap restore untested for Dapr | low | Generic path covered by `ReviewBarrierRewrapTests` with Missing fakes. | reject |
| R2-21 (BH14) | Recovery proven only with the test double | low | Persisted reconciliation transitions are generic and covered by the lifecycle lane. A live recovery case would be new ceremony. | reject |
| R2-22 (BH17) | No hosted-evidence gate before `done` | low | Hosted runs need a push, which this build does not perform; surfaced at presentation. | reject |
| R2-23 (BH18) | Digest-move count and header tie | low | 21 files hold 31 references; headers are convention with no test (pass 1). Cosmetic. | reject |
| R2-24 (BH20) | B12 ledger heading says "build of" | low | Cosmetic; the review is part of the build. | reject |
| R2-25 (ECH4) | Dapr affirms absence for another backend's intent | false | `PayloadProtectionStateCodec` rejects a `FenceV1`/`DeploymentGenerationV1` whose digest differs from the process profile (lines 328-334), so a Dapr process cannot admit or reconcile another authority's root. | reject |
| R2-26 (ECH5) | Retrying a restored rewrap's operation id throws | low | Reusing an id after its claim completed fails closed with a typed outcome. The behavior predates 8.12 and is the same for every backend's Missing path. | reject |
| R2-27 (ECH9) | Normative range edited after approval | false | The owner approved revision 2's exact bytes. No revision-1 state exists outside disposable test lanes. | reject |

## Design Notes

Planning decisions (the amendment carries the exact bytes):
- **API and bounds.** Use `Dapr.Client` bytes overloads with RSA-OAEP-256 / AES-GCM. The key address is `namespace/secretName/dataKey`, with namespace ≤ 63, secret ≤ 128 and data key ≤ 63 characters, using DNS-1123 or Kubernetes data-key characters. A diagnostic run on 1.19.1 measured envelope size as 673 bytes plus the name length, so a 256-character address gives about 929 bytes, against the 2,048-byte bound. The same run showed the decrypt key name overrides the embedded name. The "16 KiB" in the planning text is the OpenBao wire bound; the 65,536-byte state value limit applies here.
- **Inventory.** It is options-based: component name, current write address and retained addresses (≤ 64). Changing it is a two-phase rollout (amendment §4). No inventory field is added to the lease schema.
- **Reconcile.** Dapr has no provider-side mutation, so Reconcile returns Missing without transport (owner decision, review pass 1). Existing 8.5 recovery then fails interrupted reservations and restores interrupted rewraps from durable state.
- **Authority.** The new profile derives as SHA-256(`HXPA` ‖ `03` ‖ `eb4565…` ‖ amendment digest). The 8.7 OpenBao authority is historical and is not chained.
- **Generation succession.** 8.12 defines the rules and enforces them in the codec and admission paths. 8.7 still executes the rollout (owner decision of 2026-10-31).
- **Inventory guard.** The Story 3.17 inventory maps these operations later; no guard exists yet.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx` — expected: 0 warnings, 0 errors.
- `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --fail-skips on` — expected: all pass, at least the existing minimum plus the new tests.
- `dotnet test --project tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.csproj --filter-trait Lane=KubernetesCrypto --explicit only --fail-skips on --minimum-expected-tests 12` — expected: every selected case passes, zero skips. The lane tests are xUnit `Explicit`, so they run only with `--explicit only`.
- Existing contracts, payload-protection and lifecycle lanes — expected: unchanged counts, green.

**Results (2026-11-10, local uncommitted tree on `aa16227bd`; no CI run exists yet):**
- Solution build: 0 warnings, 0 errors. Server compiles the new engine files dormant. The engine project also builds clean.
- `Hexalith.EventStore.PayloadProtection.Tests` with `--fail-skips on --minimum-expected-tests 324`: 1,137 passed, 0 failed, 0 skipped, including 76 new cases.
- Kubernetes lane with `--filter-trait Lane=KubernetesCrypto --explicit only --fail-skips on --minimum-expected-tests 12`: 12 of 12 passed, 0 skipped, in about 2 minutes. It ran on kind 0.31.0, `kindest/node:v1.35.0@sha256:452d707d…` and daprd 1.19.1 (`b1ece5da…` per the runtime lock). The maximum-length address envelope was 666 bytes. No cluster or container survived.
- `ReleasePackageManifestTests`: 162 passed, so the manifest stays at 14. Azure Key Vault unit: 192 passed. Azure authority schema: 15 passed. Server `PayloadProtectionRegistrationTests`: 2 passed.
- Lifecycle lane: its fixture requires daprd 1.18.7 and this host has 1.19.1, so the full lane must run in CI. Run without `--explicit`, the project reports the 12 new tests as not run, exits 0 under `--fail-skips on`, and creates no cluster.

- Lifecycle lane (unchanged workflow command, run by the parent session): the existing fixture used a checksum-verified daprd 1.18.7 (`e988a6f5…`, per `runtime-lock.json`) under an isolated scratch `HOME`, leaving the host's 1.19.1 install untouched. Command: `dotnet test --project …LiveSidecar.Tests.csproj --no-build --minimum-expected-tests 31 --fail-skips on` (Debug). Result: 63 passed, 0 failed, 12 reported not run (the explicit Kubernetes cases), exit 0.

**Re-verification after review pass 1 (2026-11-10, local uncommitted tree on `aa16227bd`, parent session):**
- `dotnet build Hexalith.EventStore.slnx`: 0 warnings, 0 errors. No revision-1 digest remains under `src/`, `tests/` or `.github/`.
- `Hexalith.EventStore.PayloadProtection.Tests` (`--fail-skips on --minimum-expected-tests 324`): 1,139 passed, 0 skipped.
- `ReleasePackageManifestTests`: 162 passed; the manifest stays at 14. Azure Key Vault unit: 192 passed. Server `PayloadProtectionRegistrationTests`: 2 passed.
- LiveSidecar project in Release with `-warnaserror` and `UseHexalithProjectReferences=false` (the CI build command): 0 warnings, 0 errors. Outputs are newer than every source file.
- Kubernetes lane (`--filter-trait Lane=KubernetesCrypto --explicit only --fail-skips on --minimum-expected-tests 12`): 12 of 12 passed, 0 skipped, about 2 minutes. No kind cluster or container survived.
- Lifecycle lane (unchanged command, checksum-verified daprd 1.18.7 under an isolated `HOME`): 63 passed, 0 failed, 12 reported not run, exit 0.
- No CI run exists yet. Hosted evidence needs a push, which this build does not perform.

**Final verification after review pass 2 (2026-11-10, parent session, pre-commit tree):**
- **Builds.** `dotnet build Hexalith.EventStore.slnx`: 0 warnings, 0 errors. Release `-warnaserror` `UseHexalithProjectReferences=false` builds of `PayloadProtection.Tests`, `PayloadProtection.LiveSidecar.Tests` and `PayloadProtection.AzureKeyVault.Tests` each had 0 warnings and 0 errors.
- **Unit suites.**
  - `PayloadProtection.Tests` (`--fail-skips on --minimum-expected-tests 324`): 1,142 passed, 0 skipped.
  - Azure Key Vault unit: 192 passed.
  - `ReleasePackageManifestTests`: 162 passed; the manifest stays at 14.
  - Server `PayloadProtectionRegistrationTests`: 2 passed.
- **Golden vectors.** `node scripts/payload-protection/verify-golden-vectors.mjs` and `python3 -I scripts/payload-protection/verify-golden-vectors.py` both PASS, with base normative digest `de9ba886…` unchanged.
- **Kubernetes lane.** 12 of 12 passed, 0 skipped, about 2 minutes. No kind cluster survived.
- **Lifecycle lane.** Checksum-verified daprd 1.18.7 under an isolated `HOME`: 63 passed, 0 failed, 12 reported not run, exit 0.
- **Hosted CI.** Pending. The push runs the push/PR lanes. The Kubernetes lane is manual-dispatch only, and its first hosted run is a separate step.

**Platform-owned limitations, explicitly blocked and not proven:** in-cluster sidecar injection and API-token authentication; component scopes; production workload identity and RBAC separation; encryption at rest; Secret and etcd backup and restore; the target-cluster rotation rehearsal; the old-key retention policy. The Alpha posture remains a recorded limitation for this profile only.

**Authorization:** passing authorizes only the bounded Story 8.7 handoff. Server wiring, Enabled activation, packaging, release, deployment, Azure 8.6 and G5 remain outside this story.

### Review Findings (code review pass 3, 2026-11-10)

Code review pass 3 (2026-11-10) reviewed the pushed implementation commit `adc2c2032` against `baseline_commit` `aa16227bd`. The diff covered 25 files, +2,583/−33, 3,032 diff lines and 216,469 bytes. The five `_bmad-output/` tracking files were excluded; the spec and amendment went to the Acceptance Auditor as context. HEAD `a940fb616` differs from `adc2c2032` only in the AI.Tools pointer. Only the edge-case layer received the claims, which were the commit message. Layers: Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG), Acceptance Auditor (AA). None failed. Of 34 findings, 8 form 5 patch entries and 26 were rejected; there were no decisions and no deferrals. Re-raised items that passes 1 and 2 had already settled are rejected as duplicates.

- [x] [Review][Patch] Limit the executor's Missing-Reconcile exception to backends whose Reconcile uses no transport. Review pass 2 (R2-1) treats any `Reconcile` that returns `Missing` as transport-free: it releases a held probe without touching the breaker and records no `provider_failures` measurement. `DaprPayloadProtectionBackend.ReconcileAsync`, the unchanged 8.7 first-phase PlatformCustody backend, returns `Missing` only after a real Lookup and `ConfirmsOriginalMutationAbsentAsync`. For that approved profile, a half-open probe that gets a provider answer is now released instead of being observed, and the Missing measurement disappears. No PlatformCustody test covers either change. Fix: add an internal SPI flag, overridden to `true` only by `DaprCryptographyPayloadProtectionBackend`, and require it in `affirmedAbsence`. That restores the exact pre-8.12 executor behavior for every other backend. Add a regression test showing that a transport-backed Missing on a probe is still observed [PayloadBackendExecutor.cs:65-69; PayloadProtectionBackend.cs:13-15] (medium; BH1, AA1, ECH1)
- [x] [Review][Patch] Test the wrap round-trip check against a wrong 32-byte decrypt-back. `EncryptCoreAsync` accepts a wrap only if `verified` is exactly the original DEK. The only wrap-path decrypt fault returns `[]`, which already fails the length check. If the check were reduced to `verified.Length != 32`, every unit and live test would still pass, and an envelope that does not open to its DEK could be persisted as the reservation's key. Reservation does not unwrap again; rewrap does (`PayloadKeyRotationCoordinator.cs:123-131`). Fix: add a theory row in which the verification decrypt returns 32 non-zero bytes, for example `4 when !encrypt => Enumerable.Repeat((byte)1, 32).ToArray()`. Assert `ConsistencyMismatch`, that no bytes are returned and that the buffers are cleared [DaprCryptographyPayloadProtectionBackend.cs:117; DaprCryptographyBackendTests.cs:94] (medium; VG1)
- [x] [Review][Patch] Report a transport-free Missing Reconcile as a completed operation in diagnostics. `diagnostic.Complete` still maps every result other than `None` and `Denied` to `Unavailable`. Under this backend every Reconcile returns `Missing`, so each recovery reconcile emits an error-status activity and a `result="unavailable"` operation measurement. `MissingReconcileRecordsNoProviderFailureMeasurement` listens only to `provider_failures`. Fix: when `affirmedAbsence` is true (behind the patch-1 flag), complete with `Success`. Pin this in the existing executor test [PayloadBackendExecutor.cs:72-73] (low; VG3, ECH2)
- [x] [Review][Patch] Test the backend constructor's copy-then-validate inventory (pass-1 fix B15). No test constructs the backend with an invalid `RetainedKeys` (duplicate, malformed or null), and none changes the caller's array after construction. Removing `_options.Validate()` or the `with` copy would leave every test green. Fix: add one `DaprCryptographyBackendTests` case. Construction with an invalid inventory must throw `PayloadProtectionFormatException`. After construction, setting `retained[0] = Foreign` must still make `UnwrapAsync(…, Foreign, …)` return `ConsistencyMismatch` with no sidecar call [DaprCryptographyPayloadProtectionBackend.cs:34-35] (low; VG2)
- [x] [Review][Patch] Correct the `docs/ci.md` Kubernetes crypto row. It says "a restricted ServiceAccount", but the lane creates two: `pp-crypto`, restricted to named Secrets, and `pp-denied`, with no grant. The row also omits the accepted limitation: with Dapr.Client 1.19.0, denied, missing-Secret and stopped-sidecar failures all report `ConsistencyMismatch`. A reader could take a green run as proof that `Denied` and `Unavailable` are mapped. Fix: name both accounts and add one sentence about the limitation that points to the deferred-work entry [docs/ci.md:15] (low; BH15)

Rejected (pass 3):

- BH2 / ECH7 (`low`): `Encode` picks the Dapr profile from the runtime type when no profile is passed. The store re-decodes every key `Put` under the process authority (`DaprPayloadProtectionStateStore.cs:175`), so this pre-check is never the last guard. In restore admission, a foreign-schema backup is still quarantined on the backend-id comparison. Passing the authority explicitly would touch every key `Put` caller.
- BH3 (`low`): KEK strength and type are not enforced in code. Amendment §3 makes RSA-3072 a provisioning rule, and provisioning is Platform-owned. Enforcing it would mean parsing the Dapr envelope header, and an operator is unlikely to provision a weaker key.
- BH4 (`false`): the maximum-length-address test cannot fail on the bound because envelope size does not depend on the address. That is true, but the test measures AC2 as written, amendment §3 fixes the KEK at RSA-3072, and a larger envelope fails closed at `IsEnvelope` and at the 2,048-byte `PayloadBackendResult` bound.
- BH5 (`low`): the denied case has no RBAC positive control. The SDK collapses every failure into the same outcome, so the case can prove only fail-closed, the owner-accepted claim, and it does. The denied kubeconfig is built by the same fixture loop as the working restricted one. A `kubectl auth can-i` control would add fixture steps.
- BH6 (`low`): the live lane never runs recovery. Duplicate of R2-21.
- BH7 (`false`): the no-leak test skips the failed reservation's durable records. The codec admits only typed fields and the closed `Reasons` set (`PayloadProtectionStateCodec.cs:119-122`), so a durable record cannot hold exception text.
- BH8 / VG4 (`low`): the new workflow has never run on GitHub. Duplicate of R2-22. Now that `adc2c2032` is pushed, the first dispatch is the recorded next step; no code change is needed.
- BH9 (`low`): the fixture's failure messages and artifacts are hard to diagnose. Duplicate of B6.
- BH10 / ECH8 (`low`): a cleanup failure masks the start failure. Duplicate of E7.
- BH11 / ECH5 (`low`): the grammar admits `.` and `..` data keys and hyphen-edged component names. Duplicate of B14+E4. `ComponentName` follows amendment §4 exactly, and the fix needs a normative change.
- BH12 (`low`): `SchemaVersion` is a settable `init` property. No caller sets it, it was already settable on V1, and a mismatched record fails decode loudly.
- BH13 (`low`): the unknown-field predicate is duplicated in two loaders. Drift would fail loudly at `Load`, and the fix is a refactor.
- BH14 (`false`): the scalar refusal breaks existing profiles. Approved configurations use sections at these keys. Only a meaningless stray scalar is refused, which is the base closed-configuration rule.
- ECH3 (`low`): an open breaker also blocks the transport-free Reconcile. Recovery then waits at most 300 s and only while the breaker is open, which is rare because only budget expiry counts. The fix adds a branch to the shared executor.
- ECH4 (`low`, already tracked): SDK-hidden outages surface as `ConsistencyMismatch`. The owner chose "Accept + track", and the deferred-work entry exists.
- ECH6 (`false`): a null `backend` or `leases` would cause a NullReferenceException. No caller can pass null, and either exception fails loudly at construction.
- ECH9 (`low`): a failed restart can leave a dead sidecar in the shared fixture. That cascade follows only a real failure that already fails the lane, and the fix adds fixture `try`/`finally` paths.
- ECH10 (`low`): the unwrap leg can pass on key mismatch alone. The wrap and exact-wrap legs under the same addresses would succeed on any over-grant, so the denial is already proven. Aliasing the key bytes would need fixture changes.
- AA2 (rejected because the fix would edit the spec under review): test files suppress `DAPR_CRYPTOGRAPHY`. A test double or test against the experimental API cannot compile without that under `TreatWarningsAsErrors`. The only production suppression stays in the backend file, and the Implementation Notes disclose this. The fix would be to renegotiate the normative §2 sentence.
- AA3 (`false`): a non-canonical operation id throws, and the throw counts against the breaker. Reconcile operation ids come only from codec-validated ULID claims, and the Azure backend has the identical guard.
- AA4 (`false`): the fence-advancement check for the reader token cannot be reached. It is redundant defense in depth: the codec's capability-lease check is the effective gate and is tested (`KeyRecordAuthorityTests.cs:189`, `WrappedDekV2CodecTests`).
- AA5 (rejected because the fix would edit the spec under review): Verification still says "pre-commit tree" and "Hosted CI: Pending" and does not record `adc2c2032`. This is closure bookkeeping for the owner.

Pass-3 disposition (2026-11-10): the owner chose to apply every patch. All five are applied on HEAD `a940fb616` and are uncommitted. `PayloadProtectionBackend` gains `internal virtual bool ReconcilesWithoutTransport => false`, and only `DaprCryptographyPayloadProtectionBackend` overrides it to `true`. The executor's `affirmedAbsence` now requires that flag, so PlatformCustody, Azure and the test backends get their exact pre-8.12 handling of a Missing Reconcile, and an affirmed transport-free absence completes its diagnostic scope as `Success`. The test changes are:
- a new `TransportBackedMissingReconcileIsStillObservedByTheBreakerAndMeasured`;
- `MissingReconcileRecordsNoProviderFailureMeasurement`, renamed `…AndCompletesSuccessfully`, which now asserts the reconcile operation result is `success`;
- a fifth wrap-fault row that returns a wrong 32-byte decrypt-back;
- a new `ConstructorValidatesAndOwnsAPrivateCopyOfTheInventory`.

The `docs/ci.md` row now names both ServiceAccounts and states the accepted `ConsistencyMismatch` limitation.

Mutation check, in a scratch copy rather than the tree. Each new test failed against its mutation, and the restored control passed the four new tests 8/8 (rows included):
- M1 removed the flag from `affirmedAbsence`;
- M2 removed `|| affirmedAbsence` from the diagnostic mapping;
- M3 reduced the wrap check to `verified.Length != 32`;
- M4 deleted `_options.Validate()`;
- M5 replaced the `with` copy with `_options = options`.

Verification (Debug, source mode):
- `PayloadProtection.Tests` with `--fail-skips on --minimum-expected-tests 324`: 1,145/1,145, 0 skipped.
- Azure Key Vault unit: 192/192.
- `PayloadProtection.LiveSidecar.Tests`, `PayloadProtection.AzureKeyVault.Tests` and `Hexalith.EventStore.Server` build with 0 warnings and 0 errors.
- `PayloadProtection.Tests` Release `-warnaserror` `UseHexalithProjectReferences=false`: 0 warnings, 0 errors.
- `git diff --check` is clean.

The Kubernetes and lifecycle live lanes were not re-run. The change touches only executor bookkeeping for Missing Reconciles, so the Dapr lane keeps its pass-2 behavior and the lifecycle backend returns to its pre-8.12 behavior. The pushed lifecycle CI lane will cover the latter. With no high or medium finding left unresolved, the spec stays `done` and sprint tracking moves to `done`.


### SDK 1.19.1 successor handoff — 2026-11-12

The owner answered **"approve 1.19.1"** while resuming Story 8.7. The [supplement](spec-8-12-dapr-kubernetes-cryptography-amendment.md) is revision 4: its current generated-client/SDK input is now Dapr.Protos/Dapr.Client 1.19.1, normative `3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd`, effective authority `5d11b464555ac1f40fde3eadfd03c2a3f984f0770d594b30062a4cc641e34204`. The frozen intent, previous approval records, runtime/component lock, schemas, request semantics and exclusions are preserved. No existing authority root is restamped; the new regression refuses revision 3 fence/deployment/capability bytes.

Fresh source-mode Debug results: 1,245/1,245 core tests and 14/14 isolated-kind crypto cases, zero failures/skips. The lane resolved SDK 1.19.1, used lock-verified daprd 1.19.1, and removed its cluster and PostgreSQL container. API comparison found no public changes for either SDK package; both golden verifiers passed V001–V003 unchanged. Exact commands and scope limits are in the [8.7 SDK approval entry](spec-8-7-server-persistence-and-snapshot-integration.md#sdk-1191-approval-and-successor-verification--2026-11-12). The earlier 1.19.0 verification above remains historical. Story 8.12 remains done only for its bounded EventStore handoff; Platform target, shared-state/fleet/restore/history, Server activation, release and G5 remain gated.
