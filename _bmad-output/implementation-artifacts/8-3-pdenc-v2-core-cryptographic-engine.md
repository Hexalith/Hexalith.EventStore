# Story 8.3 pdenc-v2 Core Cryptographic Engine

## Disposition

Implementation and technical verification completed on 2026-09-14 against
EventStore baseline `e8886ec4c277460de3d3208b3fc0b9c261c4967d`, Story 8.2 approval
packet `AR-20260914-01`, and normative digest
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.

Approval packet `AR-20260914-02` amended and reapproved the frozen Story 8.3
requirements to the safe, constructible interpretations proven below. The
implementation is ready for independent review but is not self-approved.
Stories 8.4 and 8.5 remain unauthorized pending that review and exact approval.

## Delivered Scope

- Added a provider-neutral, internal, non-packable
  `Hexalith.EventStore.PayloadProtection` core with strict HXP2/base64url,
  HXAD/HXPM, RFC 6901, bounded JSON, AES-256-GCM, ordinal nonces, atomic
  event protect/unprotect, cancellation, closed diagnostics, collision seams,
  and observable owned-buffer zeroing.
- Added a non-packable xUnit v3/Shouldly test project with linked immutable Story
  8.2 fixtures and a separate vector-execution manifest.
- Implemented traits for all 51 assigned identifiers: inherited V001-V003 plus
  V004-V048/V135-V136/V138. The applicable core behavior passes 137 test cases;
  Story 8.5-owned policy portions of V038/V039 are not claimed.
- Closed the Step-3 audit gaps for strict carrier/header matrices, exact
  resolver inputs, constructible AAD sources/boundaries, independent manifest
  commitments and wrapper-set changes, Unicode/control/path ordering,
  collision freshness, configured write limits, atomic exit/clearing matrices,
  and closed diagnostic surfaces.
- Preserved Contracts, frozen fixtures/verifiers, Server/no-op hooks, domain and
  Parties code, `Hexalith.EventStore.slnx`, `tools/release-packages.json`,
  topology, persisted data, and external resources.

## Content Binding

| Artifact | SHA-256 |
| --- | --- |
| Requirements amendment approval | `cd95ab5546939d2f79338c354ea1bf4456c50093cf0525dafb7b82cebab4d3a6` |
| Sorted production source/project hash stream (29 files) | `9a8d0e85f8f3416e0615cdbc51275c020bf8a91f0a5eb6d683ee6d875b2a1206` |
| Sorted focused-test/project/manifest hash stream (11 files) | `610323256f1b9a2dd4206d63b63bd1c41877031bed551f6f859959861fbff01d` |
| Core project | `358feb7e012807a2e54da26ca5324e668a35cefeb7689dd49d5b424ef9b7e91f` |
| Test project | `5b29d17454fd11c65965c6cc66deb70571f7c995d9512384f28b38d37185aed5` |
| Vector execution manifest | `3cc4898d645fb0cf31481abebfe5730d0869d385b13d8f96960c58841bd75297` |
| Preflight evidence | `ab3a5d7ac7dc4838e91b0ecc4bc3d77438f22a545d25ec92cba9dcf89ff2c8a2` |
| Verification evidence | `99aa997a3409094944deb2fd62eca51b56f444aabf93c3e000c3a072c3ca1fc0` |
| Unchanged release manifest | `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` |
| Unchanged solution | `dd9c0a74a6ca81d50e05ddcfd336f4f92882d77afa313287d69cf2bc87e7fc74` |

Detailed source identity, inventory, tool versions, source rechecks, commands,
counts, and observed load values are in
`evidence/story-8-3/preflight.md` and `evidence/story-8-3/verification.md`.

## Verification Summary

- Both independent frozen-vector verifiers passed V001-V003 unchanged.
- The focused Release suite passed 137/137 with zero failures or skips.
- The core Release build with AOT and trim analyzers passed with zero warnings
  and errors; repository style verification and the `.gitattributes` LF check
  also passed.
- The complete `.slnx` Release build passed with zero warnings and errors.
- Existing release packaging and both validators produced exactly 14 archives;
  the new non-packable project was absent as required.
- Dependency, public-surface, solution/release-preservation, vector-coverage,
  and whitespace scans passed.

## Approved Constructibility Boundary And Review State

V030's original total AAD boundary cannot be constructed: its eleven
individual field maxima plus framing total 3,873 bytes, below 4,096, and the
existing `AggregateIdentity` restrictions lower the runtime maximum to 3,617.
The implementation proves the 3,617-byte constructible maximum and rejects a
maximum-plus-one field before crypto. `AR-20260914-02` accepts this as the
executable Story 8.3 boundary while retaining the 4,096-byte defensive cap and
all individual field bounds.

The exact-format source cases in V023 and the policy-cycle/converter/getter
cases in V038/V039 remain unconstructible at the Story 8.3 byte-core boundary;
raw format-field substitutions and the applicable bounded depth/cancellation
paths are covered. V008's request to treat `02` as an unsupported version byte
conflicts with the frozen required HXP2 version `02`, so the core preserves
G-001, accepts that exact byte, and rejects `03`/`ff`. These limitations and
V016's required pre-lookup ordinal consistency rejection are detailed in the
verification evidence. `AR-20260914-02` explicitly accepts these
interpretations for Story 8.3 only; the shared authority bytes remain unchanged.

No independent Story 8.3 implementation review or completion approval has yet
been recorded. No package, provider, lifecycle, compatibility, Server/snapshot
integration, deployment, NIST CAVP certification, later vector, or G5 claim is
authorized by this artifact.
