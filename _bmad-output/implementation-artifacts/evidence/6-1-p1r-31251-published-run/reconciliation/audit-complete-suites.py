"""Independently compare literal unfiltered suite results with discovered tests."""
import ast
import datetime
import hashlib
import json
from pathlib import Path
import sys
import unittest

root = Path(__file__).resolve().parents[5]
base = Path(__file__).resolve().parent / 'verification-34825a9564d245df8fab3bb8a28ec74a'
index_path = base / 'index.json'
index = json.loads(index_path.read_bytes())
expected = ('qualification', 'published_qualification', 'published_executor', 'snapshot_registration',
            'isolation', 'isolation_hardening', 'isolation_hardening_optimized')
if tuple(row['name'] for row in index['suites']) != expected:
    raise SystemExit('complete literal suite inventory not yet available')
manifest_path = base / 'source-manifest-before.json'
manifest = json.loads(manifest_path.read_bytes())
sha = lambda data: hashlib.sha256(data).hexdigest()
if any(sha((root / name).read_bytes()) != digest for name, digest in manifest.items()):
    raise SystemExit('source bytes differ from suite manifest')
sys.path.insert(0, str(root / 'tools/tests'))
sys.path.insert(0, str(root / 'tools'))

def flatten(suite):
    for item in suite:
        if isinstance(item, unittest.TestSuite):
            yield from flatten(item)
        else:
            yield item

rows = []
all_ids = set()
for row in index['suites']:
    name = row['name']
    optimized = name.endswith('_optimized')
    pattern = 'test_p1r_' + name.replace('_optimized', '') + '.py'
    argv = ['/usr/bin/python3', *(['-O'] if optimized else []), '-m', 'unittest', 'discover',
            '-s', 'tools/tests', '-p', pattern]
    if row['argv'] != argv or row['cwd'] != str(root):
        raise SystemExit('suite filtering or caller differs: ' + name)
    log = Path(row['log']).read_bytes()
    if not (row['exit_code'] == 0 and row['ok'] and not row['skips'] and row['source_unchanged']
            and sha(log) == row['log_sha256'] and log.rstrip().endswith(b'OK')):
        raise SystemExit('literal suite did not pass unchanged and unskipped: ' + name)
    suite = unittest.defaultTestLoader.discover(str(root / 'tools/tests'), pattern=pattern)
    tests = list(flatten(suite))
    ids = [test.id() for test in tests]
    if len(ids) != row['tests'] or len(ids) != len(set(ids)):
        raise SystemExit('discovered inventory differs: ' + name)
    for test in tests:
        method = getattr(test, test._testMethodName)
        if getattr(test, '__unittest_skip__', False) or getattr(method, '__unittest_skip__', False):
            raise SystemExit('discovered test is disabled: ' + test.id())
    tree = ast.parse((root / 'tools/tests' / pattern).read_bytes())
    if any(isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) and node.name == 'load_tests'
           for node in ast.walk(tree)):
        raise SystemExit('custom discovery requires separate audit: ' + name)
    all_ids.update(ids)
    rows.append({'name': name, 'tests': len(ids), 'test_ids': ids,
                 'literal_record': str(base / (name + '.json')),
                 'record_sha256': sha((base / (name + '.json')).read_bytes()),
                 'log_sha256': sha(log), 'optimized_python': optimized})

matrix = {
    'Published candidate': [
        'test_p1r_published_executor.MeasurementTests.testFiniteInventoryIncludesActualHistoricalAndCandidateDirections',
        'test_p1r_snapshot_registration.FixtureSnapshotRegistrationTests.testProposedAndUnenrolledPackageProjectEvaluations',
        'test_p1r_snapshot_registration.FixtureSnapshotRegistrationTests.testRealHostAndDomainRegistrationAndMissingEnrollmentNegativeControls'],
    'Source substitution': [
        'test_p1r_published_executor.PublishedPatchSourceTests.testNewerVerifierCanSelectOnlyExactCleanTaggedProductionAndBuildClosure',
        'test_p1r_published_executor.PublishedPatchSourceTests.testMissingCandidateClosureCannotBeRehashedIntoAnImport',
        'test_p1r_published_executor.PublishedPatchSourceTests.testReadyWaitRechecksSourceAfterReturningAndRefusesDrift',
        'test_p1r_published_qualification.TaggedSourceConsumerTests.testIndependentSourceGraphProofRejectsRehashedRootAndLoadedBinarySubstitution',
        'test_p1r_qualification.TaggedPhysicalSourceTests.testIndexFlagsCannotHidePhysicalProductionOrBuildChanges',
        'test_p1r_qualification.CleanSourceFilterBoundaryTests.testPinnedRolesRejectExternalCleanFiltersBeforeExecutingThem'],
    'Finite enrollment': [
        'test_p1r_published_executor.MeasurementTests.testEverySeedIntervalRefusesUnenrolledCandidatesAndWrongWritersBeforeMutation',
        'test_p1r_snapshot_registration.FixtureSnapshotRegistrationTests.testProposedAndUnenrolledPackageProjectEvaluations'],
    'Nonpassing evidence': [
        'test_p1r_qualification.PacketTests.testRequiredFailedSkippedUnmeasuredIncompatibleLanesCannotPass',
        'test_p1r_published_executor.CorrectionTests.testFailedMetadataCheckCannotBeFlippedWithRecomputedCountersAndHashes',
        'test_p1r_published_qualification.RuntimeContractTests.testCleanupFailuresRemainNonpassing',
        'test_p1r_published_qualification.SupportBoundaryTests.testSuccessorPacketKeepsInputsWhenDecisionsFollowSupportBoundary',
        'test_p1r_published_qualification.EvaluationTests.testIncompleteDecisionsKeepUsabilityFalse'],
    'Interruption': [
        'test_p1r_published_executor.CleanupTests.testInterruptedActualChildIsRemovedAndRepeatedCleanupIsIdempotent',
        'test_p1r_isolation.ControllerTests.testFailedStartupPreservesErrorAndRunsBothCleanupAttempts',
        'test_p1r_isolation.PrivateCommandTests.testActualSigintAndSigtermAllowChildCleanupBeforeFinalSupervisorReceipt',
        'test_p1r_published_qualification.ProcessControlTests.testRealProcessControlsProveOnlyTheirLocalScope']}
missing = {name: [test for test in ids if test not in all_ids] for name, ids in matrix.items()}
if any(missing.values()):
    raise SystemExit('matrix inventory incomplete: ' + json.dumps(missing))
result = {'observed_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'scope': 'parent independent merged-source suite inventory and frozen matrix test audit; actual canonical measurement audited separately',
          'script_sha256': sha(Path(__file__).read_bytes()), 'index_sha256': sha(index_path.read_bytes()),
          'source_manifest_sha256': sha(manifest_path.read_bytes()), 'source_files_checked': len(manifest),
          'total_test_executions': sum(row['tests'] for row in rows),
          'suite_inventory_and_literal_logs_match': True, 'suites': rows, 'matrix_covering_tests': matrix,
          'qualifies_published_candidate': False, 'p1r_usable': False}
output = base / 'parent-independent-audit.json'
if output.exists():
    raise SystemExit('refuse existing audit replacement')
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'output': str(output), 'sha256': sha(output.read_bytes()),
                  'test_executions': result['total_test_executions'], 'matrix_rows': len(matrix)}))
