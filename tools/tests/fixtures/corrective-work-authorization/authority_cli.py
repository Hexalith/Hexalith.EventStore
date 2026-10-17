"""Run the actual isolated CLI with a disposable, synthetic GitHub HTTP transport.

This fixture-only launcher intercepts the standard-library HTTPS opener. The
production validator has no fixture flag, endpoint override, or approval bypass.
Every request must use its fixed read-only GitHub endpoint and normal checks.
"""

import io
import datetime
import json
from pathlib import Path
import runpy
import sys
import urllib.error
from unittest import mock


state_path, validator, *arguments = sys.argv[1:]


class AuthorityTransport:
    def __init__(self):
        self.approval_reads = 0

    def open(self, request, timeout):
        state = json.loads(Path(state_path).read_text())
        if state.get("http_failure"):
            raise urllib.error.URLError("synthetic unavailable authority")
        url = request.full_url
        if request.get_method() != "GET" or timeout != 20 or url not in state["responses"]:
            raise RuntimeError("unexpected authority request: " + url)
        rows = state["responses"][url]
        if "/issues/11/comments?per_page=100&page=1" in url:
            self.approval_reads += 1
            if state.get("revocation_fetched_now") or (state.get("revoke_on_second_approval_read") and self.approval_reads >= 2):
                approval = next(row for row in rows if json.loads(row["body"]).get("schema") == "hexalith.corrective-work-approval.v1")
                revocation = dict(approval)
                payload = json.loads(approval["body"])
                payload["schema"] = "hexalith.corrective-work-revocation.v1"
                revocation["body"] = json.dumps(payload, sort_keys=True, separators=(",", ":"))
                stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
                revocation["created_at"] = revocation["updated_at"] = stamp
                rows = rows + [revocation]
        if state.get("concurrent_write"):
            Path(state["concurrent_write"]).write_text("concurrently changed\n")
        return io.BytesIO(json.dumps(rows).encode())


sys.argv = [validator, *arguments]
with mock.patch("urllib.request.build_opener", return_value=AuthorityTransport()):
    runpy.run_path(validator, run_name="__main__")
