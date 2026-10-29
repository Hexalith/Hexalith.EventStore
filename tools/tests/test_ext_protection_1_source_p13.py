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
SPEC = importlib.util.spec_from_file_location('ext_protection_p13_source', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class SourceContainmentEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-p13-source-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        supplied = os.environ.get('EXT_PROTECTION_P13_COMPONENT_ASSEMBLY')
        if supplied:
            assembly = Path(supplied)
            if not assembly.is_absolute() or assembly.is_symlink() or not assembly.is_file() or hashlib.sha256(assembly.read_bytes()).hexdigest() != os.environ.get('EXT_PROTECTION_P13_COMPONENT_SHA256'):
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
        vector = cls.directory / 'actual.json'
        environment = dict(os.environ, EXT_PROTECTION_P13_VECTOR=str(vector))
        argv = ['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ProtectionQualificationPreparationTests',
                '-method', '*.TwoExpectedContainmentTargetsUseDistinctRealControlRootAndEmitCompleteVerifierEvidence', '-noColor']
        executed = subprocess.run(argv, env=environment, capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode or not vector.is_file():
            raise AssertionError(executed.stdout + executed.stderr)
        cls.actual = json.loads(vector.read_bytes())
        cls.case = next(value for value in json.loads((ROOT / 'tools/ext-protection-1/plan.json').read_bytes())['cases'] if value['id'] == 'P13')
        anchor = cls.directory / 'public.pem'
        anchor.write_text(cls.actual['publicKeyPem'])
        cls.trust = [{'issuer': 'qualified-issuer', 'audience': 'protection-audience', 'tenantId': 'tenant-a',
                      'keyFamily': 'DeletionBatchCapabilitySigningKey', 'keyVersion': 'healthy-key-a', 'publicAnchorId': 'test-anchor',
                      'publicAnchorVersion': '1', 'publicAnchorFile': str(anchor), 'publicAnchorSha256': hashlib.sha256(anchor.read_bytes()).hexdigest(), 'trustProfileRevision': 1}]

    def verify(self, observation):
        v.evaluate(observation, self.case, self.actual['targets'], b'p13-canary', self.actual['engineCommit'],
                   self.actual['engineBinarySha256'], self.trust, {})

    def test_actual_two_target_containment_passes_without_accepted_set_coverage(self):
        self.assertEqual(len(self.actual['targets']), 2)
        self.assertEqual(len(self.actual['observation']['records']), 4)
        self.assertEqual(len(self.actual['observation']['replays']), 4)
        self.verify(self.actual['observation'])

    def test_accepted_set_reuse_duplicate_ordinal_or_missing_actual_replay_refuses(self):
        for mutation in ('accepted-set', 'ordinal', 'replay'):
            changed = copy.deepcopy(self.actual['observation'])
            if mutation == 'accepted-set':
                changed['targetCoverage'][0]['reusesAcceptedSetBatch'] = True
            elif mutation == 'ordinal':
                changed['targetCoverage'][1]['ordinals'] = changed['targetCoverage'][0]['ordinals']
            else:
                changed['replays'].pop()
            with self.subTest(mutation=mutation), self.assertRaises(v.Failed):
                self.verify(changed)


if __name__ == '__main__':
    unittest.main()
