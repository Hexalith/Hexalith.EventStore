# Story 3.15 owner validation comment drafts

**Historical request for superseded subject `c98fdef2...`; not an acceptance.** The request below
was posted byte-for-byte as
[issue `#352` comment `5844166955`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844166955)
at `2026-09-26T07:11:49Z` by authenticated `github:jpiquot` (`id: 6775094`). The posted body has
SHA-256 `2be14c91009792f502ea5528720fb1d0173d976bc4fbf356167efbe3ea393167`, matching the
validated draft. No EventStore-owner or Release-owner decision was issued for this subject. The
Test Architect independently declined it, and it was superseded by `66be1b4a...`, which later
validated at 3/3.

## Proposed validation request text

> Please review Story 3.15 corrected deployed-runtime parity for exact subject
> `c98fdef266671a8b35e05c64b95eed275cb506e4368e28ab6957aa7750640df3`.
> The review brief is
> `_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-acceptance-review.md`;
> the proof packet is
> `_bmad-output/implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure-proof-packet.md`.
> The claimed OCI index is
> `registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
> The verifier currently rejects closure at 0 of 3 receipts, so no deployed identity is selected.
>
> The two owner roles require **separate** decisions from rostered `github:jpiquot` and, if
> accepted, separate canonical comments on this issue. Each decision must bind the exact subject,
> the scope “Story 3.15 corrected deployed-runtime parity for
> `c98fdef266671a8b35e05c64b95eed275cb506e4368e28ab6957aa7750640df3`,” and these four
> limitations:
>
> 1. This packet supplies immutable deployed-runtime parity evidence only.
> 2. It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.
> 3. The Test Architect acceptance is a self-attested BMAD record without independent external authentication.
> 4. Every acceptance receipt is composed by repository tooling and posted with the rostered role holder's credential, not typed by hand.
>
> The independent Test Architect review **declined** the current subject. The Production smoke
> observations were captured before the `curl -q` correction, and the retained records do not
> preserve the original curl arguments or `.curlrc`. The reviewer also found that limitation 4
> describes every receipt as credential-posted while the Test Architect source is a local
> self-attested record. Neither a prior receipt nor an owner decision resolves those blockers.
>
> Please state separate decisions as `EventStore owner: accept/decline` and
> `Release owner: accept/decline`. If you want the Test Architect blockers corrected, authorize a
> fresh two-platform Production smoke capture and a controlled correction of limitation 4. Both
> changes will re-mint the subject, so every acceptance for the current subject would become
> superseded and new role decisions would be needed for the new subject.

This posted request names the old subject and old limitation 4. It does not solicit or authorize
acceptance of current subject `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`.
The fresh independent Test Architect
[decision report](3-15-test-architect-decision-66be1b4a.md) accepts the technical evidence for
the new subject, but is not a packet receipt. The user approved the new-subject materials and
separately authorized credentialed owner actions. The request below was posted byte-for-byte as
[issue `#352` comment `5844480896`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844480896)
at `2026-09-26T08:06:20Z` by authenticated `github:jpiquot` (`id: 6775094`). Its body has SHA-256
`a4db1c268bd210d8db063a6bc778728ecb5ae4687d05bd89d8a003f7f29e193c`, matching the
approved draft, and GitHub reports equal `created_at` and `updated_at` timestamps. It is a review
request, not an acceptance. The owner subsequently gave separate accept decisions for both roles.
Canonical [EventStore-owner comment `5844573563`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844573563)
and [Release-owner comment `5844574016`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844574016)
were posted with authenticated `github:jpiquot` at `08:22:25Z` and `08:22:30Z`, respectively;
each has `created_at == updated_at == accepted_at`. Their closed-schema envelopes and receipts are
retained under the current subject. The independent Test Architect ACCEPT report was transcribed
into the local self-attested source at `08:23:47Z`. The assembler and retained verifier pass at
3/3 receipts for the unchanged subject.
GitHub rendered the request's relative document links as issue-relative links. A separate
[link correction](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844496516)
was posted at `2026-09-26T08:09:02Z` with commit-pinned review-file links; its body has SHA-256
`461a92deeab4b3ed6e32dc014c2b94ab2b29106e34c23061daa5e3d8f0706731` and matches the
posted draft. The original review request remains intact. The link correction is not an acceptance.

## Posted new-subject owner review request

> Please review Story 3.15 corrected deployed-runtime parity for exact subject
> `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`.
> The [review brief](3-15-corrected-deployed-runtime-parity-acceptance-review.md),
> [proof packet](3-15-corrected-deployed-runtime-parity-closure-proof-packet.md), and
> [independent Test Architect decision](3-15-test-architect-decision-66be1b4a.md) describe the
> new evidence. The claimed OCI index is
> `registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
> The verifier currently rejects closure at 0 of 3 packet-bound receipts, so no deployed identity
> is selected and none of the four operational-authority flags is granted.
>
> The two owner roles require separate decisions from rostered `github:jpiquot` and, if accepted,
> distinct canonical issue `#352` comments. Each decision must bind this exact subject, the scope
> “Story 3.15 corrected deployed-runtime parity for
> `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`,” and these four
> limitations:
>
> 1. This packet supplies immutable deployed-runtime parity evidence only.
> 2. It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.
> 3. The Test Architect acceptance is a self-attested BMAD record without independent external authentication.
> 4. The two owner acceptance comments are composed by repository tooling and posted with the rostered role holder's credential, rather than typed by hand; the Test Architect source is a local self-attested BMAD record.
>
> The Test Architect independently accepted the technical evidence in a local self-attested
> decision report. That report is not a packet receipt or independent external authentication.
> Please state separate decisions as `EventStore owner: accept/decline` and
> `Release owner: accept/decline`. A decision for `c98fdef2...` or an older subject cannot be used
> for this subject.

The JSON acceptance templates in the current review brief remain historical review inputs; the
actual canonical comments are the two links above. No earlier-subject acceptance was reused.
