# Story 8.3 pdenc-v2 Core Cryptographic Engine

## Disposition

Implementation and technical verification through the full-diff fourth review pass completed on 2026-09-17 against
EventStore baseline `e8886ec4c277460de3d3208b3fc0b9c261c4967d`, Story 8.2 approval
packet `AR-20260914-01`, and normative digest
`de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.

Approval packet `AR-20260914-02` amended and reapproved the frozen Story 8.3
requirements to the safe, constructible interpretations proven below. No open
material Story 8.3 code finding is currently known. The full-diff fourth pass
covered Blind Hunter, Edge Case Hunter, Verification Gap, and acceptance audit,
superseding the earlier partial-chunk review plan.
On 2026-09-15 the human EventStore owner authorized and applied the V004
amendment-list and authority section 8.4 Server-ownership corrections. Stories
8.4 and 8.5 remain unauthorized pending completed review and exact approval.

## Delivered Scope

- Added a provider-neutral, internal, non-packable
  `Hexalith.EventStore.PayloadProtection` core with strict HXP2/base64url,
  HXAD/HXPM, RFC 6901, payload-proportional byte-indexed JSON, AES-256-GCM,
  ordinal nonces, atomic event and root-snapshot protect/unprotect,
  cancellation, closed diagnostics, collision seams, and observable
  owned-buffer zeroing.
- Added a non-packable xUnit v3/Shouldly test project with linked immutable Story
  8.2 fixtures and a separate vector-execution manifest.
- Implemented traits for all 51 assigned identifiers: inherited V001-V003 plus
  V004-V048/V135-V136/V138. The focused suite contains 290 test cases;
  Story 8.5-owned policy portions of V038/V039 are not claimed.
- Closed the Step-3 audit gaps for strict carrier/header matrices, exact
  resolver inputs, constructible AAD sources/boundaries, independent manifest
  commitments and wrapper-set changes, Unicode/control/path ordering,
  collision freshness, configured write limits, atomic exit/clearing matrices,
  and closed diagnostic surfaces.
- Closed final-review gaps for deterministic nonce-prefix validation, bounded
  JSON-pointer and disposal cancellation, exact canonical wrapper/raw-token
  restoration, frozen vector ownership, exact writer/reader maxima,
  invalid-material cleanup, and snapshot callback isolation.
- Closed the final 14 independent-review actions with strict whole-input UTF-8
  validation, production entropy coverage, centralized wire constants,
  no-leak record formatting, production escaped-name and snapshot-ordinal
  regressions, explicit resolver ownership, direct snapshot plaintext
  transfer, corrected collision taxonomy/null flow, and dead-guard removal.
- Closed all 13 mutable second-pass actions with independent snapshot golden
  bytes, explicit path-to-ordinal alignment, zero-token and `\/` path coverage,
  complete normative source citations, centralized HXAD/HXPM/key/ULID
  constants, corrected pre-material validation and pass-through diagnostics,
  and removal of dead or allocation-backed format logic.
- Closed two follow-up runtime findings so cancellation during the final
  collision cleanup wins over retry exhaustion and a faulting operations
  counter cannot suppress the duration measurement.
- Closed the third-pass codec/wire-format findings with fail-closed invariant-
  globalization handling, enforced writer nonce derivation, single-sourced
  wire constants, distinct manifest cancellation checkpoints, bounded non-BMP
  pointer accounting, and documented codec buffer ownership.
- Closed the third-pass bounded-JSON findings with allocation-free parser-frame
  cleanup, explicit zeroing evidence for failed/cancelled parse and rewrite
  exits, earlier-envelope cleanup after a later malformed wrapper, exact
  discovered-path boundaries, over-limit unselected-path preservation, and a
  literal multibyte complete-reader round trip.
- Closed all 24 third-pass chunk-4a findings across test scaffolding, envelope,
  AAD/path, and cryptography coverage: the required Contracts lane now guards
  the dedicated workflow; zeroing observations fail closed; cross-scope and
  wrapper-relocation paths run end to end; V017 offsets derive from frozen
  fixture fields; boundary and malformed-input matrices are complete; and the
  deterministic entropy seam is thread-safe beyond 255 fills.
- Closed the full-diff fourth-pass findings by parsing the active CI workflow
  step instead of searching comments, preserving caller cancellation over a
  simultaneous malformed-JSON failure, proving 300 deterministic entropy fills
  are distinct, correcting the replayable preservation check for the one
  Story-owned Contracts guard, and fixing out-of-scope baseline disclosure.
- Closed both fifth-pass group-1 codec findings by applying non-elidable
  cryptographic zeroing to all base64url staging arrays and deriving the
  protected JSON string label from its canonical UTF-8 wire bytes.
- Story 8.3 edits preserved Contracts, frozen fixtures/verifiers, Server/no-op
  hooks, domain and Parties code, `Hexalith.EventStore.slnx`,
  `tools/release-packages.json`, topology, and persisted data. The baseline diff
  also contains separately committed idempotency intent-adapter work,
  command-status client/contract/fake/test work including the AggregateActor
  no-op result preservation change, trusted-extension policy and sanitizer work,
  the previously disclosed FrontComposer gitlink advance, two later FrontComposer
  gitlink advances, and the later Hexalith.Builds gitlink advance to `000abf86`.
  Those excluded changes were not authored, altered, reverted, or approved by
  this workflow.
- Added blocking direct-project GitHub and local CI lanes with the exact
  290-case minimum and skipped-test failure policy, plus a required-lane test
  that binds the GitHub workflow contents.

## Content Binding

| Artifact | SHA-256 |
| --- | --- |
| Requirements amendment approval | `1d511941c09d12e1d3a09a82968fc82737dcd786b0b35082c75584b6e7358537` |
| Sorted production source/project hash stream (35 files) | `f6cf266c83d1d79ca23b5e92547aff5e908d29bea43e93ae36dac3c0434711f5` |
| Sorted focused-test/project/manifest hash stream (12 files) | `863412576147db58c24343dc0bd13c9359e98ea73dd5824360b0f8975d7c10dd` |
| Core project | `c73a8db3b4eb994adbbdf5bd90ea9e9d9bacac5b5ac9dd792ff3021f56e4fbe9` |
| Test project | `5b29d17454fd11c65965c6cc66deb70571f7c995d9512384f28b38d37185aed5` |
| Vector execution manifest | `dfa6a44ccdab595af6081d7b2c752e5f01870aeb74265d76940e43c5ba336ea8` |
| GitHub focused lane | `12344282faad41c129378a1cccab53bae6ee5f4f3f75a97be733653e9109edd7` |
| Local focused lane | `5c632674e428481c5f2e1f20a4316229659569b8cba152743b76a76703a6ec07` |
| Preflight evidence | `ab3a5d7ac7dc4838e91b0ecc4bc3d77438f22a545d25ec92cba9dcf89ff2c8a2` |
| Verification evidence | `78920ae9352068d6221063369a0d99f9d4b2163ce67e3f7c06f9f4986e9d51f3` |
| Unchanged release manifest | `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae` |
| Unchanged solution | `dd9c0a74a6ca81d50e05ddcfd336f4f92882d77afa313287d69cf2bc87e7fc74` |

Detailed source identity, inventory, tool versions, source rechecks, commands,
counts, and observed load values are in
`evidence/story-8-3/preflight.md` and `evidence/story-8-3/verification.md`.

## Verification Summary

- Both independent frozen-vector verifiers passed V001-V003 unchanged.
- An explicit focused Release build passed with zero warnings/errors, then the
  no-build gate passed 290/290 with zero failures or skips.
- The two follow-up regression methods passed 1/1 independently and as part of
  that complete focused-suite rerun.
- The core Release build passed with zero warnings and errors under the AOT and
  trim analyzers, which are enabled as project properties and therefore apply to
  every build of the core, including the blocking focused lane. Those properties
  do not propagate to project references, so the result covers the core's own
  compilation and not the frozen Contracts dependency, which does not satisfy
  those analyzers at HEAD. XML-documentation, repository style, and
  `.gitattributes` LF checks also passed.
- The complete `.slnx` Release build passed with zero warnings and errors.
- The direct GitHub workflow passed `actionlint` and the local mirror passed
  `bash -n`; both require all 290 cases and fail on skips. The required
  Contracts packaging-test class passed 115/115 and asserts the workflow's
  project path, floor, and fail-skips policy.
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

All surviving findings from the completed review layers, including the
full-diff fourth pass, were patched and reverified, and the carried FrontComposer
and Hexalith.Builds gitlinks remain explicitly out of scope. The fourth pass
supersedes the remaining partial-chunk plan, so this artifact records
independent-review closure for the Story 8.3 implementation. The human-
authorized V004 amendment-list and section 8.4 boundary corrections are
recorded in the amended `AR-20260914-02` packet and frozen requirements block.
No separate human completion approval beyond that requirements approval is
claimed. No package, provider, lifecycle, compatibility, Server/snapshot
integration, deployment, NIST CAVP certification, later vector, or G5 claim is
authorized by this artifact.
