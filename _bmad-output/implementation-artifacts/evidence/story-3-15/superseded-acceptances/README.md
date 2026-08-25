# Superseded Story 3.15 acceptances

This audit area retains two superseded Story 3.15 acceptance sets:

- Three genuine, GitHub-minted acceptance receipts collected on 2026-08-22 for subject
  `bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`.
- Three roster-bound role receipts collected on 2026-08-25 for subject
  `dab64f5fbbf55783630ad75451d35d517d829e194fb618dc8b0526d39761d38d`. The EventStore-owner
  and Release-owner roles map to one authenticated GitHub human (`github:jpiquot`); the Test
  Architect receipt is the explicitly limited self-attested `bmad:murat` record.

A 2026-08-25 code review found that the closure bound only `v1.py` and its dispatcher, leaving the
transitively imported `release_evidence_handlers.v3` — which performs predecessor validation,
nuspec identity parsing, and the release-manifest check — unbound. A tampered `v3.py` therefore
produced the identical subject and selected identity with all three receipts still valid, which the
closure's own `rerun_trigger` and frozen AC2 forbid.

Binding those bytes changed `v1.py`, and with it the canonical subject. Per that rerun trigger,
**every prior receipt is rejected**. They are retained here, outside the packet root, so the bytes
survive for audit while the packet stays closed over exactly the current subject.

The later `dab64f5f...` set was superseded when the trusted verifiers were hardened to execute only
the exact verified initializer/handler sources under sanitized import resolution, nuspec XML was
restricted to strict UTF-8 before DTD/entity rejection, smoke numeric facts were restricted to exact
JSON integers, and the rerun trigger was corrected to bind receipt-source *policy* changes without
claiming the subject directly binds each post-subject source instance. Those trusted-byte and policy
changes re-minted the subject; no replacement receipts are retained here for the new subject.

They authorize nothing for the current subject. Do not move them back into the packet.
