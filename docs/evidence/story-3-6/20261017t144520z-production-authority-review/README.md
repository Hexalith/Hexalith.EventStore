# Production authority review follow-up

This packet preserves the original dated preparation and the actual pre-review
source bytes. The previous `20261017t142649z-production-authority-preparation`
receipt remains unchanged. Every previously hashed source matches its retained
`pre-review-sources/` copy; `pre-review-sources.json` records the relation.
The prior 187-test result is dated pre-review evidence, not final validation of
these revised sources.

The review corrections are:

- Token HTTP requests now complete at headers and consume at most 64 KiB plus
  one overflow-detection byte. A nonseekable one-MiB synthetic response proves
  the client refuses before consuming the whole body. Both profile and ordinary
  authority token paths use the same bounded reader.
- Validated metadata/JWKS occupy one trust-keyed snapshot with a five-minute
  refresh interval. Authority changes discard old discovery; normal token cache
  entries also bind the credential/trust configuration. Profile tokens remain
  uncached. Tests prove metadata reuse, controlled expiry refresh and two-authority
  credential rotation without delivering the new secret to the old endpoint.
- The issuer path must match the mapped prefix at startup, and path drift after
  mapping fails closed. Actual mapped token/discovery/JWKS endpoints carry three
  fixed pre-authentication bucket markers; both actual global limiters isolate
  those bounded buckets from unrelated anonymous traffic. Caller claims/form
  fields cannot create authority partitions or attest client authentication.
- Deterministic test clocks prove exact remaining grant, certificate and maximum
  token lifetime clipping. The test handler now uses the transmitted client
  secret. The real issuer talks to the mapped TestServer authority for correct
  secret acceptance and incorrect secret refusal.

`verification-final.json`, `build-final.txt`, `targeted-final-tests.txt` and
`reviewed-final-sources/` retain the final commands, source bytes/hashes and actual
results. Final Debug source-reference Server.Tests build passed with **zero
warnings/errors**. Only the three affected issuer/authority/global-limiter test
classes ran: **83 passed**, zero failures/skips. No wider suite was rerun by this
follow-up. The earlier passing 83-test result remains in `verification.json` with
its own snapshots; the final run additionally verifies bucket metadata on the
actual mapped routes.

`first-targeted-test-failure.txt` preserves the first oversize negative-case
failure: InvalidDataException escaped the fail-closed catch. The catch was fixed,
and the final result passed. An earlier local CA2007 compile failure was resolved
by synchronous disposal of the bounded response stream; no warning or check was
suppressed.

These tests use only synthetic certificates, grants, secrets and in-process HTTP.
They establish neither real TLS nor operational client identity, grants,
revocation history, deployment, legacy inventory, maintenance or persisted deployed
outcomes. No live application, permission, signing-enrollment or domain mutation
occurred. The deployment approval and Story 3.6 acceptance gates remain closed.
