# Superseded Story 3.15 acceptances

These are the three genuine, GitHub-minted acceptance receipts collected on 2026-08-22 for subject
`bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`.

A 2026-08-25 code review found that the closure bound only `v1.py` and its dispatcher, leaving the
transitively imported `release_evidence_handlers.v3` — which performs predecessor validation,
nuspec identity parsing, and the release-manifest check — unbound. A tampered `v3.py` therefore
produced the identical subject and selected identity with all three receipts still valid, which the
closure's own `rerun_trigger` and frozen AC2 forbid.

Binding those bytes changed `v1.py`, and with it the canonical subject. Per that rerun trigger,
**every prior receipt is rejected**. They are retained here, outside the packet root, so the bytes
survive for audit while the packet stays closed over exactly the current subject.

They authorize nothing for the current subject. Do not move them back into the packet.
