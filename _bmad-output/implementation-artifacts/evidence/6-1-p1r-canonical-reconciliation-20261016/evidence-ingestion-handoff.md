# Evidence-ingestion landing blocker

The 34 delivered source paths pass the unchanged canonical reader/scanner,
including every untracked source and the four dated helpers. The independently
run canonical secret/evidence controls pass 778 selected cases. Source production,
security logic, approved retirement records and their hashes are unchanged.

A separate [refreshed audit of the new evidence bundle](refreshed-evidence-ingestion-audit.json)
invoked the **actual compiled canonical `ScanTrackedFile`** method through
reflection, passing the original repository root explicitly. The probe uses the
canonical reader, decoder, semantic recognition and scanner; it adds no allowlist
or retirement. The first audit examined 192 files and found six flags. After preserving the
pre-refresh captures and refreshing canonical source, the latest audit examined
308 files and found nine flags, including those preserved copies:

- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/pre-canonical-refresh-release-analysis/registry-observations.json:572`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/pre-canonical-refresh-reviewed-reconciliation.diff:211`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/pre-canonical-refresh-reviewed-reconciliation.diff:6310`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/release-analysis/registry-observations.json:572`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/review-v1.diff:193`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/review-v1.diff:6287`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/review-v1.diff:6288`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/reviewed-reconciliation.diff:211`
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/reviewed-reconciliation.diff:6310`

The raw outputs and reviewed diffs remain byte-preserved. The original hardening,
recovery and enrollment bundles were not rewritten or retired. This audit covers
this new package, not a claim that every historical bundle would pass newly
tracked ingestion. Its count is a capture-time inventory; later index/handoff
files are separately bound by the final delivery manifest.

**Do not bulk-land the evidence directory or assert clean-checkout readiness.**
Before adding these captures as tracked reusable text, obtain an explicit,
reviewed disposition under the existing evidence contract. It must preserve raw
historical bytes and canonical detection guarantees. A new filename, historical
label, blanket exclusion, re-pinned hash, silent binary conversion or invented
retirement approval is not a disposition. If the authorized landing delivers the
34 source paths separately from externally retained evidence, explicitly name
that boundary and bind the retained evidence hashes; do not represent an
unscanned bulk evidence import as passing.

Then repeat the actual tracked-file ingestion gate on the exact authorized
landing and all exact-source CI. No acceptance, approval, source freeze,
publication or Story 6.1 readiness is granted by this source-only work package.
