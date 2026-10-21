#!/usr/bin/env python3
"""Capture exact source/binary-bound bounded-owner identity qualification; no package or deployment authority."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / '_bmad-output/implementation-artifacts/evidence/folders-12-2-bounded-owner-identity'
PROJECTS = ['Client', 'DomainService', 'Server', 'Server.LiveSidecar']


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def save(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, help='Fresh packet directory; existing packets are never overwritten')
    parser.add_argument('--skip-live', action='store_true', help='Capture unit evidence only; packet remains unqualified')
    parser.add_argument('--focused', action='store_true', help='Run affected projection classes only; packet records the narrowed gate')
    args = parser.parse_args()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    out = (args.evidence or EVIDENCE / ('run-' + stamp)).resolve()
    out.mkdir(parents=True, exist_ok=False)
    commands = []
    snapshots = {}
    for tree in ['src', 'tests', 'samples']:
        for path in sorted((ROOT / tree).rglob('*.cs')):
            if 'obj' not in path.parts and 'bin' not in path.parts:
                snapshots[str(path)] = sha(path)
    save(out / 'sources.json', snapshots)
    # Preserve exact changed source bytes as well as the full PDB-document comparison inventory.
    tracked = subprocess.check_output(['git', 'diff', '--name-only', 'HEAD'], cwd=ROOT, text=True).splitlines()
    untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard'], cwd=ROOT, text=True).splitlines()
    for name in sorted(set(tracked + untracked)):
        path = ROOT / name
        if path.suffix in {'.cs', '.py'} and not path.is_relative_to(EVIDENCE) and path.is_file():
            destination = out / 'captured-source' / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, destination)
    save(out / 'subject.json', {
        'baselineCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'buildGraph': 'Debug;UseHexalithProjectReferences=true;net10.0',
        'focusedOnly': args.focused, 'liveRequested': not args.skip_live,
        'qualificationScriptSha256': sha(Path(__file__)),
        'authority': 'EventStore source qualification only; package/release and Folders acceptance are separate',
    })

    def run(label, command, extra_env=None):
        print(label + ': ' + ' '.join(command), flush=True)
        started = time.monotonic()
        with (out / (label + '.log')).open('w') as log:
            result = subprocess.run(command, cwd=ROOT, env=os.environ | (extra_env or {}), stdout=log, stderr=subprocess.STDOUT)
        record = {'label': label, 'command': command, 'cwd': str(ROOT), 'exitCode': result.returncode,
                  'elapsedSeconds': round(time.monotonic() - started, 3), 'log': label + '.log'}
        if extra_env:
            record['environment'] = extra_env
        commands.append(record)
        save(out / 'commands.json', commands)
        print(f'{label}: exit={result.returncode}', flush=True)
        return result.returncode == 0

    built = {}
    for project in PROJECTS:
        name = 'Hexalith.EventStore.' + project + '.Tests'
        built[project] = run('build-' + project, ['dotnet', 'build', f'tests/{name}/{name}.csproj', '-c', 'Debug', '-m:1',
                                '-p:UseHexalithProjectReferences=true', '--nologo'])
    binaries = {}
    for project, success in built.items():
        if not success:
            continue
        directory = ROOT / 'tests' / ('Hexalith.EventStore.' + project + '.Tests') / 'bin/Debug/net10.0'
        for path in directory.glob('Hexalith.EventStore.*'):
            if path.suffix in {'.dll', '.pdb'}:
                binaries[str(path)] = sha(path)
    save(out / 'binaries.json', binaries)
    passed = all(built.values())
    filters = {
        'Client': ['-class', 'Hexalith.EventStore.Client.Tests.Projections.SharedProjection*'],
        'DomainService': ['-class', 'Hexalith.EventStore.DomainService.Tests.DomainPartitionedSharedProjectionRebuildTests'],
        'Server': ['-class', 'Hexalith.EventStore.Server.Tests.Projections.SharedProjectionSource*'],
    }
    for project in PROJECTS[:-1]:
        if not built[project]:
            continue
        name = 'Hexalith.EventStore.' + project + '.Tests'
        command = ['dotnet', f'tests/{name}/bin/Debug/net10.0/{name}.dll']
        if args.focused:
            command += filters[project]
        passed = run('tests-' + project, command) and passed
    runtime = out / 'runtime'
    if not args.skip_live and built['Server.LiveSidecar']:
        runtime.mkdir()
        name = 'Hexalith.EventStore.Server.LiveSidecar.Tests'
        passed = run('tests-live-process', ['dotnet', f'tests/{name}/bin/Debug/net10.0/{name}.dll', '-class',
                         'Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionBoundedIdentityLiveTests'],
                     {'BOUNDED_IDENTITY_EVIDENCE_DIR': str(runtime)}) and passed
    errors = []
    maxima = {}
    for count in [10001, 32769]:
        pids = set()
        for phase in ['stage', 'promote', 'resume']:
            path = runtime / f'{count}-{phase}.json'
            if not path.exists():
                errors.append(f'Missing process proof {count}-{phase}')
                continue
            proof = json.loads(path.read_text())
            pids.add(proof['pid'])
            for assembly in proof['runtime']:
                if assembly['loadedMvid'] != assembly['peMvid'] or not assembly['loadedMvidMatchesPe']:
                    errors.append('Loaded MVID differs from PE: ' + assembly['name'])
                for key, checksum in [('pe', 'peSha256'), ('pdb', 'pdbSha256')]:
                    if binaries.get(assembly[key]) != assembly[checksum]:
                        errors.append('Runtime binary differs from captured build: ' + assembly[key])
                for document in assembly['documents']:
                    if snapshots.get(document['path']) != document['sha256']:
                        # Generated documents are bound by their exact PDB/PE; source files must be captured.
                        if '/obj/' not in document['path']:
                            errors.append('Runtime source differs from captured source: ' + document['path'])
            for key, value in proof['measurements'].items():
                maxima[key] = max(maxima.get(key, 0), value)
        if len(pids) != 3:
            errors.append(f'Expected three distinct worker PIDs for {count}')
    for key, bound in [('MaximumBatchOperations', 100), ('MaximumCanonicalBatchBytes', 1048576),
                       ('MaximumPhysicalValueBytes', 1048576), ('MaximumIdentityValueBytes', 65536)]:
        if not 0 < maxima.get(key, 0) <= bound:
            errors.append(f'Measured maximum missing or exceeds bound: {key}')
    for path, digest in snapshots.items():
        if sha(Path(path)) != digest:
            errors.append('Source changed during qualification: ' + path)
    for path, digest in binaries.items():
        if sha(Path(path)) != digest:
            errors.append('Binary changed during qualification: ' + path)
    save(out / 'results.json', {'commandsPassed': passed, 'qualified': passed and not errors and not args.focused and not args.skip_live,
                               'runtimeBindingErrors': errors, 'measuredMaxima': maxima,
                               'limitations': ['Source Debug graph only; no package/release or Folders acceptance claim.',
                                              'Dapr lane uses a test-only physical byte observer and process-exit injector around the unchanged production protocol.']})
    files = {str(path.relative_to(out)): sha(path) for path in sorted(out.rglob('*')) if path.is_file()}
    save(out / 'packet-sha256.json', files)
    print(str(out), flush=True)
    return 0 if passed and not errors else 1


if __name__ == '__main__':
    sys.exit(main())
