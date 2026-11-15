"""HTTP-only substitution for real transition CLI subprocesses; no verifier bypass."""

import base64
import json
import os
from pathlib import Path
import urllib.error
import urllib.request


class Response:
    def __init__(self, content, headers=None):
        self.content, self.headers, self.status = content, headers or {}, 200

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False

    def read(self, size=-1):
        return self.content if size == -1 else self.content[:size]


class Transport:
    def open(self, request, timeout=20):
        path = Path(os.environ['PHASE4_FIXTURE_STATE'])
        state = json.loads(path.read_text())
        state['observed'].append({'url': request.full_url, 'method': request.get_method(),
                                  'headers': {key.lower(): value for key, value in request.header_items()}})
        response = state['responses'].get(request.full_url)
        if response is not None and 'sequence' in response:
            response = response['sequence'].pop(0)
        path.write_text(json.dumps(state))
        if response is None:
            raise urllib.error.HTTPError(request.full_url, 404, 'absent fixture endpoint', {}, None)
        if 'error' in response:
            raise urllib.error.HTTPError(request.full_url, response['error'], 'fixture API failure', response.get('headers', {}), None)
        content = base64.b64decode(response['binary']) if 'binary' in response else json.dumps(response['json']).encode()
        return Response(content, response.get('headers'))


def install():
    urllib.request.build_opener = lambda *handlers: Transport()
