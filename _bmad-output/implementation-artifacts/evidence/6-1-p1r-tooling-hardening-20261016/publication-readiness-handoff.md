# P1R tooling hardening: reconciliation and publication handoff

This is a source-only hardening package. P1R remains unusable and Story 6.1
remains blocked. No package was selected, reserved, published or qualified. No
commit, index, branch, gitlink, acceptance record, dependency pin or sprint state
was changed. The separate Projects Story 6.1 draft and all completed recovery and
historical evidence retain their starting bytes.

The [new reviewed closure](reviewed-source-hardening.json) supersedes the recovered
tooling bytes only for this work package. It includes the recovered 23-path
closure plus the new lifecycle validator, Docker boundary fixture and hardening
suite. The original recovery/enrollment manifests remain unchanged historical
records. [Fresh verification](parent-verification-v4/verification.json), its
before/after source bindings, [independent review](independent-review.md),
[preservation](final-preservation.json) and [delivery manifest](delivery-manifest.json)
define this package's evidence boundary. Earlier attempts, including the
verification-driver correction, remain retained and do not substitute for the
final run. The successful [post-review live smoke](parent-verification-v3/live-isolation-smoke.json)
uses the same runtime/fixture bytes as the final closure; only coherent negative
test controls changed afterward, as verified by the
[source applicability comparison](final-source-applicability.json).

The existing [recovery handoff](../6-1-p1r-recovery-20261015/README.md) and
[publication handoff](../6-1-p1r-successor-enrollment-20261014/publication-readiness-handoff.md)
continue to govern qualification and authority. The following is a concrete
sequence for separately authorized reconciliation and publication work.

## Reconcile canonical source before selecting a publication identity

Starting EventStore HEAD is `247a192274d0ab01b81b8f5430d6293c3bdbda95`.
Starting Projects HEAD is `2b7edae27175fa5921de5768d9580e16d615db34`.
The locally available EventStore `origin/main` reference is
`746b843a24f18b17a7572be25b76b735fa8e4ca6`; this package did not fetch or claim
that reference is the current live main. Literal observations are retained in
[canonical reconciliation evidence](canonical-reconciliation-observation.json).

At authorized landing, first observe then-current canonical main. Reconcile the
new reviewed closure with that source, preserving canonical production changes,
recovery history, all prior failed attempts, snapshot controls and private
isolation. Do not replace main with the old recovery branch or blindly overwrite
canonical files. Review canonical security/evidence-ingestion additions and the
generated negative PostgreSQL credential fixtures in both
`tools/tests/test_p1r_published_executor.py` and
`tools/tests/test_p1r_published_qualification.py`; the recovery's
[applicability analysis](../6-1-p1r-recovery-20261015/canonical-applicability.json)
identifies the known overlap. Run canonical secret/evidence-recognition tests
against the reconciled delivery without rewriting historical bytes.

Land all three new Python files and the new preparation-CI steps. The four dated
audit helper dependencies listed in the unchanged
[CI dependency manifest](../6-1-p1r-successor-enrollment-20261014/required-ci-test-dependencies.json)
must also be present at their exact paths and hashes in a clean checkout.
Re-freeze every changed source byte and repeat source verification after
reconciliation; this dirty local package is not a frozen publication source.

The old `3.122.1` enrollment remains a finite proposal. It is not a reservation or
an observed successor selection. Re-run canonical release analysis and registry
version-floor/absence checks for the actual authorized landing. If the resulting
version differs, explicitly reconcile finite consumer/compiler/witness/importer
enrollment and repeat its controls; arbitrary versions remain refused.

## Required exact-source verification and authority

Freeze full source `S`, its Builds gitlink `Btag`, construction SDK/Builds/props
identities and separately approved publisher Builds execution identity `Bexec`.
Record actual Git objects and archive hashes rather than inferred version
labels. The tracked release execution currently names
`22a578b576a515d2af214fe81859447fffc97981`; it is a distinct publisher identity,
not an assertion about the future tag's Builds gitlink.

From the reconciled EventStore root, run the five suites individually and retain
their exact source bindings:

```bash
python3 -m unittest discover -s tools/tests -p 'test_p1r_qualification.py'
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_qualification.py'
python3 -m unittest discover -s tools/tests -p 'test_p1r_published_executor.py'
python3 -m unittest discover -s tools/tests -p 'test_p1r_isolation.py'
python3 -m unittest discover -s tools/tests -p 'test_p1r_isolation_hardening.py'
python3 -O -m unittest discover -s tools/tests -p 'test_p1r_isolation_hardening.py'
```

Require successful push `ci.yml`, `p1r-qualification.yml` and Commitlint for the
same current-main `S`, including the canonical .NET/package/consumer gates.
Local controls and historical green CI grant no exact-source publication
approval. The required plain Aspire startup was independently observed to build
but exit 134 because nested Tenants hosts are absent; its exact command and logs
are in [the baseline receipt](aspire-baseline.json). No nested submodule was
initialized. Resolve clean-checkout provisioning through approved root-declared
dependencies in the later environment rather than relaxing that gate.

Publication remains blocked: the unchanged release workflow sets
`HEXALITH_RELEASE_REQUIRE_AUTHORITY=false`. A separate reviewed change must wire
the reserved version, real authority issue/owner and identity-bound one-use
authority, including refusal of missing, mismatched, expired and replayed
authority. Require protected production approval, publication enablement,
publisher credentials/OIDC, authenticated registry checks, exact-source CI and
`bypass-validation=false`. Consume authority before the first package write.
This work package neither enables nor dispatches publication.

## Published successor selection and qualification

After authorized publication, independently observe release/run/attempt, tag
object and peeled source, all fourteen NuGet package identities, signatures,
archive hashes and repository commits, plus OCI multi-platform/child digests
and provenance. Obtain explicit owner selection of the newly observed tuple and
its exact input bytes. Future source/tag, archives and Builds identities are
unobserved here. Never replace an existing published version's bytes.

Qualification must use a new invocation and the isolated wrapper. The supported
controller profile is a local Unix socket; TCP/TLS/SSH controllers are explicitly
refused before provisioning. Canonical Docker Desktop bind-source translation
is supported through exact requested mounts, stable canonical nonsocket engine
paths and the in-SDK source closure. Private Docker start plus readiness has one
30-second monotonic deadline and probes of at most two seconds. The unchanged
tool images supply SDK 10.0.401, Docker 29.8.2 and Python 3.12.12; the recorded
Docker/dotnet binary guards derive from their actual source-only live smoke.

Preserve rollback `null`, comparisons `3.70.1`/`3.110.0`, Dapr `1.18.2`, and the
complete PostgreSQL v1 profile with
`postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
Require all seventeen canonical lanes, adopted Reminder recovery and logical
event evolution, genuine nine/manual-version-one/three-tail snapshot proof,
fresh persisted restore, append/restart thirteen, floor five, original hashes,
second-tenant preservation and corruption/protection/uncovered controls. Retain
single-writer drain, mutation freeze, forward recovery, complete shared
inventory equality, every failed attempt and exact owned cleanup evidence.

The final isolation importer must accept newly persisted receipts from the real
controller only after successful private and outer cleanup. It now checks the
literal pinned image/create/copy/launch journal, exact input bytes, tool/process
identities, distinct ordered observations and absence of unrelated mutations.
Synthetic tests and the live Python smoke remain source-only controls.

EventStore Owner, Builds Owner, Solution Architect and Test Architect must decide
and provide conformance for the same exact published tuple. The Solution
Architect's support-boundary disposition remains pending. Independent
Projects P1R acceptance is still required; this package changes no acceptance
state.

Remaining sequence: **published successor qualification and P1R acceptance →
P0/P2 → P3 → architect sign-off → P4 → independent READY → Story 6.1
implementation**.
