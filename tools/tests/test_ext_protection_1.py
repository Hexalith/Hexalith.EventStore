"""Offline policy/receipt tests only. Never invoke execute(), main(Live), or installed roles."""
import base64
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('ext_protection_1', Path(__file__).parents[1] / 'ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class ExtProtectionPolicyTests(unittest.TestCase):
    def test_plan_covers_owner_requirements_without_running_it(self):
        plan = v.read_json(v.PLAN_FILE)
        self.assertEqual(41, len(plan['cases']))
        self.assertEqual(41, len({c['id'] for c in plan['cases']}))
        checks = {s for c in plan['cases'] for s in c['checks']}
        self.assertTrue({'sealed', 'roundtrip', 'erased', 'pin', 'atomic', 'race', 'restore', 'replacement-blocked', 'signing-attempts'} <= checks)

    def test_register_accepts_future_or_past_dates_without_a_literal_date_gate(self):
        for when in ('2026-11-02', '2031-01-01', '2020-01-01'):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'register.md'
                path.write_text('### EXT-PROTECTION-1 — Engine\n' + '\n'.join([
                    '| `AcceptedStatus` | `Committed` |', '| `TargetVersionOrCommit` | `' + 'a' * 40 + '` |',
                    '| `TargetIntegrationDate` | `' + when + '` |',
                    '| `CompatibilityContractAndVerificationCommand` | ProtectionProfileSha256=' + 'b' * 64 + ' |']))
                self.assertEqual(when, v.accepted_record(path)['TargetIntegrationDate'])

    def test_uncommitted_and_unpinned_records_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'register.md'
            path.write_text('### EXT-PROTECTION-1 — Engine\n| `AcceptedStatus` | `Uncommitted` |\n')
            with self.assertRaisesRegex(v.Blocked, 'DependencyNotAvailable'):
                v.accepted_record(path)

    def test_evidence_cannot_be_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'original.json'
            v.durable(path, {'operationId': 'original'})
            with self.assertRaises(FileExistsError):
                v.durable(path, {'operationId': 'replacement'})
            self.assertEqual('original', json.loads(path.read_text())['operationId'])

    def test_unknown_destructive_result_recovers_by_exact_lookup_only(self):
        request = {'operationId': 'original-operation', 'role': 'executor', 'bindingDigest': 'binding', 'action': 'DestroyDekManifest', 'targets': ['a', 'b']}
        calls = []
        class Role:
            def request(self, value):
                calls.append(value)
                if value['action'] == 'DestroyDekManifest':
                    raise subprocess.TimeoutExpired('synthetic-test-transport', 1)
                return {'statement': {'response': {'status': 'Consumed', 'originalOperationId': request['operationId'], 'originalRequestSha256': v.digest(request), 'receipts': ['r1', 'r2']}}}
        with tempfile.TemporaryDirectory() as directory:
            result = v.mutate(Role(), request, Path(directory))
            self.assertEqual('Consumed', v.response(result)['status'])
            self.assertEqual(['DestroyDekManifest', 'LookupOriginalOperation'], [c['action'] for c in calls])
            self.assertEqual(request, calls[1]['originalRequest'])
            self.assertEqual(v.digest(request), calls[1]['originalRequestSha256'])
            self.assertEqual(request, v.read_json(Path(directory) / 'original-operation.request.json'))

    def test_unknown_lookup_retains_original_and_cannot_retry_destruction(self):
        request = {'operationId': 'original', 'role': 'executor', 'bindingDigest': 'binding', 'action': 'DestroyDekManifest'}
        calls = []
        class Role:
            def request(self, value):
                calls.append(value)
                return {'statement': {'response': {'status': 'Unknown'}}}
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(v.Blocked):
                v.mutate(Role(), request, Path(directory))
            self.assertEqual(1, sum(c['action'] == 'DestroyDekManifest' for c in calls))
            self.assertTrue((Path(directory) / 'original.request.json').exists())
            self.assertFalse((Path(directory) / 'original.result.json').exists())

    def test_changed_original_lookup_identity_is_rejected(self):
        class Role:
            def request(self, _):
                return {'statement': {'response': {'status': 'Consumed', 'originalOperationId': 'other', 'originalRequestSha256': 'x'}}}
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(v.Blocked, 'UNRESOLVED'):
                v.lookup_original(Role(), {'operationId': 'original', 'bindingDigest': 'binding'}, Path(directory))

    def test_cross_tenant_target_identity_does_not_collapse_to_alias(self):
        a = {'TenantId': 'a', 'AgentInteractionId': 'i', 'TargetProtectionKeyAlias': 'same'}
        b = {**a, 'TenantId': 'b'}
        self.assertNotEqual(v.target_key(a), v.target_key(b))

    def baseline(self):
        targets = [{'TenantId': 'a', 'AgentInteractionId': str(i), 'TargetProtectionKeyAlias': 'same'} for i in (1, 2)]
        observation = {'engineSourceRevision': 'a' * 40, 'engineBinarySha256': 'b' * 64, 'agentsRuntimeEnabled': False,
                       'roots': [{'target': t, 'state': 'Active'} for t in targets], 'records': [],
                       'effectRevisionAfterRejectedOperation': 2, 'effectRevisionBeforeRejectedOperation': 2}
        return targets, observation

    def evaluate(self, observation, checks):
        targets, _ = self.baseline()
        v.evaluate(observation, {'steps': [], 'checks': checks}, targets, b'plaintext-canary', 'a' * 40, 'b' * 64)

    def test_authentic_active_state_has_a_real_passing_assertion_path(self):
        _, observation = self.baseline()
        self.evaluate(observation, ['active', 'no-effect'])

    def test_partial_provider_effect_cannot_pass(self):
        _, observation = self.baseline()
        observation['roots'][0]['state'] = 'Destroyed'
        with self.assertRaisesRegex(v.Failed, 'PARTIAL_PROVIDER_EFFECT'):
            self.evaluate(observation, ['atomic'])

    def test_persisted_plaintext_and_enabled_agents_are_rejected(self):
        targets, observation = self.baseline()
        observation['records'] = [{'kind': 'target-event', 'target': targets[0], 'bytesBase64': base64.b64encode(b'plaintext-canary').decode(), 'protectedState': 'Sealed'}]
        with self.assertRaisesRegex(v.Failed, 'PLAINTEXT_PERSISTED'):
            self.evaluate(observation, [])
        observation['agentsRuntimeEnabled'] = True
        with self.assertRaisesRegex(v.Failed, 'AGENTS_RUNTIME_FORBIDDEN'):
            self.evaluate(observation, [])

    def test_same_owner_race_requires_exactly_one_winner(self):
        _, observation = self.baseline()
        observation['ownerTransitions'] = [{'from': 'Unconsumed', 'to': 'ConsumptionBlocked'}]
        self.evaluate(observation, ['race'])
        observation['ownerTransitions'].append({'from': 'Unconsumed', 'to': 'ConsumptionReserved'})
        with self.assertRaisesRegex(v.Failed, 'SINGLE_WINNER'):
            self.evaluate(observation, ['race'])

    def test_erased_source_and_target_replay_cannot_report_readable_external_content(self):
        _, observation = self.baseline()
        for root in observation['roots']:
            root['state'] = 'Destroyed'
        observation['replays'] = [{'target': observation['roots'][0]['target'], 'kind': 'source-outbox', 'field': {'State': 'Erased'}, 'externalOutcome': 'Unreadable'}]
        self.evaluate(observation, ['erased'])
        observation['replays'][0]['externalOutcome'] = 'Readable'
        with self.assertRaisesRegex(v.Failed, 'ERASED_REPLAY_INVALID'):
            self.evaluate(observation, ['erased'])

    def test_duplicate_json_members_and_changed_pinned_bytes_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'input.json'
            path.write_text('{"value":1,"value":2}')
            with self.assertRaises(v.Blocked):
                v.read_json(path)
            pin = v.digest(path.read_bytes())
            v.pinned_file(str(path), pin)
            path.write_text('{}')
            with self.assertRaises(v.Blocked):
                v.pinned_file(str(path), pin)

    def test_role_environment_contains_only_its_own_credential(self):
        with patch.dict(v.os.environ, {'EXECUTOR_TOKEN': 'one', 'OBSERVER_TOKEN': 'two', 'ENROLLMENT_TOKEN': 'three'}, clear=True):
            environment = v.isolated_environment('env:OBSERVER_TOKEN')
            self.assertEqual('two', environment['OBSERVER_TOKEN'])
            self.assertNotIn('EXECUTOR_TOKEN', environment)
            self.assertNotIn('ENROLLMENT_TOKEN', environment)

    def test_empty_hold_orders_cannot_pass(self):
        _, observation = self.baseline()
        observation['holdOrders'] = []
        with self.assertRaisesRegex(v.Failed, 'HOLD_ORDER_PROOF_MISSING'):
            self.evaluate(observation, ['hold-order'])

    def test_hold_dispatch_race_does_not_require_post_dispatch_owner_reservation(self):
        _, observation = self.baseline()
        observation['holdOrders'] = [{'holdOperationId': 'hold', 'dispatchOperationId': 'dispatch',
                                      'holdRevision': 1, 'dispatchRevision': 2, 'dispatchWon': False}]
        self.evaluate(observation, ['race', 'hold-order'])
        observation['roots'][0]['state'] = 'Destroyed'
        with self.assertRaisesRegex(v.Failed, 'HOLD_RACE_STATE_CONTRADICTION'):
            self.evaluate(observation, ['race', 'hold-order'])

    def test_every_target_requires_source_and_target_copies(self):
        targets, observation = self.baseline()
        with self.assertRaisesRegex(v.Failed, 'SOURCE_TARGET_PROOF_MISSING'):
            v.evaluate(observation, {'steps': ['SealAllTargets'], 'checks': []}, targets,
                       b'plaintext-canary', 'a' * 40, 'b' * 64)

    def test_snapshot_and_cache_plan_requires_real_records(self):
        targets, observation = self.baseline()
        with self.assertRaisesRegex(v.Failed, 'SOURCE_TARGET_PROOF_MISSING'):
            v.evaluate(observation, {'steps': ['SealSnapshotAndCache'], 'checks': []}, targets,
                       b'plaintext-canary', 'a' * 40, 'b' * 64)

    def test_sealed_label_cannot_make_empty_or_arbitrary_bytes_protected(self):
        targets, observation = self.baseline()
        for raw in (b'', b'{}'):
            observation['records'] = [{'kind': 'target-event', 'target': targets[0], 'bytesBase64': base64.b64encode(raw).decode(), 'protectedState': 'Sealed'}]
            with self.assertRaises((v.Failed, KeyError)):
                self.evaluate(observation, [])

    def test_negative_fixture_dependency_outage_is_not_a_compatibility_rejection(self):
        for action in ('RejectChangedManifest', 'RejectCrossTenantAlias', 'RejectActivation'):
            self.assertNotIn('DependencyNotAvailable', v.rejection_statuses(action, []))

    def test_real_es256_public_anchor_verification_and_substitution_rejection(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            private, public, signed = [directory / name for name in ('test-private.pem', 'test-public.pem', 'test-signature.der')]
            subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(private)], check=True, capture_output=True)
            subprocess.run(['openssl', 'pkey', '-in', str(private), '-pubout', '-out', str(public)], check=True, capture_output=True)
            statement = {'role': 'offline-unit-fixture', 'operationId': 'original'}
            subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(private), '-out', str(signed)], input=v.canonical(statement), check=True, capture_output=True)
            der = signed.read_bytes()
            self.assertEqual(0x30, der[0])
            offset = 2
            parts = []
            for _ in range(2):
                self.assertEqual(2, der[offset])
                size = der[offset + 1]
                parts.append(der[offset + 2:offset + 2 + size].lstrip(b'\0').rjust(32, b'\0'))
                offset += size + 2
            signature = base64.b64encode(b''.join(parts)).decode()
            v.verify_statement(statement, signature, public, directory)
            self.assertEqual(64, len(v.anchor_identity(public)))
            with self.assertRaisesRegex(v.Blocked, 'PROOF_INVALID'):
                v.verify_statement({**statement, 'operationId': 'substituted'}, signature, public, directory)


if __name__ == '__main__':
    unittest.main()
