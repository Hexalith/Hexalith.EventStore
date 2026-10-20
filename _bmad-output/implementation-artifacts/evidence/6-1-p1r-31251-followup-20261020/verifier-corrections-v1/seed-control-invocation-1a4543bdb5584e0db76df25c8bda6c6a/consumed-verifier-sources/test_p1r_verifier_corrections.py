"""Bounded V1–V3 regressions; these fake resources grant no runtime qualification."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_published_executor as executor
import p1r_qualification as preparation
import p1r_qualification_runtime as runtime
from test_p1r_published_executor import verifier_fixture, verifier_rows
import test_p1r_published_executor as fixtures


class RetainedFixtureCorrectionsTests(unittest.TestCase):
    @staticmethod
    def worker(raw):
        worker = executor.Executor.__new__(executor.Executor)
        worker.active, worker.redis = [], 'owned'
        state = dict(raw)
        worker.raw_state = mock.Mock(side_effect=lambda *_: dict(state))
        worker.postgres_write = mock.Mock(side_effect=lambda _, key, value: state.__setitem__(key, executor.canonical(value)))
        worker.postgres_delete = mock.Mock(side_effect=lambda _, key: state.pop(key))
        return worker, state

    def testCoherentRetainedFixtureChangesOnlyBothRetentionFloorsAndRemovesPrefix(self):
        raw, _ = verifier_fixture(proof=True)
        worker, state = self.worker(raw)
        worker.mutate('retained')
        metadata = executor.retained_metadata(state, 5)
        original = json.loads(raw['tenant-a:counter:fixture:metadata'])
        expected = copy.deepcopy(original)
        expected['retainedFloor'] = expected['sourceProof']['retainedFloor'] = 5
        self.assertEqual(metadata, expected)
        self.assertEqual(worker.last_retained_fixture['before_metadata'], original)
        self.assertEqual({key: value for key, value in raw.items() if ':events:' not in key or int(key.rsplit(':', 1)[1]) >= 5
                          or 'tenant-b' in key} | {'tenant-a:counter:fixture:metadata': executor.canonical(expected)}, state)
        self.assertEqual(sorted(runtime.stream(verifier_rows(state), 'tenant-a')[2]), list(range(5, 13)))

    def testExplicitMismatchedProofControlPreservesOriginalMutationAndRefusalState(self):
        raw, _ = verifier_fixture(proof=True)
        worker, state = self.worker(raw)
        worker.mutate('retained', 'mismatched-proof')
        metadata = json.loads(state['tenant-a:counter:fixture:metadata'])
        self.assertEqual(metadata['retainedFloor'], 5)
        self.assertEqual(metadata['sourceProof'], json.loads(raw['tenant-a:counter:fixture:metadata'])['sourceProof'])
        before = dict(state)
        with self.assertRaisesRegex(preparation.InvalidPacket, 'source proof'):
            executor.retained_metadata(state, 5)
        self.assertEqual(state, before)

    def testLegacyAbsenceAndInvalidFloorNegativeRemainUnrepaired(self):
        for kind in ('retained', 'invalid-floor'):
            raw, _ = verifier_fixture()
            worker, state = self.worker(raw)
            worker.mutate(kind)
            metadata = json.loads(state['tenant-a:counter:fixture:metadata'])
            self.assertNotIn('sourceProof', metadata)
            self.assertEqual(metadata['retainedFloor'], 5 if kind == 'retained' else 0)
        raw, _ = verifier_fixture(proof=True)
        worker, state = self.worker(raw)
        worker.mutate('invalid-floor')
        self.assertEqual(json.loads(state['tenant-a:counter:fixture:metadata'])['sourceProof'],
                         json.loads(raw['tenant-a:counter:fixture:metadata'])['sourceProof'])

    def testMalformedRetainedPreconditionsRefuseBeforeAnyWrite(self):
        for field, value in (('headSequence', 11), ('retainedFloor', 5), ('erased', True), ('tenantId', 'tenant-b'),
                             ('domain', 'other'), ('aggregateId', 'other'), ('headMessageId', 'wrong'),
                             ('sourceIncarnation', 'wrong'), ('cumulativeDigest', 'f' * 64)):
            raw, _ = verifier_fixture(proof=True)
            metadata = json.loads(raw['tenant-a:counter:fixture:metadata'])
            metadata['sourceProof'][field] = value
            raw['tenant-a:counter:fixture:metadata'] = executor.canonical(metadata)
            worker, _ = self.worker(raw)
            with self.subTest(field=field), self.assertRaises(preparation.InvalidPacket):
                worker.mutate('retained')
            worker.postgres_write.assert_not_called()
            worker.postgres_delete.assert_not_called()

    def testRetainedMutationRefusesActiveWriterAndMissingEvent(self):
        raw, _ = verifier_fixture(proof=True)
        for active in (True, False):
            worker, state = self.worker(raw)
            if active:
                worker.active = ['writer']
            else:
                del state['tenant-a:counter:fixture:events:7']
            with self.subTest(active=active), self.assertRaises(preparation.InvalidPacket):
                worker.mutate('retained')
            worker.postgres_write.assert_not_called()
            worker.postgres_delete.assert_not_called()


class SeedPreconditionCorrectionsTests(unittest.TestCase):
    def testPartialTwoCheckSeedRetainsBothTenantsAndNoUnattemptedProbeChecks(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.candidate, worker.redis, worker.sidecar_port = '3.125.1', 'owned', 12345
        worker.stop_nodes = worker.start_nodes = worker.postgres_query = mock.Mock()
        raw, rows = verifier_fixture(snapshot=False)
        raw = {key: data for key, data in raw.items() if 'tenant-b' in key}
        worker.raw_state = mock.Mock(return_value=raw)
        worker.inventory = mock.Mock(return_value=verifier_rows(raw))
        calls = []
        def probe(version, arguments, measurements, prefix):
            tenant = arguments[2]
            calls.append(tenant)
            checks = ([{'id': 'append-1-accepted', 'passed': False}, {'id': 'append-1-one-event', 'passed': False}]
                      if tenant == 'tenant-a' else [{'id': 'append-1-accepted', 'passed': True}, {'id': 'append-1-one-event', 'passed': True}])
            measurements.import_probe({'instrumentation': executor.MECHANISM,
                'measurement': {'checks': checks, 'assertions': executor.checks_counter(checks)}}, prefix, len(calls))
            return {'committed': 0, 'accepted': False} if tenant == 'tenant-a' else {'committed': 3, 'sequence': 3}
        worker.probe = mock.Mock(side_effect=probe)
        worker.mutate = worker.actor = mock.Mock()
        measured = executor.Measurements()
        with self.assertRaises(executor.SeedFailure) as caught:
            worker.live_case('full-replay', '3.110.0-to-source', measured)
        self.assertEqual(calls, ['tenant-a', 'tenant-b'])
        self.assertEqual([row['id'] for row in measured.checks if row['id'].startswith('tenant-a:append-')],
                         ['tenant-a:append-1-accepted', 'tenant-a:append-1-one-event'])
        self.assertEqual(worker.last_seed['writer'], '3.110.0')
        self.assertFalse(worker.last_seed['valid'])
        self.assertEqual(worker.last_seed['persisted']['inventory'], verifier_rows(raw))
        self.assertEqual(caught.exception.observations['dependent_operations'], 'unattempted')
        worker.actor.assert_not_called()
        worker.mutate.assert_not_called()
        self.assertGreater(measured.counter['failed'], 0)

    @staticmethod
    def paired_worker(unequal=False, failure=False):
        worker = executor.Executor.__new__(executor.Executor)
        worker.candidate, worker.redis, worker.redis_port = '3.125.1', 'original', 12345
        worker.inputs = {'candidate': {'version': '3.125.1'}}
        worker.stop_nodes = mock.Mock()
        worker.postgres_container = mock.Mock(side_effect=[('package-db', 12346, 1), ('source-db', 12347, 2)])
        preparations = []
        def prepare(lane, identifier, checks):
            preparations.append(identifier)
            passed = not (failure and len(preparations) == 1)
            checks.check('seed-measured', passed is True)
            seed = {'writer': identifier.split('-to-')[0], 'valid': passed, 'checks': list(checks.checks)}
            if not passed:
                raise executor.SeedFailure(seed)
            raw, rows = verifier_fixture(snapshot=False, suffix='dynamic' if len(preparations) == 2 else '')
            if unequal and len(preparations) == 2:
                value = json.loads(raw['tenant-a:counter:fixture:events:7'])
                value['payload'] = 'eyJjb3VudCI6OTk5fQ=='
                raw['tenant-a:counter:fixture:events:7'] = executor.canonical(value)
                rows = verifier_rows(raw)
            return {'before': rows, 'seed_snapshot': None, 'seed_preconditions': seed,
                    'persisted_preconditions': executor.persisted_preconditions(rows, raw)}
        worker.prepare_live_case = mock.Mock(side_effect=prepare)
        def live(lane, identifier, checks, prepared):
            if len(preparations) != 2:
                raise AssertionError('reader ran before both seeds')
            return dict(prepared, after=prepared['before'], primary={'accepted': True, 'sequence': 12}), 'effect', 'compatible'
        worker.live_case = mock.Mock(side_effect=live)
        return worker

    def testInvalidOrUnequalSeedsBlockBothReadersAndPreserveBothBranches(self):
        for failure in (False, True):
            worker = self.paired_worker(unequal=not failure, failure=failure)
            checks = executor.Measurements()
            observed, outcome, disposition = worker.checkout_case('full-replay', checks)
            self.assertEqual(set(observed['paired_observations']), {'package', 'source'})
            self.assertEqual(outcome, 'error')
            self.assertNotEqual(disposition, 'compatible')
            self.assertFalse(observed['comparison']['preconditions']['equal'])
            self.assertFalse(checks.checks[-2]['passed'])
            worker.live_case.assert_not_called()
            self.assertEqual(worker.redis, 'original')
            self.assertEqual(worker.prepare_live_case.call_count, 2)

    def testEquivalentSuccessfulSeedsAllowReadersWithExplicitDynamicBytes(self):
        worker, checks = self.paired_worker(), executor.Measurements()
        observed, outcome, disposition = worker.checkout_case('full-replay', checks)
        self.assertEqual((outcome, disposition), ('effect', 'compatible'))
        self.assertEqual(worker.live_case.call_count, 2)
        self.assertTrue(observed['comparison']['preconditions']['equal'])
        paired = observed['paired_observations']
        self.assertNotEqual(paired['package']['before'], paired['source']['before'])
        self.assertTrue(paired['source']['persisted_preconditions']['dynamic_fields'])

    def testInvalidEvidenceCheckoutAlsoBlocksFailedOrUnequalSeedsBeforeEitherReader(self):
        for failure in (False, True):
            worker = self.paired_worker(unequal=not failure, failure=failure)
            prepare = worker.prepare_live_case.side_effect
            worker.prepare_invalid_case = mock.Mock(side_effect=lambda identifier, checks: prepare('invalid-evidence', identifier, checks))
            worker.invalid_case = mock.Mock(side_effect=AssertionError('invalid-evidence reader must remain unattempted'))
            checks = executor.Measurements()
            observation, outcome, disposition = worker.checkout_case('invalid-evidence', checks)
            self.assertEqual((outcome, disposition), ('error', 'unverified'))
            self.assertEqual(worker.prepare_invalid_case.call_count, 2)
            self.assertEqual(observation['comparison']['selected_case'], '3.125.1-unknown-version')
            self.assertEqual(observation['comparison']['source_case'], 'source-unknown-version')
            self.assertFalse(observation['comparison']['preconditions']['equal'])
            worker.invalid_case.assert_not_called()

    def testEqualSuccessfulInvalidEvidenceFixturesPreserveMeasuredRefusalEquivalence(self):
        worker = self.paired_worker()
        prepare = worker.prepare_live_case.side_effect
        worker.prepare_invalid_case = mock.Mock(side_effect=lambda identifier, checks: prepare('invalid-evidence', identifier, checks))
        def invalid(identifier, checks, prepared):
            checks.check('invalid-evidence-refused', True)
            return dict(prepared, after=prepared['before'], actor={'accepted': False, 'event_count': 0, 'sequence': 12}), 'refusal', 'compatible'
        worker.invalid_case = mock.Mock(side_effect=invalid)
        observation, outcome, disposition = worker.checkout_case('invalid-evidence', executor.Measurements())
        self.assertEqual((outcome, disposition), ('refusal', 'compatible'))
        self.assertTrue(observation['comparison']['preconditions']['equal'])
        self.assertEqual(worker.invalid_case.call_count, 2)

    def testInvalidEvidencePreparationRetainsExactCorruptionAndRefusalDomainState(self):
        for version in ('3.125.1', 'source'):
            raw, _ = verifier_fixture(snapshot=False)
            worker, state = RetainedFixtureCorrectionsTests.worker(raw)
            worker.last_seed = {'writer': version, 'valid': True}
            worker.seed = mock.Mock()
            worker.inventory = mock.Mock(side_effect=lambda: verifier_rows(state))
            worker.start_nodes = worker.stop_nodes = mock.Mock()
            worker.actor = mock.Mock(return_value={'accepted': False, 'event_count': 0, 'sequence': 12})
            observed, outcome, disposition = worker.invalid_case(version + '-unknown-version', executor.Measurements())
            self.assertEqual((outcome, disposition), ('refusal', 'compatible'))
            self.assertEqual(observed['before'], observed['after'])
            self.assertEqual(observed['seed_preconditions']['writer'], version)
            self.assertEqual(json.loads(state['tenant-a:counter:fixture:events:7'])['metadataVersion'], 987)
            self.assertEqual(observed['persisted_preconditions']['inventory'], observed['before'])

    def testIndependentPreconditionsRefuseChangedContentsOrUndeclaredExclusions(self):
        raw, rows = verifier_fixture(snapshot=False)
        record = executor.persisted_preconditions(rows, raw)
        commands = [{'exit_code': 0, 'output': json.dumps([[key, json.loads(data)] for key, data in raw.items()])}]
        executor.witnesses.validate_persisted_preconditions(record, [rows], commands)
        for change in ('semantic', 'dynamic', 'bytes'):
            altered = copy.deepcopy(record)
            if change == 'semantic':
                altered['semantic'][0]['value']['payload'] = 'changed'
            elif change == 'dynamic':
                altered['dynamic_fields'].pop()
            else:
                altered['state'][next(iter(raw))]['base64'] = 'e30='
            with self.subTest(change=change), self.assertRaises(preparation.InvalidPacket):
                executor.witnesses.validate_persisted_preconditions(altered, [rows], commands)


class RestoreOutcomeCorrectionsTests(unittest.TestCase):
    @staticmethod
    def worker(scratch, append=True, restart=True, persisted=True, fresh=True, sequence=True):
        worker = fixtures.CleanupTests.worker(unittest.TestCase(), scratch)
        worker.candidate, worker.redis = '3.125.1', 'source'
        worker.inputs = {'candidate': {'packages': []}, 'operational_profile': {}}
        worker.seed = worker.mutate = worker.stop_nodes = worker.postgres_wait = mock.Mock()
        raw, source = verifier_fixture(floor=5, proof=True)
        after = verifier_fixture(head=13, floor=5, proof=True)[1] if append and persisted else source
        def run(argv, **kwargs):
            worker.record(argv, worker.scratch, executor.stamp(), 0, b'')
            return b''
        worker.run = run
        inventories = iter([source, source, after, after])
        def inventory(*args):
            run(['python', 'executor', 'inventory'])
            return next(inventories)
        worker.inventory = mock.Mock(side_effect=inventory)
        def raw_state(*args):
            run(['docker', 'exec', 'owned', 'psql'])
            return raw
        worker.raw_state = mock.Mock(side_effect=raw_state)
        backup = Path(scratch) / 'backup'; backup.write_bytes(b'backup')
        def backup_command(*args):
            run(['docker', 'exec', 'source', 'pg_dump'])
            return backup, worker.commands[-1]['id']
        worker.postgres_backup = mock.Mock(side_effect=backup_command)
        def create(*args):
            run(['docker', 'start', 'restored'])
            return 'restored', 12345, worker.commands[-1]['id']
        worker.postgres_container = mock.Mock(side_effect=create)
        worker.postgres_restore = mock.Mock(side_effect=lambda *_: run(['docker', 'exec', 'restored', 'pg_restore']))
        def start(*args, **kwargs):
            run(['dotnet', 'Host.dll'])
            return [{'pid': 10 if worker.start_nodes.call_count == 1 or not fresh else 20, 'start_ticks': 1}]
        worker.start_nodes = mock.Mock(side_effect=start)
        def actor(version, tenant, expected, kind, checks, prefix):
            run(['dotnet', 'Probe.dll', 'actor', tenant, kind])
            accepted = append if prefix == 'append:' else restart if prefix == 'restart:' else True
            result = {'accepted': accepted, 'event_count': 1 if kind == 'IncrementCounter' and accepted else 0}
            if sequence:
                result['sequence'] = 13 if kind == 'IncrementCounter' and accepted else expected
            return result
        worker.actor = mock.Mock(side_effect=actor)
        return worker

    def testRestorePositiveRequiresActualAppendPersistedStateAndFreshRestart(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker, checks = self.worker(scratch), executor.Measurements()
            observed, outcome, disposition = worker.restore_case('fixture', checks)
            self.assertEqual((outcome, disposition), ('effect', 'compatible'))
            self.assertEqual(checks.counter['failed'], 0)
            self.assertEqual(worker.pending_restore['observations'], {'state_before_append': 12, 'appended_sequence': 13, 'state_after_restart': 13})
            receipt = dict(worker.pending_restore, cleanup={'execution': 'passed'})
            recovery_checks = runtime.restore_observations(receipt, {command['id']: command for command in receipt['commands']},
                {name: item['rows'] for name, item in receipt['inventories'].items()})
            self.assertTrue(all(row['passed'] for row in recovery_checks))
            for name in ('source', 'restored', 'appended', 'restarted'):
                index = worker.pending_restore['inventories'][name]['command']
                self.assertIn('inventory', worker.pending_restore['commands'][index - 1]['argv'])

    def testZeroExitAppendRefusalFailedRestartMissingSequenceAndUnpersistedEffectCannotPass(self):
        for options in ({'append': False}, {'restart': False}, {'persisted': False}, {'fresh': False}, {'sequence': False}):
            with self.subTest(options=options), tempfile.TemporaryDirectory() as scratch:
                worker, checks = self.worker(scratch, **options), executor.Measurements()
                _, outcome, disposition = worker.restore_case('fixture', checks)
                self.assertNotEqual(disposition, 'compatible')
                self.assertGreater(checks.counter['failed'], 0)
                self.assertTrue(all(command['exit_code'] == 0 for command in worker.commands))
                receipt = dict(worker.pending_restore, cleanup={'execution': 'passed'})
                recovery_checks = runtime.restore_observations(receipt, {command['id']: command for command in receipt['commands']},
                    {name: item['rows'] for name, item in receipt['inventories'].items()})
                self.assertFalse(all(row['passed'] for row in recovery_checks))
                if options.get('append') is False or options.get('restart') is False:
                    self.assertEqual(outcome, 'refusal')
                if options.get('sequence') is False or options.get('persisted') is False or options.get('append') is False:
                    self.assertIsNone(worker.pending_restore['observations']['appended_sequence'])

    def testUnavailableRestartRetainsSuccessfulAppendAndNonpassingCase(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            worker.check_sources = mock.Mock()
            start = worker.start_nodes.side_effect
            def unavailable(*args, **kwargs):
                if worker.start_nodes.call_count == 2:
                    raise RuntimeError('restart unavailable')
                return start(*args, **kwargs)
            worker.start_nodes.side_effect = unavailable
            row, evidence = worker.case('post-upgrade-restore', 'restore', lambda checks: worker.restore_case('restore', checks), True)
            self.assertEqual((row['outcome'], row['disposition']), ('error', 'unverified'))
            self.assertTrue(evidence['observations']['append']['accepted'])
            self.assertEqual(evidence['observations']['append']['sequence'], 13)
            self.assertNotIn('replay', evidence['observations'])
            self.assertIn({'id': 'execution-completed', 'passed': False}, row['checks'])


if __name__ == '__main__':
    unittest.main()
