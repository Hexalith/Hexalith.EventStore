"""Offline real-signature evidence for genuine rejected-unissued/undispatched branches."""
import base64
import copy
import unittest
import test_ext_protection_1 as existing
v = existing.v


class RejectedReplacementEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = existing.CapabilityOriginalEvidenceTests(methodName='runTest')
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.fixture.trust.append({**self.fixture.trust[0], 'keyVersion': 'rejected-v2'})

    def observation(self, unissued=True):
        f = self.fixture
        observed = copy.deepcopy(f.observation)
        old_request = copy.deepcopy(f.request)
        candidate = {**old_request['Capability'], 'CapabilityKeyVersion': 'rejected-v2', 'AttestationOrdinal': 2,
                     'SigningAttemptOrdinal': 1, 'IntendedIssuedGuardRevision': 31}
        header = {'alg': 'ES256', 'anchorVersion': f.trust[1]['publicAnchorVersion'], 'keyVersion': 'rejected-v2',
                  'kid': f.trust[1]['publicAnchorId'], 'typ': 'DeletionBatchCapabilityV1'}
        jws = f.sign(candidate, header)
        signing, signature = v.digest(v.capability_bytes(candidate)).upper(), v.digest(jws.encode()).upper()
        def revocation(key, number):
            envelope = {'Issuer': 'issuer', 'Audience': 'registrar', 'TenantId': 'tenant-a', 'KeyFamily': 'DeletionBatchCapabilitySigningKey',
                        'KeyVersion': key, 'RevocationRevision': 1, 'TrustProfileRevision': 7, 'EventIdentity': 'revocation-' + key, 'SignatureDigest': 'A' * 64}
            return {'Envelope': envelope, 'OwnerRevision': 10 + number, 'KeyBlockSetRevision': number,
                    'ReceiptId': v.digest(v.native_json_bytes(envelope)).upper(), 'AffectedBatchIds': [candidate['BatchId']] if number == 1 else []}
        old_revocation, revoked = revocation('retained-v1', 1), revocation('rejected-v2', 2)
        previous_block = v.digest(v.native_json_bytes([old_revocation['ReceiptId'], candidate['BatchId']])).upper()
        batch = copy.deepcopy(observed['guardBatches'][0])
        batch.update(ProtectionOutcome='ConsumptionBlocked:CapabilityKeyCompromise', ProtectionReceiptId=previous_block,
                     TargetReceiptIds=[], BlockSetRevision=1)
        phase = {'OperationId': 'rejected-owner-operation', 'CompromiseBlockReceiptId': previous_block, 'ExpectedKeyBlockSetRevision': 2,
                 'GuardReplacementReceiptId': 'candidate-issued', 'Capability': candidate, 'DetachedJws': jws, 'SigningRequestId': signing,
                 'CommittedIssuedGuardRevision': 32, 'Targets': f.targets, 'RevocationReceipt': revoked}
        if unissued:
            command = dict.fromkeys('TenantId OperationId Operation ExpectedGuardRevision EpochId DeletionRequestId Scope Write Repair Ordinal Hold Batch AuthorizationId ReferenceId BridgeOriginal RevocationReceipt'.split())
            command.update(TenantId='tenant-a', OperationId='rejected-issue-operation', Operation=23, ExpectedGuardRevision=31,
                           EpochId='', DeletionRequestId=candidate['DeletionRequestId'], AuthorizationId='', ReferenceId='')
            carrier = {'BatchId': candidate['BatchId'], 'BatchKind': candidate['BatchKind'], 'BatchOrdinal': candidate['BatchOrdinal'],
                       'Targets': f.targets, 'ManifestDigest': candidate['ManifestDigest'], 'AttestationOrdinal': 2, 'CapabilityKeyVersion': 'rejected-v2',
                       'AttestationDigest': signature, 'ProtectionOutcome': '', 'ProtectionReceiptId': '', 'TargetReceiptIds': [], 'BlockSetRevision': 1,
                       'Capability': candidate, 'DetachedJws': jws, 'SigningRequestId': signing}
            command['Batch'] = carrier
            target_digest = v.digest(b'[]').upper()
            # This wire reconstruction mirrors the native property order, even after canonical transport sorting.
            receipt = copy.deepcopy(observed['guardReceipts'][0])
            receipt.update(OperationId=command['OperationId'], Status='Stale', GuardHighWater=34, AcceptedAtAdmissionFenceOrdinal=0,
                           ReferenceId='', ReceiptId='actual-refused-issue', Capability=candidate, SigningRequestId=signing, DetachedJwsDigest=signature)
            proof = {'Transition': command, 'TargetMutationDigest': target_digest, 'Refusal': receipt, 'CurrentGuardRevision': 34,
                     'CurrentGuardStateDigest': '', 'CurrentGuardStateBytes': ''}
            phase.update(CommittedIssuedGuardRevision=0, GuardReplacementReceiptId=receipt['ReceiptId'], UnissuedProof=proof)
            ordered = v.ordered_blocked_replacement(phase)['UnissuedProof']
            receipt['IntentDigest'] = v.digest(v.native_json_bytes({'Transition': ordered['Transition'], 'TargetMutationDigest': target_digest})).upper()
            state = {'TenantId': 'tenant-a', 'Revision': 34, 'Deletions': [{'Batches': [copy.deepcopy(batch)]}],
                     'CompromisedKeyVersions': ['retained-v1', 'rejected-v2'], 'Receipts': observed['guardReceipts'], 'Revocations': [old_revocation, revoked]}
            raw = v.canonical(state)
            proof.update(CurrentGuardStateDigest=v.digest(raw).upper(), CurrentGuardStateBytes=base64.b64encode(raw).decode())
        else:
            receipt = copy.deepcopy(observed['guardReceipts'][0])
            receipt.update(OperationId='new-issued-operation', GuardHighWater=32, ReceiptId='candidate-issued', Capability=candidate,
                           SigningRequestId=signing, DetachedJwsDigest=signature)
            observed['guardReceipts'].append(receipt)
            batch.update(Capability=candidate, AttestationOrdinal=2, CapabilityKeyVersion='rejected-v2', AttestationDigest=signature, DetachedJws=jws,
                         SigningRequestId=signing, IssuedGuardRevision=32, IssueReceiptId='candidate-issued', DispatchReceiptId='', DispatchGuardRevision=0)
        ordered_phase = v.ordered_blocked_replacement(phase)
        label = 'blocked-unissued-replacement' if unissued else 'blocked-issued-replacement'
        owner_receipt = v.digest(v.native_json_bytes([phase['OperationId'], v.digest(v.native_json_bytes(ordered_phase)).upper(), label])).upper()
        outcome = {'TenantId': 'tenant-a', 'BatchId': candidate['BatchId'], 'Status': 6, 'OwnerRevision': 14, 'KeyBlockSetRevision': 2,
                   'ReceiptId': owner_receipt, 'BlockReason': 1, 'BlockedKeyVersion': 'rejected-v2', 'RevocationRevision': 1, 'TargetReceipts': []}
        batch.update(ProtectionReceiptId=owner_receipt, BlockSetRevision=2)
        if unissued:
            batch['RejectedUnissuedReplacement'] = {'Original': phase, 'Outcome': outcome}
        observed.update(roots=[{'target': target, 'state': 'Active'} for target in f.targets], guardBatches=[batch], consumptionRequests=[old_request],
                        ownerBatches=[{'Original': old_request, 'Current': old_request, 'Outcome': {**outcome, 'Status': 2}, 'BlockedReplacement': phase}],
                        ownerOutcome={'status': 'ConsumptionBlocked', 'receiptId': owner_receipt, 'blockedKeyVersion': 'rejected-v2', 'revocationRevision': 1},
                        activationOutcome={'status': 'ActivationBlockedByReplacementKeyCompromise', 'newBlockReceiptId': owner_receipt,
                                           'newKeyVersion': 'rejected-v2', 'revocationRevision': 1}, providerResults=[],
                        ownerRevocations=[old_revocation, revoked], guardRevocations=[old_revocation, revoked])
        observed['rejectedUnissuedOriginal' if unissued else 'blockedReplacementOriginal'] = {'Original': phase, 'Outcome': outcome, 'IssuedRequest': old_request}
        return v.strict_json(v.canonical(observed))

    def post_activation(self):
        observed = self.observation(unissued=False)
        phase = observed.pop('blockedReplacementOriginal')['Original']
        request = {'Capability': phase['Capability'], 'DetachedJws': phase['DetachedJws'], 'CommittedIssuedGuardRevision': 32,
                   'DispatchReceiptId': 'replacement-dispatch', 'DispatchGuardRevision': 34, 'Targets': phase['Targets']}
        batch = observed['guardBatches'][0]
        batch.update(DispatchReceiptId=request['DispatchReceiptId'], DispatchGuardRevision=34)
        receipt = copy.deepcopy(observed['guardReceipts'][-1])
        receipt.update(OperationId='replacement-dispatch-operation', ReceiptId=request['DispatchReceiptId'], GuardHighWater=34)
        observed['guardReceipts'].append(receipt)
        activation = {'OperationId': 'activation-won', 'CompromiseBlockReceiptId': phase['CompromiseBlockReceiptId'],
                      'ExpectedKeyBlockSetRevision': 1, 'GuardReplacementReceiptId': phase['GuardReplacementReceiptId'], 'Replacement': request}
        ordered_request = {key: request[key] for key in 'Capability DetachedJws CommittedIssuedGuardRevision DispatchReceiptId DispatchGuardRevision Targets'.split()}
        cap_fields = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split()
        ordered_request['Capability'] = {key: request['Capability'][key] for key in cap_fields}
        ordered_request['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in request['Targets']]
        ordered_activation = {**activation, 'Replacement': ordered_request}
        activated = {'TenantId': 'tenant-a', 'BatchId': request['Capability']['BatchId'], 'Status': 0, 'OwnerRevision': 14, 'KeyBlockSetRevision': 1,
                     'ReceiptId': v.digest(v.native_json_bytes(['activation-won', v.digest(v.native_json_bytes(ordered_activation)).upper(), 'activation'])).upper(),
                     'BlockReason': None, 'BlockedKeyVersion': None, 'RevocationRevision': None, 'TargetReceipts': []}
        revocation = observed['ownerRevocations'][1]
        revocation.update(OwnerRevision=16, AffectedBatchIds=[request['Capability']['BatchId']])
        observed['guardRevocations'][1] = copy.deepcopy(revocation)
        blocked = {**activated, 'Status': 2, 'OwnerRevision': 16, 'KeyBlockSetRevision': 2, 'BlockReason': 1, 'BlockedKeyVersion': 'rejected-v2', 'RevocationRevision': 1,
                   'ReceiptId': v.digest(v.native_json_bytes([revocation['ReceiptId'], request['Capability']['BatchId']])).upper()}
        observed['ownerBatches'][0].update(Current=request, Outcome=blocked, BlockedReplacement=None)
        observed.update(consumptionRequests=[request], postActivationBlockOriginal={'Activation': activation, 'ActivatedOutcome': activated,
                        'RevocationReceipt': revocation, 'BlockedOutcome': blocked},
                        activationOutcome={'status': 'ActivatedThenReplacementBlocked', 'newBlockReceiptId': blocked['ReceiptId'], 'newKeyVersion': 'rejected-v2', 'revocationRevision': 1},
                        ownerOutcome={'status': 'ConsumptionBlocked', 'receiptId': blocked['ReceiptId'], 'blockedKeyVersion': 'rejected-v2', 'revocationRevision': 1})
        batch.update(ProtectionReceiptId=blocked['ReceiptId'])
        return v.strict_json(v.canonical(observed))

    def test_real_post_activation_compromise_retains_both_originals(self):
        self.evaluate(self.post_activation())

    def test_post_activation_requires_exact_activation_dispatch_and_later_compromise(self):
        mutations = [lambda o: o.pop('postActivationBlockOriginal'),
                     lambda o: o['postActivationBlockOriginal']['ActivatedOutcome'].update(Status=2),
                     lambda o: o['postActivationBlockOriginal']['Activation']['Replacement'].update(DispatchReceiptId='invented'),
                     lambda o: o['postActivationBlockOriginal']['Activation'].update(GuardReplacementReceiptId='invented'),
                     lambda o: o['postActivationBlockOriginal']['RevocationReceipt'].update(OwnerRevision=14),
                     lambda o: o['postActivationBlockOriginal']['BlockedOutcome'].update(KeyBlockSetRevision=1),
                     lambda o: o['ownerBatches'][0].update(BlockedReplacement={}),
                     lambda o: o['ownerRevocations'].clear(),
                     lambda o: o['ownerOutcome'].update(receiptId='invented'),
                     lambda o: o['postActivationBlockOriginal']['BlockedOutcome'].update(TenantId='tenant-b'),
                     lambda o: o['ownerBatches'][0]['Original']['Capability'].update(DestructionSealId='substituted-seal'),
                     lambda o: o['guardReceipts'].pop()]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                observed = self.post_activation()
                mutate(observed)
                with self.assertRaises((v.Failed, v.Blocked)):
                    self.evaluate(observed)

    def evaluate(self, observed):
        f = self.fixture
        v.evaluate(observed, {'steps': [], 'checks': ['active', 'replacement-blocked']}, f.targets, b'offline-canary', 'a' * 40, 'b' * 64, f.trust, f.roles)

    def test_genuine_unissued_or_undispatched_branch_keeps_historical_authority_distinct(self):
        for unissued in (True, False):
            self.evaluate(self.observation(unissued))

    def test_both_rejected_branches_bind_historical_owner_immutable_original(self):
        for unissued in (True, False):
            for member in ('DestructionSealId', 'BatchId', 'Targets'):
                observed = self.observation(unissued)
                observed['ownerBatches'][0]['Original'] = copy.deepcopy(observed['ownerBatches'][0]['Original'])
                original = observed['ownerBatches'][0]['Original']
                if member == 'Targets':
                    original['Targets'][0]['AgentInteractionId'] = 'substituted-owner-target'
                else:
                    original['Capability'][member] = 'substituted-owner-' + member
                with self.subTest(unissued=unissued, member=member), self.assertRaises((v.Failed, v.Blocked)):
                    self.evaluate(observed)

    def test_missing_or_substituted_unissued_original_refuses(self):
        mutations = [lambda o: o.pop('ownerRevocations'), lambda o: o['guardBatches'][0].update(AttestationOrdinal=2),
                     lambda o: o['rejectedUnissuedOriginal']['Original']['UnissuedProof']['Refusal'].update(Status='Committed'),
                     lambda o: o['rejectedUnissuedOriginal']['Original']['UnissuedProof'].update(CurrentGuardStateBytes='e30='),
                     lambda o: o['rejectedUnissuedOriginal']['Original'].update(CommittedIssuedGuardRevision=32),
                     lambda o: o['ownerBatches'][0]['Current']['Capability'].update(CapabilityKeyVersion='rejected-v2'),
                     lambda o: o['rejectedUnissuedOriginal']['Outcome'].update(ReceiptId='invented'),
                     lambda o: o['rejectedUnissuedOriginal']['Original'].update(DetachedJws=o['consumptionRequests'][0]['DetachedJws'])]
        for mutate in mutations:
            observed = self.observation()
            mutate(observed)
            with self.assertRaises((v.Failed, v.Blocked)):
                self.evaluate(observed)


if __name__ == '__main__':
    unittest.main()
