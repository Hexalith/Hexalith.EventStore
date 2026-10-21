#!/usr/bin/env python3
"""Verify retained local command/scenario bytes without granting qualification authority."""
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

commands = []
scenarios = []
for path in sorted(packet.glob('*.json')):
    receipt = json.loads(path.read_text())
    if 'sourcesBefore' not in receipt:
        continue
    commands.append(receipt['label'])
    assert sha(packet / (receipt['label'] + '.log')) == receipt['outputSha256'], path
    snapshot = packet / receipt['snapshot']
    for original, expected in receipt['sourcesBefore'].items():
        retained = receipt['retainedSourcePaths'][original]
        assert sha(snapshot / retained) == expected, (path, original)
    assert receipt['sourcesBefore'] == receipt['sourcesAfter'], path
    if any('IntegrationTests.dll' in arg for arg in receipt['command']):
        assert receipt['binariesBefore'] == receipt['binariesAfter'], path
for path in sorted(packet.glob('scenarios/*/manifest.json')):
    manifest = json.loads(path.read_text())
    scenarios.append((manifest['Scenario'], manifest['Result']))
    for relative, expected in manifest['Artifacts'].items():
        assert sha(path.parent / relative) == expected, (path, relative)
    for original, expected in manifest['Sources'].items():
        assert sha(path.parent / manifest['RetainedSourcePaths'][original]) == expected, (path, original)
for path in packet.rglob('*'):
    assert not (path.is_file() and path.suffix in ('.csproj', '.props', '.targets', '.slnx')), path
index = packet / 'index.json'
if index.exists():
    retained_index = json.loads(index.read_text())
    entries = retained_index['files']
    excluded = set(retained_index['excludedFromIndex'])
    actual = {path.relative_to(packet).as_posix() for path in packet.rglob('*') if path.is_file()}
    assert actual - excluded == set(entries), 'indexed file membership'
    for relative, expected in entries.items():
        assert sha(packet / relative) == expected, relative
    preservation = json.loads((packet / 'historical-preservation-corrected.json').read_text())
    assert sha(packet / 'baseline-identities.json') == preservation['baselineIdentityFileSha256']
    baseline = json.loads((packet / 'baseline-identities.json').read_text())
    historical = {path: expected for path, expected in baseline.items()
                  if path.startswith('_bmad-output/implementation-artifacts/evidence/story-2-13/')}
    assert len(historical) == preservation['preExistingEvidenceFileCount']
    for relative, expected in historical.items():
        assert sha(packet.parents[4] / relative) == expected, relative
print(json.dumps({'result': 'retained-byte-integrity-only', 'commands': len(commands), 'scenarios': scenarios}, indent=2))
