"""Offline native hold refusal/order evidence; no owner runtime or native qualification."""
import copy
import hashlib
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('hold_evidence_verifier', Path(__file__).resolve().parents[1] / 'ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class HoldAttemptOriginals(unittest.TestCase):
    @staticmethod
    def observation(status='Stale'):
        fields = 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest'.split()
        hold = dict.fromkeys(fields)
        hold.update(OperationId='hold-operation', ReceiptId='hold-receipt', Status='Committed', GuardHighWater=9)
        transition = dict.fromkeys('TenantId OperationId Operation ExpectedGuardRevision EpochId DeletionRequestId Scope Write Repair Ordinal Hold Batch AuthorizationId ReferenceId BridgeOriginal RevocationReceipt'.split())
        transition.update(TenantId='tenant-a', OperationId='dispatch-operation', Operation=21, ExpectedGuardRevision=8 if status == 'Stale' else 9)
        target_digest = 'A' * 64
        intent = hashlib.sha256(v.native_json_bytes({'Transition': transition, 'TargetMutationDigest': target_digest})).hexdigest().upper()
        receipt = dict.fromkeys(fields)
        receipt.update(OperationId='dispatch-operation', ReceiptId='refused-dispatch-original', IntentDigest=intent, Status=status, GuardHighWater=9)
        return {'guardReceipts': [hold], 'holdOrders': [{'holdOperationId': 'hold-operation', 'holdRevision': 9,
                'dispatchOperationId': 'dispatch-operation', 'dispatchRevision': 9, 'dispatchWon': False, 'dispatchStatus': status}],
                'guardAttempts': [{'Transition': transition, 'TargetMutationDigest': target_digest, 'BeforeRevision': 8,
                                   'AfterRevision': 9, 'Receipt': receipt}]}

    def test_native_stale_or_blocked_refusal_needs_no_fabricated_committed_dispatch(self):
        for status in ('Stale', 'Blocked'):
            v.bind_hold_originals(self.observation(status))

    def test_missing_or_substituted_receipt_order_and_intent_refuse(self):
        mutations = [lambda o: o.pop('guardAttempts'), lambda o: o['guardAttempts'][0]['Receipt'].update(Status='Committed'),
                     lambda o: o['holdOrders'][0].update(dispatchWon=True), lambda o: o['guardAttempts'][0]['Transition'].update(Operation=20),
                     lambda o: o['guardAttempts'][0].update(BeforeRevision=10), lambda o: o['guardAttempts'][0]['Receipt'].update(IntentDigest='B' * 64),
                     lambda o: o['guardAttempts'].append(copy.deepcopy(o['guardAttempts'][0]))]
        for mutate in mutations:
            observed = self.observation()
            mutate(observed)
            with self.assertRaises(v.Failed):
                v.bind_hold_originals(observed)


if __name__ == '__main__':
    unittest.main()
