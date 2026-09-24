# OQ8 v5 successor activation guide

The current selector and lifecycle still point to the approved v4 successor.
The revised `docs/ci.md`, release manifest test, and OQ8 validator therefore
make the normal current-source gate fail closed. The v5 draft grants no approval
or publication authority.

## Prepare the review subject

Finish the EventStore implementation source, commit it on the review branch,
and run the focused pre-review Contracts checks on that clean candidate. The
full Contracts suite contains the active OQ8 gate and cannot pass until the
reviewed packet and selector exist. Create a fresh
draft outside the repository before freezing the subject:

```bash
bash scripts/verify-oq8-v5-candidate.sh
python3 tools/oq8-v5-packet.py --prepare-draft /tmp/eventstore-oq8-v5-subject.json
python3 tools/validate-oq8-platform-evidence.py --v5-subject-draft /tmp/eventstore-oq8-v5-subject.json
```

The draft contains `subjectInputs`, its canonical SHA-256 `subjectSha256`, the
final reviewed source commit and historical v4 identities, all current gate-input hashes, the
release source hashes, the 14 package IDs, limitations, and the required review
roster. It records `frozenAt: null`, three pending reviews, and no authority.
Regenerate it after any source edit or commit and use only the final clean
checkout's subject hash. `tools/oq8-v5-packet.schema.json` defines the draft
and future active packet shapes; `tools/oq8-v5-packet.py` checks their semantic
and repository bindings. The active validator requires the frozen reviewed
commit to be an ancestor of the current clean checkout. It permits only the
v5 packet, manifest, selector, and lifecycle paths to change after that
reviewed commit; any other source change requires a new subject and reviews.

## Obtain independent reviews

Freeze one final subject at an actual UTC second in `YYYY-MM-DDTHH:MM:SSZ`
form, after receipt-independent validation. Architecture, security, and test
reviewers inspect that same `subjectSha256` and the limitations in its
`subjectInputs`. Each reviewer supplies a fresh receipt with the active packet
schema fields: `role`, `reviewer`, `scope`, `issuedAt`, `decision`,
`subjectSha256`, nonempty `findings`, and no-authority declaration. The Test
Architect receipt also binds the focused Contracts test command, positive passed
count, zero failures, skipped count, log SHA-256, and the subject's
`sourceIdentitySha256`. Receipt times must follow the freeze. This repository
does not generate or preapprove those receipts. The validator binds receipt
content and chronology but cannot independently authenticate a named reviewer;
retain the review provenance with the PR.

## Assemble and activate after approval

After all three reviews exist, assemble
`_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5/packet.json`
with the draft's unchanged `subjectInputs` and `subjectSha256`, the real
`frozenAt`, three approved receipts, and a handoff assembled after the latest
receipt. The handoff binds the canonical SHA-256 of each receipt. The packet
claims current-source approval only; release, package, and registry authority
remain false. The v5 directory contains exactly `packet.json` and a
path-sorted `closure-sha256.txt` line for that packet.

Canonical object hashes use UTF-8 JSON with sorted keys, no indentation, and
separators `(',', ':')`, matching `digest()` in `tools/oq8-v5-packet.py`.
The closure manifest is exactly one line: `<packet-file-sha256>  packet.json`.
Use the packet tool's active validation to check those hashes and the
receipt chronology before relying on the selector.

Update `4-15-oq8-platform-closure-successor.json` to selection schema `v4`:
retain all historical v1/SDK/v2/v3 entries, add `v4Successor` equal to the
current selector's complete `successor` object, and select the v5 directory.
Use the exact v5 selection reason defined in `tools/oq8-v5-packet.py` and a
selection date no earlier than the reviewed handoff.
Bind the packet hash, manifest hash, canonical source identity hash, subject
hash, and canonical handoff hash in the selected object. Update the separate
`4-15-oq8-platform-lifecycle-state.json` to that v5 directory, manifest, and
subject while keeping Story 4.15 closed. The old v4 directory and archived
source remain untouched.

Run the full Contracts suite and reviewed PR CI with the complete packet and
selector, then merge and require successful exact-source main push CI. On that
final clean source, run
`python3 tools/oq8-v5-packet.py
--validate-active` and then the normal
`python3 tools/validate-oq8-platform-evidence.py`. The normal validator checks
historical v1–v4 evidence before accepting the v5 packet, exact selector, and
lifecycle. No current-source claim is available while the selector is v4 or
reviews are missing. NuGet policy activation and protected production approval
remain separate; any eventual ordinary release uses `bypass-validation=false`.
