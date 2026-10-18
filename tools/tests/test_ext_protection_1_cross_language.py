"""Offline .NET/openssl interoperability; ephemeral test keys, no installed roles or Live."""
import base64
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('ext_protection_1_interop', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)
FIXTURES = ROOT / 'tools/tests/fixtures/ext-protection-1'


class CapabilityCrossLanguageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ext-protection-interop-')
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name)
        platform = ROOT / 'references/Hexalith.Platform/src/Hexalith.Platform.Custody'
        if not platform.is_dir():
            platform = ROOT.parent / 'platform/src/Hexalith.Platform.Custody'
        configuration = os.environ.get('EXT_PROTECTION_INTEROP_CONFIGURATION', 'Debug')
        project_references = os.environ.get('EXT_PROTECTION_INTEROP_PROJECT_REFERENCES', 'true')
        if configuration not in ('Debug', 'Release') or project_references not in ('true', 'false'):
            raise ValueError('Invalid offline interop build configuration')
        sources = [FIXTURES / 'CapabilityInterop.cs', ROOT / 'src/Hexalith.EventStore.Server/Security/DeletionConsumptionIdentity.cs', *[platform / name for name in
                   ('DeletionBatchCapabilityCodec.cs', 'DeletionCapabilityTrustProfile.cs', 'DetachedEs256JwsCore.cs')]]
        contracts = ROOT / 'src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj'
        project = cls.directory / 'Interop.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                           '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
                           '<Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                           '</PropertyGroup><ItemGroup>' + ''.join('<Compile Include="' + escape(str(source)) + '" />' for source in sources)
                           + '<ProjectReference Include="' + escape(str(contracts)) + '" /></ItemGroup></Project>')
        command = ['dotnet', 'build', str(project), '--configuration', configuration,
                   '-p:UseHexalithProjectReferences=' + project_references, '-m:1', '--nologo']
        result = subprocess.run(command, capture_output=True, text=True, timeout=180)
        print(json.dumps({'buildCommand': command, 'exitCode': result.returncode}))
        print(json.dumps({'sourceSha256': {str(source): v.digest(source.read_bytes()) for source in [*sources,
                         ROOT / 'src/Hexalith.EventStore.Contracts/Security/DeletionBatchCapabilityIdentity.cs',
                         ROOT / 'src/Hexalith.EventStore.Contracts/Security/DeletionBatchCapabilityV1.cs',
                         ROOT / 'src/Hexalith.EventStore.Contracts/Security/ProtectionTarget.cs']}}))
        print(result.stdout)
        if result.returncode:
            raise RuntimeError('Cross-language build failed: ' + result.stderr)
        cls.assembly = cls.directory / ('bin/' + configuration + '/net10.0/Interop.dll')

    def test_golden_canonical_manifest_and_bidirectional_es256_match_actual_csharp_sources(self):
        vectors = v.read_json(FIXTURES / 'capability-vectors.json')['vectors']
        private, public = self.directory / 'offline.private.pem', self.directory / 'offline.public.pem'
        subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(private)], check=True, capture_output=True)
        subprocess.run(['openssl', 'pkey', '-in', str(private), '-pubout', '-out', str(public)], check=True, capture_output=True)
        for vector in vectors:
            with self.subTest(vector=vector['id']):
                payload, targets = vector['capability'], vector['targets']
                expected = vector['expected']
                self.assertEqual(expected['canonicalBase64'], base64.b64encode(v.capability_bytes(payload)).decode())
                self.assertEqual(expected['nativeTargetsBase64'], base64.b64encode(v.target_manifest_bytes(targets)).decode())
                trust = {'issuer': payload['Issuer'], 'audience': payload['Audience'], 'tenantId': payload['TenantId'],
                         'keyFamily': 'DeletionBatchCapabilitySigningKey', 'keyVersion': payload['CapabilityKeyVersion'],
                         'publicAnchorId': 'offline-public-anchor', 'publicAnchorVersion': 'version-é',
                         'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes()), 'trustProfileRevision': 1}
                header = v.base64url(v.canonical({'alg': 'ES256', 'anchorVersion': trust['publicAnchorVersion'], 'keyVersion': trust['keyVersion'],
                                                'kid': trust['publicAnchorId'], 'typ': 'DeletionBatchCapabilityV1'}))
                signed = self.directory / 'offline-signature.der'
                subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(private), '-out', str(signed)],
                               input=(header + '.' + v.base64url(v.capability_bytes(payload))).encode(), check=True, capture_output=True)
                der, offset, parts = signed.read_bytes(), 2, []
                for _ in range(2):
                    size = der[offset + 1]
                    parts.append(der[offset + 2:offset + size + 2].lstrip(b'\0').rjust(32, b'\0'))
                    offset += size + 2
                jws = header + '..' + v.base64url(b''.join(parts))
                request = {'Capability': payload, 'Targets': targets, 'DetachedJws': jws,
                           'CommittedIssuedGuardRevision': payload['IntendedIssuedGuardRevision'] + 1,
                           'DispatchReceiptId': 'offline-dispatch', 'DispatchGuardRevision': payload['IntendedIssuedGuardRevision'] + 3}
                candidates, expected_capture = [], []
                for tenant, batch, dispatch, alias, accepted in (
                        (payload['TenantId'].upper(), payload['BatchId'], 'dispatch', None, True),
                        ('a' * 64, 'b' * 256, 'dispatch', None, True),
                        ('a' * 65, 'valid-batch', 'dispatch', None, False),
                        ('tenant-é', 'valid-batch', 'dispatch', None, False),
                        ('tenant-a', 'b' * 257, 'dispatch', None, False),
                        ('tenant-a', 'invalid_', 'dispatch', None, False),
                        ('tenant-a', 'valid-batch', 'é' * 1024, 'é' * 1024, True),
                        ('tenant-a', 'valid-batch', 'é' * 1025, None, False),
                        ('tenant-a', 'valid-batch', 'dispatch', 'é' * 1025, False)):
                    candidate = copy.deepcopy(request)
                    candidate['Capability'].update(TenantId=tenant, BatchId=batch)
                    candidate['DispatchReceiptId'] = dispatch
                    for target in candidate['Targets']:
                        target['TenantId'] = tenant
                        if alias is not None:
                            target['TargetProtectionKeyAlias'] = alias
                    # Serialize all candidate vectors, including byte-bound negatives, as the actual default serializer does.
                    raw_targets = ('[' + ','.join('[' + ','.join(v.native_json_string(value) for value in (
                        target['TenantId'], target['AgentInteractionId'], target['TargetProtectionKeyAlias'])) + ']' for target in candidate['Targets']) + ']').encode('ascii') if alias is None or len(alias.encode()) <= 2048 else b'[]'
                    candidate['Capability']['ManifestDigest'] = v.digest(raw_targets).upper()
                    candidates.append(candidate)
                    expected_capture.append(accepted)
                    try:
                        v.capture_consumption_request(candidate)
                        python_accepted = True
                    except v.Failed:
                        python_accepted = False
                    self.assertEqual(accepted, python_accepted)
                input_value = {**request, 'PublicAnchorId': trust['publicAnchorId'], 'PublicAnchorVersion': trust['publicAnchorVersion'],
                               'PublicAnchorFile': str(public), 'OfflinePrivateKeyFile': str(private), 'CaptureCandidates': candidates}
                result = subprocess.run(['dotnet', str(self.assembly)], input=v.canonical(input_value), capture_output=True, check=True)
                actual = v.strict_json(result.stdout)
                for field, value in expected.items():
                    self.assertEqual(value, actual[field], field)
                self.assertTrue(actual['pythonSignatureVerified'])
                self.assertTrue(actual['changedPayloadRejected'])
                self.assertTrue(actual['nativeCaptureAccepted'])
                self.assertEqual(expected_capture, actual['nativeCaptureChecks'])
                request['DetachedJws'] = actual['dotnetDetachedJws']
                self.assertEqual(expected['signingRequestId'], v.validate_consumption_request(request, [trust], {})[0])
                print(json.dumps({'vector': vector['id'], **expected, 'pythonSignatureVerified': True,
                                  'dotnetSignatureVerified': True, 'changedPayloadRejected': True,
                                  'nativeCaptureAccepted': True, 'nativeCaptureChecks': actual['nativeCaptureChecks']}, sort_keys=True))


if __name__ == '__main__':
    unittest.main()
