"""Offline mixed-root carrier structure only; no installed role, native custody or Live execution."""
import base64
import copy
import hashlib
import importlib.util
import json
import struct
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('ext_protection_1_mixed', Path(__file__).parents[1] / 'ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class MixedSnapshotEvidenceTests(unittest.TestCase):
    def observation(self):
        carrier, targets = self.carrier()
        owner = {'TenantId': 'tenant-a', 'Domain': 'directory', 'AggregateId': 'aggregate'}
        parent = {'RecordSequence': 77, 'PayloadKind': 2, 'PayloadTypeId': carrier['SnapshotTypeId']}
        digest = hashlib.sha256(base64.b64decode(carrier['SkeletonBytes'])).hexdigest().upper()
        children, originals = [], []
        for index, entry in enumerate(carrier['Entries']):
            operation = '01ARZ3NDEKTSV4RRFFQ69G5FA' + ('V' if index == 0 else 'W')
            identity = {'Target': entry['Target'], 'Owner': owner, 'Kind': 2, 'Sequence': 77, 'PayloadTypeId': entry['Protected']['SnapshotTypeId'],
                        'RootKeyVersion': 'root-v1', 'DerivationProfileVersion': 'candidate-hkdf-sha256-v1'}
            binding = {'Identity': identity, 'DigestKeyVersion': 'digest-v1', 'ReservationAttemptOrdinal': 1, 'KeyReference': entry['KeyReference'], 'OperationId': operation, 'LifecycleEpoch': 1}
            payload = v.native_json_bytes(entry['Protected'])
            child = {'Identity': owner, 'Occurrence': {'RecordSequence': 77, 'PayloadKind': 2, 'PayloadTypeId': identity['PayloadTypeId']},
                     'OperationId': operation, 'KeyReference': entry['KeyReference'], 'DekVersion': 1, 'LifecycleEpoch': 1}
            children.append(child)
            originals.append({'Path': entry['Path'], 'SourceEnrollment': {'Binding': binding, 'SnapshotEntry': {'Parent': parent, 'Path': entry['Path'], 'ContainerDigest': digest, 'EntryCount': 2}},
                              'RegistryOriginal': {'Request': {'Identity': identity}, 'KeyReference': entry['KeyReference'], 'WriterState': 2, 'WriterProofId': operation,
                                                   'Sealed': {'KeyReference': entry['KeyReference'], 'PayloadBytes': base64.b64encode(payload).decode(), 'SerializationFormat': 'json+pdenc-v2', 'ProtectedPathCount': 1}},
                              'WriterProof': {'Binding': binding, 'SealedPayloadSha256': hashlib.sha256(payload).hexdigest().upper(), 'Persisted': True, 'WriterRetired': True, 'ReceiptId': 'native-source-' + str(index)}})
        completion = dict(children[0], Occurrence=parent, SnapshotEntries=children, SnapshotContainerDigest=digest,
                          SnapshotEntryManifest=[{'Path': entry['Path'], 'Target': entry['Target']} for entry in carrier['Entries']])
        return {'records': [{'kind': 'snapshot', 'target': target, 'bytesBase64': base64.b64encode(v.native_json_bytes(carrier)).decode(), 'protectedState': 'Sealed'} for target in targets],
                'snapshotOriginals': [{'CarrierSha256': hashlib.sha256(v.native_json_bytes(carrier)).hexdigest().upper(), 'Completion': completion, 'Entries': originals}]}

    def test_composite_originals_bind_every_source_registry_writer_and_completion(self):
        value = self.observation()
        v.bind_snapshot_originals(value)
        changes = [lambda value: value.pop('snapshotOriginals'),
                   lambda value: value['snapshotOriginals'][0]['Entries'][0]['WriterProof'].update(Persisted=False),
                   lambda value: value['snapshotOriginals'][0]['Entries'][0]['RegistryOriginal'].update(WriterState=1),
                   lambda value: value['snapshotOriginals'][0]['Completion']['SnapshotEntries'].pop(),
                   lambda value: value['snapshotOriginals'][0]['Completion'].update(SnapshotContainerDigest='0' * 64),
                   lambda value: value['snapshotOriginals'][0]['Entries'][0]['SourceEnrollment']['SnapshotEntry'].update(Path='/other')]
        for change in changes:
            changed = copy.deepcopy(value)
            change(changed)
            with self.subTest(change=changes.index(change)), self.assertRaises(v.Failed):
                v.bind_snapshot_originals(changed)
    def carrier(self):
        targets = [{'TenantId': 'tenant-a', 'AgentInteractionId': 'interaction-' + value, 'TargetProtectionKeyAlias': 'alias-' + value} for value in ('a', 'b')]
        skeleton = {'Safe': 7, 'Entries': [{'ProtectedContent': {'Target': target, 'Value': None}} for target in targets]}
        parent = 'hx-snapshot-v1:directory-state'
        entries = []
        reference = '01ARZ3NDEKTSV4RRFFQ69G5FAV'
        envelope = b'HXP2\x02\x01\x01\x01' + struct.pack('>HHII4BI', 28, 26, 1, 0, 12, 16, 0, 0, 1) + reference.encode() + bytes(12) + b'X' + bytes(16)
        for index, target in enumerate(targets):
            path = '/Entries/' + str(index) + '/ProtectedContent/Value'
            child = 'hx-snapshot-v1:entry-' + hashlib.sha256(v.native_json_bytes({'PayloadTypeId': parent, 'path': path})).hexdigest()
            entries.append({'Path': path, 'Target': target, 'KeyReference': reference,
                            'Protected': {'Format': 'json+pdenc-v2', 'SnapshotTypeId': child, 'Envelope': v.base64url(envelope)}})
        return {'Format': 'json+pdenc-v2-entries', 'SnapshotTypeId': parent, 'SkeletonBytes': base64.b64encode(v.native_json_bytes(skeleton)).decode(), 'Entries': entries}, targets

    def test_complete_mixed_carrier_and_sealed_cache_keep_both_targets(self):
        carrier, targets = self.carrier()
        for target in targets:
            self.assertEqual(1, len(v.snapshot_record_envelopes(carrier, target)))
        self.assertEqual(41, len(v.read_json(v.PLAN_FILE)['cases']))

    def test_single_root_v2_shape_remains_compatible(self):
        carrier, targets = self.carrier()
        single = carrier['Entries'][0]['Protected']
        self.assertEqual([single['Envelope']], v.snapshot_record_envelopes(single, targets[0]))

    def test_malformed_reserved_content_cannot_hide_beside_complete_entry_manifest(self):
        original, targets = self.carrier()
        plaintext = {'Text': 'unregistered-private-content'}
        malformed = [None, 'unregistered-private-content', [],
                     {'Target': None, 'Value': plaintext},
                     {'Target': 'ordinary-domain-target', 'Value': plaintext},
                     {'Target': [], 'Value': plaintext},
                     {'Target': {}, 'Value': plaintext},
                     {'Target': {'TenantId': 'tenant-a', 'AgentInteractionId': 'interaction-a'}, 'Value': plaintext},
                     {'Value': plaintext}, {'Target': targets[0]},
                     {'target': targets[0], 'Value': plaintext},
                     {'Target': targets[0], 'value': plaintext},
                     {'Target': targets[0], 'Value': None, 'Unsealed': plaintext}]
        for index, content in enumerate(malformed):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            skeleton['Extra'] = [{'Nested': {'ProtectedContent': content}}]
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            # Both legitimate entry envelopes and their exact manifest are untouched.
            self.assertEqual(original['Entries'], changed['Entries'])
            for target in targets:
                with self.subTest(shape=index, target=target), self.assertRaisesRegex(v.Failed, '^SNAPSHOT_SKELETON_INVALID$'):
                    v.snapshot_record_envelopes(changed, target)

    def test_ordinary_domain_target_value_data_remains_compatible(self):
        carrier, targets = self.carrier()
        skeleton = v.strict_json(base64.b64decode(carrier['SkeletonBytes']))
        skeleton['Domain'] = [{'Target': None, 'Value': {'Text': 'ordinary-domain-value'}},
                              {'Target': {'Name': 'queue-a'}, 'Value': 5}]
        carrier['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
        for index, target in enumerate(targets):
            self.assertEqual([carrier['Entries'][index]['Protected']['Envelope']], v.snapshot_record_envelopes(carrier, target))

    def test_standalone_target_declarations_must_have_exact_same_tenant_shape(self):
        original, targets = self.carrier()
        malformed = [dict(targets[0], Extra='unprotected'), dict(targets[0], TenantId='tenant-b'),
                     {'TenantId': 'tenant-a', 'TargetProtectionKeyAlias': 'alias-a'},
                     dict(targets[0], AgentInteractionId=None), dict(targets[0], TargetProtectionKeyAlias=' '),
                     {'tenantId': 'tenant-a', 'AgentInteractionId': 'interaction-a', 'TargetProtectionKeyAlias': 'alias-a'},
                     {'TenantId': 'tenant-a', 'AgentInteractionId': 'interaction-a', 'targetProtectionKeyAlias': 'alias-a'}]
        for index, declaration in enumerate(malformed):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            skeleton['Extra'] = [{'Nested': declaration}]
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            self.assertEqual(original['Entries'], changed['Entries'])
            for target in targets:
                with self.subTest(shape=index, target=target), self.assertRaises(v.Failed):
                    v.snapshot_record_envelopes(changed, target)

    def test_case_varied_ownership_envelopes_cannot_hide_outside_reserved_content(self):
        original, targets = self.carrier()
        for target_name, value_name, alias_name in (
                ('target', 'Value', 'TargetProtectionKeyAlias'), ('Target', 'value', 'TargetProtectionKeyAlias'),
                ('TARGET', 'VALUE', 'TargetProtectionKeyAlias'), ('Target', 'Value', 'targetProtectionKeyAlias')):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            addressed = {alias_name if key == 'TargetProtectionKeyAlias' else key: value for key, value in targets[0].items()}
            skeleton['Extra'] = [{target_name: addressed, value_name: {'Secret': 'unsealed-ownership-value'}}]
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            self.assertEqual(original['Entries'], changed['Entries'])
            for target in targets:
                with self.subTest(names=(target_name, value_name, alias_name), target=target), self.assertRaises(v.Failed):
                    v.snapshot_record_envelopes(changed, target)

    def test_reserved_ciphertext_markers_cannot_appear_in_public_skeleton(self):
        original, targets = self.carrier()
        for marker in ('$pdenc', '$enc'):
            for escaped in (False, True):
                changed = copy.deepcopy(original)
                skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
                skeleton['Extra'] = [{'Nested': {marker: 'unregistered-ciphertext'}}]
                raw = v.native_json_bytes(skeleton)
                if escaped:
                    raw = raw.replace(('"' + marker + '"').encode(), ('"\\u0024' + marker[1:] + '"').encode())
                changed['SkeletonBytes'] = base64.b64encode(raw).decode()
                self.assertEqual(original['Entries'], changed['Entries'])
                for target in targets:
                    with self.subTest(marker=marker, escaped=escaped, target=target), self.assertRaises(v.Failed):
                        v.snapshot_record_envelopes(changed, target)

    def test_valid_standalone_target_and_ordinary_marker_names_remain_compatible(self):
        carrier, targets = self.carrier()
        skeleton = v.strict_json(base64.b64decode(carrier['SkeletonBytes']))
        skeleton['Extra'] = [dict(targets[0], AgentInteractionId='declared-other-interaction'),
                             dict(targets[0], TargetProtectionKeyAlias='a' * 2049),
                             dict(targets[0], TargetProtectionKeyAlias='\U0001f600' * 1025),
                             {'$PDENC': 'ordinary-domain-data', '$ENC': 3}]
        carrier['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
        for index, target in enumerate(targets):
            self.assertEqual([carrier['Entries'][index]['Protected']['Envelope']], v.snapshot_record_envelopes(carrier, target))

    def test_standalone_target_declarations_reject_escaped_lone_surrogates(self):
        original, targets = self.carrier()
        for field in ('AgentInteractionId', 'TargetProtectionKeyAlias'):
            for value in ('\ud800', '\udfff', 'prefix-\ud800'):
                changed = copy.deepcopy(original)
                skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
                skeleton['Extra'] = [dict(targets[0], **{field: value})]
                raw = json.dumps(skeleton, ensure_ascii=True, separators=(',', ':')).encode()
                changed['SkeletonBytes'] = base64.b64encode(raw).decode()
                for target in targets:
                    with self.subTest(field=field, value=ascii(value), target=target), self.assertRaises(v.Failed):
                        v.snapshot_record_envelopes(changed, target)

    def test_skeleton_container_depth_matches_production_limit(self):
        original, targets = self.carrier()
        for depth, accepted in ((63, True), (64, False)):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            nested = 0
            for _ in range(depth):
                nested = [nested]
            skeleton['Extra'] = nested
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            for index, target in enumerate(targets):
                with self.subTest(depth=depth, target=target):
                    if accepted:
                        self.assertEqual([changed['Entries'][index]['Protected']['Envelope']], v.snapshot_record_envelopes(changed, target))
                    else:
                        with self.assertRaises(v.Failed):
                            v.snapshot_record_envelopes(changed, target)

    def test_skeleton_scalar_and_container_node_count_matches_production_limit(self):
        original, targets = self.carrier()
        # This source skeleton has 17 nodes; Extra adds one array plus its scalar children.
        for scalars, accepted in ((65518, True), (65519, False)):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            skeleton['Extra'] = [0] * scalars
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            for index, target in enumerate(targets):
                with self.subTest(scalars=scalars, target=target):
                    if accepted:
                        self.assertEqual([changed['Entries'][index]['Protected']['Envelope']], v.snapshot_record_envelopes(changed, target))
                    else:
                        with self.assertRaises(v.Failed):
                            v.snapshot_record_envelopes(changed, target)

    def test_missing_duplicate_reordered_or_substituted_entries_refuse(self):
        original, targets = self.carrier()
        changes = [lambda value: value['Entries'].pop(), lambda value: value['Entries'].reverse(),
                   lambda value: value['Entries'][1].update(Path=value['Entries'][0]['Path']),
                   lambda value: value['Entries'][0].update(Target=targets[1]),
                   lambda value: value['Entries'][0].update(KeyReference='not-a-canonical-reference')]
        for mutate in changes:
            with self.subTest(change=changes.index(mutate)):
                changed = copy.deepcopy(original)
                mutate(changed)
                with self.assertRaises(v.Failed):
                    v.snapshot_record_envelopes(changed, targets[0])

    def test_plaintext_skeleton_unknown_fields_and_changed_entry_context_refuse(self):
        original, targets = self.carrier()
        for variation in ('plaintext', 'field', 'context', 'header'):
            changed = copy.deepcopy(original)
            if variation == 'plaintext':
                skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
                skeleton['Entries'][0]['ProtectedContent']['Value'] = {'Secret': 'must-not-persist'}
                changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            elif variation == 'field':
                changed['Entries'][0]['Authorized'] = True
            elif variation == 'context':
                changed['Entries'][0]['Protected']['SnapshotTypeId'] = changed['Entries'][1]['Protected']['SnapshotTypeId']
            else:
                changed['Entries'][0]['Protected']['Envelope'] = v.base64url(bytes(83))
            with self.subTest(variation=variation), self.assertRaises(v.Failed):
                v.snapshot_record_envelopes(changed, targets[0])

    def test_reserved_plaintext_sibling_with_null_or_malformed_target_refuses(self):
        original, targets = self.carrier()
        for malformed in (None, {}, {'TenantId': targets[0]['TenantId']}, 'not-a-target'):
            changed = copy.deepcopy(original)
            skeleton = v.strict_json(base64.b64decode(changed['SkeletonBytes']))
            skeleton['Extra'] = {'ProtectedContent': {'Target': malformed, 'Value': {'Secret': 'must-not-persist'}}}
            changed['SkeletonBytes'] = base64.b64encode(v.native_json_bytes(skeleton)).decode()
            with self.subTest(target=malformed), self.assertRaises(v.Failed):
                v.snapshot_record_envelopes(changed, targets[0])


if __name__ == '__main__':
    unittest.main()
