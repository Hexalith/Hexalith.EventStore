# Publication authority v1

This is Story 3.19's construction contract. It issues no records and grants no
release, deployment or story-completion authority. Candidate references retain
immutable identities, rather than committed proof packets. Historical 3.15 and
9.2 conclusions stay on their original subjects.

`tools/publication-authority.schema.json` defines closed version-1 JSON records.
The stdlib validator additionally enforces relationships, ordered states,
predecessor digests, time, role and assurance constraints. Bounds are five
records, 1 MiB per record document, depth 32, 100 packages/extensions and 512
characters per identifier. Duplicate or unknown fields, nonfinite numbers,
unsafe paths, symlinks and oversized files refuse with safe stable codes.

The only ordered chain is `built` → `evidence-candidate-published` →
`evidence-validated` → `release-available` → `production-promoted`. Structural
checks accept a complete prefix, beginning at `built`, and report
`structural: PASS, authority: unevaluated`. Structural validation authenticates
neither a synthetic fixture nor a real declaration. Authority evaluation today
always reports truthful `FAIL`. Malformed input exits 2; an unproven prerequisite
or invalid chain exits 1; structural success alone exits 0. No pass option or
caller-selected executable adapter exists.

Each record binds its schema/version, unique ID/state, immutable subject and
source, package identities and OCI index digest, candidate identity, validator
repository/commit/path/code digest/result/receipt-set digest, predecessor digest,
numeric human issuer/role, owner-written approval comment identity/body digest,
role registry identity, matching state-specific outcome, issuance/expiry,
revocation/invalidation and their rules, publisher/gitlink identities and required,
achieved and input assurance levels. A record cannot exceed its lowest input or
predecessor assurance. Expired, future, revoked and invalidated records refuse;
there is no inherited authority from an earlier passing result.

EventStore subjects have kind `eventstore-package` and owner/source repository
`Hexalith/Hexalith.EventStore`. Platform subjects have kind
`platform-composed-host` and owner/source repository `Hexalith/Hexalith.Platform`.
The complete subject (including eligible server package, extensions, catalog and
runtime) and candidate stay identical throughout a chain. Changing either starts
a new chain at `built`. Package receipts cannot establish a composed host's state.
Platform subject/issuer registration and extension API approvals have separate
fixed missing-prerequisite refusals. Existing EventStore role registration never
implies those Platform approvals.

The roles, in state order, are `eventstore-owner`, `release-owner`, `test-owner`,
`release-owner`, `deployment-owner`; a Platform `built` record instead uses its
registered `deployment-owner`. Separate content-bound records/comments are required
for each role and state even if one human holds several roles. Production authority
rechecks every record's current registry, unedited authenticated GitHub comment,
numeric human issuer, exact claim and 24-hour push-based subject window through
9.2's read-only transport. Tools, CI identities and fixture transports grant no
human authority. Two-human registries retain independent-author separation.

## Canonical record and approval identities

Canonical JSON is ASCII UTF-8, sorted object keys, compact separators,
`ensure_ascii=true`, decimal integers, no nonfinite values, no BOM or final LF.
Arrays retain their declared order. This encoding applies only to these records;
it never replaces production-profile or Builds release-evidence encodings.

A record's lowercase SHA-256 preimage is the canonical object
`{"purpose":"publication-authority-record","schemaVersion":1,"record":R}`.
There is no serialized self-digest. Each successor binds this digest of the entire
predecessor. To avoid a comment/record hash cycle, an approval claim uses
`{"purpose":"publication-authority-approval","schemaVersion":1,"record":C}`,
where C is R with `approval.comment_id`, `approval.body_sha256` and
`issuer.authentication_evidence_sha256` set to null. The comment ID is attached
only after the owner posts its immutable body. The immutable comment body is
closed JSON with `schema: hexalith.eventstore.publication-authority-approval.v1`,
`record_claim_sha256`, `role`, `subject_sha256`, `decision: approved`. Both record
authentication digest fields bind SHA-256 of its exact body bytes. The full record
digest still includes these authentication digests and the comment ID.

## Profile and deployment

Only `deploy/dapr/production-profile.yaml` supplies profile bytes. The bounded
read-only `tools/ProductionProfileBridge/Program.cs` bridge calls the genuine Contracts
`ProductionProfileCodec.Decode` and `ProductionProfileValidator.Validate` for v1,
or explicitly selected `ProductionProfileV2Codec.Decode` and
`ProductionProfileV2Validator.Validate` for v2.
It accepts no alternate profile path or generation mode. `tools/profile_bridge_build.py`
captures bounded regular files with safe paths and no symbolic ancestors: the owner
`global.json`, closed bridge project/configuration and fixed observation target, unchanged bridge body, all 38
explicit owner profile sources and separately validated profile/inventory/source
bytes (plus rendered bytes when present). Capture requires complete fixed source and
bridge membership. Before each child, the private snapshot must have exactly the
captured files/directories and SHA-256 hashes; changed, missing, extra or symbolic
members refuse. Checkout edits after capture cannot change compiled or decoded bytes.
These local hashes detect copying/mutation; they authenticate no source.

The SDK-only project has explicit compile items and no project/package references.
Unrelated Contracts sources, Builds imports and Commons dependencies are unnecessary.
Restore, build and execution use fixed arguments, the existing bounded child with
filtered credentials/environment and descendant reaping, private outputs and an
empty private package cache. Cleared package sources/fallback folders and disabled
targeting/runtime pack downloads refuse package fallback; a missing installed SDK
or targeting pack cannot be repaired by fetching. Ancestor build/central-package
imports and build servers/shared compilation are disabled. SDK generators remain
enabled. Exact owner SDK selection is retained; local builds use Debug and CI uses
Release. A zero-exit child must still produce a fresh assembly and a closed genuine
owner result. No checkout project is built directly.

The fixed verifier proves one invocation's input closure inside the existing
installed SDK/runtime/OS trust boundary. Hashes authenticate no source or binary.
Authenticated evaluation first compares all captured/control bytes with the
request's exact-head input and subject bindings and current authenticated file
bytes. It rechecks head before preflight and after owner validation.
Unbound or changed controls cannot launch SDK preflight. Comparison uses raw
bytes without EOL normalization: a verification checkout must preserve exact
Git-blob bytes for bound controls, including `global.json`, even when a normal
working checkout applies an EOL filter. The owner SDK selection policy is unchanged.

Each invocation has a fresh private snapshot, nonce, outputs, home, package caches
and stage contracts binding its captured membership, fixed arguments, exact
filtered environment, installed file identities and preceding generated inputs.
The unchanged owner `global.json`, including `latestPatch`, selects the SDK;
the resolved SDK version is recorded rather than replacing its selection policy.
Preprocessing observes imported files, and target-free MSBuild evaluation resolves
declared task assemblies into that selected SDK before restore. Only the fixed
`BuildProof.targets` is explicitly imported by the bridge. Ancestor build/package
imports, user import wildcards, project-extension imports and workload resolution
are disabled. Caller resolver properties, startup hooks and credentials are not
inherited. The supported SDK workload advertising opt-out
`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true` and telemetry opt-out also apply
on first use with a fresh home. Microsoft documents the advertising opt-out in
[.NET CLI environment variables](https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_workload_update_notify_disable).

Offline restore precedes a fixed generation/reference-resolution plan. The plan
observes every owner/generated source, reference assembly, analyzer, additional
file, analyzer configuration and task/import input. Inputs must be captured
snapshot bytes, files within the selected installed SDK/pack/runtime roots, or
private outputs with an observed completed SDK producer. Installed identity and
generated-byte checks run again before each stage. The native csc launch is
stopped before exec: its actual SDK-produced response file must contain exactly
the planned source/reference/analyzer/configuration/additional-file sets and
supported compiler option names. Planned file inputs are closed; scalar settings
come from the fixed project/CLI and trusted selected SDK. The verifier does not
add a separate scalar-value configuration policy. Nested or uncontrolled response
files refuse. SDK
analyzers and source generators remain enabled; their private generated files and
fresh assembly/runtime files are checked before owner execution. Post-build
observations must agree with the plan. Preprocessing or exit zero alone cannot
establish proof.

Process and network observation currently requires Linux x86-64 with `/proc`,
`ptrace` descendant tracing, seccomp observation filters and `PTRACE_GET_SYSCALL_INFO`. Temporary and SDK paths must be plain absolute paths without MSBuild property/list
metacharacters. The supported SDK execution shape uses the selected SDK's native `Roslyn/bincore/csc` apphost.
The observer follows every thread/fork/exec through termination; unexpected
executables, Internet socket attempts, execveat and io_uring refuse. Only the
fixed SDK command, csc and SDK first-use OS permission probes (`sh -c umask`,
`ls -ld` on private paths) are supported. Runtime mappings identify installed
shared-runtime roots. This observes the trusted toolchain; it is not a sandbox
or binary attestation. Unsupported resolution/observation shapes, missing packs,
unavailable tracing, incomplete receipts, mutations and no-op children retain
`PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` before the affected SDK stage or its
successor. The existing bounded child still controls time, output and descendant
reaping unchanged.

A successful genuine owner result carries an in-memory capability created only
by that fresh verifier invocation. Caller JSON and saved results cannot supply
it. Only this build-input prerequisite is discharged; structural validation
remains non-authorizing and the remaining authority verifiers still refuse.
`tools/tests/check_production_profile_bridge.py` exercises the real bridge and
Python child in disposable source copies, with poisoned unrelated Contracts and
ancestor build/package configuration and without Builds/Commons/Tenants sources;
fixture success grants no authority. Both infrastructure and
resolved digest domains remain exactly as documented in
[production profile v1](production-profile-v1.md) and
[production profile v2](production-profile-v2.md), respectively.
It refuses noncanonical bytes and uses external expected app/component coverage.

The separate `deploy/dapr/production-composing-inventory.json` has exactly
`schema: hexalith.eventstore.production-composing-inventory.v1` or explicit
`schema: hexalith.eventstore.production-composing-inventory.v2`, `source`
(`path`, `sha256`), `expectedApplicationIds`, `expectedComponentIds`. Its source
must be a separately bound composition inventory, never the profile or itself.
Its schema selects the owner decoder without fallback; mismatched versions refuse.
V2 expected component coverage includes every infrastructure and Dapr instance.
The snapshot helper, bridge project/configuration/body, both version contracts and
every owner `ProductionProfile*.cs` source, including
the shared canonical writer and binding checks, remain required execution and
lifecycle subject bindings. No production coverage file is published by this
construction. Declared coverage
alone does not establish exhaustive authenticated Platform composition.

Promotion additionally binds environment, both profile digests, inventory bytes,
rendered deployment bytes and deployment SHA-256. The latter preimage is canonical
`{"purpose":"publication-rendered-deployment","schemaVersion":1,"oci_index_sha256":O,"profile_sha256":P,"rendered_sha256":D}`.
The closed structured owner result must report structural success, no issues,
unevaluated authorization/qualification and both matching digest domains. Ordinary
SDK diagnostics may precede that result; empty or no-op output refuses. The
validated profile's environment/rendered digest and composed-host digest agree
with the promotion/subject bindings. Package IDs, versions and digests match the
profile's package artifacts; the server package digest belongs to that set.
Extensions, catalog content and runtime image digests also agree exactly. These
are declaration associations, with qualification and build-input proofs separate. Changing a deployment, profile,
source, workflow, inputs, registry or revocation state invalidates consumption.

## Fixed authority boundary

The lifecycle adapter uses 9.2's authenticated evaluation and separate completed
sealed-run consumption. Code/schema/owner profile sources, release workflow,
record, sole profile, external inventory, source composition and rendered inputs
must all bind exact current-head bytes and belong to the lifecycle subject used
for the push window. Only the records slot uses an authored-claim projection:
its window digest replaces each record with its ID and approval-claim digest,
excluding the returned comment ID and the two body hashes. Attaching a receipt
therefore preserves the window; every authored claim change resets it. Each record
uses its own claim projection for its issuance window, so a later state does not
rewrite an earlier issuance. Exact raw records bytes remain bound in the request
and sealed result; consumption reruns these checks and
rechecks main after evaluation. Seals belong only to dedicated dispatched
release/promotion attempts. Main evaluation is non-promoting and never seals.

Publisher SHA is resolved from the tracked release workflow's pinned Builds
checkout; Builds gitlink SHA is resolved from the authenticated EventStore Git
tree, never supplied as proof by a record. These revisions may differ: both exact
identities are bound. A future approved integration must verify the Builds-owned
`ReleaseEvidenceCodec` at both identities; neither a similarly named file nor
`ReleaseRecordCodec` supplies that verification. No future codec API is assumed.
Until an approved fixed integration exists, `RELEASE_EVIDENCE_CODEC_UNAVAILABLE`
refuses all records beyond `evidence-validated`.

After a complete invocation proof, the remaining fixed missing verifiers are G-RUNTIME-PARITY (3.15's historical closure
is insufficient), 3.21 exact runtime/selected broker, 5.7 components/physical-target
isolation, 5.13 activated catalogs, 7.22 complete scheduler/placement restore,
AD-26 Architecture/Platform ratification, 4.16 append protection, workload issuer
approval, applicable corrective authorization and sealed predecessor reruns.
Platform subjects also need explicit issuer and extension API approvals.
Registry/transition authentication and required/achieved/lowest-input assurance
remain mandatory even after these future integrations exist. Saved results,
planning status, package availability, approvals or fixture substitutions never
replace any fixed prerequisite. No deleted 9.1/parity-packet machinery is restored.
