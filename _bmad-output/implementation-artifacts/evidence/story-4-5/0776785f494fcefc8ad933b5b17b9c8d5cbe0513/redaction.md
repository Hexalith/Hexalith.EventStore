# Evidence Redaction

The raw CTRF files were mechanically redacted after capture, and the exact solution build was
redacted inside its `pipefail`/`tee` pipeline. Only the permitted local-machine identifiers were
changed:

| Captured value kind | Replacement |
| --- | --- |
| Machine name | `<redacted-machine>` |
| Local machine user reported by the test runner | `<redacted-machine-user>` |
| Absolute repository workspace prefix | `<workspace>` |

Test names, statuses, timestamps, durations, assertion messages, runtime versions, HTTP responses,
Redis keys, contender identifiers, and captured state values were not changed. The two domain
captures (`append-durability-race.json` and `generic-etag-control.json`) contained none of the
redacted machine/workspace fields and were left unchanged; `validate-evidence.py` additionally
asserts that each is byte-for-byte identical to the copy embedded in the CTRF receipt of the run
that produced it, so a redaction pass could not silently alter them.

Validation performed before hashing (the checked-in semantic validator repeats these checks):

```bash
python3 validate-evidence.py .
```

The redaction scan must distinguish "no match" from "the scan could not run". `rg` exits `1` when it
finds nothing and `2` when the scan itself failed (bad glob, missing PCRE2, unreadable file); a bare
`! rg …` inverts both into success and would report the directory clean having scanned nothing. The
scan in `commands.md` branches explicitly on `0`/`1`/other and is expected to report
`Redaction scan clean`.

`validate-evidence.py` additionally parses every CTRF receipt, verifies the exact positive and
mutation summaries, requires each mutation receipt's embedded capture to name the perturbation that
was armed and to falsify exactly the invariant set that perturbation is pinned to, checks the
race/provider/ETag semantics, pins the exact invariant key sets, resolves every source hash against
the repository, requires every evidence-relevant source to be bound, verifies redaction, and
verifies exact manifest coverage. It refuses to run under `python -O`, where `assert` statements
would be stripped.
