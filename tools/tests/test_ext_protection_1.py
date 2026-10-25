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
        observation['guardReceipts'] = [self.hold_receipt('hold', 1, 'hold-receipt'), self.hold_receipt('dispatch', 2, 'dispatch-receipt')]
        self.evaluate(observation, ['race', 'hold-order'])
        observation['roots'][0]['state'] = 'Destroyed'
        with self.assertRaisesRegex(v.Failed, 'HOLD_RACE_STATE_CONTRADICTION'):
            self.evaluate(observation, ['race', 'hold-order'])

    def hold_receipt(self, operation, revision, receipt):
        return {'OperationId': operation, 'IntentDigest': 'D' * 64, 'Status': 'Committed', 'GuardHighWater': revision,
                'AcceptedAtAdmissionFenceOrdinal': 1, 'ReferenceId': operation, 'ReceiptId': receipt,
                'AdmissionAttributionsJson': '[]', 'AcceptedWriteResourceId': '', 'AcceptedWriteFacts': None,
                'AcceptedTargetMutationDigest': '', 'Capability': None, 'SigningRequestId': '', 'DetachedJwsDigest': ''}

    def test_substituted_hold_pin_activation_and_restore_originals_reject(self):
        _, observation = self.baseline()
        observation['holdOrders'] = [{'holdOperationId': 'hold', 'dispatchOperationId': 'dispatch',
                                      'holdRevision': 1, 'dispatchRevision': 2, 'dispatchWon': False}]
        observation['guardReceipts'] = [self.hold_receipt('hold', 1, 'hold-receipt'), self.hold_receipt('dispatch', 2, 'dispatch-receipt')]
        self.evaluate(observation, ['hold-order'])
        observation['guardReceipts'][0]['GuardHighWater'] = 9
        with self.assertRaisesRegex(v.Failed, 'HOLD_ORIGINAL_SUBSTITUTED'):
            self.evaluate(observation, ['hold-order'])

        _, observation = self.baseline()
        observation['pinOutcomes'] = [{'status': status} for status in ('Pinned', 'HoldPinned', 'HoldPinned', 'Authorized', 'Unpinned')]
        observation['pinOriginals'] = [{'OperationId': 'pin-' + str(index), 'Status': item['status'], 'ReceiptId': 'pin-receipt-' + str(index),
                                        'Target': observation['roots'][0]['target']} for index, item in enumerate(observation['pinOutcomes'])]
        self.evaluate(observation, ['pin'])
        observation['pinOriginals'][1]['Status'] = 'Pinned'
        with self.assertRaisesRegex(v.Failed, 'PIN_ORIGINAL_SUBSTITUTED'):
            self.evaluate(observation, ['pin'])
        observation['pinOriginals'][1]['Status'] = 'HoldPinned'
        observation['pinOriginals'][1]['OperationId'] = observation['pinOriginals'][0]['OperationId']
        with self.assertRaisesRegex(v.Failed, 'PIN_ORIGINAL_SUBSTITUTED'):
            self.evaluate(observation, ['pin'])
        observation['pinOriginals'][1]['OperationId'] = 'pin-1'
        observation['pinOutcomes'][1]['target'] = observation['roots'][1]['target']
        with self.assertRaisesRegex(v.Failed, 'PIN_ORIGINAL_SUBSTITUTED'):
            self.evaluate(observation, ['pin'])

        _, observation = self.baseline()
        observation['copyInventoryBefore'] = ['copy-1']
        observation['copyStatesAfter'] = [{'copyId': 'copy-1', 'readOutcome': 'Retained'}]
        observation['restoreOriginals'] = [{'CopyId': 'copy-1', 'ReadOutcome': 'Retained', 'Kind': 'restored'}]
        self.evaluate(observation, ['restore'])
        observation['restoreOriginals'][0]['ReadOutcome'] = 'Revived'
        with self.assertRaisesRegex(v.Failed, 'RESTORE_ORIGINAL_SUBSTITUTED'):
            self.evaluate(observation, ['restore'])

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


class CapabilityOriginalEvidenceTests(unittest.TestCase):
    """Real offline signatures and original contract-shaped persisted evidence."""
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-offline-test-')
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.private, self.public = self.key('capability')
        self.roles = {}
        for name in v.ROLE_NAMES:
            _, public = self.key(name)
            executable = self.directory / (name + '.sh')
            executable.write_text('#!/bin/sh\nexit 77\n')
            executable.chmod(0o700)
            manifest = self.directory / (name + '.inventory.json')
            manifest.write_bytes(v.canonical({'files': [{'path': str(executable), 'sha256': v.digest(executable.read_bytes())}]}))
            self.roles[name] = {'executable': str(executable), 'executableSha256': v.digest(executable.read_bytes()),
                                'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes()),
                                'credentialReference': 'env:OFFLINE_' + name.upper(), 'identity': name,
                                'installationManifest': str(manifest), 'installationManifestSha256': v.digest(manifest.read_bytes())}
        self.trust = [{'issuer': 'offline-issuer', 'audience': 'offline-protection', 'tenantId': 'tenant-a',
                       'keyFamily': 'DeletionBatchCapabilitySigningKey', 'keyVersion': 'retained-v1',
                       'publicAnchorId': 'public-anchor-1', 'publicAnchorVersion': 'anchor-v1',
                       'publicAnchorFile': str(self.public), 'publicAnchorSha256': v.digest(self.public.read_bytes()),
                       'trustProfileRevision': 7}]
        self.targets = [{'TenantId': 'tenant-a', 'AgentInteractionId': interaction,
                         'TargetProtectionKeyAlias': 'alias<&>+`\"\'😀\n'} for interaction in ('interaction-1', 'interaction-雪')]
        capability = {'Issuer': 'offline-issuer', 'Audience': 'offline-protection', 'TenantId': 'tenant-a',
                      'DeletionRequestId': 'deletion-\"\\\n雪', 'DestructionSealId': 'stable-seal', 'BatchKind': 'AcceptedSet',
                      'BatchOrdinal': 0, 'BatchId': 'batch-original', 'ManifestDigest': v.digest(v.target_manifest_bytes(self.targets)).upper(),
                      'GuardStreamId': 'tenant-a:guard', 'IntendedIssuedGuardRevision': 23, 'AttestationOrdinal': 1,
                      'SigningAttemptOrdinal': 3, 'CapabilityKeyVersion': 'retained-v1'}
        self.request = {'Capability': capability, 'DetachedJws': self.sign(capability), 'CommittedIssuedGuardRevision': 24,
                        'DispatchReceiptId': 'dispatch-original', 'DispatchGuardRevision': 27, 'Targets': self.targets}
        signing_id = v.digest(v.capability_bytes(capability)).upper()
        jws_digest = v.digest(self.request['DetachedJws'].encode()).upper()
        batch = {'BatchId': capability['BatchId'], 'Kind': 'AcceptedSet', 'Ordinal': 0, 'Targets': self.targets,
                 'ManifestDigest': capability['ManifestDigest'], 'AttestationOrdinal': 1, 'CapabilityKeyVersion': 'retained-v1',
                 'AttestationDigest': jws_digest, 'IssueReceiptId': 'issue-original', 'DispatchReceiptId': 'dispatch-original',
                 'ProtectionOutcome': 'Consumed', 'ProtectionReceiptId': 'reservation-original',
                 'TargetReceiptIds': ['target-original-1', 'target-original-2'], 'BlockSetRevision': 0, 'CoveredResourceIds': [],
                 'Capability': capability, 'DetachedJws': self.request['DetachedJws'], 'SigningRequestId': signing_id,
                 'IssuedGuardRevision': 24, 'DispatchGuardRevision': 27}
        receipts = []
        for name, revision in (('issue', 24), ('dispatch', 27)):
            receipts.append({'OperationId': name + '-operation', 'IntentDigest': 'D' * 64, 'Status': 'Committed',
                             'GuardHighWater': revision, 'AcceptedAtAdmissionFenceOrdinal': 1, 'ReferenceId': capability['BatchId'],
                             'ReceiptId': name + '-original', 'AdmissionAttributionsJson': '[]', 'AcceptedWriteResourceId': '',
                             'AcceptedWriteFacts': None, 'AcceptedTargetMutationDigest': '', 'Capability': capability,
                             'SigningRequestId': signing_id, 'DetachedJwsDigest': jws_digest})
        vector = [{'Target': target, 'OriginalBatchId': capability['BatchId'], 'ReceiptId': 'target-original-' + str(i)}
                  for i, target in enumerate(self.targets, 1)]
        owner = {'TenantId': capability['TenantId'], 'BatchId': capability['BatchId'], 'Status': 3, 'OwnerRevision': 9,
                 'KeyBlockSetRevision': 0, 'ReceiptId': 'reservation-original', 'BlockReason': None,
                 'BlockedKeyVersion': None, 'RevocationRevision': None, 'TargetReceipts': vector}
        provider = {'TenantId': capability['TenantId'], 'BatchId': capability['BatchId'], 'State': 1,
                    'ReservationReceiptId': 'reservation-original', 'TargetReceipts': vector}
        self.observation = v.strict_json(v.canonical({'engineSourceRevision': 'a' * 40, 'engineBinarySha256': 'b' * 64,
                                         'agentsRuntimeEnabled': False, 'roots': [{'target': target, 'state': 'Destroyed'} for target in self.targets],
                                         'records': [], 'replays': [], 'consumptionRequests': [self.request], 'guardBatches': [batch],
                                         'guardReceipts': receipts, 'ownerResults': [owner], 'providerResults': [provider],
                                         'ownerRetryResults': [owner], 'ownerLookupResults': [owner], 'providerLookupResults': [provider],
                                         'ownerBatches': [{'Original': self.request, 'Current': self.request, 'Outcome': owner, 'BlockedReplacement': None}],
                                         'exactResults': [{'operationId': 'activation-original', 'receiptId': 'activation-result', 'status': 'Activated'}] * 2}))

    def key(self, name):
        private, public = (self.directory / (name + suffix) for suffix in ('.private.pem', '.public.pem'))
        subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(private)], check=True, capture_output=True)
        subprocess.run(['openssl', 'pkey', '-in', str(private), '-pubout', '-out', str(public)], check=True, capture_output=True)
        return private, public

    def signature(self, raw, private=None):
        signed = self.directory / 'signature.der'
        subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(private or self.private), '-out', str(signed)], input=raw, check=True, capture_output=True)
        der, offset, parts = signed.read_bytes(), 2, []
        for _ in range(2):
            size = der[offset + 1]
            parts.append(der[offset + 2:offset + 2 + size].lstrip(b'\0').rjust(32, b'\0'))
            offset += size + 2
        return b''.join(parts)

    def sign(self, capability, header=None):
        entry = self.trust[0]
        header = header or {'alg': 'ES256', 'anchorVersion': entry['publicAnchorVersion'], 'keyVersion': entry['keyVersion'],
                            'kid': entry['publicAnchorId'], 'typ': 'DeletionBatchCapabilityV1'}
        encoded = v.base64url(v.canonical(header))
        signature = self.signature((encoded + '.' + v.base64url(v.capability_bytes(capability))).encode())
        return encoded + '..' + v.base64url(signature)

    def evaluate(self, observation=None):
        v.evaluate(observation or self.observation, {'steps': [], 'checks': ['erased', 'receipts', 'capability', 'exact-results']},
                   self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)

    def cohort(self, target_sets):
        observed = copy.deepcopy(self.observation)
        for field in ('consumptionRequests', 'guardBatches', 'guardReceipts', 'ownerResults', 'providerResults',
                      'ownerRetryResults', 'ownerLookupResults', 'providerLookupResults', 'ownerBatches'):
            observed[field] = []
        for index, targets in enumerate(target_sets):
            request = copy.deepcopy(self.request)
            payload = request['Capability']
            payload.update(BatchId='batch-' + str(index), BatchKind='Containment' if len(target_sets) > 1 else 'AcceptedSet',
                           BatchOrdinal=index + 1 if len(target_sets) > 1 else 0,
                           IntendedIssuedGuardRevision=23 + index * 10, ManifestDigest=v.digest(v.target_manifest_bytes(targets)).upper())
            request.update(Targets=copy.deepcopy(targets), DetachedJws=self.sign(payload), CommittedIssuedGuardRevision=24 + index * 10,
                           DispatchGuardRevision=27 + index * 10, DispatchReceiptId='dispatch-' + str(index))
            batch = copy.deepcopy(self.observation['guardBatches'][0])
            batch.update(BatchId=payload['BatchId'], Kind=payload['BatchKind'], Ordinal=payload['BatchOrdinal'], Targets=request['Targets'],
                         ManifestDigest=payload['ManifestDigest'], IssueReceiptId='issue-' + str(index), DispatchReceiptId=request['DispatchReceiptId'],
                         Capability=payload, DetachedJws=request['DetachedJws'], SigningRequestId=v.digest(v.capability_bytes(payload)).upper(),
                         AttestationDigest=v.digest(request['DetachedJws'].encode()).upper(), IssuedGuardRevision=request['CommittedIssuedGuardRevision'],
                         DispatchGuardRevision=request['DispatchGuardRevision'], ProtectionReceiptId='reservation-' + str(index),
                         TargetReceiptIds=['target-' + str(index) + '-' + str(i) for i in range(len(targets))])
            for position, name in enumerate(('issue', 'dispatch')):
                receipt = copy.deepcopy(self.observation['guardReceipts'][position])
                receipt.update(OperationId=name + '-operation-' + str(index), ReceiptId=batch['IssueReceiptId' if name == 'issue' else 'DispatchReceiptId'],
                               ReferenceId=payload['BatchId'], Capability=payload, SigningRequestId=batch['SigningRequestId'], DetachedJwsDigest=batch['AttestationDigest'],
                               GuardHighWater=batch['IssuedGuardRevision' if name == 'issue' else 'DispatchGuardRevision'])
                observed['guardReceipts'].append(receipt)
            vector = [{'Target': target, 'OriginalBatchId': payload['BatchId'], 'ReceiptId': batch['TargetReceiptIds'][i]} for i, target in enumerate(targets)]
            owner = {**self.observation['ownerResults'][0], 'BatchId': payload['BatchId'], 'OwnerRevision': 9 + index,
                     'ReceiptId': batch['ProtectionReceiptId'], 'TargetReceipts': vector}
            provider = {**self.observation['providerResults'][0], 'BatchId': payload['BatchId'],
                        'ReservationReceiptId': owner['ReceiptId'], 'TargetReceipts': vector}
            for field, value in (('consumptionRequests', request), ('guardBatches', batch), ('ownerResults', owner), ('providerResults', provider),
                                 ('ownerRetryResults', owner), ('ownerLookupResults', owner), ('providerLookupResults', provider),
                                 ('ownerBatches', {'Original': request, 'Current': request, 'Outcome': owner, 'BlockedReplacement': None})):
                observed[field].append(copy.deepcopy(value))
        return observed

    def covered(self):
        observed = copy.deepcopy(self.observation)
        request = copy.deepcopy(self.request)
        # The original public artifact is retained; this defense-in-depth request grants no new authority.
        request['Capability']['BatchId'] = 'later-coverage-batch'
        outcome = {**copy.deepcopy(observed['ownerResults'][0]), 'BatchId': 'later-coverage-batch', 'Status': 4,
                   'OwnerRevision': 10, 'ReceiptId': 'coverage-original'}
        observed.update(coverageRequest=request, coverageOutcome=outcome,
                        targetCoverage=[{'target': t, 'batchIds': ['batch-original'], 'ordinals': [0]} for t in self.targets],
                        coverageOriginals=[{'Target': target, 'BatchId': 'batch-original', 'Ordinal': 0,
                                            'ManifestDigest': self.request['Capability']['ManifestDigest'],
                                            'ReceiptId': 'coverage-receipt-' + str(index)} for index, target in enumerate(self.targets, 1)])
        observed['ownerBatches'].append(copy.deepcopy({'Original': request, 'Current': request, 'Outcome': outcome, 'BlockedReplacement': None}))
        return observed

    def evaluate_coverage(self, observed):
        v.evaluate(observed, {'steps': [], 'checks': ['erased', 'receipts', 'coverage', 'already-destroyed']},
                   self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)

    def test_nontrivial_separately_signed_request_and_native_originals_pass(self):
        self.evaluate()
        self.assertNotEqual(self.request['CommittedIssuedGuardRevision'], self.request['Capability']['IntendedIssuedGuardRevision'])

    def test_retained_capability_boolean_and_float_substitutions_reject(self):
        for location in ('guardBatches', 'guardReceipts'):
            for field, values in (('BatchOrdinal', (False, 0.0)), ('AttestationOrdinal', (True, 1.0))):
                for value in values:
                    with self.subTest(location=location, field=field, value=value):
                        observed = copy.deepcopy(self.observation)
                        observed[location][0]['Capability'][field] = value
                        with self.assertRaisesRegex(v.Failed, 'ORDINAL_INVALID'):
                            self.evaluate(observed)

    def test_p36_operation_exact_results_are_checked_in_addition_to_consumed_originals(self):
        observed = copy.deepcopy(self.observation)
        observed['reattestation'] = {'sameBatch': True, 'sameManifest': True, 'oldAttestationRevoked': True,
                                     'activeCredentialCount': 1, 'compromiseBlockReceiptId': 'compromise-block',
                                     'expectedBlockSetRevision': 1, 'activatedBlockSetRevision': 1}
        observed['activationOriginal'] = {'OperationId': 'activation-original', 'CompromiseBlockReceiptId': 'compromise-block',
                                          'ExpectedKeyBlockSetRevision': 1, 'ActivatedBlockSetRevision': 1,
                                          'GuardReplacementReceiptId': 'guard-replacement', 'BatchId': self.request['Capability']['BatchId'],
                                          'ManifestDigest': self.request['Capability']['ManifestDigest'],
                                          'AttestationOrdinal': self.request['Capability']['AttestationOrdinal'],
                                          'OldAttestationRevoked': True, 'ActiveCredentialCount': 1}
        replacement = copy.deepcopy(observed['guardReceipts'][0])
        replacement.update(OperationId='guard-replacement-operation', ReceiptId='guard-replacement')
        observed['guardReceipts'].append(replacement)
        checks = ['erased', 'receipts', 'reattested', 'exact-results']
        v.evaluate(observed, {'steps': [], 'checks': checks}, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)
        for values in ([], observed['exactResults'][:1], [observed['exactResults'][0], {**observed['exactResults'][0], 'receiptId': 'changed-activation'}]):
            with self.subTest(values=values):
                with self.assertRaisesRegex(v.Failed, 'EXACT_RESULT_DIVERGED'):
                    self.evaluate({**observed, 'exactResults': values})
        for field, value in (('BatchId', 'substituted-batch'), ('ManifestDigest', 'substituted-manifest'), ('AttestationOrdinal', 2), ('GuardReplacementReceiptId', 'missing-replacement')):
            substituted = copy.deepcopy(observed)
            substituted['activationOriginal'][field] = value
            with self.assertRaisesRegex(v.Failed, 'ACTIVATION_ORIGINAL_SUBSTITUTED'):
                v.evaluate(substituted, {'steps': [], 'checks': checks}, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)
        substituted = copy.deepcopy(observed)
        substituted['reattestation']['expectedBlockSetRevision'] = True
        with self.assertRaisesRegex(v.Failed, 'ACTIVATION_ORIGINAL_SUBSTITUTED'):
            v.evaluate(substituted, {'steps': [], 'checks': checks}, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)

    def test_complete_multi_request_containment_cohort_passes(self):
        self.evaluate(self.cohort([[target] for target in self.targets]))

    def test_internally_consistent_signed_partial_and_overlapping_cohorts_reject(self):
        for groups in ([self.targets[:1]], [self.targets[:1], self.targets[:1]]):
            with self.subTest(groups=groups):
                with self.assertRaisesRegex(v.Failed, 'CONSUMPTION_TARGET_INVENTORY_MISMATCH'):
                    self.evaluate(self.cohort(groups))

    def test_conflicting_guard_operation_id_across_two_containment_requests_rejects(self):
        observed = self.cohort([[target] for target in self.targets])
        observed['guardReceipts'][2]['OperationId'] = observed['guardReceipts'][0]['OperationId']
        with self.assertRaisesRegex(v.Failed, 'GUARD_OPERATION_ORIGINAL_CONFLICT'):
            self.evaluate(observed)

    def test_guard_and_owner_native_revision_and_terminal_mirror_invariants(self):
        for value in (False, 0.0, -1, 2 ** 63):
            observed = copy.deepcopy(self.observation)
            observed['guardBatches'][0]['BlockSetRevision'] = value
            with self.assertRaisesRegex(v.Failed, 'ORDINAL_INVALID'):
                self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        observed['ownerResults'][0]['KeyBlockSetRevision'] = observed['ownerResults'][0]['OwnerRevision'] + 1
        with self.assertRaisesRegex(v.Failed, 'ORDINAL_INVALID'):
            self.evaluate(observed)
        for status in ('Unconsumed', 'ReplacementAwaitingActivation', 'AlreadyDestroyed'):
            observed = copy.deepcopy(self.observation)
            observed['guardBatches'][0].update(ProtectionOutcome=status, ProtectionReceiptId='unrelated-terminal')
            with self.assertRaisesRegex(v.Failed, 'GUARD_TERMINAL_VECTOR_MISMATCH'):
                self.evaluate(observed)

    def test_actual_retained_owner_request_and_reservation_are_required(self):
        observed = copy.deepcopy(self.observation)
        observed.pop('ownerBatches')
        with self.assertRaisesRegex(v.Failed, 'RETAINED_OWNER_BATCHES_MISSING'):
            self.evaluate(observed)
        for part, field, value in (('Current', 'DispatchReceiptId', 'other-dispatch'),
                                   ('Outcome', 'ReceiptId', 'other-reservation')):
            observed = copy.deepcopy(self.observation)
            observed['ownerBatches'][0][part][field] = value
            with self.assertRaisesRegex(v.Failed, 'RETAINED_OWNER_REQUEST_OR_RESERVATION_MISMATCH'):
                self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        observed['ownerBatches'][0]['BlockedReplacement'] = {}
        with self.assertRaisesRegex(v.Failed, 'RETAINED_OWNER_REQUEST_OR_RESERVATION_MISMATCH'):
            self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        original = observed['ownerBatches'][0]['Original']
        original['Capability']['DeletionRequestId'] = 'changed-but-validly-signed-original'
        original['DetachedJws'] = self.sign(original['Capability'])
        with self.assertRaisesRegex(v.Failed, 'RETAINED_ORIGINAL_BATCH_IDENTITY_CHANGED'):
            self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        observed['ownerBatches'][0]['Original']['DispatchReceiptId'] = 'changed-original-dispatch'
        with self.assertRaisesRegex(v.Failed, 'RETAINED_ORIGINAL_ATTESTATION_CHANGED'):
            self.evaluate(observed)

    def test_consistent_response_reservation_substitution_cannot_override_retained_owner(self):
        observed = copy.deepcopy(self.observation)
        for field in ('ownerResults', 'ownerRetryResults', 'ownerLookupResults'):
            observed[field][0]['ReceiptId'] = 'substituted-reservation'
        for field in ('providerResults', 'providerLookupResults'):
            observed[field][0]['ReservationReceiptId'] = 'substituted-reservation'
        observed['guardBatches'][0]['ProtectionReceiptId'] = 'substituted-reservation'
        with self.assertRaisesRegex(v.Failed, 'RETAINED_OWNER_REQUEST_OR_RESERVATION_MISMATCH'):
            self.evaluate(observed)

    def test_p15_native_coverage_request_and_exact_original_vector_pass(self):
        self.evaluate_coverage(self.covered())

    def test_substituted_replacement_batch_rejects(self):
        observed = copy.deepcopy(self.observation)
        observed['ownerOutcome'] = {'status': 'ConsumptionBlocked', 'receiptId': 'block-receipt', 'blockedKeyVersion': 'key-v2', 'revocationRevision': 4}
        observed['activationOutcome'] = {'status': 'ActivationBlockedByReplacementKeyCompromise', 'newBlockReceiptId': 'new-block',
                                         'newKeyVersion': 'key-v2', 'revocationRevision': 4}
        observed['activationOriginal'] = {'OperationId': 'activation-1', 'NewBlockReceiptId': 'new-block', 'NewKeyVersion': 'key-v2',
                                          'RevocationRevision': 4, 'BatchId': self.request['Capability']['BatchId']}
        case = {'steps': [], 'checks': ['replacement-blocked']}
        v.evaluate(observed, case, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)
        observed['activationOriginal']['BatchId'] = 'substituted-batch'
        with self.assertRaisesRegex(v.Failed, 'ACTIVATION_ORIGINAL_SUBSTITUTED'):
            v.evaluate(observed, case, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)
        observed['activationOriginal']['BatchId'] = self.request['Capability']['BatchId']
        observed['activationOriginal']['NewBlockReceiptId'] = 'substituted-block'
        with self.assertRaisesRegex(v.Failed, 'ACTIVATION_ORIGINAL_SUBSTITUTED'):
            v.evaluate(observed, case, self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)

    def test_substituted_coverage_original_rejects(self):
        observed = self.covered()
        observed['coverageOriginals'][0]['BatchId'] = 'substituted-batch'
        with self.assertRaisesRegex(v.Failed, 'COVERAGE_ORIGINAL_SUBSTITUTED'):
            self.evaluate_coverage(observed)
        observed = self.covered()
        observed['coverageOriginals'][0]['ManifestDigest'] = 'substituted-manifest'
        with self.assertRaisesRegex(v.Failed, 'COVERAGE_ORIGINAL_SUBSTITUTED'):
            self.evaluate_coverage(observed)
        for ordinal in (False, 0.0):
            observed = self.covered()
            observed['targetCoverage'][0]['ordinals'] = [ordinal]
            with self.assertRaisesRegex(v.Failed, 'COVERAGE_ORIGINAL_SUBSTITUTED'):
                self.evaluate_coverage(observed)

    def test_p15_changed_batch_target_original_batch_and_order_reject(self):
        for mutation in ('object-batch', 'changed-outcome-batch', 'changed-request-batch', 'changed-target', 'changed-original-batch', 'changed-order'):
            with self.subTest(mutation=mutation):
                observed = self.covered()
                request, outcome = observed['coverageRequest'], observed['coverageOutcome']
                if mutation == 'object-batch':
                    outcome['BatchId'] = {'invalid': 'object'}
                elif mutation == 'changed-outcome-batch':
                    outcome['BatchId'] = 'substituted-later-batch'
                elif mutation == 'changed-request-batch':
                    request['Capability']['BatchId'] = 'substituted-later-batch'
                elif mutation == 'changed-target':
                    request['Targets'][0]['AgentInteractionId'] = 'interaction-0'
                    request['Capability']['ManifestDigest'] = v.digest(v.target_manifest_bytes(request['Targets'])).upper()
                elif mutation == 'changed-original-batch':
                    outcome['TargetReceipts'][0]['OriginalBatchId'] = 'substituted-original-batch'
                else:
                    outcome['TargetReceipts'].reverse()
                observed['ownerBatches'][-1] = copy.deepcopy({'Original': request, 'Current': request, 'Outcome': outcome, 'BlockedReplacement': None})
                with self.assertRaises(v.Failed):
                    self.evaluate_coverage(observed)

    def test_native_aggregate_shape_and_consumption_identity_byte_bounds(self):
        for tenant, batch in (('tenant-é', 'valid-batch'), ('a' * 65, 'valid-batch'), ('tenant-a', '-invalid'),
                              ('tenant-a', 'invalid_'), ('tenant-a', 'a' * 257)):
            request = copy.deepcopy(self.request)
            request['Capability'].update(TenantId=tenant, BatchId=batch)
            with self.assertRaisesRegex(v.Failed, 'AGGREGATE_IDENTITY_INVALID'):
                v.capture_consumption_request(request)
        request = copy.deepcopy(self.request)
        request['DispatchReceiptId'] = 'é' * 1025
        with self.assertRaisesRegex(v.Failed, 'IDENTITY_INVALID'):
            v.capture_consumption_request(request)
        for field, member in (('ownerResults', 'ReceiptId'), ('providerResults', 'ReservationReceiptId')):
            observed = copy.deepcopy(self.observation)
            observed[field][0][member] = 'é' * 1025
            with self.assertRaisesRegex(v.Failed, 'IDENTITY_INVALID'):
                self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        observed['ownerResults'][0]['TargetReceipts'][0]['ReceiptId'] = 'é' * 1025
        with self.assertRaisesRegex(v.Failed, 'IDENTITY_INVALID'):
            self.evaluate(observed)

    def test_observation_trust_index_and_signature_cache_bound_openssl_work(self):
        self.trust.extend({**self.trust[0], 'keyVersion': 'retained-' + str(index)} for index in range(100))
        observed = self.cohort([[target] for target in self.targets])
        with patch.object(v.subprocess, 'run', wraps=subprocess.run) as run:
            self.evaluate(observed)
        commands = [call.args[0] for call in run.call_args_list]
        self.assertEqual(4, sum(command[:2] == ['openssl', 'ec'] for command in commands))
        self.assertEqual(2, sum(command[:2] == ['openssl', 'dgst'] for command in commands))

    def test_observation_deadline_and_end_pin_drift_block(self):
        with patch.object(v, 'OBSERVATION_VERIFICATION_SECONDS', 0), patch.object(v.subprocess, 'run') as run:
            with self.assertRaisesRegex(v.Blocked, 'OBSERVATION_VERIFICATION_DEADLINE'):
                self.evaluate()
            run.assert_not_called()
        original_public = self.public.read_bytes()
        actual_run = subprocess.run
        def drifting_run(command, **kwargs):
            if command[:2] == ['openssl', 'dgst']:
                verification_path = Path(command[command.index('-verify') + 1])
                self.assertNotEqual(self.public, verification_path)
                self.assertEqual(original_public, verification_path.read_bytes())
                self.public.write_bytes(original_public + b'\nchanged-after-capture')
            return actual_run(command, **kwargs)
        with patch.object(v.subprocess, 'run', side_effect=drifting_run):
            with self.assertRaisesRegex(v.Blocked, 'INSTALLED_FILE_MISSING_OR_CHANGED'):
                self.evaluate()

    def test_compressed_transport_key_encoding_cannot_bypass_role_separation(self):
        compressed = self.directory / 'compressed-role-public.pem'
        subprocess.run(['openssl', 'ec', '-pubin', '-in', self.roles['executor']['publicAnchorFile'], '-conv_form', 'compressed',
                        '-out', str(compressed)], check=True, capture_output=True)
        self.roles['observer'].update(publicAnchorFile=str(compressed), publicAnchorSha256=v.digest(compressed.read_bytes()))
        with self.assertRaisesRegex(v.Blocked, 'ROLE_SEPARATION_MISSING'):
            v.validate_capability_trust(self.trust, self.roles)

    def test_retained_old_version_is_verifiable_without_expiry_or_renewal(self):
        _, public = self.key('current-capability')
        self.trust.append({**self.trust[0], 'keyVersion': 'current-v2', 'publicAnchorId': 'public-anchor-2',
                           'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes()), 'trustProfileRevision': 8})
        self.evaluate()

    def test_signed_payload_identity_and_signature_substitutions_are_rejected(self):
        for field in ('Issuer', 'Audience', 'TenantId', 'CapabilityKeyVersion', 'DeletionRequestId', 'BatchId', 'ManifestDigest', 'GuardStreamId'):
            with self.subTest(field=field):
                request = copy.deepcopy(self.request)
                request['Capability'][field] += '-substituted'
                with self.assertRaises((v.Failed, v.Blocked)):
                    v.validate_consumption_request(request, self.trust, self.roles)
        request = copy.deepcopy(self.request)
        request['DetachedJws'] = request['DetachedJws'][:-4] + 'AAAA'
        with self.assertRaisesRegex(v.Failed, 'SIGNATURE_INVALID'):
            v.validate_consumption_request(request, self.trust, self.roles)

    def test_header_anchor_version_key_and_unknown_field_substitutions_are_rejected_even_when_signed(self):
        original = v.strict_json(v.decode_base64url(self.request['DetachedJws'].split('.')[0]))
        for field, value in (('alg', 'HS256'), ('typ', 'other'), ('kid', 'other-anchor'), ('anchorVersion', 'v99'),
                             ('keyVersion', 'v99'), ('unknown', 'field')):
            with self.subTest(field=field):
                request = {**self.request, 'DetachedJws': self.sign(self.request['Capability'], {**original, field: value})}
                with self.assertRaisesRegex(v.Failed, 'HEADER_SUBSTITUTION'):
                    v.validate_consumption_request(request, self.trust, self.roles)

    def test_changed_trusted_anchor_identity_or_public_key_cannot_verify_original_signature(self):
        _, public = self.key('substituted-capability')
        for changes in ({'publicAnchorId': 'other'}, {'publicAnchorVersion': 'other'},
                        {'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes())},
                        {'issuer': 'other'}, {'audience': 'other'}, {'tenantId': 'other'}, {'keyVersion': 'other'}):
            with self.subTest(changes=changes):
                with self.assertRaises(v.Failed):
                    v.validate_consumption_request(self.request, [{**self.trust[0], **changes}], self.roles)

    def test_noncanonical_header_and_signature_over_noncanonical_payload_are_rejected(self):
        header = v.strict_json(v.decode_base64url(self.request['DetachedJws'].split('.')[0]))
        encoded = v.base64url(json.dumps(header, ensure_ascii=False).encode())
        raw = self.signature((encoded + '.' + v.base64url(v.capability_bytes(self.request['Capability']))).encode())
        with self.assertRaisesRegex(v.Failed, 'HEADER_SUBSTITUTION'):
            v.validate_consumption_request({**self.request, 'DetachedJws': encoded + '..' + v.base64url(raw)}, self.trust, self.roles)
        encoded = self.request['DetachedJws'].split('.')[0]
        raw = self.signature((encoded + '.' + v.base64url(json.dumps(self.request['Capability'], ensure_ascii=False).encode())).encode())
        with self.assertRaisesRegex(v.Failed, 'SIGNATURE_INVALID'):
            v.validate_consumption_request({**self.request, 'DetachedJws': encoded + '..' + v.base64url(raw)}, self.trust, self.roles)

    def test_bool_fraction_overflow_unknown_fields_and_invalid_unicode_are_rejected(self):
        for field in v.CAPABILITY_NUMBERS:
            for value in (True, 1.25, 2 ** 53, -1):
                with self.subTest(field=field, value=value):
                    payload = {**self.request['Capability'], field: value}
                    with self.assertRaisesRegex(v.Failed, 'ORDINAL_INVALID'):
                        v.capability_bytes(payload)
        for payload in ({**self.request['Capability'], 'ExpiresAt': 'tomorrow'},
                        {**self.request['Capability'], 'Issuer': '\ud800'}):
            with self.assertRaises(v.Failed):
                v.capability_bytes(payload)
        for field in ('CommittedIssuedGuardRevision', 'DispatchGuardRevision'):
            with self.assertRaisesRegex(v.Failed, 'ORDINAL_INVALID'):
                v.validate_consumption_request({**self.request, field: True}, self.trust, self.roles)

    def test_duplicate_members_padded_noncanonical_base64url_and_attached_payload_reject(self):
        header, _, signature = self.request['DetachedJws'].split('.')
        # The low four bits of the final ES256 base64url character must be zero.
        alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'
        alternate = signature[:-1] + alphabet[alphabet.index(signature[-1]) + 1]
        duplicate = v.base64url(b'{"alg":"ES256","alg":"ES256"}')
        for jws in (header + '=..' + signature, header + '..' + signature + '=', header + '..' + alternate,
                    duplicate + '..' + signature, header + '.payload.' + signature):
            with self.subTest(jws=jws[:20]):
                with self.assertRaises((v.Failed, v.Blocked)):
                    v.validate_consumption_request({**self.request, 'DetachedJws': jws}, self.trust, self.roles)
        with self.assertRaisesRegex(v.Blocked, 'DUPLICATE_JSON_MEMBER'):
            v.strict_json(b'{"Capability":{"BatchOrdinal":0,"BatchOrdinal":1}}')

    def test_target_sort_complete_identity_and_native_digest_reject_mutations(self):
        for targets in (list(reversed(self.targets)), [self.targets[0], self.targets[0]],
                        [{**self.targets[0], 'TenantId': 'other'}, self.targets[1]], self.targets[:1]):
            with self.subTest(targets=targets):
                with self.assertRaises(v.Failed):
                    v.validate_consumption_request({**self.request, 'Targets': targets}, self.trust, self.roles)
        payload = {**self.request['Capability'], 'ManifestDigest': v.digest(v.canonical([list(v.target_key(t)) for t in self.targets])).upper()}
        with self.assertRaisesRegex(v.Failed, 'NATIVE_MANIFEST'):
            v.validate_consumption_request({**self.request, 'Capability': payload}, self.trust, self.roles)

    def test_missing_ambiguous_or_changed_issue_dispatch_and_batch_records_reject(self):
        mutations = [lambda o: o['guardReceipts'].pop(0), lambda o: o['guardReceipts'].pop(1),
                     lambda o: o['guardReceipts'].append(copy.deepcopy(o['guardReceipts'][0])),
                     lambda o: o['guardBatches'].append(copy.deepcopy(o['guardBatches'][0]))]
        for index in (0, 1):
            for field, value in (('ReceiptId', 'other'), ('ReferenceId', 'other-batch'), ('GuardHighWater', 100),
                                 ('SigningRequestId', '0' * 64), ('DetachedJwsDigest', '0' * 64), ('Capability', None)):
                mutations.append(lambda o, i=index, f=field, x=value: o['guardReceipts'][i].__setitem__(f, x))
        for field, value in (('IssueReceiptId', 'missing'), ('DispatchReceiptId', 'missing'), ('IssuedGuardRevision', 23),
                             ('DispatchGuardRevision', 26), ('SigningRequestId', 'other'), ('Targets', self.targets[:1]),
                             ('AttestationDigest', '0' * 64), ('CapabilityKeyVersion', 'other')):
            mutations.append(lambda o, f=field, x=value: o['guardBatches'][0].__setitem__(f, x))
        for index, mutate in enumerate(mutations):
            with self.subTest(index=index):
                observed = copy.deepcopy(self.observation)
                mutate(observed)
                with self.assertRaises((v.Failed, v.Blocked)):
                    self.evaluate(observed)

    def test_native_owner_provider_retry_lookup_receipt_contradictions_reject(self):
        for field in ('ownerResults', 'providerResults', 'ownerRetryResults', 'ownerLookupResults', 'providerLookupResults'):
            for receipt_field, value in (('ReceiptId', 'different-original'), ('OriginalBatchId', 'other-batch'),
                                         ('Target', {**self.targets[0], 'AgentInteractionId': 'substituted'})):
                with self.subTest(field=field, receipt_field=receipt_field):
                    observed = copy.deepcopy(self.observation)
                    observed[field][0]['TargetReceipts'][0][receipt_field] = value
                    with self.assertRaises(v.Failed):
                        self.evaluate(observed)
            observed = copy.deepcopy(self.observation)
            observed[field] = []
            with self.assertRaises(v.Failed):
                self.evaluate(observed)
        for field, value in (('TenantId', 'other'), ('BatchId', 'other'), ('ReservationReceiptId', 'other'), ('State', 0)):
            observed = copy.deepcopy(self.observation)
            observed['providerResults'][0][field] = value
            with self.assertRaises(v.Failed):
                self.evaluate(observed)

    def signing_chain(self, attestation_ordinal=1, block_set_revision=0):
        observed = copy.deepcopy(self.observation)
        final_request = copy.deepcopy(self.request)
        if attestation_ordinal != final_request['Capability']['AttestationOrdinal']:
            final_request['Capability']['AttestationOrdinal'] = attestation_ordinal
            final_request['DetachedJws'] = self.sign(final_request['Capability'])
        final_signing_id = v.digest(v.capability_bytes(final_request['Capability'])).upper()
        final_signature_digest = v.digest(final_request['DetachedJws'].encode()).upper()
        observed['consumptionRequests'] = [copy.deepcopy(final_request)]
        observed['guardBatches'][0].update(Capability=copy.deepcopy(final_request['Capability']), DetachedJws=final_request['DetachedJws'],
                                          AttestationOrdinal=attestation_ordinal, SigningRequestId=final_signing_id,
                                          AttestationDigest=final_signature_digest, BlockSetRevision=block_set_revision)
        for receipt in observed['guardReceipts']:
            receipt.update(Capability=copy.deepcopy(final_request['Capability']), SigningRequestId=final_signing_id,
                           DetachedJwsDigest=final_signature_digest)
        for field in ('ownerResults', 'ownerRetryResults', 'ownerLookupResults'):
            for outcome in observed[field]:
                outcome['KeyBlockSetRevision'] = block_set_revision
        observed['ownerBatches'][0].update(Original=copy.deepcopy(final_request), Current=copy.deepcopy(final_request))
        observed['ownerBatches'][0]['Outcome']['KeyBlockSetRevision'] = block_set_revision
        second_private, second_public = self.key('second-capability')
        second_trust = {**self.trust[0], 'keyVersion': 'rotated-v2', 'publicAnchorId': 'public-anchor-2',
                        'publicAnchorFile': str(second_public), 'publicAnchorSha256': v.digest(second_public.read_bytes())}
        self.trust.append(second_trust)
        observed.update(signingAttempts=[], signingOriginals=[], noIssueOriginals=[], signingRecoveries=[], noIssueIntents=[])
        final = final_request['Capability']
        for index, revision, key_version in ((0, 10, 'retained-v1'), (1, 16, 'rotated-v2'), (2, 23, 'retained-v1')):
            payload = {**final, 'SigningAttemptOrdinal': index + 1, 'IntendedIssuedGuardRevision': revision,
                       'CapabilityKeyVersion': key_version}
            entry = self.trust[1 if index == 1 else 0]
            if index == 2:
                jws = final_request['DetachedJws']
            else:
                header = v.base64url(v.canonical({'alg': 'ES256', 'anchorVersion': entry['publicAnchorVersion'],
                                                'keyVersion': key_version, 'kid': entry['publicAnchorId'], 'typ': 'DeletionBatchCapabilityV1'}))
                jws = header + '..' + v.base64url(self.signature((header + '.' + v.base64url(v.capability_bytes(payload))).encode(),
                                                               second_private if index == 1 else self.private))
            signing_id, signature_digest = v.digest(v.capability_bytes(payload)).upper(), v.digest(jws.encode()).upper()
            original = {'SigningRequestId': signing_id, 'Payload': payload, 'State': 5 if index < 2 else 0,
                        'DetachedJws': jws, 'PublicAnchorId': entry['publicAnchorId'], 'PublicAnchorVersion': entry['publicAnchorVersion']}
            proof_id = None if index == 2 else 'native-no-issue-' + str(index)
            summary = {key: payload[key] for key in ('DestructionSealId', 'BatchId', 'ManifestDigest', 'AttestationOrdinal', 'SigningAttemptOrdinal')}
            summary.update(status='Signed' if index == 2 else 'SignedAttestationObsoleteUnissued', exactNoIssueReceiptId=proof_id)
            observed['signingAttempts'].append(summary)
            observed['signingOriginals'].append(original)
            if index == 2:
                continue
            proof = {'Payload': payload, 'SigningRequestId': signing_id, 'DetachedJwsDigest': signature_digest,
                     'ProofId': proof_id, 'CurrentGuardRevision': revision + 2, 'CurrentHealthyKeyVersion': key_version,
                     'AuthorityRevision': 'original-authority-' + str(index),
                     'ObservedAt': '2026-10-18T12:00:0' + str(index * 2) + '.1234567+00:00',
                     'ValidUntil': '2026-10-18T12:00:0' + str(index * 2 + 1) + '.1234567+00:00'}
            original['NoIssueProof'] = proof
            recovery = {key: value for key, value in proof.items() if key != 'ProofId'}
            recovery.update(NoIssueReceiptId=proof_id, CurrentGuardRevision=16 if index == 0 else 23,
                            CurrentHealthyKeyVersion='rotated-v2' if index == 0 else 'retained-v1',
                            AuthorityRevision='fresh-authority-' + str(index),
                            ObservedAt='2026-10-18T12:00:0' + str(index * 2 + 1) + '.1234567+00:00',
                            ValidUntil='2026-10-18T12:00:0' + str(index * 2 + 2) + '.1234567+00:00')
            receipt = copy.deepcopy(self.observation['guardReceipts'][0])
            receipt.update(OperationId='issue-' + v.digest([signing_id, signature_digest]).upper(), Status='Stale',
                           GuardHighWater=revision + 1, AcceptedAtAdmissionFenceOrdinal=0, ReferenceId='', ReceiptId=proof_id,
                           Capability=payload, SigningRequestId=signing_id, DetachedJwsDigest=signature_digest)
            intent = self.native_no_issue_intent(original, operation=23 if attestation_ordinal > 1 else 18, block_set_revision=block_set_revision)
            receipt['IntentDigest'] = v.digest(base64.b64decode(intent['BytesBase64'])).upper()
            observed['noIssueIntents'].append(intent)
            observed['noIssueOriginals'].append(receipt)
            observed['signingRecoveries'].append(recovery)
        return observed

    def native_no_issue_intent(self, original, operation=18, block_set_revision=0):
        payload = original['Payload']
        batch = {'BatchId': payload['BatchId'], 'BatchKind': payload['BatchKind'], 'BatchOrdinal': payload['BatchOrdinal'],
                 'Targets': self.targets, 'ManifestDigest': payload['ManifestDigest'], 'AttestationOrdinal': payload['AttestationOrdinal'],
                 'CapabilityKeyVersion': payload['CapabilityKeyVersion'], 'AttestationDigest': v.digest(original['DetachedJws'].encode()).upper(),
                 'ProtectionOutcome': '', 'ProtectionReceiptId': '', 'TargetReceiptIds': [], 'BlockSetRevision': block_set_revision,
                 'Capability': {key: payload[key] for key in ('Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion').split()},
                 'DetachedJws': original['DetachedJws'], 'SigningRequestId': original['SigningRequestId']}
        batch['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in self.targets]
        command = {'TenantId': payload['TenantId'], 'OperationId': 'issue-' + v.digest([original['SigningRequestId'], batch['AttestationDigest']]).upper(),
                   'Operation': operation, 'ExpectedGuardRevision': payload['IntendedIssuedGuardRevision'], 'EpochId': '',
                   'DeletionRequestId': payload['DeletionRequestId'], 'Scope': None, 'Write': None, 'Repair': None, 'Ordinal': None,
                   'Hold': None, 'Batch': batch, 'AuthorizationId': '', 'ReferenceId': '', 'BridgeOriginal': None, 'RevocationReceipt': None}
        raw = v.native_json_bytes({'Transition': command, 'TargetMutationDigest': v.digest(b'[]').upper()})
        return {'BytesBase64': base64.b64encode(raw).decode()}

    def rewrite_no_issue_intent(self, observed, index, intent):
        raw = v.native_json_bytes(intent)
        observed['noIssueIntents'][index] = {'BytesBase64': base64.b64encode(raw).decode()}
        observed['noIssueOriginals'][index]['IntentDigest'] = v.digest(raw).upper()

    def evaluate_signing(self, observed):
        v.evaluate(observed, {'steps': [], 'checks': ['erased', 'receipts', 'signing-attempts']},
                   self.targets, b'offline-canary', 'a' * 40, 'b' * 64, self.trust, self.roles)

    def test_native_two_stale_then_signed_chain_with_fresh_successor_basis_passes(self):
        observed = self.signing_chain()
        self.evaluate_signing(observed)
        # The signed original alone has no issue/dispatch and is verified directly.
        first = observed['signingOriginals'][0]
        self.assertEqual(first['SigningRequestId'], v.validate_capability_signature(first['Payload'], first['DetachedJws'], self.trust, self.roles)[0])
        observed['noIssueOriginals'][1].update(Status='IssuanceStale', ReferenceId=self.request['Capability']['BatchId'], AcceptedAtAdmissionFenceOrdinal=1,
                                             GuardHighWater=observed['signingOriginals'][1]['Payload']['IntendedIssuedGuardRevision'])
        intent = v.strict_json(base64.b64decode(observed['noIssueIntents'][1]['BytesBase64']))
        intent['Transition'].update(Operation=19, AuthorizationId='native-stale-authorization')
        self.rewrite_no_issue_intent(observed, 1, intent)
        self.evaluate_signing(observed)
        for count, accepted in ((1024, True), (1025, False)):
            bounded = copy.deepcopy(observed)
            bounded_intent = v.strict_json(base64.b64decode(bounded['noIssueIntents'][1]['BytesBase64']))
            bounded_intent['Transition']['AuthorizationId'] = '😀' * count
            self.rewrite_no_issue_intent(bounded, 1, bounded_intent)
            if accepted:
                self.evaluate_signing(bounded)
            else:
                with self.assertRaisesRegex(v.Failed, 'NO_ISSUE_COMMAND_AUTHORIZATION_INVALID'):
                    self.evaluate_signing(bounded)
        observed['noIssueOriginals'][1]['AcceptedAtAdmissionFenceOrdinal'] = 0
        with self.assertRaisesRegex(v.Failed, 'NO_ISSUE_DELETION_ORDINAL_INVALID'):
            self.evaluate_signing(observed)

    def test_native_replacement_signing_chain_retains_attestation_and_block_set_revision(self):
        observed = self.signing_chain(attestation_ordinal=2, block_set_revision=7)
        self.evaluate_signing(observed)
        self.assertEqual([2, 2, 2], [original['Payload']['AttestationOrdinal'] for original in observed['signingOriginals']])
        for value in observed['noIssueIntents']:
            command = v.strict_json(base64.b64decode(value['BytesBase64']))['Transition']
            self.assertEqual(23, command['Operation'])
            self.assertEqual(2, command['Batch']['AttestationOrdinal'])
            self.assertEqual(7, command['Batch']['BlockSetRevision'])
        self.assertEqual(7, observed['guardBatches'][0]['BlockSetRevision'])
        self.assertEqual(7, observed['ownerResults'][0]['KeyBlockSetRevision'])

    def test_signing_chain_reuses_one_verification_budget_cache_and_rechecks_all_pins(self):
        observed = self.signing_chain()
        with patch.object(v.subprocess, 'run', wraps=subprocess.run) as run:
            self.evaluate_signing(observed)
        commands = [call.args[0] for call in run.call_args_list]
        self.assertEqual(3, sum(command[:2] == ['openssl', 'dgst'] for command in commands))
        with patch.object(v, 'OBSERVATION_VERIFICATION_SECONDS', 0), patch.object(v.subprocess, 'run') as run:
            with self.assertRaisesRegex(v.Blocked, 'OBSERVATION_VERIFICATION_DEADLINE'):
                self.evaluate_signing(observed)
            run.assert_not_called()
        actual_run = subprocess.run
        def drift_after_stale_signature(command, **kwargs):
            result = actual_run(command, **kwargs)
            if command[:2] == ['openssl', 'dgst']:
                self.public.write_bytes(self.public.read_bytes() + b'\nchanged')
            return result
        with patch.object(v.subprocess, 'run', side_effect=drift_after_stale_signature):
            with self.assertRaisesRegex(v.Blocked, 'INSTALLED_FILE_MISSING_OR_CHANGED'):
                self.evaluate_signing(observed)

    def test_signing_native_missing_duplicate_summary_schema_and_typed_ordinals_reject(self):
        baseline = self.signing_chain()
        for field in ('signingOriginals', 'noIssueOriginals', 'signingRecoveries', 'noIssueIntents'):
            for mutation in ('missing', 'empty', 'duplicate', 'unmatched'):
                with self.subTest(field=field, mutation=mutation):
                    observed = copy.deepcopy(baseline)
                    if mutation == 'missing':
                        observed.pop(field)
                    elif mutation == 'empty':
                        observed[field] = []
                    elif mutation == 'duplicate':
                        observed[field][1] = copy.deepcopy(observed[field][0])
                    else:
                        observed[field].append(copy.deepcopy(observed[field][0]))
                    with self.assertRaises(v.Failed):
                        self.evaluate_signing(observed)
        mutations = [(['signingOriginals', 0, 'State'], True), (['signingOriginals', 0, 'State'], '5'),
                     (['signingOriginals', 2, 'State'], 5), (['signingOriginals', 0, 'Extra'], 'forged'),
                     (['signingOriginals', 0, 'Payload', 'SigningAttemptOrdinal'], True),
                     (['signingAttempts', 0, 'SigningAttemptOrdinal'], True),
                     (['signingAttempts', 0, 'AttestationOrdinal'], True),
                     (['signingAttempts', 1, 'SigningAttemptOrdinal'], 3),
                     (['signingAttempts', 0, 'status'], 'Signed'),
                     (['signingAttempts', 2, 'exactNoIssueReceiptId'], 'invented'),
                     (['signingOriginals', 2, 'NoIssueProof'], baseline['signingOriginals'][0]['NoIssueProof']),
                     (['noIssueOriginals', 0, 'GuardHighWater'], True),
                     (['noIssueOriginals', 0, 'AcceptedAtAdmissionFenceOrdinal'], False),
                     (['signingRecoveries', 0, 'CurrentGuardRevision'], True)]
        for path, value in mutations:
            with self.subTest(path=path):
                observed = copy.deepcopy(baseline)
                destination = observed
                for key in path[:-1]:
                    destination = destination[key]
                destination[path[-1]] = copy.deepcopy(value)
                with self.assertRaises(v.Failed):
                    self.evaluate_signing(observed)

    def test_signing_native_coherent_payload_and_signature_substitutions_reject(self):
        baseline = self.signing_chain()
        for field, value in (('Issuer', 'other'), ('Audience', 'other'), ('TenantId', 'tenant-b'),
                             ('DeletionRequestId', 'other'), ('DestructionSealId', 'other'), ('BatchKind', 'Containment'),
                             ('BatchOrdinal', 1), ('BatchId', 'other-batch'), ('ManifestDigest', 'F' * 64),
                             ('GuardStreamId', 'other-guard'), ('AttestationOrdinal', 2)):
            with self.subTest(field=field):
                observed = copy.deepcopy(baseline)
                original = observed['signingOriginals'][0]
                original['Payload'][field] = value
                # Retain native correlations and a valid signature where the same trust applies.
                original['DetachedJws'] = self.sign(original['Payload'])
                original['SigningRequestId'] = v.digest(v.capability_bytes(original['Payload'])).upper()
                proof = original['NoIssueProof']
                proof.update(Payload=copy.deepcopy(original['Payload']), SigningRequestId=original['SigningRequestId'],
                             DetachedJwsDigest=v.digest(original['DetachedJws'].encode()).upper())
                observed['signingRecoveries'][0].update(Payload=copy.deepcopy(proof['Payload']), SigningRequestId=proof['SigningRequestId'], DetachedJwsDigest=proof['DetachedJwsDigest'])
                receipt = observed['noIssueOriginals'][0]
                receipt.update(Capability=copy.deepcopy(proof['Payload']), SigningRequestId=proof['SigningRequestId'], DetachedJwsDigest=proof['DetachedJwsDigest'],
                               OperationId='issue-' + v.digest([proof['SigningRequestId'], proof['DetachedJwsDigest']]).upper())
                for key in ('DestructionSealId', 'BatchId', 'ManifestDigest', 'AttestationOrdinal'):
                    observed['signingAttempts'][0][key] = original['Payload'][key]
                with self.assertRaises(v.Failed):
                    self.evaluate_signing(observed)
        observed = copy.deepcopy(baseline)
        original = observed['signingOriginals'][0]
        header = original['DetachedJws'].split('.')[0]
        original['DetachedJws'] = header + '..' + v.base64url(bytes(64))
        signature_digest = v.digest(original['DetachedJws'].encode()).upper()
        original['NoIssueProof']['DetachedJwsDigest'] = signature_digest
        observed['signingRecoveries'][0]['DetachedJwsDigest'] = signature_digest
        observed['noIssueOriginals'][0].update(DetachedJwsDigest=signature_digest,
                                            OperationId='issue-' + v.digest([original['SigningRequestId'], signature_digest]).upper())
        with self.assertRaisesRegex(v.Failed, 'CAPABILITY_SIGNATURE_INVALID'):
            self.evaluate_signing(observed)

    def test_obsolete_signatures_cannot_also_be_committed_or_reuse_final_issue_receipt_identity(self):
        baseline = self.signing_chain()
        for match in ('SigningRequestId', 'DetachedJwsDigest', 'Capability', 'OperationId'):
            with self.subTest(committed_match=match):
                observed = copy.deepcopy(baseline)
                stale = observed['noIssueOriginals'][0]
                committed = copy.deepcopy(observed['guardReceipts'][0])
                committed.update(OperationId='another-committed-operation', ReceiptId='another-committed-receipt')
                committed[match] = copy.deepcopy(stale[match])
                observed['guardReceipts'].append(committed)
                with self.assertRaises(v.Failed):
                    self.evaluate_signing(observed)
        observed = copy.deepcopy(baseline)
        reused = observed['guardReceipts'][0]['ReceiptId']
        observed['noIssueOriginals'][0]['ReceiptId'] = reused
        observed['signingOriginals'][0]['NoIssueProof']['ProofId'] = reused
        observed['signingRecoveries'][0]['NoIssueReceiptId'] = reused
        observed['signingAttempts'][0]['exactNoIssueReceiptId'] = reused
        with self.assertRaisesRegex(v.Failed, 'SIGNING_GUARD_ORIGINAL_IDENTITY_REUSED'):
            self.evaluate_signing(observed)
        # The exact same immutable stale receipt may appear in both independent inventories.
        observed = copy.deepcopy(baseline)
        observed['guardReceipts'].extend(copy.deepcopy(observed['noIssueOriginals']))
        self.evaluate_signing(observed)

    def test_no_issue_intent_requires_native_original_bytes_and_rejects_coherent_command_substitutions(self):
        baseline = self.signing_chain()
        observed = copy.deepcopy(baseline)
        observed['noIssueOriginals'][0]['IntentDigest'] = 'F' * 64
        with self.assertRaisesRegex(v.Failed, 'NO_ISSUE_INTENT_DIGEST_MISMATCH'):
            self.evaluate_signing(observed)
        mutations = [(['Transition', 'TenantId'], 'tenant-b'), (['Transition', 'DeletionRequestId'], 'other-request'),
                     (['Transition', 'OperationId'], 'caller-derived-issue'), (['Transition', 'Operation'], 21),
                     (['Transition', 'Operation'], True), (['Transition', 'ExpectedGuardRevision'], 99),
                     (['Transition', 'Write'], {}), (['Transition', 'EpochId'], 'unrelated-epoch'),
                     (['Transition', 'AuthorizationId'], 'unexpected-authorization'),
                     (['Transition', 'Batch', 'Targets', 0, 'TargetProtectionKeyAlias'], 'other-alias'),
                     (['Transition', 'Batch', 'BatchId'], 'other-batch'),
                     (['Transition', 'Batch', 'ManifestDigest'], 'F' * 64),
                     (['Transition', 'Batch', 'SigningRequestId'], 'F' * 64),
                     (['Transition', 'Batch', 'DetachedJws'], 'other-signature'),
                     (['Transition', 'Batch', 'AttestationDigest'], 'F' * 64),
                     (['Transition', 'Batch', 'ProtectionOutcome'], 'Consumed'),
                     (['Transition', 'Batch', 'BlockSetRevision'], 1),
                     (['Transition', 'Batch', 'Capability', 'TenantId'], 'tenant-b'),
                     (['TargetMutationDigest'], 'F' * 64)]
        for path, value in mutations:
            with self.subTest(path=path):
                observed = copy.deepcopy(baseline)
                intent = v.strict_json(base64.b64decode(observed['noIssueIntents'][0]['BytesBase64']))
                destination = intent
                for key in path[:-1]:
                    destination = destination[key]
                destination[path[-1]] = value
                # Keep the receipt hash coherent with the substituted full raw command.
                if type(value) is bool:
                    raw = v.canonical(intent)
                    observed['noIssueIntents'][0]['BytesBase64'] = base64.b64encode(raw).decode()
                    observed['noIssueOriginals'][0]['IntentDigest'] = v.digest(raw).upper()
                else:
                    self.rewrite_no_issue_intent(observed, 0, intent)
                with self.assertRaises(v.Failed):
                    self.evaluate_signing(observed)
        observed = copy.deepcopy(baseline)
        raw = base64.b64decode(observed['noIssueIntents'][0]['BytesBase64'])
        observed['noIssueIntents'][0]['BytesBase64'] = base64.b64encode(raw + b' ').decode()
        observed['noIssueOriginals'][0]['IntentDigest'] = v.digest(raw + b' ').upper()
        with self.assertRaisesRegex(v.Failed, 'NO_ISSUE_INTENT_NATIVE_BYTES_MISMATCH'):
            self.evaluate_signing(observed)

    def test_signing_original_recovery_guard_identity_key_revision_and_time_substitutions_reject(self):
        baseline = self.signing_chain()
        mutations = [(['signingOriginals', 0, 'PublicAnchorId'], 'other'),
                     (['signingOriginals', 0, 'PublicAnchorVersion'], 'other'),
                     (['signingOriginals', 0, 'SigningRequestId'], 'F' * 64),
                     (['signingOriginals', 0, 'NoIssueProof', 'ProofId'], 'other'),
                     (['signingOriginals', 0, 'NoIssueProof', 'Payload', 'Audience'], 'other'),
                     (['signingOriginals', 0, 'NoIssueProof', 'CurrentGuardRevision'], 10),
                     (['signingOriginals', 0, 'NoIssueProof', 'ObservedAt'], 'not-a-timestamp'),
                     (['signingOriginals', 0, 'NoIssueProof', 'ValidUntil'], '2026-10-18T12:00:00.1234567Z'),
                     (['signingRecoveries', 0, 'NoIssueReceiptId'], 'other'),
                     (['signingRecoveries', 0, 'SigningRequestId'], 'F' * 64),
                     (['signingRecoveries', 0, 'DetachedJwsDigest'], 'F' * 64),
                     (['signingRecoveries', 0, 'CurrentHealthyKeyVersion'], 'wrong-current-key'),
                     (['signingRecoveries', 0, 'CurrentGuardRevision'], 17),
                     (['signingRecoveries', 0, 'AuthorityRevision'], ''),
                     (['signingRecoveries', 0, 'ObservedAt'], '2026-10-18T12:00:00.1234566Z'),
                     (['signingRecoveries', 0, 'ObservedAt'], '2026-10-18T12:00:01.1234567+14:01'),
                     (['signingRecoveries', 0, 'ValidUntil'], '2026-10-18T12:00:01.1234567Z'),
                     (['noIssueOriginals', 0, 'OperationId'], 'caller-issue-operation'),
                     (['noIssueOriginals', 0, 'ReceiptId'], 'other'), (['noIssueOriginals', 0, 'ReferenceId'], 'other-batch'),
                     (['noIssueOriginals', 0, 'Capability', 'TenantId'], 'tenant-b'),
                     (['noIssueOriginals', 0, 'Status'], 'Committed'), (['noIssueOriginals', 0, 'GuardHighWater'], 100),
                     (['noIssueOriginals', 0, 'SigningRequestId'], 'F' * 64),
                     (['noIssueOriginals', 0, 'DetachedJwsDigest'], 'F' * 64),
                     (['noIssueOriginals', 0, 'AcceptedWriteResourceId'], 'effect'),
                     (['signingAttempts', 0, 'exactNoIssueReceiptId'], 'other')]
        for path, value in mutations:
            with self.subTest(path=path):
                observed = copy.deepcopy(baseline)
                destination = observed
                for key in path[:-1]:
                    destination = destination[key]
                destination[path[-1]] = value
                with self.assertRaises(v.Failed):
                    self.evaluate_signing(observed)
        observed = copy.deepcopy(baseline)
        # Another genuine signature over the same final payload is not the consumed original signature.
        observed['signingOriginals'][2]['DetachedJws'] = self.sign(observed['signingOriginals'][2]['Payload'])
        with self.assertRaisesRegex(v.Failed, 'FINAL_SIGNING_CONSUMPTION_MISMATCH'):
            self.evaluate_signing(observed)

    def test_irreversible_flags_and_summary_capabilities_cannot_replace_native_originals(self):
        observed = copy.deepcopy(self.observation)
        observed.pop('consumptionRequests')
        observed['capabilities'] = [self.request['Capability']]
        observed['targetReceipts'] = [{'target': t, 'receiptId': str(i), 'irreversible': True} for i, t in enumerate(self.targets)]
        observed['guardDispatch'] = {'soleActiveAttestation': True, 'lookupConsistency': 'Linearizable'}
        with self.assertRaisesRegex(v.Failed, 'CONSUMPTION_REQUESTS_MISSING'):
            self.evaluate(observed)
        observed = copy.deepcopy(self.observation)
        observed['providerResults'][0]['TargetReceipts'][0]['irreversible'] = True
        with self.assertRaisesRegex(v.Failed, 'TARGET_RECEIPT_SCHEMA_INVALID'):
            self.evaluate(observed)

    def test_trust_anchor_missing_drift_wrong_curve_transport_substitution_and_ambiguity_block(self):
        bad_sets = [[{**self.trust[0], 'publicAnchorFile': str(self.directory / 'absent.pem')}], self.trust * 2,
                    [{**self.trust[0], 'keyFamily': 'OtherKeyFamily'}]]
        transport = self.roles['observer']
        bad_sets.append([{**self.trust[0], 'publicAnchorFile': transport['publicAnchorFile'], 'publicAnchorSha256': transport['publicAnchorSha256']}])
        # Public-only SEC2 P-384 generator point; no non-P256 key pair is generated.
        point = bytes.fromhex('04AA87CA22BE8B05378EB1C71EF320AD746E1D3B628BA79B9859F741E082542A385502F25DBF55296C3A545E3872760AB7'
                              '3617DE4A96262C6F5D9E98BF9292DC29F8F41DBD289A147CE9DA3113B5F0B8C00A60B1CE1D7E819D7A431D7C90EA0E5F')
        public_der = bytes.fromhex('3076301006072A8648CE3D020106052B81040022036200') + point
        wrong = self.directory / 'wrong-curve-public.pem'
        wrong.write_bytes(b'-----BEGIN PUBLIC KEY-----\n' + base64.encodebytes(public_der) + b'-----END PUBLIC KEY-----\n')
        bad_sets.append([{**self.trust[0], 'publicAnchorFile': str(wrong), 'publicAnchorSha256': v.digest(wrong.read_bytes())}])
        for trust in bad_sets:
            with self.assertRaises(v.Blocked):
                v.validate_capability_trust(trust, self.roles)
        self.public.write_bytes(b'changed-public-anchor')
        with self.assertRaisesRegex(v.Blocked, 'MISSING_OR_CHANGED'):
            v.validate_capability_trust(self.trust, self.roles)

    def test_capability_public_key_cannot_be_reused_across_tenant_key_families(self):
        with self.assertRaisesRegex(v.Blocked, 'CAPABILITY_CROSS_TENANT_KEY_REUSE'):
            v.validate_capability_trust([*self.trust, {**self.trust[0], 'tenantId': 'other-tenant'}], self.roles)

    def test_all_anchor_pins_are_rechecked_before_a_later_role_call(self):
        engine = self.directory / 'engine.dll'
        engine.write_bytes(b'offline-binary')
        config = self.roles['executor']
        request = {'operationId': 'offline-original-call', 'action': 'InspectInstallation', 'bindingDigest': 'binding'}
        statement = {'role': 'executor', 'identity': config['identity'], 'bindingDigest': 'binding',
                     'operationId': request['operationId'], 'requestSha256': v.digest(request), 'response': {'status': 'OfflineOnly'}}
        envelope = {'statement': statement, 'signature': base64.b64encode(self.signature(v.canonical(statement), self.directory / 'executor.private.pem')).decode()}
        executable = Path(config['executable'])
        executable.write_text("#!/bin/sh\ncat <<'OFFLINE_JSON'\n" + json.dumps(envelope) + '\nOFFLINE_JSON\n')
        config['executableSha256'] = v.digest(executable.read_bytes())
        manifest = Path(config['installationManifest'])
        manifest.write_bytes(v.canonical({'files': [{'path': str(executable), 'sha256': config['executableSha256']}]}))
        config['installationManifestSha256'] = v.digest(manifest.read_bytes())
        role = v.Role('executor', config, 'binding', self.directory,
                      {'path': str(engine), 'sha256': v.digest(engine.read_bytes())}, self.trust, self.roles)
        with patch.dict(v.os.environ, {'OFFLINE_EXECUTOR': 'offline-value'}):
            self.assertEqual(envelope, role.request(request))
        for path in (self.public, Path(self.roles['observer']['publicAnchorFile'])):
            original = path.read_bytes()
            path.write_bytes(original + b'\nchanged')
            with self.assertRaisesRegex(v.Blocked, 'MISSING_OR_CHANGED'):
                role.request({'operationId': 'later-call'})
            path.write_bytes(original)

    def test_profile_v2_hash_binds_separate_capability_trust_and_rejects_v1(self):
        engine = self.directory / 'engine.dll'
        engine.write_bytes(b'offline-binary')
        profile = {'schema': 'ext-protection-1-profile/v2', 'targetCommit': 'a' * 40, 'planSha256': 'b' * 64,
                   'tenantReferences': {'primary': 'tenant-a', 'other': 'other'}, 'roles': self.roles,
                   'engineBinary': {'path': str(engine), 'sha256': v.digest(engine.read_bytes())}, 'capabilityTrust': self.trust}
        path = self.directory / 'profile.json'
        with patch.dict(v.os.environ, {'OFFLINE_' + name.upper(): 'offline-value' for name in v.ROLE_NAMES}):
            for schema in ('ext-protection-1-profile/v2', 'ext-protection-1-profile/v1'):
                profile['schema'] = schema
                path.write_bytes(v.canonical(profile))
                record = {'profileSha256': v.digest(path.read_bytes()), 'TargetVersionOrCommit': 'a' * 40}
                if schema.endswith('/v2'):
                    self.assertEqual(profile, v.validate_profile(path, record, 'b' * 64))
                else:
                    with self.assertRaisesRegex(v.Blocked, 'PROFILE_BINDING_MISMATCH'):
                        v.validate_profile(path, record, 'b' * 64)


if __name__ == '__main__':
    unittest.main()
