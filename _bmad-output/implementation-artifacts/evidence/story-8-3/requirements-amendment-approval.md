# Story 8.3 Requirements Amendment Approval `AR-20260914-02`

- Approver: human EventStore owner in the current interactive BMad build session.
- UTC timestamp: `2026-09-14T09:52:51Z`.
- Decision: **AMENDED REQUIREMENTS APPROVED; STORY 8.3 REVIEW AUTHORIZED**.
- Approval addendum: at `2026-09-15T11:02:04Z`, the human EventStore owner in
  the current interactive BMad build session authorized the V004 interpretation
  and the authority section 8.4 Server-ownership correction recorded below.
- Reviewed repository commit: `220e722df0f088bd6790b4815eedd3e993de09fb`.
- Normative Story 8.1 SHA-256, unchanged:
  `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
- Story 8.3 preflight evidence SHA-256:
  `ab3a5d7ac7dc4838e91b0ecc4bc3d77438f22a545d25ec92cba9dcf89ff2c8a2`.
- Story 8.3 verification evidence SHA-256:
  `99aa997a3409094944deb2fd62eca51b56f444aabf93c3e000c3a072c3ca1fc0`.
- Story 8.3 completion artifact SHA-256 before this approval update:
  `ff81684cffa8e3fdbbe52c261541d582df15b2d30cfcdaa72f4856683ec1ead5`.

## Approved Amendment

The approver authorized amending and reapproving the frozen Story 8.3
requirements to match the safe, constructible behavior and mathematical proof
recorded in `verification.md`. The accepted interpretations are:

1. V008 preserves HXP2 version `02` and rejects `03`/`ff` at the version byte;
   unsupported identifier/flag fields continue to exercise `02`/`ff`.
2. V004 envelope-ordinal bit flips at offsets 16-19 and V016 ordinal-only
   substitutions are rejected locally before lookup because manifest/ordinal
   consistency is mandatory. V016 still performs the exact substituted lookup
   for a valid DEK-version change.
3. V023 proves raw AAD format absence/emptiness and v1 substitution without
   inventing a nullable or caller-selected format source at the byte-core seam.
4. V030 retains the 4,096-byte total-AAD defensive cap and proves the exact
   3,617-byte runtime-constructible maximum plus rejection of a 2,049-byte path.
   No individual bound is weakened to manufacture the unreachable 4,096/4,097
   pair; the field-bound-only mathematical ceiling remains 3,873 bytes.
5. V038/V039 prove core-owned bounded depth/cycle-equivalent and cancellation
   behavior. Policy discovery, converter/getter execution, and policy-fault
   mapping stay assigned to Story 8.5.
6. Authority section 8.4's durable metadata allowlist and `Unprotected()`
   pairing are Server-owned and excluded from Story 8.3 because this core-engine
   story forbids Server persistence integration.

These interpretations supersede contrary registry wording for Story 8.3 only.
The shared payload-protection authority, Story 8.2 Contracts and fixtures, G-001
and NIST bytes, existing field and resource bounds, and later-story ownership
remain unchanged. This approval authorizes independent Story 8.3 review and
closure if the reviewed implementation and executed tests match the amended
requirements. It does not authorize Story 8.4 or Story 8.5, a commit, push,
branch, dependency update, external mutation, deployment, or G5 claim.
