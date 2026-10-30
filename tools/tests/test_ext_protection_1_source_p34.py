"""Actual two-target source-catalog/engine/guard/ERC projection against the unchanged offline verifier."""
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
SPEC = importlib.util.spec_from_file_location('ext_protection_p34_source', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class SourcePostActivationEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-p34-source-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        supplied = os.environ.get('EXT_PROTECTION_P34_COMPONENT_ASSEMBLY')
        if supplied:
            assembly = Path(supplied)
            if not assembly.is_absolute() or assembly.is_symlink() or not assembly.is_file() or hashlib.sha256(assembly.read_bytes()).hexdigest() != os.environ.get('EXT_PROTECTION_P34_COMPONENT_SHA256'):
                raise ValueError('Exact independently hash-pinned component test assembly required')
        else:
            platform = ROOT.parent / 'platform'
            if not (platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj').is_file():
                platform = ROOT / 'references/Hexalith.Platform'
            output = cls.directory / 'bin'
            argv = ['dotnet', 'build', str(platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj'),
                    '-c', 'Debug', '-p:UseHexalithProjectReferences=true', '-p:HexalithEventStoreRoot=' + str(ROOT),
                    '-p:OutputPath=' + str(output) + '/', '-p:AppendTargetFrameworkToOutputPath=false', '-p:GeneratePackageOnBuild=false', '-m:1', '-nodeReuse:false']
            built = subprocess.run(argv, cwd=platform, capture_output=True, text=True, timeout=300, check=False)
            if built.returncode:
                raise AssertionError(built.stdout + built.stderr)
            assembly = output / 'Hexalith.Platform.Protection.Tests.dll'
        cls.assembly = assembly
        vector = cls.directory / 'actual.json'
        environment = dict(os.environ, EXT_PROTECTION_P34_VECTOR=str(vector))
        argv = ['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ErasureRootLoop2OwnerTests',
                '-method', '*.CompiledP34ActivatesBeforeReplacementCompromiseAndEmitsActualVerifierObservation', '-noColor']
        executed = subprocess.run(argv, env=environment, capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode or not vector.is_file():
            raise AssertionError(executed.stdout + executed.stderr)
        cls.actual = json.loads(vector.read_bytes())
        cls.case = next(value for value in json.loads((ROOT / 'tools/ext-protection-1/plan.json').read_bytes())['cases'] if value['id'] == 'P34')
        anchor = cls.directory / 'public.pem'
        anchor.write_text(cls.actual['publicKeyPem'])
        cls.trust = [{'issuer': 'qualified-issuer', 'audience': 'protection-audience', 'tenantId': 'tenant-a',
                      'keyFamily': 'DeletionBatchCapabilitySigningKey', 'keyVersion': version, 'publicAnchorId': 'test-anchor',
                      'publicAnchorVersion': '1', 'publicAnchorFile': str(anchor), 'publicAnchorSha256': hashlib.sha256(anchor.read_bytes()).hexdigest(), 'trustProfileRevision': 1}
                     for version in ('healthy-key-a', 'healthy-key-b')]

    def verify(self, observation):
        v.evaluate(observation, self.case, self.actual['targets'], b'p34-canary', self.actual['engineCommit'],
                   self.actual['engineBinarySha256'], self.trust, {})

    def test_actual_compiled_activation_before_compromise_passes(self):
        self.verify(self.actual['observation'])
        original = self.actual['observation']['postActivationBlockOriginal']
        self.assertLess(original['ActivatedOutcome']['OwnerRevision'], original['RevocationReceipt']['OwnerRevision'])

    def test_substituted_actual_activation_and_compromise_originals_refuse(self):
        for mutation in ('activation', 'compromise', 'historical-owner'):
            changed = copy.deepcopy(self.actual['observation'])
            if mutation == 'activation':
                changed['postActivationBlockOriginal']['ActivatedOutcome']['Status'] = 2
            elif mutation == 'compromise':
                changed['postActivationBlockOriginal']['RevocationReceipt']['OwnerRevision'] = 1
            else:
                changed['ownerBatches'][0]['Original']['Capability']['DestructionSealId'] = 'substituted'
            with self.subTest(mutation=mutation), self.assertRaises(v.Failed):
                self.verify(changed)

    def test_independent_native_boundaries_refuse_missing_call_policy_and_state_mutations(self):
        original = self.actual['observation']
        self.assertTrue(original['rejectedEffectOriginals'])
        for mutation in ('missing', 'empty', 'missing-producer', 'missing-producer-and-proofs', 'call', 'rotation', 'trim', 'pending', 'wrapped', 'holder', 'receipt', 'boolean'):
            changed = copy.deepcopy(original)
            if mutation == 'missing': del changed['rejectedEffectOriginals']
            elif mutation == 'empty': changed['rejectedEffectOriginals'] = []
            elif mutation.startswith('missing-producer'):
                changed.pop('producerIdentity'); changed.pop('copyOriginals')
                if mutation == 'missing-producer-and-proofs': changed.pop('rejectedEffectOriginals')
            elif mutation == 'call': changed['rejectedEffectOriginals'][0]['Call']['Id'] = 'undispatched'
            else:
                proof = changed['rejectedEffectOriginals'][0]['After']
                if mutation == 'rotation': proof['NativePolicy']['LatestVersion'] += 1
                elif mutation == 'trim': proof['NativePolicy']['MinAvailableVersion'] += 1
                elif mutation == 'boolean': proof['NativePolicy']['Exportable'] = 'false'
                elif mutation == 'receipt': proof['ReceiptId'] += 'substituted'
                else: proof['CustodyStateSha256'] = hashlib.sha256(mutation.encode()).hexdigest().upper()
            with self.subTest(mutation=mutation), self.assertRaises(v.Failed): self.verify(changed)


class SourceReplacementRaceEvidenceTests(SourcePostActivationEvidenceTests):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        environment = dict(os.environ, EXT_PROTECTION_P33_VECTOR_DIRECTORY=str(cls.directory))
        executed = subprocess.run(['dotnet', str(cls.assembly), '-class', 'Hexalith.Platform.Protection.Tests.ErasureRootLoop2OwnerTests',
                                   '-method', '*.CompiledP33RacesBothActualOwnerOrderingsAndEmitsExactVerifierObservation', '-noColor'],
                                  env=environment, capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode:
            raise AssertionError(executed.stdout + executed.stderr)
        cls.vectors = [json.loads((cls.directory / name).read_bytes()) for name in ('activation-first.json', 'compromise-first.json')]
        cls.race_case = next(value for value in json.loads((ROOT / 'tools/ext-protection-1/plan.json').read_bytes())['cases'] if value['id'] == 'P33')

    def verify_race(self, vector, observation):
        anchor = self.directory / 'race-public.pem'; anchor.write_text(vector['publicKeyPem'])
        trust = [dict(entry, publicAnchorFile=str(anchor), publicAnchorSha256=hashlib.sha256(anchor.read_bytes()).hexdigest()) for entry in self.trust]
        v.evaluate(observation, self.race_case, vector['targets'], b'p33-canary', vector['engineCommit'], vector['engineBinarySha256'], trust, {})

    def test_both_actual_compiled_race_orderings_pass_and_preserve_second_compromise(self):
        for vector in self.vectors:
            self.verify_race(vector, vector['observation'])
            self.assertEqual(vector['observation']['ownerOutcome']['blockedKeyVersion'], 'healthy-key-b')
            self.assertTrue(all(root['state'] == 'Active' for root in vector['observation']['roots']))

    def test_actual_race_identity_order_and_compromise_substitutions_refuse(self):
        for vector in self.vectors:
            for mutation in ('key', 'revocation', 'activation', 'order'):
                changed = copy.deepcopy(vector['observation'])
                if mutation == 'key': changed['activationOutcome']['newKeyVersion'] = 'foreign-key'
                elif mutation == 'revocation': changed['activationOutcome']['revocationRevision'] += 1
                elif mutation == 'activation': changed['ownerBatches'][0]['Original']['Capability']['DestructionSealId'] = 'foreign-seal'
                elif changed.get('postActivationBlockOriginal'):
                    changed['postActivationBlockOriginal']['RevocationReceipt']['OwnerRevision'] = 1
                else: changed['activationOriginal']['NewBlockReceiptId'] = 'foreign-block'
                with self.subTest(mutation=mutation), self.assertRaises(v.Failed): self.verify_race(vector, changed)


if __name__ == '__main__':
    unittest.main()
