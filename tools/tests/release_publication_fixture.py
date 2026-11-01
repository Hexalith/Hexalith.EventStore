"""Local HTTP boundary for exercising the unmodified approved publisher engine."""

import json
import os
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


class Response:
    def __init__(self, document, status=200):
        self.status = status
        self.headers = {}
        self.document = json.dumps(document).encode()

    def __enter__(self):
        return self

    def __exit__(self, *arguments):
        return False

    def read(self, size=-1):
        return self.document if size == -1 else self.document[:size]


class LocalGitHubAndDestinations:
    def open(self, request, timeout=30):
        path = Path(os.environ["PUBLICATION_FIXTURE_STATE"])
        state = json.loads(path.read_text())
        url = urllib.parse.urlsplit(request.full_url)
        endpoint = url.path
        method = request.get_method()
        state["requests"].append({"method": method, "url": request.full_url})
        status = 200
        if "/git/ref/heads/" in endpoint:
            branch = urllib.parse.unquote(endpoint.split("/git/ref/heads/", 1)[1])
            if branch not in state["refs"]:
                raise urllib.error.HTTPError(request.full_url, 404, "unknown fixture ref", {}, None)
            document = {"object": {"sha": state["refs"][branch]}}
        elif "/actions/workflows/" in endpoint:
            workflow = endpoint.split("/workflows/")[1].split("/")[0]
            document = {"workflow_runs": state["runs"].get(workflow, [])}
        elif endpoint.endswith("/comments"):
            if method == "POST":
                document = {
                    "id": 900 + len(state["comments"]),
                    "body": json.loads(request.data)["body"],
                    "user": {"login": "github-actions[bot]"},
                }
                state["comments"].append(document)
                status = 201
            else:
                document = state["comments"]
        elif endpoint.endswith("/releases"):
            document = [{"tag_name": "v1.0.0"}]
        elif endpoint.endswith("/tags"):
            document = [{"name": "v1.0.0"}]
            if state.get("self_tag"):
                document.append({"name": "v" + state["version"], "commit": {"sha": state["source_sha"]}})
        elif endpoint.endswith("/index.json"):
            document = {"versions": ["1.0.0"]}
        elif endpoint.endswith("/tags/list"):
            document = {"tags": ["1.0.0"]}
            status = state.get("registry_status", 200)
        elif method == "HEAD":
            document = {}
            status = state.get("absence_status", 404)
        else:
            raise AssertionError(f"Unexpected local HTTP fixture endpoint: {method} {endpoint}")
        path.write_text(json.dumps(state))
        if status in {401, 403, 500}:
            raise urllib.error.HTTPError(request.full_url, status, "fixture refusal", {}, None)
        return Response(document, status)


def install():
    """Replace transport only; identity, authority, destination and consumption logic are real."""
    urllib.request.build_opener = lambda *handlers: LocalGitHubAndDestinations()


def gh_main():
    """Expose the same local read-only GitHub state to the source-proof shell."""
    import sys
    path = Path(os.environ["PUBLICATION_FIXTURE_STATE"])
    state = json.loads(path.read_text())
    endpoint = next(argument for argument in sys.argv[1:] if argument.startswith("repos/"))
    state["requests"].append({"method": "GET", "url": endpoint})
    path.write_text(json.dumps(state))
    if "/git/ref/heads/" in endpoint:
        branch = urllib.parse.unquote(endpoint.split("/git/ref/heads/", 1)[1])
        if branch not in state["refs"]:
            raise SystemExit("Unknown fixture ref: " + branch)
        print(state["refs"][branch])
    else:
        workflow = endpoint.split("/workflows/")[1].split("/")[0]
        print(json.dumps({"workflow_runs": state["runs"].get(workflow, [])}))
