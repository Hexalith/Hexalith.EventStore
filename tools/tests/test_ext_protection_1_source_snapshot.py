"""Actual single and mixed engine/observer snapshot emissions against the strict offline verifier."""
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
SPEC = importlib.util.spec_from_file_location('ext_protection_snapshot_source', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class SourceSnapshotEmissionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-snapshot-source-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        supplied = os.environ.get('EXT_PROTECTION_SNAPSHOT_COMPONENT_ASSEMBLY')
        if supplied:
            assembly = Path(supplied)
            if not assembly.is_absolute() or assembly.is_symlink() or not assembly.is_file() or hashlib.sha256(assembly.read_bytes()).hexdigest() != os.environ.get('EXT_PROTECTION_SNAPSHOT_COMPONENT_SHA256'):
                raise ValueError('Exact independently hash-pinned component test assembly required')
        else:
            platform = ROOT.parent / 'platform'
            if not (platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj').is_file():
                platform = ROOT / 'references/Hexalith.Platform'
            argv = ['dotnet', 'build', str(platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj'),
                    '-c', 'Debug', '-p:UseHexalithProjectReferences=true', '-p:HexalithEventStoreRoot=' + str(ROOT),
                    '-p:GeneratePackageOnBuild=false', '-p:RunAnalyzers=false', '-m:1', '-nodeReuse:false']
            source_build_env = {key: value for key, value in os.environ.items() if key != 'CI'}
            built = subprocess.run(argv, cwd=platform, env=source_build_env, capture_output=True, text=True, timeout=300, check=False)
            if built.returncode:
                raise AssertionError(built.stdout + built.stderr)
            assembly = platform / 'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll'
        vector = cls.directory / 'actual.json'
        environment = dict(os.environ, EXT_PROTECTION_SNAPSHOT_VECTOR=str(vector))
        argv = ['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ErasureRootLoop2SnapshotObservationTests',
                '-method', '*.ActualSingleAndMixedObserverEmissionsKeepLegacyEnvelopesAndStrictFragmentOriginals', '-noColor']
        executed = subprocess.run(argv, env=environment, capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode or not vector.is_file():
            raise AssertionError(executed.stdout + executed.stderr)
        cls.actual = json.loads(vector.read_bytes())

    def test_actual_mixed_fragment_originals_preserve_strict_four_field_wire(self):
        mixed = self.actual['mixed']
        v.bind_snapshot_originals(mixed)
        for entry in mixed['snapshotOriginals'][0]['Entries']:
            self.assertEqual(set(entry['RegistryOriginal']['Sealed']), {'KeyReference', 'PayloadBytes', 'SerializationFormat', 'ProtectedPathCount'})

    def test_actual_single_root_keeps_original_v2_envelope_and_mixed_skeleton_substitution_refuses(self):
        for key in ('single', 'mixed'):
            observed = self.actual[key]
            for record in observed['records']:
                carrier = json.loads(__import__('base64').b64decode(record['bytesBase64']))
                self.assertTrue(v.snapshot_record_envelopes(carrier, record['target']))
        changed = copy.deepcopy(self.actual['mixed'])
        changed['snapshotOriginals'][0]['Entries'][0]['RegistryOriginal']['Sealed']['SnapshotReplaySkeleton'] = 'e30='
        with self.assertRaises(v.Failed):
            v.bind_snapshot_originals(changed)


if __name__ == '__main__':
    unittest.main()
