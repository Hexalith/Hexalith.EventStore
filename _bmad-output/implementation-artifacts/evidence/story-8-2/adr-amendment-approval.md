# Story 8.1 Amendment Approval `AR-20260913-01`

- Approver: Jérôme Piquot.
- UTC timestamp: `2026-09-13T12:31:46Z`.
- Normative SHA-256:
  `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
- EventStore source: `dfc0ac557c43363159b55bffb4d40feceab1f787`.
- Parties source: `4378dede55d92e489caf7aad63d6c2892e6f856d`.
- Evidence references: this detached packet `AR-20260913-01`; preflight
  `_bmad-output/implementation-artifacts/evidence/story-8-2/preflight-before-adr-amendment.md`
  at SHA-256 `3a37fa8a1dc523a9f86f68a2b07e2b5443a023af0197ddf91ec9314f72d61b9f`;
  amendment validation
  `_bmad-output/implementation-artifacts/evidence/story-8-2/adr-amendment-validation.md`
  at SHA-256 `3b7fb4cc964e1bcff076ae21dfec58b071024eaa48ee388c3fdabe4449868a8e`;
  and the unchanged independent vector evidence in superseded packet
  `AR-20260801-01`.
- Review method: reviewed the exact replacement normative digest/source and
  amendment scope, confirmed that the API/source-identity amendment did not
  change the G-001, NIST, or V001–V138 inputs/results, and re-bound the prior
  independent Node.js `v26.4.0`/OpenSSL `3.5.7` and Python `3.14.4`/cryptography
  `46.0.5` reproduction result SHA-256
  `91744a9a620158fa982c0128e88c20eecd81764338e3e71a38eff526fbd53382`
  to the replacement approval subject. This records re-review and re-approval;
  it does not claim that the unchanged commands were executed again on
  2026-09-13.
- Roles: Architect, Security Reviewer, EventStore owner, Release owner,
  Operations owner, Parties maintainer, and Test Architect / independent vector
  reviewer.
- Disposition: approved the exact replacement digest/source, accepted the
  documented residual risks and additive API design, and reported no open
  material findings.
- Authorization: Story 8.2 may implement the bounded Contracts/default/vector
  scope. Stories 8.3-8.11 and G5 remain predecessor/evidence gated.

The user supplied the exact confirmation requested for the prepared approval
subject in the current interactive BMad build session. The detached record and
the traceability clarification above change no normative bytes. The Story 8.1
section 1.2 recomputation remains the authorization check before and after Story
8.2.
