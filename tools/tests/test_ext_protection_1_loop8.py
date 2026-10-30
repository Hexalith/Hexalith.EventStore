"""Loop 8 verifier boundaries. These do not execute a role or a native container."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('ext_protection_1_loop8', Path(__file__).parents[1] / 'ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)
ROOT = Path(__file__).parents[2]


class Loop8VerifierTests(unittest.TestCase):
    def test_role_credential_matches_the_http_authorization_limit(self):
        accepted = 'A' * 8185
        self.assertEqual(accepted, v.validate_role_credential(accepted))
        self.assertEqual(8192, len(('Bearer' + ' ') + accepted))
        for rejected in ('A' * 8186, 'bad\nvalue', 'has space', 'café'):
            with self.subTest(rejected=rejected[:12]), self.assertRaisesRegex(v.Blocked, 'CREDENTIAL_REFERENCE_NOT_INSTALLED'):
                v.validate_role_credential(rejected)

    def test_executed_producer_original_requires_the_emitted_safe_request(self):
        original = json.loads((Path(__file__).parent / 'fixtures' / 'p36-operation-producer-original.json').read_text())
        vector = ROOT.parent / 'platform' / 'docs' / 'implementation' / 'ext-protection-1-loop7' / 'verification' / 'recovery-vectors' / 'P36.json'
        if vector.is_file():
            self.assertEqual(original, json.loads(vector.read_text())['observation']['operationProducerOriginals'][0])
        expected = {'operationId': original['OperationId'], 'requestSha256': original['RequestSha256']}
        v.require_executed_operation_producer(original, expected, original['BindingDigest'], original['AttemptId'], original['Producer'])
        missing = {key: value for key, value in original.items() if key != 'SafeRequest'}
        with self.assertRaisesRegex(v.Failed, 'OPERATION_PRODUCER_ORIGINAL_SCHEMA_INVALID'):
            v.require_executed_operation_producer(missing, expected, original['BindingDigest'], original['AttemptId'], original['Producer'])

    def test_qualification_platform_root_prefers_the_declared_submodule(self):
        self.assertEqual((ROOT / 'references' / 'Hexalith.Platform').resolve(), v.qualification_platform_root(ROOT).resolve())
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'eventstore'
            sibling = Path(directory) / 'platform' / 'src' / 'Hexalith.Platform.Protection.Qualification.Executor'
            sibling.mkdir(parents=True)
            (sibling / 'Hexalith.Platform.Protection.Qualification.Executor.csproj').write_text('<Project />')
            self.assertEqual((Path(directory) / 'platform').resolve(), v.qualification_platform_root(root).resolve())
            (sibling / 'Hexalith.Platform.Protection.Qualification.Executor.csproj').unlink()
            with self.assertRaisesRegex(v.Blocked, 'QUALIFICATION_PLATFORM_CHECKOUT_MISSING'):
                v.qualification_platform_root(root)


if __name__ == '__main__':
    unittest.main()
