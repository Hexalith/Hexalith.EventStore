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


def story_completion_facts(root, spec, target, head, tree, before, before_tree, entries):
    """Bind synthetic HTTP facts to real private Git inputs, never business PASS."""
    import phase_4_story_5_3 as A
    import phase_4_story_completion as S
    import phase_4_transition as C

    prefix = 'https://api.github.com/repos/' + C.REPOSITORY
    state = {'observed': [], 'responses': {}}
    def put(path, value):
        state['responses'][prefix + path] = {'json': value}
    def binding(path):
        content = (root / path).read_bytes()
        put('/contents/' + path + '?ref=' + head,
            {'type': 'file', 'path': path, 'encoding': 'base64', 'size': len(content),
             'content': base64.b64encode(content).decode(), 'sha': A.blob_sha(content)})
        return {'path': path, 'sha256': C.digest(content)}

    # Refuse invented fixture identities before they can serve as HTTP facts.
    C.require(A.object_sha('commit', A.verified_object(root, 'commit', head)) == head
              and A.object_sha('tree', A.verified_object(root, 'tree', tree)) == tree
              and A.git(root, 'rev-parse', head + '^{tree}').decode().strip() == tree
              and A.git(root, 'rev-parse', before + '^{tree}').decode().strip() == before_tree,
              'SOURCE_MISMATCH', 'synthetic HTTP facts require real private Git identities')
    C.require(A.index_entries(root) == A.validate_entries(entries), 'SOURCE_MISMATCH', 'private HTTP membership differs from index')
    local = C.Control.__new__(C.Control)
    local.root = root
    claim = S.derive(local.local, spec, target)
    request = {'schema': C.REQUEST_SCHEMA, 'repository': C.REPOSITORY, 'transition': 'high-risk-nfr-story-done',
               'head_sha': head, 'subject_mode': 'files', 'subjects': [binding(path) for path in (S.PRD, S.EPICS, S.TRACKER, spec)],
               'evidence': [], 'matrix': binding(C.MATRIX), 'registry': binding(C.REGISTRY),
               'required_level': 'single-maintainer-attested', 'author': 'github:jpiquot', 'gate': None,
               'nfr_ids': claim['nfr_ids'], 'approvals': [], 'predecessors': [], 'story': claim}
    for path in (*C.CONTROL_FILES, *C.STORY_CONTROL_FILES, C.workflow_path(request['transition'])):
        binding(path)
    put('/git/ref/heads/main', {'object': {'sha': head}})
    put('/git/commits/' + head, {'sha': head, 'tree': {'sha': tree}})
    put('/git/commits/' + before, {'sha': before, 'tree': {'sha': before_tree}})
    put('/git/trees/' + tree + '?recursive=1', {'sha': tree, 'truncated': False, 'tree': [
        {'path': row['path'], 'mode': row['mode'], 'sha': row['git_sha'], 'type': 'commit' if row['mode'] == '160000' else 'blob'} for row in entries]})
    put('/activity?ref=refs%2Fheads%2Fmain&per_page=100', [{'ref': 'refs/heads/main', 'activity_type': 'push',
        'after': head, 'before': before, 'timestamp': '2026-01-01T00:00:00Z'}])
    subject = C.digest(C.canonical({'adapter': A.ADAPTER, 'root_tree': tree}))
    bound = C.approval_request_digest(request)
    for index, role in enumerate(('product-owner', 'test-owner')):
        body = C.canonical({'schema': C.APPROVAL_SCHEMA, 'repository': C.REPOSITORY, 'transition': request['transition'],
            'head_sha': head, 'role': role, 'subject_sha256': subject, 'request_sha256': bound,
            'required_level': request['required_level'], 'traceability_sha256': None, 'decision': 'approved'}).decode()
        request['approvals'].append({'comment_id': 100 + index, 'body_sha256': C.digest(body.encode()), 'role': role})
        put('/issues/comments/' + str(100 + index), {'id': 100 + index, 'body': body,
            'user': {'id': 6775094, 'login': 'synthetic-owner-fixture', 'type': 'User'},
            'created_at': '2026-01-02T00:00:00Z', 'updated_at': '2026-01-02T00:00:00Z',
            'issue_url': prefix + '/issues/42', 'performed_via_github_app': None})
    workflow = C.workflow_path(request['transition'])
    put('/actions/runs/7/attempts/2', {'id': 7, 'run_attempt': 2, 'event': 'workflow_dispatch', 'head_sha': head,
        'head_branch': 'main', 'repository': {'full_name': C.REPOSITORY}, 'head_repository': {'full_name': C.REPOSITORY},
        'status': 'completed', 'conclusion': 'success', 'path': workflow, 'workflow_id': 12, 'check_suite_id': 13})
    put('/actions/workflows/12', {'id': 12, 'path': workflow, 'state': 'active'})
    put('/check-suites/13', {'id': 13, 'head_sha': head, 'status': 'completed', 'conclusion': 'success',
        'app': {'id': C.ACTIONS_APP_ID, 'slug': 'github-actions'}})
    put('/actions/runs/7/attempts/2/jobs?per_page=100', {'total_count': 1, 'jobs': [
        {'id': 14, 'name': 'Phase 4 transition attempt', 'run_id': 7, 'head_sha': head, 'status': 'completed',
         'conclusion': 'success', 'check_run_url': prefix + '/check-runs/15'}]})
    put('/check-suites/13/check-runs?per_page=100', {'total_count': 1, 'check_runs': [
        {'id': 15, 'url': prefix + '/check-runs/15', 'head_sha': head, 'app': {'id': C.ACTIONS_APP_ID},
         'check_suite': {'id': 13}, 'status': 'completed', 'conclusion': 'success'}]})
    return C.validate_request(request), state


def bind_story_candidate(state, request, candidate):
    """Package the just-evaluated candidate; never manufacture a passing report."""
    import io
    import zipfile
    import phase_4_story_5_3 as A
    import phase_4_transition as C

    C.require(candidate.get('phase') == 'evaluate' and candidate.get('result') == 'PASS' and candidate.get('seal') is None
              and candidate.get('binding', {}).get('head_sha') == request['head_sha']
              and candidate['binding'].get('request_sha256') == C.digest(C.canonical(request))
              and candidate['binding'].get('story') == request['story'],
              'RESULT_MISMATCH', 'synthetic artifact must bind the genuine private candidate')
    A.validate_report(candidate['story_evidence'], candidate['binding']['story_adapter'])
    C.require(candidate['story_evidence']['result'] == 'PASS', 'STORY_EVIDENCE_FAILED', 'failed business evidence cannot become a passing artifact')
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr('result.json', C.canonical(candidate))
    content = buffer.getvalue()
    prefix = 'https://api.github.com/repos/' + C.REPOSITORY
    state['responses'][prefix + '/actions/runs/7/artifacts?per_page=100'] = {'json': {'total_count': 1, 'artifacts': [
        {'id': 16, 'name': 'phase-4-transition-result-7-2', 'expired': False, 'size_in_bytes': len(content),
         'workflow_run': {'id': 7, 'head_sha': request['head_sha']}, 'digest': 'sha256:' + C.digest(content)}]}}
    state['responses'][prefix + '/actions/artifacts/16/zip'] = {'binary': base64.b64encode(content).decode()}
