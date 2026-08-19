# Reviewer Gate - 2026-08-16 Adversarial Divergence Closure

- **Artifact:** `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md`
- **Prior final review:** `reviews/review-update-2026-08-16-adversarial-divergence-final.md`
- **Focus:** final codec/hash and owner-role forgery counterexamples.
- **Deterministic pre-pass:** `lint_spine.py` returned `ok: true`, zero findings.
- **Mutation posture:** review only; the spine was not edited.

## Verdict

**PASS - no critical or high adversarial-divergence finding remains.** The single versioned, content-addressed `ReleaseEvidenceCodec` closes serialization/hash drift, and packet-bound trusted owner-role registries plus platform-owned signature/immutable-identity verification close self-declared or substituted reviewer/consumer authority.

## Codec And Hash Closure

### Binding rule

AD-11 now establishes one authority for release-evidence encoding (`ARCHITECTURE-SPINE.md:161`):

- the EventStore platform evidence verifier owns the single versioned `ReleaseEvidenceCodec`;
- `ReleaseIdentity` records the codec identifier, schema/version, and verifier content digest;
- every producer and verifier hashes the retained UTF-8 canonical bytes emitted by that codec;
- reserialization is forbidden; and
- alternate codecs fail closed.

Story 3.15 separately binds the resulting `ReleaseIdentity` by SHA-256 inside a canonical review subject computed over exact canonical bytes (`:352-360`).

### Counterexamples retried

**Equivalent JSON, different byte order:** Producer A hashes codec-emitted canonical bytes. Producer B parses the record and reserializes with different member order, whitespace, escaping, Unicode normalization, or newline convention. B violates the explicit no-reserialization rule; its digest cannot authorize the candidate.

**Alternate codec under the same schema:** Producer B uses a locally implemented “compatible” encoder. Unless its verifier content digest and emitted retained bytes equal the one platform-owned codec exactly, the recorded codec identity does not match and the alternate fails closed.

**Codec-version substitution:** A packet encoded with codec/schema version `v1` cannot be validated as `v2`; the codec identifier, schema/version, and verifier digest are identity fields. If the named codec is unavailable, validation fails closed rather than negotiating or re-encoding.

**Hash-algorithm substitution:** A Story 3.15 subject or evidence object using SHA-512, a display checksum, or a hash of reparsed logical content cannot satisfy the required SHA-256 digests over retained codec bytes. Package, manifest, OCI, evidence, and subject identities remain distinct named fields; no hash can be silently reused for a different edge.

**Hash-the-label attack:** Child labels and hand-authored mappings remain non-authoritative by AD-11. A correct codec digest over a record containing false lineage still fails Story 3.15's independent derivation from trusted workflow facts and retained raw bytes (`:161`).

### Result

No two codecs or canonicalization choices can both authorize the same candidate. Different bytes produce different SHA-256 subjects; different codecs/versions/verifier content digests fail identity equality; unavailable tooling produces a safe failure, not a second accepted representation.

## Owner-Role And Signature Closure

### Binding rule

AD-22 binds one trusted owner-role registry into the unchanged packet by canonical owner, path, schema, version, and SHA-256 content digest (`:325`). The platform-owned verifier accepts reviewer and Consumer-owner receipts only after validating their signature or immutable approval identity against that packet-bound registry (`:331,359-361`). Self-declared roles, free-form approval, Booleans, EventStore-side substitution for Consumer authority, and unverifiable receipts are expressly non-authorizing.

### Counterexamples retried

**Self-declared role:** An evidence author sets `role: Release owner` or `role: Consumer owner` and signs with an unrelated identity. The platform verifier cannot map the signer to that role in the bound registry, so the receipt fails.

**Registry substitution:** An attacker creates a second registry mapping itself to all roles. Its canonical owner/path/schema/version or SHA-256 content digest differs from the packet-bound trusted registry; substituting it changes a bound fact and invalidates the packet and receipts.

**Registry mutation under one version:** Adding or replacing a signer without changing the version still changes the registry content digest. Existing packet and receipt bindings no longer match.

**Valid signature, wrong role:** Cryptographic validity alone is insufficient. The immutable signer/approval identity must map to the exact required EventStore owner, Release owner, Test Architect, or Consumer owner role in the bound registry.

**Receipt replay across packets or removal subjects:** Reviewer receipts bind the recomputed canonical packet subject; Consumer-owner receipts bind packet, catalog, mode matrix, consumer repository/commit, removal subject, outcome, timestamp, and validity. Reuse against a different packet, consumer, role registry, catalog, mode, removal, or expired validity changes a bound fact and fails.

**Unsigned or mutable approval channel:** Free-form email, comments, story status, planning approval, release authority, and unverifiable approval records are explicitly not receipts and grant no release-parity or cross-repository mutation authority.

### Result

No forged, self-selected, wrong-role, stale-registry, or replayed identity can satisfy the corrected verifier contract. The trust decision is single-valued because one platform-owned verifier evaluates one content-addressed registry already bound into the subject being approved.

## Regression Sweep

- The codec addition does not weaken independent lineage derivation, raw-byte OCI verification, or immutable digest-only deployment identity.
- The registry addition does not merge the EventStore/Release/Test evidence roles with Consumer-owner removal authority; they remain distinct.
- Registry or codec rotation necessarily changes a bound digest/version and therefore requires a new packet/subject/receipts; it cannot silently preserve prior authority.
- Story 3.13 remains rejected/non-authorizing; Stories 3.14 and 3.15 remain separate publication and validation steps.
- The dated Story 2.12 exception remains scoped and gains no authority over deployed mode, later candidates, or other consumers.
- No new contradiction among AD-11, AD-12, AD-22, the capability map, and runtime topology was introduced by these final bindings.

## Non-Blocking Tail

AD-11's local `Binds` field still omits FR36 while the capability map names AD-11 for FR36. That is a medium traceability cleanup, not a codec, signer-trust, lineage, acceptance, or removal-authority bypass. Concrete codec schemas and signature mechanisms remain implementation seed owned by the single named verifier; the spine fixes their authority, version/content identity, exact input bytes, and fail-closed behavior.

Gate result: **PASS.** No critical or high finding remains after codec/hash and owner-role forgery closure.
