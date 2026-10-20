"""Check retained local bytes and bindings; this is not an AD-12 CI seal."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
index = json.loads((root / 'index.json').read_text())
expected_index = (root / 'index.sha256').read_text().split()[0]
assert hashlib.sha256((root / 'index.json').read_bytes()).hexdigest() == expected_index
for relative, expected in index['artifacts'].items():
    assert hashlib.sha256((root / relative).read_bytes()).hexdigest() == expected, relative
for relative in index['finalChecks']:
    assert json.loads((root / relative).read_text())['exitCode'] == 0, relative
for relative in index['commands']:
    receipt = json.loads((root / relative).read_text())
    if receipt['exitCode'] != 0:
        assert relative in index['expectedNonPassingChecks'], relative
    log = root / relative.replace('.json', '.log')
    if 'outputSha256' in receipt:
        assert hashlib.sha256(log.read_bytes()).hexdigest() == receipt['outputSha256'], relative
    if 'sourcesBefore' in receipt:
        assert receipt['sourcesBefore'] == receipt['sourcesAfter'], relative
        if receipt.get('scenarioManifests'):
            assert receipt['binariesBefore'] == receipt['binariesAfter'], relative
        for source, retained in receipt.get('retainedSources', {}).items():
            assert hashlib.sha256((root / retained).read_bytes()).hexdigest() == receipt['sourcesBefore'][source]
    for manifest_path in receipt.get('scenarioManifests', []):
        manifest_file = root / manifest_path
        manifest = json.loads(manifest_file.read_text())
        for artifact, expected in manifest['Artifacts'].items():
            assert hashlib.sha256((manifest_file.parent / artifact).read_bytes()).hexdigest() == expected
        if relative == 'retry-distribution-tests.json' and manifest['Scenario'] == 'replica-restart-and-serialized-bounds':
            observations = manifest['Observations']
            assert len(observations['FirstRejectedCallbackEventIds']) == 3
            assert observations['FirstRejectedCallbackEventIds'] == observations['SecondRejectedCallbackEventIds']
            assert observations['RejectedSerializedVectorBytes'] == [2129, 2050, 2060]
            assert observations['ExactBoundaryAcceptedOnBothHosts'] == 2048
        for source, expected in receipt.get('sourcesAfter', {}).items():
            if source in manifest['Sources']:
                assert manifest['Sources'][source] == expected, source
profile = json.loads((root / 'profile-identity.json').read_text())
for source, retained in profile['retainedInputs'].items():
    assert hashlib.sha256((root / retained).read_bytes()).hexdigest() == profile['sources'][source]
closing = json.loads((root / 'closing-document-snapshot.json').read_text())
for source, retained in closing.items():
    assert hashlib.sha256((root / retained['retained']).read_bytes()).hexdigest() == retained['sha256'], source
spec = '_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md'
def frozen_intent(path):
    value = path.read_text()
    return value[value.index('<frozen-after-approval'):value.index('</frozen-after-approval>') + len('</frozen-after-approval>')].encode()
assert frozen_intent(root / profile['retainedInputs'][spec]) == frozen_intent(root / closing[spec]['retained'])
assert hashlib.sha256(frozen_intent(root / closing[spec]['retained'])).hexdigest() == index['frozenIntentSha256']
assert closing[spec]['sha256'] == index['workspaceSpecSha256AtPacketClose']
subject = json.loads((root / 'post-audit-metadata-tests.json').read_text())['binariesBefore']
for relative in ['post-audit-metadata-tests.json', 'retry-distribution-tests.json', 'final-real-projection.json', 'final-ordinary-regression.json', 'post-audit-redis-baseline.json']:
    receipt = json.loads((root / relative).read_text())
    assert receipt['binariesBefore'] == receipt['binariesAfter'] == subject, relative
cleanup = json.loads((root / 'closing-observations.json').read_text())
assert cleanup['binariesUnchangedFromFinalTests'] == subject
assert not cleanup['retainedProcessIdsStillPresent']
assert not cleanup['temporaryDistributionComponentDirectoriesStillPresent']
for relative, expected in json.loads((root / 'runtime-assembly-copy-bindings.json').read_text())['bindings'].items():
    assert len(set(expected.values())) == 1, relative
for binding in json.loads((root / 'redis-baseline-evidence-bindings.json').read_text()):
    assert hashlib.sha256((root / binding['retained']).read_bytes()).hexdigest() == binding['sha256']
assert index['disposition'] == 'unresolved-no-transition-or-exception-authority'
print('PASS: exact retained local bytes and run bindings; disposition remains unresolved')
