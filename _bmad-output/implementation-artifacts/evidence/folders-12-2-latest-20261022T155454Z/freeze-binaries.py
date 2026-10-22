#!/usr/bin/env python3
"""Freeze current SDK images and inventory Debug test graphs without selecting consumer outputs."""
import hashlib
import json
import os
from pathlib import Path
import shutil

packet = Path(__file__).resolve().parent
root = Path(os.environ['EVENTSTORE_QUALIFICATION_SOURCE_ROOT']).resolve()


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


files = {}
pairs = {}
graph = {}
for base in ('src', 'tests', 'samples'):
    for directory in sorted(list((root / base).glob('*/bin/Debug/net10.0')) + (list((root / base).glob('*/bin/qualified-Debug/net10.0')) if base == 'tests' else [])):
        for path in sorted(directory.glob('Hexalith.*')):
            if path.suffix not in ('.dll', '.pdb', '.json') or not path.is_file():
                continue
            files[str(path)] = sha(path)
            if path.suffix == '.dll' and path.with_suffix('.pdb').exists():
                pdb = path.with_suffix('.pdb')
                pair = (files[str(path)], sha(pdb))
                pairs.setdefault(pair, pdb)
        project = directory.parents[2]
        assets = project / 'obj/project.assets.json'
        if assets.exists():
            doc = json.loads(assets.read_text())
            graph[str(project)] = {'assets_sha256': sha(assets), 'restore': doc.get('project', {}).get('restore', {}),
                                   'project_libraries': {k: v for k, v in doc.get('libraries', {}).items() if v.get('type') == 'project'}}

binding = {'assemblies_sha256': {str(p): sha(p) for p in pairs.values()}}
(packet / 'debug-binary-freeze.json').write_text(json.dumps({'files_sha256': files, **binding}, indent=2) + '\n')
(packet / 'debug-project-graph.json').write_text(json.dumps(graph, indent=2) + '\n')
handoff = packet / 'handoff-runtime'
handoff.mkdir(exist_ok=False)
selected = {}
for project in sorted((root / 'src').glob('Hexalith.EventStore*')):
    directory = project / 'bin/Debug/net10.0'
    for suffix in ('.dll', '.pdb', '.deps.json', '.runtimeconfig.json'):
        path = directory / (project.name + suffix)
        if path.is_file():
            shutil.copyfile(path, handoff / path.name)
            selected[path.name] = {'source': str(path), 'sha256': sha(path)}
for name in ('Hexalith.Commons.UniqueIds.dll', 'Hexalith.Commons.UniqueIds.pdb', 'Ulid.dll', 'Ulid.pdb'):
    source = root / 'src/Hexalith.EventStore/bin/Debug/net10.0' / name
    if source.exists():
        shutil.copyfile(source, handoff / name)
        selected[name] = {'source': str(source), 'sha256': sha(source)}
(packet / 'handoff-runtime.json').write_text(json.dumps(selected, indent=2) + '\n')
print(json.dumps({'binary_and_manifest_paths': len(files), 'unique_pe_pdb_pairs': len(pairs), 'project_graphs': len(graph), 'handoff_files': len(selected)}))
