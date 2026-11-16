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
read-only `tools/production-profile.cs` bridge calls the genuine Contracts
`ProductionProfileCodec.Decode` and `ProductionProfileValidator.Validate` for v1,
or explicitly selected `ProductionProfileV2Codec.Decode` and
`ProductionProfileV2Validator.Validate` for v2.
It accepts no alternate profile path or generation mode. The fixed child resolves
only the installed `dotnet` tool before dropping caller credentials, supplies its
runtime root and an existing package cache, disables build-server reuse, and copies
the bound owner `global.json` into scratch so SDK selection honors that pin.
The declared project, SDK, NuGet and build configuration files belong to its
bound inputs. They do not prove the complete execution closure: transitive
Contracts sources, imported Builds configuration and package/SDK resolution remain
unverified. `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` therefore refuses the
authenticated adapter before any .NET launch. There is no success override or
complete-graph claim; the standalone structural bridge remains non-authorizing.
`tools/tests/check_production_profile_bridge.py` exercises the real bridge and
Python child in disposable source copies; fixture success grants no authority. Both infrastructure and
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
Both version contracts and every owner `ProductionProfile*.cs` source, including
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

A complete build-input proof is also unavailable as described above. The other
fixed missing verifiers are G-RUNTIME-PARITY (3.15's historical closure
is insufficient), 3.21 exact runtime/selected broker, 5.7 components/physical-target
isolation, 5.13 activated catalogs, 7.22 complete scheduler/placement restore,
AD-26 Architecture/Platform ratification, 4.16 append protection, workload issuer
approval, applicable corrective authorization and sealed predecessor reruns.
Platform subjects also need explicit issuer and extension API approvals.
Registry/transition authentication and required/achieved/lowest-input assurance
remain mandatory even after these future integrations exist. Saved results,
planning status, package availability, approvals or fixture substitutions never
replace any fixed prerequisite. No deleted 9.1/parity-packet machinery is restored.
