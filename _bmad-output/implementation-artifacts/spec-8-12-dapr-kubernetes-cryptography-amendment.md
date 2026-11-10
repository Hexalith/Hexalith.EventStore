---
title: 'Story 8.12 Dapr Kubernetes cryptography successor supplement'
type: 'architecture-amendment-candidate'
created: '2026-11-10'
status: 'approved-authorized'
implementation_authorized: true
approval_date: '2026-11-10'
approval_source: 'Interactive project owner: Approve revision 2'
revision: 2
revision_reason: 'Review pass 1 intent gap: Reconcile returns Missing (owner decision 2026-11-10)'
previous_approval_source: 'Interactive project owner: Approve and continue (revision 1)'
previous_normative_sha256: '2289212dd2076ebd4a7a6b155f59d2bc05d3da14bff49430c1877c83ea1f0fb0'
previous_effective_authority_sha256: 'b11cf7679867f7ab383cb007420ee3e55c22401c1b57452dc81ed5ea8899103f'
approval_assurance: 'single-maintainer-attested'
alpha_posture_decision: 'accepted-for-this-profile-only'
alpha_posture_decision_date: '2026-11-10'
predecessor_effective_authority_sha256: 'eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9'
candidate_normative_sha256: 'e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034'
proposed_effective_authority_sha256: '2c7c509921cf611cae5ec394c9ba093951d9e3693e8740f9a5894b6df915c20d'
paired_spec_frozen_intent_sha256: 'b36bbe08191d0db7bf319f920f8025a3c820e2f27bb0fc5dd3842207321bfee1'
---

# Dapr Kubernetes cryptography successor supplement

This supplement is the "separately identified normative amendment" required by Story 8.12 and by proposal P1–P7 of the [2026-11-08 course correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md). The owning build spec is [spec-8-12-dapr-kubernetes-cryptography.md](spec-8-12-dapr-kubernetes-cryptography.md). On 2026-11-10 the owner accepted the Alpha support posture for this profile only. Approval binds the exact bytes between the markers below. It does not change any predecessor approval.

<!-- HX-PP-DAPR-K8S-BEGIN -->

## 1. Scope and precedence

This supplement adds one selectable backend profile, named `DaprKubernetesCrypto`, for the on-premises path. The Story 8.1 base specification, the 8.5 lifecycle authority, pdenc-v2 event and snapshot bytes, AAD, golden vectors, `WrappedDekV1` and the 8.4 compatibility readers keep their exact meaning. The Azure profile and its exception are retained and stay deferred. The OpenBao supplement `43fc3400…` and authority `835f96ca…` are historical first-phase authority, and this profile does not chain from them.

| Existing subject | This supplement |
|---|---|
| Base 5.2/5.3 closed configuration | Adds only `Backend=DaprKubernetesCrypto` and the closed options in section 4. |
| Base 10/13 key records and lifecycle | Adds `WrappedDekV2` (section 5) for this profile only. Every reservation, completion, rotation, invalidation, restore and quarantine rule is retained. |
| Base 11 production backend | Uses the stock Dapr component in section 2 in place of the Azure or OpenBao provider for this profile. |
| Authority and readers | Adds the profile digest and the reader capability in section 6. No existing root is restamped. |

## 2. Component, runtime and API

- **Component.** The only selected component is `crypto.dapr.kubernetes.secrets`, version `v1`, from the component-inclusive Dapr runtime **1.19.1** (components-contrib **1.19.0**). Its archives and SHA-256 values are those in `deploy/dapr/qualification/runtime-lock-1.19.1.json`. The component's `defaultNamespace` metadata must be unset, so every key name carries its namespace.
- **Alpha posture.** The cryptography API and this component are Alpha. The owner accepted that posture on 2026-11-10 for this profile only. It is a recorded limitation, not a readiness claim.
- **Client calls.** EventStore calls only the `Dapr.Client` **1.19.0** byte-array overloads over gRPC:
  - `EncryptAsync(component, dek, keyName, EncryptionOptions, ct)`, with key wrap `RSA-OAEP-256` and data cipher AES-GCM;
  - `DecryptAsync(component, envelope, keyName, ct)` (or its options overload), with the recorded key address as `keyName`.
  - Their `DAPR_CRYPTOGRAPHY` experimental diagnostic is suppressed only in the backend source file.
- **Excluded.** Subtle get-key/wrap/unwrap, the `Dapr.Cryptography` package, custom runtimes, provider HTTP, any application Kubernetes client and exported KEKs are excluded.
- **No backend retries.** The backend performs one attempt per call. The existing executor's retry, breaker and budget rules remain the only retry authority.

## 3. Key profile and addresses

- **KEK.** Each KEK is a dedicated RSA-3072 private key in PEM (PKCS#8 or PKCS#1), stored under one data key of one Kubernetes Secret. Every KEK version uses a distinct Secret. A version's key bytes are never replaced, and no mutable `latest` alias exists.
- **Address grammar.** A key address is exactly `namespace/secretName/dataKey`:
  - `namespace` is a DNS-1123 label of 1–63 characters;
  - `secretName` is a DNS-1123 subdomain of 1–128 characters;
  - `dataKey` is 1–63 characters from `[-._a-zA-Z0-9]`;
  - the whole address is 5–256 ASCII characters.
- **Selection.** The address is the trusted key identity. It is persisted as `providerKeyVersionId` and is the only `keyName` passed to decrypt. A key name embedded in an envelope never selects a key.
- **Custody limits.** Kubernetes Secrets, etcd, Kubernetes backups and Dapr sidecar memory hold copies of software KEKs. These limitations are accepted for this profile only. Erasure of all copies is never claimed.

## 4. Options and inventory

Configuration lives under `EventStore:PayloadProtection:DaprKubernetesCrypto`. It has exactly these fields, and unknown fields fail validation:

- `ComponentName`: 1–128 ASCII lowercase letters, digits and hyphens.
- `CurrentWriteKey`: one key address.
- `RetainedKeys`: 0–64 distinct key addresses, none equal to `CurrentWriteKey`.

No field carries credentials, an endpoint or key material.

**Key use.**
- New wraps use only `CurrentWriteKey`.
- WrapExact and Unwrap require the recorded address to be `CurrentWriteKey` or a member of `RetainedKeys`. Any other address is refused before transport with `ConsistencyMismatch`.

**Rollout.** The inventory changes only through a controlled rollout:
1. Add the new address to `RetainedKeys` on every serving member.
2. Only then make it `CurrentWriteKey` and move the old address to `RetainedKeys`.

**Retirement.** An address may be removed only after the existing KEK inventory proves that no record is still bound to it.

**Disabled.** Omitted or Disabled registration never resolves this backend.

## 5. `WrappedDekV2`

`WrappedDekV2` keeps the 19 canonical fields of `WrappedDekV1` with schemaVersion `2`. It is stored at the same state key, and these values differ from V1:

- `backendId` = `dapr-crypto-kubernetes-secrets-v1`;
- `wrapAlgorithmId` = `dapr-enc-v1-rsa-oaep-256-aes-gcm`;
- `providerKeyVersionId` = the key address from section 3;
- `wrappedDek` = canonical unpadded base64url of the complete Dapr encryption-scheme v1 envelope. The envelope must start with the ASCII bytes `dapr.io/enc/v1\n`. Its decoded length is 1–2,048 bytes, and it is never truncated, stripped or reframed.

The canonical field hash uses magic `HXKF 02 11 00 00` with the V1 field identifiers. The 65,536-byte state value bound still applies.

**Decoding.**
- The decoder dispatches on schemaVersion. Version 1 decodes exactly as before.
- Version 2 decodes only under the `DaprKubernetesCrypto` profile. Under any other profile it is a format error, reported as `ConsistencyMismatch`.
- A version-1 record or root met under the new profile is refused with a typed outcome. It is never converted or restamped.
- Backend wrapped output is bounded at 2,048 bytes for this backend and stays at 512 for every other backend.

## 6. Authority, reader capability and downgrade

Let `E` = `eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9` and `P` = this supplement's normative digest. The profile authority is SHA-256 of ASCII `HXPA`, then byte `03`, then the 32 decoded bytes of `E`, then the 32 decoded bytes of `P`. It becomes a third closed `PayloadProtectionAuthorityProfile` value selected only by `Backend=DaprKubernetesCrypto`. Configuration never supplies a digest.

**Reader capability.** The closed read-format vocabulary gains the token `wrapped-dek-v2`.
- Under this authority, every capability lease must list both `json+pdenc-v2` and `wrapped-dek-v2`.
- Fence advancement requires every serving member to list them.
- Binaries without this supplement reject the token and V2 records as unknown, so they cannot join such a generation.

**Downgrade and rollback.**
- Roots bound to another authority are refused, never rewritten.
- Rolling back to a binary without this profile leaves V2 roots unreadable with typed outcomes. Story 8.10's rollback target must read every written key schema.
- Migrating V1 records or roots into this profile needs separate approval.

**Ownership.** Story 8.12 owns these rules and their codec and admission enforcement. Story 8.7 executes generation rollout and Server activation.

## 7. Failure mapping, buffers and diagnostics

Dapr Encrypt and Decrypt have no provider-side mutation, so no native mutation exists to reconcile. Outcomes are classified only by structural status, never by message text:

| Condition | Outcome |
|---|---|
| Caller cancellation | `OperationCanceledException`, raised after I/O completes and owned buffers are cleared |
| Expiry of the enclosing budget | `Unavailable` |
| gRPC `PermissionDenied` or `Unauthenticated` (directly or inside the SDK exception) | `Denied` |
| gRPC `Unavailable`, `DeadlineExceeded`, `ResourceExhausted`, `Aborted`, or non-caller `Cancelled` | `Unavailable` |
| Any other status or exception (including `NotFound`, `Internal`, `Unknown`, `InvalidArgument`) | `ConsistencyMismatch` |
| Wrap output that is oversized, empty or lacks the scheme prefix | `ConsistencyMismatch` |
| Unwrap output that is not exactly 32 bytes | `ConsistencyMismatch` |

`ReconcileAsync` always returns `Missing`, without transport. Encrypt and Decrypt create no provider-side state, so the exact stable mutation never exists natively, and existing durable state alone decides recovery. Wrap, unwrap and transport outcomes never produce `Missing`, `Invalidated` or deletion. A missing Secret is an operator condition, not proof of absence.

**Buffers.** Owned DEK copies, returned plaintext and envelope arrays (including SDK-returned arrays reachable through `MemoryMarshal.TryGetArray`) are cleared before ownership is released. Copies held by the sidecar, component, Kubernetes or etcd are limitations.

**Diagnostics.** Key addresses, envelopes, key bytes and error text never reach logs, traces, metrics, exceptions or public results.

## 8. Qualification

EventStore owns one isolated lane:
- **Cluster:** a disposable kind cluster (kind 0.31.0, pinned node image) that the lane creates and deletes. The process's ambient kube context and every shared cluster are refused.
- **Runtime:** self-hosted daprd 1.19.1, verified against the runtime lock, with the component's `kubeconfigPath` set to the lane's own kubeconfig. Restricted cases use a ServiceAccount kubeconfig whose Role grants only `get` on the named Secrets.
- **Keys:** RSA-3072 keys generated per run and never committed.
- **State:** PostgreSQL v2 state, as in the existing lifecycle lane.

The lane must pass every selected case with zero skips and assert persisted `WrappedDekV2`, KEK-inventory, lifecycle, fence and index state through the Dapr state boundary. The cases are:
- wrap and unwrap;
- restart with a historical read;
- rotation with retained reads and V2→V2 rewrap equality;
- invalidation, deletion and restore end state;
- a denied ServiceAccount;
- a missing Secret;
- a stopped sidecar;
- cancellation;
- a tampered envelope;
- an envelope-name mismatch;
- a maximum-length address envelope of at most 2,048 bytes;
- no-leak sentinels.

**Platform-blocked limitations.** These are recorded as explicitly blocked, not proven:
- in-cluster sidecar injection and API-token authentication;
- component scopes;
- production workload identity and RBAC separation;
- encryption at rest;
- Secret and etcd backup and restore;
- target-cluster rotation rehearsal;
- the old-key retention policy.

If the lane cannot pass, the profile stays blocked and returns to review. It never falls back to another runtime, provider, format or weaker lifecycle rule. Passing authorizes only the bounded Story 8.7 handoff. Server wiring and Enabled activation, packaging (the manifest stays at 14), release, deployment, Azure 8.6 completion and G5 all remain outside this supplement.

<!-- HX-PP-DAPR-K8S-END -->

## Planning evidence — 2026-11-10 (diagnostic only)

A local self-hosted daprd 1.19.1 used `crypto.dapr.localstorage` with an RSA-3072 key, through the HTTP API. This was not qualification evidence.
- **Envelope size.** A 32-byte DEK produced 675 bytes with a 2-character key name and 928 bytes with a 255-character name, so size = 673 + name length. A 256-character address therefore gives about 929 bytes. Even Kubernetes' 571-character maximum gives about 1,244 bytes, inside the 2,048-byte bound.
- **Name override.** Decrypt with a different caller-supplied key name holding the same key bytes succeeded. A different key failed with a generic 500 status.
- **Envelope format.** The envelope starts with `dapr.io/enc/v1\n`, and its JSON header embeds the key name (`"k"`).

Scratch keys were deleted. Official docs (v1.19) list the component as Alpha, with metadata limited to `defaultNamespace` and `kubeconfigPath`, and key names of the form `[namespace/]secretName/key`.

## Checkpoint 1 approval — 2026-11-10

The finalized spec and this complete supplement were presented. The interactive project owner answered **"Approve and continue."** This approval binds:
- normative SHA-256 `2289212dd2076ebd4a7a6b155f59d2bc05d3da14bff49430c1877c83ea1f0fb0`;
- effective authority SHA-256 `b11cf7679867f7ab383cb007420ee3e55c22401c1b57452dc81ed5ea8899103f`;
- the paired spec's frozen-intent SHA-256 `1c9ef89437b526e8defb3848f03650339586be2b7f130988781286a93977ea4d`.

Approval was recorded at EventStore revision `aa16227bd07f0564a3026d619a5abd973e02656f`. Implementation is authorized only within this supplement. Platform work, Server wiring and Enabled activation (8.7), packaging, release and G5 remain outside it. This is one owner's decision (`single-maintainer-attested`), not independent review or production acceptance.

## Post-qualification note — 2026-11-10 (non-normative)

The real kind lane (daprd 1.19.1, `crypto.dapr.kubernetes.secrets`) refined the planning measurements above. Envelopes written through `Dapr.Client` 1.19.0 embed no key name unless `DecryptionKeyName` is set, so they carry no `"k"` field. They are a fixed 666 bytes for a 32-byte DEK, including at the 256-character maximum address. The 2,048-byte bound therefore has a wide margin. The SDK also hides gRPC failure statuses; the owner's "Accept + track" decision on that is recorded in the build spec and `deferred-work.md`.

## Revision 2 — 2026-11-10 (approved)

Review pass 1 found that an always-`Ambiguous` Reconcile leaves interrupted reservations pending forever and interrupted rewraps `RewrapPending` forever, because the existing 8.5 recovery resolves only on `Missing`. The owner chose **"Return Missing"** and to keep and patch the verified code. Only the §7 Reconcile sentence changed inside the markers.

| Digest | Revision 1 (superseded) | Revision 2 (approved) |
|---|---|---|
| Normative | `2289212dd2076ebd4a7a6b155f59d2bc05d3da14bff49430c1877c83ea1f0fb0` | `e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034` |
| Effective authority | `b11cf7679867f7ab383cb007420ee3e55c22401c1b57452dc81ed5ea8899103f` | `2c7c509921cf611cae5ec394c9ba093951d9e3693e8740f9a5894b6df915c20d` |
| Paired frozen intent | `1c9ef89437b526e8defb3848f03650339586be2b7f130988781286a93977ea4d` | `b36bbe08191d0db7bf319f920f8025a3c820e2f27bb0fc5dd3842207321bfee1` |

The revision 1 approval does not cover the revision 2 bytes. Affected code waits for explicit approval of revision 2.

The interactive project owner then answered **"Approve revision 2."** The approval binds the revision 2 normative, effective-authority and frozen-intent digests above, recorded while EventStore HEAD was `aa16227bd07f0564a3026d619a5abd973e02656f`. The approved bytes were then uncommitted; the commit that first contains them is recorded in the build spec when it exists. It is `single-maintainer-attested`. Code constants and header comments move to the revision 2 digests.
