# Story 3.15 owner validation comment draft

**Local draft only. Do not post as an acceptance.** Issue `#352` has no comment naming the
current subject as of the read-only 2026-09-26 check. The EventStore-owner and Release-owner
decisions are still missing. The Test Architect independently declined this subject, so the
three-receipt gate cannot pass on the present evidence.

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

The JSON acceptance templates in the review brief are deliberately unposted. A later accepted
owner comment must be composed at posting time, then retained only if GitHub reports
`created_at == updated_at == accepted_at` and the authenticated author matches the roster.
