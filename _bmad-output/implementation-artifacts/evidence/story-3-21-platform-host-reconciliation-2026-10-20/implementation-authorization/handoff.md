# ES-DIAG implementation authorization preflight

The user's `I authorize` applies to the approved ES-DIAG scope. It is recorded in
[request.json](request.json). No further generic consent is needed for that scope.

The required command is:

```bash
python3 -B tools/validate-corrective-work-authorization.py --evaluate-registry
```

It exits 1 with `FAIL / missing-authority`: `live corrective-work authorization
registry is absent`. Exact output and input pins are in [preflight.json](preflight.json).

The existing validator requires an unchanged baseline registry at
`_bmad-output/implementation-artifacts/evidence/corrective-work-authorizations/owner-role-registry.json`,
a baseline-matching PRD, an explicit scoped authorization and its approved input
subject, and unedited authenticated numeric-human GitHub comments for both the
registry and the authorization. The current committed baseline has no registry;
the approved PRD is also an uncommitted amendment. A conversation receipt cannot
be presented as one of those GitHub comments. The Story 3.15 roster is specific
to its historical packet and cannot be copied into this authority.

The next repository-owner work is to establish that live registry and committed
planning baseline through its own authorized Git/authority process, then bind
the ES-DIAG allowed paths, evidence validator and approval payloads and obtain a
passing preflight. This requires a real owner identity and authority/approval
issue locations; no values were guessed. The current ES-DIAG scope forbids Git
mutations, external messages and changes to authority inputs, so this preflight
performed none. Do not broaden scope to all existing dirty work to make it pass.

After the prerequisite exists, resume the existing approved spec and
[build prompt](../../../../planning-artifacts/next-bmad-build-story-3-21.md)
using this already-recorded user authorization. The spec's frozen block remains
byte-for-byte the approved intent. Parent Story 3.21 stays in progress; no broker
is selected and production eligibility remains FAIL.
