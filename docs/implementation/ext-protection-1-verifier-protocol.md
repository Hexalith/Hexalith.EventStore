# EXT-PROTECTION-1 owner verifier protocol

This is the protocol implemented by `tools/verify-ext-protection-1.py`. It is a qualification harness, not a custody provider or an independently installed observer. The owner acceptance packet records the missing production implementations. Source mode and offline unit tests do not execute Live.

From the Agents repository root, the proposed entry point is:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

Do not execute this command before owner-record acceptance. It reads the Agents register, requires EXT-PROTECTION-1 `Committed` or `Available`, validates its accepted ISO integration date without requiring a particular literal date, requires HEAD equal to the accepted full target, a clean EventStore tree, and ancestry from `origin/main`. It does not fetch or alter Git state. The accepted compatibility field must bind the installed profile with `ProtectionProfileSha256=` followed by its exact SHA-256. This belongs in the existing compatibility field. It is not a new register schema row. The command has a success branch after all installed inputs and every case pass; current input absence is not converted to availability.

Each invocation creates a fresh retained UTC/UUID attempt directory, including refusals. No file is overwritten or cleaned. The `binding.json` identity contains exact target/profile/plan/anchor hashes. The independent enrollment role must durably enforce one attempt per accepted target set across machines and evidence roots. A local directory alone does not establish that policy.

## Installed profile

The profile is a JSON object with exactly these members:

| Member | Value |
| --- | --- |
| `schema` | `ext-protection-1-profile/v1` |
| `targetCommit` | Accepted full EventStore commit |
| `planSha256` | SHA-256 of exact committed `tools/ext-protection-1/plan.json` bytes |
| `tenantReferences` | Object containing distinct nonempty `primary` and `other` tenant references |
| `roles` | Object containing exactly `executor`, `observer`, `enrollment` |
| `engineBinary` | Object containing absolute installed `path` and exact `sha256` |

Each role contains exactly `executable`, `executableSha256`, `publicAnchorFile`, `publicAnchorSha256`, `credentialReference`, `identity`, `installationManifest`, `installationManifestSha256`. Paths are absolute reference-only inputs, credentials are `env:NAME` references, and anchors are public P-256 PEM keys. No private signing material, KEK, DEK, bearer value, plaintext domain content, or connection string belongs in this file. Each installation manifest is a hash-bound object with `files`, an array of exact `{path, sha256}` objects including the executable and its complete installed dependency inventory. The entire inventory, manifest, executable, anchor and engine binary are checked before each role call. Distinct role identities, credential references and actual public-key fingerprints are mandatory. A role process receives only its own referenced credential plus a fixed PATH and locale; it does not inherit the other roles' credentials.

The enrollment authority must independently bind store/deployment identity, role principals/ACLs, installation inventories, engine binary/source identity, contract/plan hashes, and the isolated fixture namespace. It cannot be the executor or observer. The observer must read actual persisted source/event/snapshot/cache/backup/key/provider and protection/guard records under its separate read-only credential. It must not accept executor-supplied state, response codes, booleans or mock call counts as a persisted observation. No Agents package or consuming runtime path may be involved. These independently operated installations do not currently exist in this workspace.

## Role transport

The verifier launches the pinned executable directly, without a shell or secret argv. It writes one canonical UTF-8 JSON request to stdin. Every request carries `operationId`, `action`, `bindingDigest` and action-specific exact identities. A role writes one object containing exactly `statement` and `signature`. `signature` is base64 IEEE-P1363 ES256 over the canonical JSON statement (sorted member names, compact separators, UTF-8, finite JSON). `statement` contains exactly `role`, `identity`, `bindingDigest`, `operationId`, `requestSha256`, `response`. Fresh request ids prevent replay of stale observations; signatures bind the exact complete request digest.

`InspectInstallation` precedes enrollment or fixture mutations. It returns installed engine/source/binary/store/deployment identities, disabled Agents consumers, supported operations, and observer read-only ACL scope. All three roles must name the same store and deployment, which are bound into enrollment. `EnrollSingleAttempt` returns the exact attempt and binding identities with status `Enrolled`; repeat/lost enrollment is resolved by exact original lookup.

The 41-case plan owns the operations and assertions. `PrepareFixture` creates an isolated deterministic namespace and complete target cohort; all `Seal*` operations apply the synthetic canary to the requested targets. They must use the shipped hooks and original actor commit paths. Other operations invoke actual named custody/guard/protection APIs, inject the specified boundary condition, or control the explicit race schedule. Race drivers may coordinate operation arrival but cannot decide the verifier result. No production domain content is used.

`ObservePersistedState` is separately signed and includes the exact requested operation identity/digest/action vector plus persisted `roots`, sealed `records`, loaded engine identity, disabled-consumer state, and the case-specific receipts/guard transitions/copy inventories checked in `evaluate`. Field names and required checks are executable in `tools/ext_protection_1.py`; incomplete observations fail. `Unseal*` returns `canaryBase64` for in-memory equality comparison only. Its plaintext response is not retained.

## Recovery

Every destructive invocation has a complete original `.request.json` durably fsynced before dispatch, followed by a separate immutable result file. An uncertain result, timeout or invalid proof triggers only `LookupOriginalOperation` with the original operation id, original request digest and original request. A changed identity, pending, unknown or unavailable outcome stops the run. No replacement batch or authorization is minted. Reserved recovery remains the protection owner's same original operation. Seal identities retain the original request digest without retaining canary plaintext.

Before executor mutation, `RegisterOriginalOperation` independently retains the original operation id and complete request digest under the enrolled attempt. Its acknowledgement loss also uses exact lookup. `LookupAttemptOperations` must later supply the signed complete inventory; local recovery refuses a missing, empty, changed or incomplete original journal.

For recovery-only execution, add `--recover-journal` naming the retained original `original-operations` directory. A fresh recovery directory links the original binding and records exact lookups; it never reruns the case plan or enrolls a successor attempt. Original journals remain intact. The standalone recovery of a seal identity does not authorize re-encryption. Successful recovery reports `RECOVERED` with `qualificationPassed:false`; it cannot qualify a plan that did not finish.

## Limits that prevent acceptance today

The transport programs, independent observation/enrollment deployments, accepted profile and public anchors are not installed. This repository has no concrete production custody/atomic provider or signed online deletion authority to connect them to. In addition, the harness still needs review against real provider schemas: several re-attestation/hold/coverage assertions consume summarized independently signed facts rather than reconstructing every original append/receipt chain, and it does not itself verify the full deletion-capability JWS. Listing a scenario in the plan is not evidence that its fixture exists or that the complete register contract is proven. Consequently this harness must not currently be described as the delivered, production-qualified owner verifier required for a `Committed` proposal.
