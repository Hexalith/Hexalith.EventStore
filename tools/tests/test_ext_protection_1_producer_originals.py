"""Actual source-owned role/driver/observer output and strict executor/ciphertext provenance; no Live or installed role runs."""
import base64
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
SPEC = importlib.util.spec_from_file_location('ext_protection_loop3_producer', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class ProducerOriginalTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-loop3-producer-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        supplied = os.environ.get('EXT_PROTECTION_SNAPSHOT_COMPONENT_ASSEMBLY')
        if supplied:
            assembly = Path(supplied)
            if not assembly.is_absolute() or assembly.is_symlink() or not assembly.is_file() or hashlib.sha256(assembly.read_bytes()).hexdigest() != os.environ.get('EXT_PROTECTION_SNAPSHOT_COMPONENT_SHA256'):
                raise ValueError('Exact independently hash-pinned component test assembly required')
        else:
            platform = ROOT.parent / 'platform'
            argv = ['dotnet', 'build', str(platform / 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj'), '-c', 'Debug',
                    '-p:UseHexalithProjectReferences=true', '-p:HexalithEventStoreRoot=' + str(ROOT), '-p:GeneratePackageOnBuild=false', '-m:1', '-nodeReuse:false']
            for name in ('HexalithCommonsRoot', 'HexalithTenantsBasePath'):
                if os.environ.get(name):
                    argv.append('-p:' + name + '=' + os.environ[name])
            built = subprocess.run(argv, cwd=platform, capture_output=True, text=True, timeout=300, check=False)
            if built.returncode:
                raise AssertionError(built.stdout + built.stderr)
            assembly = platform / 'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll'
        environment = dict(os.environ, EXT_PROTECTION_LOOP3_SEAL_VECTORS=str(cls.directory))
        executed = subprocess.run(['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ProtectionQualificationLoop3SealingTests', '-noColor'],
                                  env=environment, capture_output=True, text=True, timeout=120, check=False)
        if executed.returncode:
            raise AssertionError(executed.stdout + executed.stderr)
        cls.actual = {path.stem: json.loads(path.read_bytes()) for path in cls.directory.glob('*.json')}
        if len(cls.actual) != 6:
            raise AssertionError('Complete actual Pascal/lowercase/seeded/lost-retention observation inventory required')

    def test_actual_native_snapshot_cache_backup_and_restore_have_distinct_bound_purposes(self):
        assembly = Path(os.environ['EXT_PROTECTION_SNAPSHOT_COMPONENT_ASSEMBLY']) if os.environ.get('EXT_PROTECTION_SNAPSHOT_COMPONENT_ASSEMBLY') else None
        if assembly is None:
            self.skipTest('Separate configured source run is covered when an exact component assembly is supplied')
        vector = self.directory / 'physical-copies.json'
        environment = dict(os.environ, EXT_PROTECTION_COPY_VECTOR=str(vector))
        executed = subprocess.run(['dotnet', str(assembly), '-class', 'Hexalith.Platform.Protection.Tests.ErasureRootLoop5ConfiguredSealTests', '-noColor'], env=environment, capture_output=True, text=True, timeout=120, check=False)
        self.assertEqual(executed.returncode, 0, executed.stdout + executed.stderr)
        observed = json.loads(vector.read_bytes())['observation']; self.verify(observed)
        self.assertEqual({record['kind'] for record in observed['records']}, {'snapshot', 'cache', 'backup', 'restored'})
        case = {'steps': ['SealSnapshotAndCache', 'CreateBackup', 'RestoreBackup'], 'checks': ['erased']}
        targets = [root['target'] for root in observed['roots']]
        def verify_erased(value):
            v.evaluate(value, case, targets, b'configured-canary', observed['engineSourceRevision'], observed['engineBinarySha256'], [], {})
        verify_erased(observed)
        for mutation in ('identity', 'digest', 'copy', 'duplicate', 'borrowed-copy'):
            changed = copy.deepcopy(observed); replay = changed['replays'][0]
            if mutation == 'identity': replay['sourceIdentity'] = 'foreign-snapshot-location'
            elif mutation == 'digest': replay['sourceDigest'] = 'F' * 64
            elif mutation == 'copy': replay['copyId'] += ':foreign'
            elif mutation == 'duplicate': changed['replays'][1] = copy.deepcopy(replay)
            else: replay.update({name: changed['replays'][1][name] for name in ('copyId', 'kind', 'sourceIdentity', 'sourceDigest')})
            with self.subTest(erased_copy_mutation=mutation), self.assertRaises(v.Failed): verify_erased(changed)
        for mutation in ('missing-location', 'physical-alias', 'purpose', 'backup-source', 'restore-call', 'backup-document'):
            changed = copy.deepcopy(observed)
            locations = {location['CopyKind']: location for location in changed['copyLocations']}
            if mutation == 'missing-location': changed.pop('copyLocations')
            elif mutation == 'physical-alias': locations['cache']['PhysicalIdentity'] = locations['snapshot']['PhysicalIdentity']
            elif mutation == 'purpose':
                locations['cache']['CopyKind'] = 'snapshot'; locations['cache']['Cell']['CopyKind'] = 'snapshot'
                next(record for record in changed['records'] if record['kind'] == 'cache')['kind'] = 'snapshot'
            elif mutation == 'backup-source': locations['backup']['CopyOriginal']['SourceDigest'] = 'F' * 64
            elif mutation == 'restore-call': locations['restored']['CopyCall']['Id'] = 'undispatched'
            else: locations['backup']['PersistedDocument']['State'] = {'substituted': True}
            with self.subTest(mutation=mutation), self.assertRaises(v.Failed): self.verify(changed)

    def verify(self, observation):
        return v.bind_producer_originals(observation, observation['engineSourceRevision'], observation['engineBinarySha256'])

    def test_real_event_single_snapshot_and_retention_loss_originals_certify_the_actual_selected_executor(self):
        for name, observed in self.actual.items():
            with self.subTest(name=name):
                self.verify(observed)
                for record in observed['records']:
                    carrier = v.strict_json(base64.b64decode(record['bytesBase64'], validate=True))
                    if record['kind'] == 'snapshot':
                        self.assertTrue(v.snapshot_record_envelopes(carrier, record['target']))
                    else:
                        self.assertTrue(v.event_record_envelope(carrier, record['target'], base64.b64decode(record['bytesBase64'])))

    def test_producer_binary_role_and_native_original_substitutions_refuse(self):
        for name, observed in self.actual.items():
            mutations = [lambda value: value['producerIdentity'].__setitem__('EngineBinarySha256', 'f' * 64),
                         lambda value: value['copyOriginals'][0]['ProducerOriginal']['Producer'].__setitem__('EngineBinarySha256', 'f' * 64),
                         lambda value: value['copyOriginals'][0]['WriterProof'].__setitem__('SealedPayloadSha256', 'F' * 64),
                         lambda value: value['copyOriginals'][0]['SourceEnrollment']['Binding'].__setitem__('KeyReference', '01ARZ3NDEKTSV4RRFFQ69G5FAX'),
                         lambda value: value['copyOriginals'][0]['ProducerOriginal']['Completion'].__setitem__('OperationId', '01ARZ3NDEKTSV4RRFFQ69G5FAX'),
                         lambda value: value['copyOriginals'][0]['SourceEnrollment']['RootIdentity'].__setitem__('StoreTarget', 'foreign-erc'),
                         lambda value: value.pop('copyOriginals'), lambda value: value.pop('producerIdentity')]
            for index, mutate in enumerate(mutations):
                changed = copy.deepcopy(observed)
                mutate(changed)
                with self.subTest(source=name, mutation=index), self.assertRaises((v.Failed, v.Blocked)):
                    self.verify(changed)
            changed = copy.deepcopy(observed)
            changed['producerIdentity']['RoleIdentity'] = 'wrong-executor'
            with self.assertRaises(v.Failed):
                v.bind_producer_originals(changed, observed['engineSourceRevision'], observed['engineBinarySha256'], {'executor': {'identity': 'selected-executor'}})

    def test_single_completion_version_and_composite_fields_cannot_inherit_valid_originals(self):
        for name, observed in self.actual.items():
            for field, value in (('DekVersion', 2), ('SnapshotEntries', []), ('SnapshotContainerDigest', 'A' * 64), ('SnapshotEntryManifest', [])):
                changed = copy.deepcopy(observed)
                retained = changed['copyOriginals'][0]['ProducerOriginal']
                retained['Completion'][field] = value
                retained['CompletionDigest'] = hashlib.sha256(v.native_json_bytes(retained['Completion'])).hexdigest().upper()
                with self.subTest(source=name, field=field), self.assertRaises((v.Failed, v.Blocked)):
                    self.verify(changed)

    def test_source_execution_is_bound_to_the_exact_executed_role_request(self):
        for name, observed in self.actual.items():
            for field, replacement in (('OperationId', 'undispatched-changed-input'), ('RequestSha256', 'f' * 64)):
                changed = copy.deepcopy(observed)
                changed['copyOriginals'][0]['ProducerOriginal']['Call'][field] = replacement
                with self.subTest(source=name, field=field), self.assertRaises((v.Failed, v.Blocked)):
                    self.verify(changed)
            changed = copy.deepcopy(observed)
            changed['copyOriginals'][0]['ProducerOriginal'].pop('Call')
            with self.subTest(source=name, field='missing'), self.assertRaises((v.Failed, v.Blocked)):
                v.bind_producer_originals(changed, observed['engineSourceRevision'], observed['engineBinarySha256'], {'executor': {'identity': 'selected-executor'}})

    def test_well_shaped_nonce_tag_key_alias_and_ciphertext_substitution_cannot_inherit_a_producer(self):
        for name, observed in self.actual.items():
            for offset in (28, 54, 66, -1):
                changed = copy.deepcopy(observed)
                record = changed['records'][0]
                carrier = v.strict_json(base64.b64decode(record['bytesBase64']))
                if record['kind'] == 'snapshot':
                    encoded = carrier['Envelope']
                else:
                    encoded = carrier['ProtectedContent']['Value']['$pdenc']
                envelope = bytearray(v.decode_base64url(encoded))
                envelope[offset] ^= 1
                replacement = base64.urlsafe_b64encode(envelope).rstrip(b'=').decode()
                if record['kind'] == 'snapshot':
                    carrier['Envelope'] = replacement
                else:
                    carrier['ProtectedContent']['Value']['$pdenc'] = replacement
                record['bytesBase64'] = base64.b64encode(v.native_json_bytes(carrier)).decode()
                with self.subTest(source=name, offset=offset), self.assertRaises((v.Failed, v.Blocked)):
                    self.verify(changed)


if __name__ == '__main__':
    unittest.main()
