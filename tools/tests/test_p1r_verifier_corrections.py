"""Bounded V1–V3 regressions; these fake resources grant no runtime qualification."""
import copy
import base64
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
import test_p1r_published_qualification as qualification_fixtures


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

    def testRetainedPreflightRejectsWrongFoldAndUnsupportedEnvelopeButKeepsLegacyEnvelope(self):
        for version, count, accepted in ((1, 999, False), (2, 9, False), (0, 9, True)):
            raw, _ = verifier_fixture(proof=True)
            snapshot = json.loads(raw['tenant-a:counter:fixture:snapshot'])
            snapshot.update(snapshotEnvelopeVersion=version, state={'count': count})
            raw['tenant-a:counter:fixture:snapshot'] = executor.canonical(snapshot)
            worker, _ = self.worker(raw)
            if accepted:
                worker.mutate('retained')
            else:
                with self.assertRaises(preparation.InvalidPacket):
                    worker.mutate('retained')
                worker.postgres_write.assert_not_called()
                worker.postgres_delete.assert_not_called()

    def testFixedProductionSourceV1DigestIncludesSevenTimestampFractionDigits(self):
        vector = json.loads((Path(__file__).parent / 'fixtures/p1r-source-proof/source-v1-v4-vector.json').read_bytes())
        events = {int(key): base64.b64decode(value) for key, value in vector['event_bytes_base64'].items()}
        expected = '4ae20a8b91c81f936bda79bb919518dad47edfb022233c3deb3eb28274218bc4'
        self.assertEqual(vector['expected_digest'], expected)
        self.assertEqual(executor.source_proof_digest(events, vector['proof']), expected)
        event = json.loads(events[1])
        self.assertEqual(event['timestamp'], '2026-10-18T23:35:45.3311067+00:00')
        event['timestamp'] = '2026-10-18T23:35:45.3311068+00:00'
        events[1] = executor.canonical(event)
        self.assertNotEqual(executor.source_proof_digest(events, vector['proof']), expected)


class LiteralActorKeyTests(unittest.TestCase):
    @staticmethod
    def actor_state(raw):
        return {'eventstore||AggregateActor||' + key.partition(':metadata')[0].partition(':snapshot')[0].partition(':events:')[0]
                + '||' + key: data for key, data in raw.items()}

    @staticmethod
    def bound_record(raw, normalize=True):
        rows = verifier_rows(raw)
        record = (executor.persisted_preconditions(rows, raw) if normalize else {'inventory': rows,
            'state': {key: {'base64': base64.b64encode(data).decode(), 'sha256': executor.digest(data)} for key, data in raw.items()}})
        record.update(database='owned', invocation='fixture', commands={'inventory': 1, 'provider': 2})
        inventory = json.dumps(rows)
        provider = json.dumps([[key, json.loads(data)] for key, data in raw.items()])
        commands = [
            {'id': 1, 'exit_code': 0, 'argv': ['python', 'executor', 'inventory', '--container', 'owned', '--invocation', 'fixture', '--backend', 'state.postgresql'],
             'output': inventory, 'output_sha256': executor.digest(inventory.encode())},
            {'id': 2, 'exit_code': 0, 'argv': ['docker', 'exec', '-i', 'owned', 'psql', '-U', 'postgres', '-d', 'eventstore', '-At', '-v', 'ON_ERROR_STOP=1', '-f', '-'],
             'provider_sql': executor.PRECONDITION_QUERY, 'input_sha256': executor.digest(executor.PRECONDITION_QUERY.encode()),
             'input_bytes': len(executor.PRECONDITION_QUERY.encode()), 'output': provider, 'output_sha256': executor.digest(provider.encode())}]
        return record, commands

    def testLiteralV4ActorKeysPreserveRetainedFixtureAndIndependentPrerequisites(self):
        raw = self.actor_state(verifier_fixture(proof=True)[0])
        literal = 'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:10'
        self.assertIn(literal, raw)
        self.assertEqual(executor.fixture_domain_rows(raw), verifier_rows(raw))
        executor.validate_seed_proofs(verifier_rows(raw), raw)
        record, commands = self.bound_record(raw)
        executor.witnesses.validate_persisted_preconditions(record, [record['inventory']], commands)
        worker, state = RetainedFixtureCorrectionsTests.worker(raw)
        worker.mutate('retained')
        self.assertEqual(executor.retained_metadata(state, 5)['sourceProof']['retainedFloor'], 5)
        self.assertEqual(state[literal], raw[literal])
        record, commands = self.bound_record(state)
        executor.witnesses.validate_persisted_preconditions(record, [record['inventory']], commands)

    def testActorStateAndPayloadTenantMismatchesRefuseBothParsersBeforeMutation(self):
        for mutation in ('actor-tenant', 'payload-tenant', 'nested-tenant', 'nested-actor', 'actor-type'):
            raw = self.actor_state(verifier_fixture(proof=True)[0])
            key = 'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:10'
            event = json.loads(raw.pop(key))
            if mutation == 'actor-tenant':
                key = key.replace('||tenant-a:counter:fixture||', '||tenant-b:counter:fixture||')
            elif mutation == 'actor-type':
                key = key.replace('||AggregateActor||', '||OtherActor||')
            elif mutation == 'payload-tenant':
                event['tenantId'] = 'tenant-b'
            else:
                event['identity'] = {'tenantId': 'tenant-a', 'domain': 'counter', 'aggregateId': 'fixture', 'actorId': 'tenant-a:counter:fixture'}
                event['identity']['tenantId' if mutation == 'nested-tenant' else 'actorId'] = 'tenant-b' if mutation == 'nested-tenant' else 'tenant-b:counter:fixture'
            raw[key] = executor.canonical(event)
            with self.subTest(mutation=mutation):
                with self.assertRaises(preparation.InvalidPacket):
                    executor.fixture_domain_rows(raw)
                worker, _ = RetainedFixtureCorrectionsTests.worker(raw)
                with self.assertRaises(preparation.InvalidPacket):
                    worker.mutate('retained')
                worker.postgres_write.assert_not_called()
                worker.postgres_delete.assert_not_called()
                record, commands = self.bound_record(raw, normalize=False)
                with self.assertRaises(preparation.InvalidPacket):
                    executor.witnesses.validate_persisted_preconditions(record, [record['inventory']], commands, normalize=False)

    def testSimpleFixtureKeyFormsRemainSupportedByBothParsers(self):
        raw, _ = verifier_fixture(proof=True)
        for prefix in ('', 'eventstore||'):
            fixture = {prefix + key: data for key, data in raw.items()}
            self.assertEqual(executor.fixture_domain_rows(fixture), verifier_rows(fixture))
            record, commands = self.bound_record(fixture)
            executor.witnesses.validate_persisted_preconditions(record, [record['inventory']], commands)


class SeedPreconditionCorrectionsTests(unittest.TestCase):
    @staticmethod
    def seed_worker(raw):
        worker = executor.Executor.__new__(executor.Executor)
        worker.candidate, worker.redis = '3.125.1', 'owned'
        worker._seed = mock.Mock(return_value=verifier_rows(raw))
        worker.raw_state = mock.Mock(return_value=raw)
        worker.inventory = mock.Mock(return_value=verifier_rows(raw))
        worker.stop_nodes, worker.actor = mock.Mock(), mock.Mock()
        return worker

    def testCompleteSeedProofIsValidatedBeforeNormalizingDynamicFields(self):
        for field, bad in (('headSequence', 11), ('headMessageId', 'wrong'), ('cumulativeDigest', 'f' * 64),
                           ('codecVersion', True), ('headSequence', 12.0), ('retainedFloor', True),
                           ('tenantId', 'tenant-b'), ('sourceIncarnation', 'bad'), ('erased', True)):
            raw, _ = verifier_fixture(proof=True, snapshot=False)
            metadata = json.loads(raw['tenant-a:counter:fixture:metadata'])
            metadata['sourceProof'][field] = bad
            raw['tenant-a:counter:fixture:metadata'] = executor.canonical(metadata)
            worker = self.seed_worker(raw)
            with self.subTest(field=field), self.assertRaises(executor.SeedFailure):
                worker.seed('3.125.1', 1000, executor.Measurements())
            self.assertNotIn('persisted', worker.last_seed)
            worker.actor.assert_not_called()
        for key, bad in (('currentSequence', 12.0), ('retainedFloor', True)):
            raw, _ = verifier_fixture(snapshot=False)
            metadata = json.loads(raw['tenant-a:counter:fixture:metadata'])
            metadata[key] = bad
            raw['tenant-a:counter:fixture:metadata'] = executor.canonical(metadata)
            worker = self.seed_worker(raw)
            with self.subTest(key=key), self.assertRaises(executor.SeedFailure):
                worker.seed('3.125.1', 1000, executor.Measurements())

    def testIntervalThousandRefusesUnexpectedOrMultipleSnapshotsForEitherTenant(self):
        for tenant, duplicate in (('tenant-a', False), ('tenant-a', True), ('tenant-b', False)):
            raw, _ = verifier_fixture(snapshot=False)
            key = tenant + ':counter:fixture:snapshot'
            raw[key] = executor.canonical({'sequenceNumber': 9, 'state': {'count': 9}})
            if duplicate:
                raw['eventstore||' + key] = raw[key]
            worker = self.seed_worker(raw)
            checks = executor.Measurements()
            with self.subTest(tenant=tenant, duplicate=duplicate), self.assertRaises(executor.SeedFailure):
                worker.seed('3.125.1', 1000, checks)
            self.assertIn({'id': 'seed-snapshot-cardinality-exact', 'passed': False}, checks.checks)

    def testSecondaryProofAndBooleanEventCoordinatesCannotEnterSeedNormalization(self):
        for mutation in ('secondary-proof', 'globalPosition', 'metadataVersion'):
            raw, _ = verifier_fixture(proof=True, snapshot=False)
            if mutation == 'secondary-proof':
                primary = json.loads(raw['tenant-a:counter:fixture:metadata'])['sourceProof']
                secondary = json.loads(raw['tenant-b:counter:fixture:metadata'])
                secondary['sourceProof'] = dict(primary, tenantId='tenant-b', headSequence=3,
                    headMessageId='message-tenant-b-3', cumulativeDigest='f' * 64)
                raw['tenant-b:counter:fixture:metadata'] = executor.canonical(secondary)
            else:
                event = json.loads(raw['tenant-a:counter:fixture:events:1'])
                event[mutation] = True
                raw['tenant-a:counter:fixture:events:1'] = executor.canonical(event)
            worker = self.seed_worker(raw)
            with self.subTest(mutation=mutation), self.assertRaises(executor.SeedFailure):
                worker.seed('3.125.1', 1000, executor.Measurements())
            self.assertNotIn('persisted', worker.last_seed)

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
        self.assertEqual(worker.last_seed['captured_input']['inventory'], verifier_rows(raw))
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

    def testPackageReaderErrorMakesTheOverallPairErrorEvenWhenSourceSucceeds(self):
        worker = self.paired_worker()
        live = worker.live_case.side_effect
        def reader(*args):
            if worker.live_case.call_count == 1:
                error = RuntimeError('package reader unavailable')
                error.observations = {'primary': {'accepted': True, 'sequence': 12}, 'append': {'accepted': True, 'sequence': 13}}
                raise error
            return live(*args)
        worker.live_case.side_effect = reader
        observed, outcome, disposition = worker.checkout_case('full-replay', executor.Measurements())
        self.assertEqual((outcome, disposition), ('error', 'unverified'))
        self.assertTrue(observed['paired_observations']['package']['append']['accepted'])
        self.assertTrue(observed['paired_observations']['source']['primary']['accepted'])

    def testLiveReaderExceptionRetainsActualPrimaryAppendAndIntermediateInventory(self):
        worker = executor.Executor.__new__(executor.Executor)
        raw, rows = verifier_fixture(floor=5)
        worker.start_nodes = worker.stop_nodes = mock.Mock()
        worker.actor = mock.Mock(side_effect=[{'accepted': True, 'sequence': 12, 'event_count': 0},
            {'accepted': True, 'sequence': 3, 'event_count': 0}, {'accepted': True, 'sequence': 13, 'event_count': 1}, RuntimeError('restart probe failed')])
        worker.inventory = mock.Mock(return_value=verifier_fixture(head=13, floor=5)[1])
        with self.assertRaises(RuntimeError) as caught:
            worker.live_case('metadata-write', '3.110.0-to-3.125.1', executor.Measurements(), {'before': rows, 'seed_snapshot': None})
        observed = caught.exception.observations
        self.assertEqual(observed['primary']['sequence'], 12)
        self.assertEqual(observed['append']['sequence'], 13)
        self.assertIn('appended_inventory', observed)
        self.assertNotIn('replay', observed)

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
        record.update(database='owned', invocation='fixture', commands={'inventory': 1, 'provider': 2})
        inventory_output = json.dumps(rows)
        provider_output = json.dumps([[key, json.loads(data)] for key, data in raw.items()])
        commands = [{'id': 1, 'exit_code': 0, 'argv': ['python', 'executor', 'inventory', '--container', 'owned', '--invocation', 'fixture', '--backend', 'state.postgresql'],
            'output': inventory_output, 'output_sha256': executor.digest(inventory_output.encode())},
            {'id': 2, 'exit_code': 0, 'argv': ['docker', 'exec', '-i', 'owned', 'psql', '-U', 'postgres', '-d', 'eventstore', '-At', '-v', 'ON_ERROR_STOP=1', '-f', '-'],
            'output': provider_output, 'output_sha256': executor.digest(provider_output.encode()), 'provider_sql': executor.PRECONDITION_QUERY,
            'input_sha256': executor.digest(executor.PRECONDITION_QUERY.encode()), 'input_bytes': len(executor.PRECONDITION_QUERY.encode())}]
        executor.witnesses.validate_persisted_preconditions(record, [rows], commands)
        for change in ('semantic', 'dynamic', 'bytes', 'database', 'provider', 'query', 'coordinate', 'reader-order'):
            altered = copy.deepcopy(record)
            retained = copy.deepcopy(commands)
            if change == 'semantic':
                altered['semantic'][0]['value']['payload'] = 'changed'
            elif change == 'dynamic':
                altered['dynamic_fields'].pop()
            elif change == 'bytes':
                altered['state'][next(iter(raw))]['base64'] = 'e30='
            elif change == 'database':
                altered['database'] = 'other-branch'
            elif change == 'provider':
                altered['commands']['provider'] = 1
            elif change == 'query':
                retained[1]['provider_sql'] = 'DELETE FROM state'
            elif change == 'coordinate':
                altered['inventory'][0]['sequence'] = 999
                retained[0]['output'] = json.dumps(altered['inventory'])
                retained[0]['output_sha256'] = executor.digest(retained[0]['output'].encode())
            with self.subTest(change=change), self.assertRaises(preparation.InvalidPacket):
                executor.witnesses.validate_persisted_preconditions(altered, [altered['inventory']], retained, [1] if change == 'reader-order' else [])

    def failed_pair_receipt(self, scratch, actor=False):
        worker = self.paired_worker()
        worker.commands, worker.errors, worker.configurations = [], [], []
        worker.check_sources = mock.Mock()
        def fail(lane, identifier, checks):
            checks.check('actual-seed-attempt-failed', False)
            raise executor.SeedFailure({'writer': '3.110.0', 'valid': False, 'checks': list(checks.checks)})
        worker.prepare_live_case.side_effect = fail
        def action(checks):
            result = worker.checkout_case('full-replay', checks)
            if actor:
                measured = [{'id': 'actual-reader-observation', 'passed': False}]
                body = {'instrumentation': executor.MECHANISM, 'measurement': {'checks': measured, 'assertions': executor.checks_counter(measured)},
                        'observation': {'accepted': False}}
                output = json.dumps(body)
                worker.commands.append({'id': 1, 'argv': ['dotnet', '/Probe.dll', 'actor', 'http://127.0.0.1:12345',
                    'tenant-a', 'fixture', '12', 'AssertCounter'], 'exit_code': 0, 'output': output, 'output_sha256': executor.digest(output.encode())})
                checks.import_probe(body, 'actual-reader:', 1)
            else:
                worker.commands.append({'id': 1, 'argv': ['unattempted-reader-control'], 'exit_code': 0, 'output': 'null'})
            return result
        row, evidence = worker.case('checkout', 'full-replay', action, False)
        retained = list(worker.witness_sources.values())
        binding = {'repository': str(preparation.ROOT), 'main': {'files': [
            {'path': str(Path(item['path']).relative_to(preparation.ROOT)), 'sha256': item['sha256']} for item in retained]}}
        return row, evidence, binding, retained

    def testSourceBoundFutureGateCannotBeDroppedAlongWithItsCheckIds(self):
        with tempfile.TemporaryDirectory() as scratch:
            row, evidence, binding, retained = self.failed_pair_receipt(scratch)
            executor.witnesses.validate_case(row, evidence, binding, 'checkout', retained, '3.125.1')
            evidence['observations']['comparison'].pop('preconditions')
            removed = {'source-package-seed-preconditions-successful', 'source-package-seed-preconditions-equal'}
            row['checks'] = [item for item in row['checks'] if item['id'] not in removed]
            evidence['check_witnesses'] = [item for item in evidence['check_witnesses'] if item['id'] not in removed]
            row['assertions'] = executor.checks_counter(row['checks'])
            value = executor.digest(executor.canonical(evidence['observations']))
            row['inventory'] = {'before_sha256': value, 'after_sha256': value}
            with self.assertRaisesRegex(preparation.InvalidPacket, 'source-bound corrected executor'):
                executor.witnesses.validate_case(row, evidence, binding, 'checkout', retained, '3.125.1')
            # An independently bound historical source without the correction marker keeps its old grammar.
            for item in retained:
                if Path(item['path']).name != 'p1r_published_executor.py':
                    continue
                item['content'] = item['content'].replace('class SeedFailure(', 'class HistoricalSeedFailure(')
                item['sha256'] = executor.digest(item['content'].encode())
                for declared in binding['main']['files']:
                    if declared['path'] == str(Path(item['path']).relative_to(preparation.ROOT)):
                        declared['sha256'] = item['sha256']
                for witness in evidence['check_witnesses']:
                    if witness.get('source') == item['path']:
                        witness['source_sha256'] = item['sha256']
            executor.witnesses.validate_case(row, evidence, binding, 'checkout', retained, '3.125.1')

    def testFailedPrerequisiteUnattemptedClaimRefusesActualReaderCommand(self):
        with tempfile.TemporaryDirectory() as scratch:
            row, evidence, binding, retained = self.failed_pair_receipt(scratch, actor=True)
            with self.assertRaisesRegex(preparation.InvalidPacket, 'invalid or unequal seeds'):
                executor.witnesses.validate_case(row, evidence, binding, 'checkout', retained, '3.125.1')

    def testAfterActionSourceGuardFailureKeepsCompletedObservationAndReturnsError(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = fixtures.CleanupTests.worker(unittest.TestCase(), scratch)
            worker.check_sources = mock.Mock(side_effect=[None, preparation.InvalidPacket('source changed')])
            def action(checks):
                worker.commands.append({'id': 1})
                checks.check('actual-effect', True)
                return {'primary': {'accepted': True, 'sequence': 13}, 'before': [], 'after': []}, 'effect', 'compatible'
            row, evidence = worker.case('metadata-write', 'fixture', action, False)
            self.assertEqual((row['outcome'], row['disposition']), ('error', 'unverified'))
            self.assertEqual(evidence['observations']['primary']['sequence'], 13)
            self.assertEqual(evidence['observations']['source_guard_failure'], 'after-action')


class RestoreOutcomeCorrectionsTests(unittest.TestCase):
    def testFutureRecoveryEnvelopeValidatesWithoutChangingHistoricalGrammarOrCounters(self):
        receipt = qualification_fixtures.restore_receipt()
        original = runtime.restore_outcome(copy.deepcopy(receipt))
        self.assertEqual(original['assertions']['attempted'], 12)
        commands = receipt['commands']
        append = next(command for command in commands if command['step'] == 'append')
        replay = next(command for command in commands if command['step'] == 'replay')
        primary, secondary = copy.deepcopy(append), copy.deepcopy(replay)
        primary.update(id=-1, step='replay')
        secondary.update(id=-2, step='replay')
        commands.insert(commands.index(append), primary)
        commands.insert(commands.index(replay) + 1, secondary)
        identities = {command['id']: index + 1 for index, command in enumerate(commands)}
        for command in commands:
            command['id'] = identities[command['id']]
        for item in receipt['inventories'].values():
            item['command'] = identities[item['command']]
        receipt['backup']['command'] = identities[receipt['backup']['command']]
        responses = {}
        for name, command, tenant, expected, kind, count in (
            ('primary', primary, 'tenant-a', 12, 'AssertCounter', 0), ('append', append, 'tenant-a', 13, 'IncrementCounter', 1),
            ('replay', replay, 'tenant-a', 13, 'AssertCounter', 0), ('secondary', secondary, 'tenant-b', 3, 'AssertCounter', 0)):
            result = {'accepted': True, 'sequence': expected, 'event_count': count}
            checks = [{'id': 'actor-response-observed', 'passed': True}]
            output = executor.canonical({'instrumentation': executor.MECHANISM, 'measurement': {
                'checks': checks, 'assertions': executor.checks_counter(checks)}, 'observation': result}).decode()
            command.update(argv=['dotnet', '/Probe.dll', 'actor', 'http://127.0.0.1:12345', tenant, 'fixture',
                str(12 if name == 'append' else expected), kind], database=receipt['databases']['restored'], output_sha256=executor.digest(output.encode()))
            responses[name] = {'command': command['id'], 'output': output, 'observation': result}
        receipt['schema'] = runtime.RESTORE_SCHEMA_V2
        receipt['observations'].update(responses=responses,
            restore_checks=[{'id': identity, 'passed': True} for identity in runtime.ACTUAL_RESTORE_CHECKS])
        receipt['checks'] = runtime.restore_observations(receipt, {command['id']: command for command in commands},
            {name: item['rows'] for name, item in receipt['inventories'].items()})
        receipt['assertions'] = executor.checks_counter(receipt['checks'])
        outcome = runtime.restore_outcome(receipt)
        self.assertEqual(outcome['assertions']['attempted'], 13)
        self.assertEqual((outcome['execution'], outcome['compatibility']), ('passed', 'unverified'))
        with mock.patch.object(executor.qualification, 'bind_recovery'):
            group, lane, imported = executor.qualification.receipt_outcome(receipt, {})
        self.assertEqual((group, lane, imported['assertions']['attempted']), ('recovery', 'candidate-restore', 13))

    @staticmethod
    def worker(scratch, append=True, restart=True, persisted=True, fresh=True, sequence=True, secondary=True, primary_events=0):
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
            accepted = append if prefix == 'append:' else restart if prefix == 'restart:' else secondary if prefix == 'secondary:' else True
            result = {'accepted': accepted, 'event_count': 1 if kind == 'IncrementCounter' and accepted else 0}
            if prefix == 'restored:':
                result['event_count'] = primary_events
            if sequence:
                result['sequence'] = 13 if kind == 'IncrementCounter' and accepted else expected
            probe_checks = [{'id': 'actor-response-observed', 'passed': True}]
            output = executor.canonical({'instrumentation': executor.MECHANISM, 'measurement': {
                'checks': probe_checks, 'assertions': executor.checks_counter(probe_checks)}, 'observation': result})
            worker.record(['dotnet', '/Probe.dll', 'actor', 'http://127.0.0.1:12345', tenant, 'fixture', str(expected), kind],
                          worker.scratch, executor.stamp(), 0, output)
            return result
        worker.actor = mock.Mock(side_effect=actor)
        return worker

    def testRestorePositiveRequiresActualAppendPersistedStateAndFreshRestart(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker, checks = self.worker(scratch), executor.Measurements()
            observed, outcome, disposition = worker.restore_case('fixture', checks)
            self.assertEqual((outcome, disposition), ('effect', 'compatible'))
            self.assertEqual(checks.counter['failed'], 0)
            self.assertEqual({key: worker.pending_restore['observations'][key] for key in
                ('state_before_append', 'appended_sequence', 'state_after_restart')},
                {'state_before_append': 12, 'appended_sequence': 13, 'state_after_restart': 13})
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

    def testFutureRecoveryRefusesSecondaryDenialAndPrimaryHydrationEffect(self):
        for options in ({'secondary': False}, {'primary_events': 1}):
            with self.subTest(options=options), tempfile.TemporaryDirectory() as scratch:
                worker, checks = self.worker(scratch, **options), executor.Measurements()
                _, _, disposition = worker.restore_case('fixture', checks)
                self.assertEqual(disposition, 'incompatible')
                receipt = dict(worker.pending_restore, cleanup={'execution': 'passed'})
                commands = {command['id']: command for command in receipt['commands']}
                inventories = {name: item['rows'] for name, item in receipt['inventories'].items()}
                derived = runtime.restore_observations(receipt, commands, inventories)
                self.assertFalse(derived[-1]['passed'])
                literal = receipt['observations']['responses']
                if options.get('secondary') is False:
                    self.assertIs(literal['secondary']['observation']['accepted'], False)
                else:
                    self.assertEqual(literal['primary']['observation']['event_count'], 1)
                # Even a forged all-true actual-check list cannot override a literal failed response.
                forged = copy.deepcopy(receipt)
                for check in forged['observations']['restore_checks']:
                    check['passed'] = True
                self.assertFalse(runtime.restore_observations(forged, commands, inventories)[-1]['passed'])
                historical = copy.deepcopy(receipt)
                historical['schema'] = runtime.RESTORE_SCHEMA
                historical['observations'] = {key: historical['observations'][key] for key in
                    ('state_before_append', 'appended_sequence', 'state_after_restart')}
                self.assertEqual(len(runtime.restore_observations(historical, commands, inventories)), 12)

    def testFutureRecoveryRejectsLiteralResponseAndCommandSubstitution(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            worker.restore_case('fixture', executor.Measurements())
            receipt = dict(worker.pending_restore, cleanup={'execution': 'passed'})
            commands = {command['id']: command for command in receipt['commands']}
            inventories = {name: item['rows'] for name, item in receipt['inventories'].items()}
            for change in ('observation', 'command', 'missing-check'):
                altered = copy.deepcopy(receipt)
                if change == 'observation':
                    altered['observations']['responses']['secondary']['observation']['accepted'] = False
                elif change == 'command':
                    altered['observations']['responses']['secondary']['command'] = altered['observations']['responses']['primary']['command']
                else:
                    altered['observations']['restore_checks'].pop()
                with self.subTest(change=change), self.assertRaises(preparation.InvalidPacket):
                    runtime.restore_observations(altered, commands, inventories)

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
