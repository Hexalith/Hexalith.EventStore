# Evidence Redaction

The raw CTRF files were mechanically redacted after capture, and the exact solution build was redacted in its `pipefail`/`tee` pipeline. Only the permitted local-machine identifiers were changed:

| Captured value kind | Replacement |
| --- | --- |
| Machine name | `<redacted-machine>` |
| Local machine user reported by the test runner | `<redacted-machine-user>` |
| Absolute repository workspace prefix | `<workspace>` |

Test names, statuses, timestamps, durations, assertion messages, runtime versions, HTTP responses, Redis keys, contender identifiers, and captured state values were not changed. The two domain captures (`append-durability-race.json` and `generic-etag-control.json`) contained none of the redacted machine/workspace fields and were left unchanged.

Validation performed before hashing (the checked-in semantic validator repeats these checks):

```bash
python3 validate-evidence.py .
! rg -n '/home/[^/[:space:]]+/projects/hexalith/eventstore|"computer": "(?!<redacted-machine>)|"user": "(?!<redacted-machine-user>)' \
  . --glob '*.json' --glob '*.log' --pcre2
```

The scan is expected to return no matches from the evidence directory. `validate-evidence.py` additionally parses every CTRF receipt, verifies the exact positive and mutation summaries, checks the race/provider/ETag semantics, resolves every source hash against the repository, and verifies exact manifest coverage.
