# Superseded Story 3.15 acceptances

This audit area retains five superseded Story 3.15 acceptance sets. There are **five sets against
the successive subjects** because receipts were collected for only five subjects. Re-mints that
happened before any receipt was collected have no receipt set to retain.

These sets authorize nothing for the current subject. Do not move them back into the packet.

## Re-rooting rule for auditors

Every retained receipt declares `durable_source.file` as
`acceptances/<subject>/sources/<role>.json`, because that is the packet-relative path the receipt
bound while it was live. Those paths do not exist any more. To re-pair a superseded receipt with its
source, replace the leading `acceptances/` with this directory:

    acceptances/<subject>/sources/<role>.json
      ->  superseded-acceptances/<subject>/sources/<role>.json

Having re-rooted the path, confirm the pairing: the SHA-256 of the file you land on must equal the
receipt's own `durable_source.sha256`, and its byte length must equal `durable_source.size`. Without
that step the re-rooting is a guess about which file the receipt meant.

    sha256sum superseded-acceptances/<subject>/sources/<role>.json
    jq -r '.durable_source | "\(.sha256)  \(.size)"' superseded-acceptances/<subject>/<role>.json

The receipt bytes are deliberately left unmodified -- rewriting them would break the very digests
that make the retention auditable -- so the re-rooting is documented here instead.

## Retained sets

- Three genuine, GitHub-minted acceptance receipts collected on 2026-08-22 for subject
  `bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`.
- Three roster-bound role receipts collected on 2026-08-25 for subject
  `dab64f5fbbf55783630ad75451d35d517d829e194fb618dc8b0526d39761d38d`.
- Three roster-bound role receipts collected on 2026-08-25 for subject
  `a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`.
- Three roster-bound role receipts collected on 2026-09-05 for subject
  `86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`.
- Three roster-bound role receipts collected on 2026-09-23 for subject
  `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274`. The
  2026-09-26 curl configuration isolation changed the bound capture producer and re-minted the
  subject. These receipts and their sources moved here byte-for-byte; none accepts the new subject.

The packet's `closure.json` carries `deployed_runtime_parity: "available"` and a
`selected_deployed_identity`. Those two fields are the **claim** the three roles are asked to
accept, not a verdict: the verifier grants them only when three receipts bind the current subject,
and at 0 of 3 it exits 1 and grants nothing.

In every set the EventStore-owner and Release-owner roles map to one authenticated GitHub human
(`github:jpiquot`), and the Test Architect receipt is the explicitly limited self-attested
`bmad:murat` record.

## Why each set was superseded

A 2026-08-25 code review found that the closure bound only `v1.py` and its dispatcher, leaving the
transitively imported `release_evidence_handlers.v3` -- which performs predecessor validation,
nuspec identity parsing, and the release-manifest check -- unbound. A tampered `v3.py` therefore
produced the identical subject and selected identity with all three receipts still valid, which the
closure's own `rerun_trigger` and frozen AC2 forbid. Binding those bytes changed `v1.py`, and with
it the canonical subject, so the `bb58d691...` set was rejected.

The `dab64f5f...` set was superseded when the trusted verifiers were hardened to execute only the
exact verified initializer/handler sources under sanitized import resolution, nuspec XML was
restricted to strict UTF-8 before DTD/entity rejection, smoke numeric facts were restricted to exact
JSON integers, and the rerun trigger was corrected to bind receipt-source *policy* changes without
claiming the subject directly binds each post-subject source instance.

The `a8cc777e...` set was superseded by the loop-6 review batch, landed as one re-mint:

- The two producers -- the bounded smoke capture tool and the packet assembler -- became bound
  decision inputs in the closure `dispatch` block. Until then a producer edit could change what a
  passing Production smoke means without invalidating a single receipt.
- The retained GitHub acceptance-source envelopes became closed-schema. A stray unreviewed field
  could previously be added to the sole external authentication artifact and survive a full pass
  with the subject unchanged.
- A fourth limitation was bound into the subject, disclosing that every acceptance receipt is
  composed by repository tooling and posted with the rostered role holder's credential.

The `86c59c79...` set was superseded by the 2026-09-06 Group A tools patch, landed as one re-mint:

- A `TimeoutExpired` during `docker run` now inspects and force-removes the uuid-named container.
- The assembler restores the previous `closure.json` when the pinned verifier does not complete.
- Encrypted or unsupported-compression nuspec reads fail closed as `EvidenceError`.
- The closed inventory exempts only the validated evidence path, not a hardcoded `closure.json`.

The `7d64f87e...` set was superseded on 2026-09-26 when the smoke capture placed curl's `-q`
first, preventing the operator's default `.curlrc` from changing the requested `/alive` path while
the result still recorded `/alive`. Fresh receipts were later collected for the re-minted subject
`66be1b4a...`, which carries its own acceptance directory in the live packet. Historical GitHub
comments and the Test Architect record above were not reused for it.

Per the rerun trigger, **every prior receipt is rejected**. The bytes are retained here, outside the
packet root, so they survive for audit while the packet stays closed over exactly the current
subject.
