"""Actual emitted source-catalog native outcomes, exact result-loss lookup and reservation-recovery originals."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('ext_protection_loop7_recovery', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class ActualNativeRecoveryEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-loop7-recovery-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        supplied = os.environ.get('EXT_PROTECTION_LOOP7_COMPONENT_ASSEMBLY')
        if supplied:
            assembly = Path(supplied)
            if not assembly.is_absolute() or assembly.is_symlink() or not assembly.is_file() or hashlib.sha256(assembly.read_bytes()).hexdigest() != os.environ.get('EXT_PROTECTION_LOOP7_COMPONENT_SHA256'):
                raise ValueError('Exact independently hash-pinned component test assembly required')
        else:
            platform = ROOT.parent / 'platform'
            if not (platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj').is_file(): platform = ROOT / 'references/Hexalith.Platform'
            output = cls.directory / 'bin'
            source_build_env = {key: value for key, value in os.environ.items() if key != 'CI'}
            built = subprocess.run(['dotnet', 'build', str(platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj'), '-c', 'Debug',
                '-p:UseHexalithProjectReferences=true', '-p:HexalithEventStoreRoot=' + str(ROOT), '-p:OutputPath=' + str(output) + '/',
                '-p:AppendTargetFrameworkToOutputPath=false', '-p:GeneratePackageOnBuild=false', '-m:1', '-nodeReuse:false'], cwd=platform, env=source_build_env, capture_output=True, text=True, timeout=300, check=False)
            if built.returncode: raise AssertionError(built.stdout + built.stderr)
            assembly = output / 'Hexalith.Platform.Protection.Tests.dll'
        executed = subprocess.run(['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ProtectionQualificationLoop7RecoveryTests', '-noColor'],
            env=dict(os.environ, EXT_PROTECTION_LOOP7_VECTOR_DIRECTORY=str(cls.directory)), capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode: raise AssertionError(executed.stdout + executed.stderr)
        cls.vectors = {case_id: json.loads((cls.directory / (case_id + '.json')).read_bytes()) for case_id in ('P06', 'P25', 'P28', 'P35', 'P36')}
        cls.cases = {case['id']: case for case in json.loads((ROOT / 'tools/ext-protection-1/plan.json').read_bytes())['cases']}

    def verify(self, case_id, observation):
        vector = self.vectors[case_id]
        anchor = self.directory / (case_id + '.pem'); anchor.write_text(vector['publicKeyPem'])
        trust = [{'issuer': 'qualified-issuer', 'audience': 'protection-audience', 'tenantId': 'tenant-a', 'keyFamily': 'DeletionBatchCapabilitySigningKey',
                  'keyVersion': version, 'publicAnchorId': 'test-anchor', 'publicAnchorVersion': '1', 'publicAnchorFile': str(anchor),
                  'publicAnchorSha256': hashlib.sha256(anchor.read_bytes()).hexdigest(), 'trustProfileRevision': 1} for version in ('healthy-key-a', 'healthy-key-b')]
        v.evaluate(observation, self.cases[case_id], vector['targets'], b'recovery-canary', vector['engineCommit'], vector['engineBinarySha256'], trust, {})

    def test_all_actual_lost_result_and_reserved_recovery_vectors_pass(self):
        for case_id, vector in self.vectors.items():
            with self.subTest(case=case_id): self.verify(case_id, vector['observation'])

    def test_exact_results_require_real_unique_calls_requests_and_native_terminals(self):
        for case_id in ('P06', 'P35', 'P36'):
            for mutation in ('null', 'fake', 'duplicate', 'call', 'operation', 'request', 'native-original', 'owner-original', 'receipt'):
                changed = copy.deepcopy(self.vectors[case_id]['observation'])
                if mutation == 'null': changed['exactResults'] = [None, None]
                elif mutation == 'fake': changed['exactResults'] = [{'ok': True}, {'ok': True}]
                elif mutation == 'duplicate': changed['exactResults'][1] = copy.deepcopy(changed['exactResults'][0])
                elif mutation == 'call': changed['exactResults'][0]['Call']['Id'] = 'undispatched'
                elif mutation == 'operation': changed['exactResults'][0]['Call']['OperationId'] = 'foreign-operation'
                elif mutation == 'request': changed['exactResults'][0]['Call']['RequestSha256'] = 'f' * 64
                elif mutation == 'native-original': changed['nativeCallOriginals'] = []
                elif mutation == 'owner-original':
                    if case_id == 'P06': changed['ownerBatches'] = []
                    else: changed['ownerOperationOriginals'] = []
                else: changed['exactResults'][0]['Result']['ReceiptId'] = 'foreign-receipt'
                with self.subTest(case=case_id, mutation=mutation), self.assertRaises(v.Failed): v.bind_exact_recovery(changed, self.cases[case_id])

    def test_actual_p36_coherent_boolean_float_and_unknown_outcome_fields_refuse(self):
        actual = self.vectors['P36']['observation']
        outcome = actual['exactResults'][0]['Result']
        self.assertIs(type(outcome['Status']), int); self.assertEqual(outcome['Status'], 0)
        for mutation in ('boolean', 'float', 'unknown-field'):
            changed = copy.deepcopy(actual)
            count = 0
            def replace(value):
                nonlocal count
                if isinstance(value, dict):
                    if all(value.get(name) == outcome[name] for name in ('TenantId', 'BatchId', 'OwnerRevision', 'ReceiptId')) and 'Status' in value:
                        count += 1
                        if mutation == 'unknown-field': value['UnknownNativeOutcome'] = 'substituted'
                        else: value['Status'] = False if mutation == 'boolean' else 0.0
                    for child in value.values(): replace(child)
                elif isinstance(value, list):
                    for child in value: replace(child)
            replace(changed)
            self.assertGreaterEqual(count, 4)
            with self.subTest(mutation=mutation), self.assertRaises(v.Failed): v.bind_exact_recovery(changed, self.cases['P36'])

    def test_reserved_recovery_requires_real_request_reservation_and_provider_vectors(self):
        for case_id in ('P25', 'P28'):
            for mutation in ('missing', 'fake', 'reservation', 'request', 'original', 'recovery', 'provider', 'receipt'):
                changed = copy.deepcopy(self.vectors[case_id]['observation'])
                proof = changed['reservationRecoveries'][0]
                if mutation == 'missing': changed.pop('reservationRecoveries')
                elif mutation == 'fake': changed['reservationRecoveries'] = [{'ok': True}]
                elif mutation == 'reservation': proof['ReservationOriginal']['Status'] = False
                elif mutation == 'request': proof['Request']['Capability']['BatchId'] = 'foreign-batch'
                elif mutation == 'original': proof['Original']['Call']['OperationId'] = 'foreign'
                elif mutation == 'recovery': proof['Recovery'] = copy.deepcopy(proof['Original'])
                elif mutation == 'provider': proof['ProviderOutcome']['TargetReceipts'] = []
                else: proof['ReservationOriginal']['ReceiptId'] = 'foreign-reservation'
                with self.subTest(case=case_id, mutation=mutation), self.assertRaises(v.Failed): v.bind_reservation_recovery(changed, self.cases[case_id])


if __name__ == '__main__': unittest.main()
