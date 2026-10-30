"""Offline persisted event grammar checks; no installed role, custody or Live call."""
import base64
import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('ext_protection_1_event', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class EventEvidenceTests(unittest.TestCase):
    def event(self):
        golden = v.read_json(ROOT / 'tests/Hexalith.EventStore.Contracts.Tests/Security/Fixtures/PayloadProtectionV2/g-001.json')
        target = {'TenantId': 'tenant-a', 'AgentInteractionId': 'interaction-a', 'TargetProtectionKeyAlias': 'alias-a'}
        return {'Safe': 7, 'ProtectedContent': {'Target': target, 'Value': {'$pdenc': golden['expected']['envelopeBase64Url']}}}, target

    def evaluate_raw(self, raw, kind='target-event', recorded_target=None):
        _, target = self.event()
        observation = {'engineSourceRevision': 'a' * 40, 'engineBinarySha256': 'b' * 64, 'agentsRuntimeEnabled': False,
                       'roots': [{'target': target, 'state': 'Active'}], 'records': [
                           {'kind': kind, 'target': target if recorded_target is None else recorded_target,
                            'bytesBase64': base64.b64encode(raw).decode(), 'protectedState': 'Sealed'}]}
        v.evaluate(observation, {'steps': [], 'checks': ['active', 'sealed']}, [target], b'unique-qualification-canary', 'a' * 40, 'b' * 64)

    def test_original_v2_bytes_and_ordinary_event_fields_remain_compatible(self):
        event, _ = self.event()
        event['Domain'] = [{'Target': None, 'Value': {'Text': 'ordinary-domain-data'}},
                           {'ProtectedContentDescription': 'ordinary-domain-data', '$PDENC': 3, '$ENC': 5},
                           {'Name': 'first', 'name': 'second'}]
        event['Domain~/\U0001f9ed'] = {'Quoted"Name': 'ordinary-domain-data'}
        raw = json.dumps(event, ensure_ascii=False, indent=2).encode()
        for kind in ('source-outbox', 'target-event'):
            with self.subTest(kind=kind):
                self.evaluate_raw(raw, kind)
                self.evaluate_raw(raw.replace(b'ProtectedContent', b'Protected\\u0043ontent'), kind)

    def test_duplicate_members_in_actual_persisted_event_bytes_refuse(self):
        event, _ = self.event()
        raw = v.native_json_bytes(event)
        content = v.native_json_bytes(event['ProtectedContent'])
        duplicates = [b'{"ProtectedContent":{"Target":null,"Value":"unsealed"},' + raw[1:],
                      b'{"Protected\\u0043ontent":' + content + b',' + raw[1:],
                      raw.replace(b'"Value":', b'"Value":"unsealed","Value":'),
                      raw.replace(b'"TenantId":', b'"TenantId":"foreign","TenantId":'),
                      raw.replace(b'"$pdenc":', b'"$pdenc":"invalid","\\u0024pdenc":'),
                      b'{"Domain":{"Name":"first","Name":"second"},' + raw[1:]]
        for index, malformed in enumerate(duplicates):
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(duplicate=index, kind=kind), self.assertRaises((v.Blocked, v.Failed)):
                    self.evaluate_raw(malformed, kind)

    def test_extra_or_nested_reserved_content_cannot_hide_beside_a_valid_field(self):
        original, _ = self.event()
        malformed = []
        for key in ('ProtectedContent', 'protectedContent', 'PROTECTEDCONTENT'):
            event = copy.deepcopy(original)
            event['Domain'] = [{'Nested': {key: {'Target': None, 'Value': 'unregistered-private-content'}}}]
            malformed.append(event)
        for key in ('protectedContent', 'PROTECTEDCONTENT'):
            event = copy.deepcopy(original)
            event[key] = {'Target': None, 'Value': 'unregistered-private-content'}
            malformed.append(event)
        event = copy.deepcopy(original)
        event['ProtectedContent']['Unsealed'] = 'unregistered-private-content'
        malformed.append(event)
        for key in ('$pdenc', '$enc'):
            event = copy.deepcopy(original)
            event['Domain'] = [{key: 'unregistered-ciphertext'}]
            malformed.append(event)
        for index, event in enumerate(malformed):
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(shape=index, kind=kind), self.assertRaises((v.Blocked, v.Failed)):
                    self.evaluate_raw(v.native_json_bytes(event), kind)

    def test_event_and_record_targets_require_the_same_exact_three_member_identity(self):
        original, target = self.event()
        for extra_event, extra_record in ((True, False), (False, True), (True, True)):
            event = copy.deepcopy(original)
            recorded = dict(target)
            if extra_event:
                event['ProtectedContent']['Target']['Extra'] = 'unregistered-private-content'
            if extra_record:
                recorded['Extra'] = 'unregistered-private-content'
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(event=extra_event, record=extra_record, kind=kind), self.assertRaises(v.Failed):
                    self.evaluate_raw(v.native_json_bytes(event), kind, recorded)

    def test_full_v2_envelope_grammar_is_required_for_both_persisted_event_kinds(self):
        original, _ = self.event()
        encoded = original['ProtectedContent']['Value']['$pdenc']
        envelope = v.decode_base64url(encoded)
        candidates = [envelope + b'X', envelope[:-1]]
        for offset, value in ((9, 0), (11, 0), (15, 0), (19, 1), (20, 0), (21, 0), (22, 1), (23, 1), (27, 0), (28, ord('8')), (54, 1)):
            changed = bytearray(envelope)
            changed[offset] = value
            self.assertNotEqual(envelope, changed)
            candidates.append(bytes(changed))
        texts = [v.base64url(value) for value in candidates] + [encoded + '=', encoded + '\n']
        # Different unused low bits decode to the same original bytes in permissive base64 readers.
        alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'
        if len(encoded) % 4:
            texts.append(encoded[:-1] + alphabet[alphabet.index(encoded[-1]) + 1])
        for index, text in enumerate(texts):
            event = copy.deepcopy(original)
            event['ProtectedContent']['Value']['$pdenc'] = text
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(shape=index, kind=kind), self.assertRaises((v.Blocked, v.Failed, ValueError)):
                    self.evaluate_raw(v.native_json_bytes(event), kind)

    def test_persisted_event_json_depth_and_nodes_match_production_bounds(self):
        original, _ = self.event()
        for depth, accepted in ((63, True), (64, False)):
            event = copy.deepcopy(original)
            nested = 0
            for _ in range(depth):
                nested = [nested]
            event['Domain'] = nested
            with self.subTest(depth=depth):
                if accepted:
                    self.evaluate_raw(v.native_json_bytes(event))
                else:
                    with self.assertRaises((v.Blocked, v.Failed)):
                        self.evaluate_raw(v.native_json_bytes(event))
        # Original event has nine nodes, including its technical wrapper and target scalars.
        for count, accepted in ((65526, True), (65527, False)):
            event = copy.deepcopy(original)
            event['Domain'] = [0] * count
            with self.subTest(nodes=count + 10):
                if accepted:
                    self.evaluate_raw(v.native_json_bytes(event))
                else:
                    with self.assertRaises((v.Blocked, v.Failed)):
                        self.evaluate_raw(v.native_json_bytes(event))

    def test_persisted_event_encoding_and_nonfinite_numbers_cannot_bypass_native_json(self):
        original, _ = self.event()
        text = json.dumps(original, separators=(',', ':'))
        malformed = [text.encode('utf-16'), text.encode('utf-32'), b'{"Domain":NaN,' + text.encode()[1:],
                     b'{"Domain":Infinity,' + text.encode()[1:]]
        for index, raw in enumerate(malformed):
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(shape=index, kind=kind), self.assertRaises((v.Blocked, v.Failed, ValueError, UnicodeError)):
                    self.evaluate_raw(raw, kind)

    def test_json_escapes_cannot_rewrite_the_reserved_ciphertext_marker_or_carrier(self):
        original, _ = self.event()
        raw = v.native_json_bytes(original)
        malformed = [raw.replace(b'"$pdenc"', b'"\\u0024pdenc"'),
                     raw.replace(b'"$pdenc"', b'"$p\\u0064enc"'),
                     raw.replace(b'"SFhQ', b'"\\u0053FhQ')]
        for index, raw in enumerate(malformed):
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(shape=index, kind=kind), self.assertRaises((v.Blocked, v.Failed)):
                    self.evaluate_raw(raw, kind)

    def test_decoded_domain_member_names_follow_native_unicode_validation(self):
        original, _ = self.event()
        for value in ('\ud800', '\udfff', 'prefix-\ud800'):
            event = copy.deepcopy(original)
            event['Domain'] = {value: 7}
            raw = json.dumps(event, ensure_ascii=True, separators=(',', ':')).encode()
            for kind in ('source-outbox', 'target-event'):
                with self.subTest(name=ascii(value), kind=kind), self.assertRaises(v.Failed):
                    self.evaluate_raw(raw, kind)
            # Utf8JsonReader.CopyString validates decoded member names. Untouched ordinary
            # scalar escapes have no member-name conversion and remain compatible.
            event['Domain'] = {'Text': value}
            for kind in ('source-outbox', 'target-event'):
                self.evaluate_raw(json.dumps(event, ensure_ascii=True, separators=(',', ':')).encode(), kind)
        event = copy.deepcopy(original)
        event['Domain'] = {'\U0001f9ed': 'ordinary-domain-data'}
        self.evaluate_raw(json.dumps(event, ensure_ascii=True, separators=(',', ':')).encode())


if __name__ == '__main__':
    unittest.main()
