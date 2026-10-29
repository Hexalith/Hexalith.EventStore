"""Offline mixed-root carrier structure only; no installed role, native custody or Live execution."""
import base64
import copy
import hashlib
import importlib.util
import struct
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('ext_protection_1_mixed', Path(__file__).parents[1] / 'ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class MixedSnapshotEvidenceTests(unittest.TestCase):
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


if __name__ == '__main__':
    unittest.main()
