#!/usr/bin/env python3
"""Read-only, post-build comparison; never reconstructs pre-build observations."""
import datetime
import difflib
import hashlib
import json
import os
from pathlib import Path
import stat
import sys
import tarfile


class ValidationFailure(Exception):
    pass


def require(condition, message):
    if not condition:
        raise ValidationFailure(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def compare(archive, current, allowed):
    """Reject additions, removals, types, modes or bytes outside exact allowlist."""
    require(set(current) - set(archive) == set(allowed) - set(archive), 'unexpected added/missing source paths')
    require(not set(archive) - set(current), 'removed archive paths')
    changes = []
    for name, observed in current.items():
        expected = archive.get(name)
        if expected != observed:
            require(name in allowed and observed == allowed[name], 'unexpected source difference: ' + name)
            if expected is not None:
                require(observed['kind'] == expected['kind'] and observed['mode'] == expected['mode'], 'changed source type/mode: ' + name)
            changes.append({'path': name, 'archive': expected, 'current': observed})
    require({x['path'] for x in changes} == set(allowed), 'expected source differences missing')
    return sorted(changes, key=lambda item: item['path'])


def observe(packet):
    identity = json.loads((packet / 'final-runtime-identity.json').read_text())
    acquisition = json.loads((packet / 'runtime-acquisition.json').read_text())
    archive_path = Path(next(x['path'] for x in acquisition['downloads'] if x['label'] == 'dapr-source'))
    source = Path(acquisition['workDirectory']) / ('dapr-' + identity['sourceRevision'])
    require(sha(archive_path) == identity['sourceArchiveSha256'], 'source archive identity mismatch')
    archive = {}
    archive_modes = {}
    original_router = None
    with tarfile.open(archive_path) as tar:
        for member in tar:
            parts = Path(member.name).parts
            require(parts and parts[0] == source.name and '..' not in parts, 'unexpected archive path')
            if len(parts) == 1:
                continue
            name = Path(*parts[1:]).as_posix()
            require(name not in archive, 'duplicate archive path: ' + name)
            archive_modes[name] = stat.S_IMODE(member.mode)
            entry = {'mode': stat.S_IMODE(member.mode) & ~0o022}
            if member.isdir():
                entry['kind'] = 'directory'
            elif member.isfile():
                data = tar.extractfile(member).read()
                entry.update(kind='file', sha256=digest(data), bytes=len(data))
                if name == 'pkg/actors/router/router.go':
                    original_router = data
            elif member.issym():
                entry.update(kind='symlink', target=member.linkname)
            else:
                raise ValidationFailure('unsupported archive entry: ' + name)
            archive[name] = entry
    current = {}
    for path in sorted(source.rglob('*')):
        st = path.lstat()
        entry = {'mode': stat.S_IMODE(st.st_mode)}
        if stat.S_ISLNK(st.st_mode):
            entry.update(kind='symlink', target=os.readlink(path))
        elif stat.S_ISDIR(st.st_mode):
            entry['kind'] = 'directory'
        elif stat.S_ISREG(st.st_mode):
            entry.update(kind='file', sha256=sha(path), bytes=st.st_size)
        else:
            raise ValidationFailure('unsupported source entry: ' + str(path))
        current[path.relative_to(source).as_posix()] = entry
    allowed = {}
    for name, retained in {
        'router.go': 'router-final-candidate.go.source',
        'local_recovery_test.go': 'router_recovery_test.go.source',
        'local_cancellation_test.go': 'router_cancellation_test.go.source',
    }.items():
        relative = 'pkg/actors/router/' + name
        data = (packet / retained).read_bytes()
        # Added tests are data-only source inputs; pin their actual 0644 mode too.
        mode = archive[relative]['mode'] if relative in archive else 0o644
        allowed[relative] = {'kind': 'file', 'mode': mode, 'sha256': digest(data), 'bytes': len(data)}
    require(original_router is not None, 'archive router missing')
    patched = (packet / 'router-final-candidate.go.source').read_bytes()
    patch = ''.join(difflib.unified_diff(original_router.decode().splitlines(True), patched.decode().splitlines(True), fromfile='a/pkg/actors/router/router.go', tofile='b/pkg/actors/router/router.go')).encode()
    require(patch == (packet / 'final-runtime-recovery.patch').read_bytes(), 'retained patch does not exactly describe archive-to-candidate change')
    patch_identity = json.loads((packet / 'final-runtime-patch-identity.json').read_text())
    require(digest(original_router) == patch_identity['originalSha256'], 'archive original router mismatch')
    require(digest(patched) == patch_identity['afterSha256'] == identity['routerSha256'], 'patched router identity mismatch')
    changes = compare(archive, current, allowed)
    return {'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
            'observation': 'post-build source-tree consistency only; not retroactive pre-build evidence',
            'sourceDirectory': str(source), 'archivePath': str(archive_path),
            'sourceArchiveSha256': sha(archive_path), 'sourceRevision': identity['sourceRevision'],
            'patchSha256': digest(patch), 'result': 'only-exact-retained-router-patch-and-two-added-tests',
            'permissionComparison': 'archive modes normalized by removing group/other write (0022); all other mode bits and entry types exact',
            'archiveRawModes': archive_modes, 'archiveEntries': archive, 'currentEntries': current, 'differences': changes}


if __name__ == '__main__':
    try:
        require(sys.flags.optimize == 0, 'optimized Python execution is forbidden')
        packet = Path(__file__).resolve().parent.parent / 'runs-2026-10-21-runtime-recovery'
        print(json.dumps(observe(packet), indent=2))
    except (ValidationFailure, OSError, ValueError, KeyError, StopIteration, tarfile.TarError) as error:
        print('VALIDATION FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)
