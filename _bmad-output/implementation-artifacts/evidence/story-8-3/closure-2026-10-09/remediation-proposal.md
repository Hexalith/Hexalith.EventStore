# Closure blocker: sealed capture remediation

The required repository secrets scan failed on 15 locations: 13 fixture/test-metadata false positives and two OTLP authentication headers in sealed Story 6.6 captures. The precise paths and current SHA-256 identities are recorded in `supplemental-scan-blocker.json`. No credential values are reproduced here. Both captures still match their seals; their current lifetime is unverified.

Proposed action requiring owner direction: produce sanitized replacement captures under a new remediation directory, replace only the two authentication-header values with explicit inert placeholders, and record original/replacement hashes and the exact transformation. Retire the original reusable captures through an explicit Story 6.6 evidence disposition, retaining provenance in Git history. Do not silently rewrite their seals or represent sanitized captures as the originally reviewed bytes. Preserve all Story 8.2/8.3 frozen approvals and authority.

Then fix the scanner false positives narrowly with negative controls, rerun the scanner and affected Server tests, and repeat final source/dependency reconciliation. Token rotation or an external revocation action would require separate authorization if the captured credentials remain accepted.

Story 8.3 remains `in-review` / `in-progress` until the failing repository check has an approved resolution and final verification passes. No Story 8.4 authorization is inferred; Parties 8.7 stays blocked until G5 closes.
