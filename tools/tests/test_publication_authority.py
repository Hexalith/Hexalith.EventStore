"""Synthetic structural controls and real fixed-adapter refusals; fixtures issue no authority."""
import copy
import datetime as dt
import hashlib
import json
import shutil
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import types
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import publication_authority as P

H = lambda label: hashlib.sha256(label.encode()).hexdigest()
NOW = dt.datetime(2026, 6, 1, tzinfo=dt.timezone.utc)


def copy_bridge_inputs(root):
    for path in P.B.BUILD_INPUTS:
        destination = root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / path, destination)


def chain(kind='eventstore-package'):
    owner = 'Hexalith/Hexalith.EventStore' if kind == 'eventstore-package' else 'Hexalith/Hexalith.Platform'
    records = []
    for index, state in enumerate(P.STATES):
        role = P.ROLES[index]
        if kind == 'platform-composed-host' and index == 0: role = 'deployment-owner'
        record = {'schema': P.SCHEMA, 'version': 1, 'id': 'synthetic:' + state, 'state': state,
                  'subject': {'kind': kind, 'owner': owner, 'source_repository': owner, 'source_sha': 'a'*40,
                              'subject_sha256': H('subject'), 'packages': [{'id': 'synthetic-package', 'version': '1.0.0', 'sha256': H('package')}],
                              'oci_index_sha256': H('oci'), 'server_package_sha256': H('package'), 'extensions_sha256': [],
                              'catalog_sha256': H('catalog'), 'runtime_sha256': H('runtime')},
                  'candidate': {'id': 'synthetic:unqualified', 'sha256': H('candidate')},
                  'validator': {'repository': owner, 'commit': 'b'*40, 'path': 'synthetic/validator.py', 'sha256': H('validator'),
                                'result_sha256': H('result'), 'receipt_set_sha256': H('receipts')},
                  'predecessor_sha256': P.record_digest(records[-1]) if records else None,
                  'issuer': {'id': 'github:synthetic', 'role': role, 'github_id': 123, 'authentication_evidence_sha256': H('comment')},
                  'approval': {'comment_id': 1000 + index, 'body_sha256': H('comment')},
                  'registry': {'path': P.REGISTRY, 'sha256': H('registry')}, 'outcome': state,
                  'validity': {'issued_at': '2026-01-02T00:00:00Z', 'expires_at': '2030-01-01T00:00:00Z', 'revoked': False,
                               'invalidated': False, 'invalidation_rules': P.INVALIDATION},
                  'assurance': {'required': 'single-maintainer-attested', 'achieved': 'single-maintainer-attested',
                                'inputs': ['single-maintainer-attested']}, 'promotion': None,
                  'publisher': {'repository': 'Hexalith/Hexalith.Builds', 'commit': 'c'*40, 'gitlink_commit': 'd'*40}}
        if state == 'production-promoted':
            record['promotion'] = {'environment': 'synthetic', 'profile': {'path': P.PROFILE, 'resolved_sha256': H('resolved'),
                                    'infrastructure_sha256': H('infrastructure')}, 'inventory': {'path': P.INVENTORY, 'sha256': H('inventory')},
                                   'rendered_deployment': {'path': 'synthetic/rendered.yaml', 'sha256': H('rendered')},
                                   'deployment_sha256': P.digest(P.canonical({'purpose': 'publication-rendered-deployment', 'schemaVersion': 1,
                                                        'oci_index_sha256': H('oci'), 'profile_sha256': H('resolved'), 'rendered_sha256': H('rendered')}))}
        records.append(record)
    return {'schema': P.SCHEMA, 'records': records}


def relink(document):
    for index, record in enumerate(document['records']):
        record['predecessor_sha256'] = P.record_digest(document['records'][index-1]) if index else None


class PublicationAuthorityTests(unittest.TestCase):
    def refusal(self, expected, action):
        with self.assertRaises(P.Refusal) as caught: action()
        self.assertEqual(expected, caught.exception.code)

    def test_complete_synthetic_chain_and_all_prefixes_grant_only_structural_validity(self):
        for kind in ('eventstore-package', 'platform-composed-host'):
            document = chain(kind)
            for count in range(1, 6):
                with self.subTest(kind=kind, count=count):
                    result = P.validate_chain({'schema': P.SCHEMA, 'records': document['records'][:count]}, NOW)
                    self.assertEqual('PASS', result['structural'])
                    self.assertEqual('unevaluated', result['authority'])

    def test_record_digest_preimage_and_approval_hash_cycle_are_explicit(self):
        record = chain()['records'][0]
        preimage = json.dumps({'purpose':'publication-authority-record','schemaVersion':1,'record':record}, sort_keys=True,
                              separators=(',', ':'), ensure_ascii=True).encode()
        self.assertEqual(hashlib.sha256(preimage).hexdigest(), P.record_digest(record))
        claim = P.claim_digest(record)
        record['approval']['body_sha256'] = H('changed')
        record['issuer']['authentication_evidence_sha256'] = H('changed')
        self.assertEqual(claim, P.claim_digest(record))
        self.assertNotEqual(hashlib.sha256(preimage).hexdigest(), P.record_digest(record))
        before_post = copy.deepcopy(record)
        before_post['approval']['comment_id'] = None
        before_post['approval']['body_sha256'] = None
        before_post['issuer']['authentication_evidence_sha256'] = None
        pending_claim = P.claim_digest(before_post)
        before_post['approval']['comment_id'] = 987654
        before_post['approval']['body_sha256'] = H('posted-body')
        before_post['issuer']['authentication_evidence_sha256'] = H('posted-body')
        self.assertEqual(pending_claim, P.claim_digest(before_post))
        self.assertNotEqual(P.record_digest(record), P.record_digest(before_post))

    def test_skips_duplicates_unknown_states_and_wrong_predecessors_refuse(self):
        for mutation, code in ((lambda d: d['records'].pop(1), 'CHAIN_ORDER'),
                               (lambda d: d['records'].insert(1, copy.deepcopy(d['records'][0])), 'CHAIN_INVALID'),
                               (lambda d: d['records'][1].update(state='unknown'), 'STATE_UNKNOWN'),
                               (lambda d: d['records'][1].update(predecessor_sha256=H('wrong')), 'CHAIN_PREDECESSOR')):
            document = chain(); mutation(document)
            self.refusal(code, lambda: P.validate_chain(document, NOW))
        document = chain(); document['records'][1]['id'] = document['records'][0]['id']; relink(document)
        self.refusal('CHAIN_DUPLICATE', lambda: P.validate_chain(document, NOW))

    def test_wrong_subject_source_or_package_changes_require_new_built_chain(self):
        for key, value in (('source_sha', 'f'*40), ('subject_sha256', H('other')), ('oci_index_sha256', H('other')),
                           ('server_package_sha256', H('other')), ('runtime_sha256', H('other'))):
            document = chain(); document['records'][2]['subject'][key] = value; relink(document)
            with self.subTest(key=key): self.refusal('CHAIN_SUBJECT', lambda: P.validate_chain(document, NOW))
        document = chain(); document['records'][2]['candidate']['sha256'] = H('other'); relink(document)
        self.refusal('CHAIN_SUBJECT', lambda: P.validate_chain(document, NOW))

    def test_platform_composed_host_cannot_inherit_eventstore_package_receipts(self):
        document = chain('platform-composed-host')
        document['records'][2] = chain()['records'][2]; relink(document)
        self.refusal('CHAIN_SUBJECT', lambda: P.validate_chain(document, NOW))
        document = chain('platform-composed-host'); document['records'][0]['subject']['owner'] = 'Hexalith/Hexalith.EventStore'
        self.refusal('SUBJECT_OWNER_MISMATCH', lambda: P.validate_chain(document, NOW))

    def test_wrong_role_expiry_revocation_invalidation_and_assurance_refuse(self):
        cases = [('issuer', 'role', 'deployment-owner', 'ISSUER_ROLE_MISMATCH'),
                 ('validity', 'expires_at', '2026-01-03T00:00:00Z', 'RECORD_EXPIRED_OR_FUTURE'),
                 ('validity', 'issued_at', '2028-01-01T00:00:00Z', 'RECORD_EXPIRED_OR_FUTURE'),
                 ('validity', 'revoked', True, 'RECORD_REVOKED'), ('validity', 'invalidated', True, 'RECORD_INVALIDATED'),
                 ('assurance', 'achieved', 'independent', 'ASSURANCE_MISMATCH')]
        for section, key, value, code in cases:
            document=chain(); document['records'][0][section][key]=value; relink(document)
            with self.subTest(section=section,key=key): self.refusal(code, lambda: P.validate_chain(document, NOW))

    def test_alternate_profile_slot_or_digest_domain_substitution_refuses(self):
        document=chain(); document['records'][-1]['promotion']['profile']['path']='alternate.yaml'
        self.refusal('PROFILE_SLOT_MISMATCH', lambda: P.validate_chain(document, NOW))
        document=chain(); document['records'][-1]['promotion']['profile']['resolved_sha256']=H('infrastructure')
        self.refusal('DEPLOYMENT_MISMATCH', lambda: P.validate_chain(document, NOW))

    def test_closed_bounded_parser_and_safe_diagnostics(self):
        for content in (b'{"schema":1,"schema":2}', b'{"x":NaN}', b'\xff', b'['*40+b']'*40):
            with self.subTest(content=content[:20]):
                with self.assertRaises(P.Refusal): P.parse(content)
        document = chain()
        record = document['records'][0]
        record.update({'untrusted-secret-value': secrets.token_hex(32)})
        canary = record['untrusted-secret-value']
        with self.assertRaises(P.Refusal) as caught:
            P.validate_chain(document, NOW)
        self.assertEqual('SCHEMA_INVALID', caught.exception.code)
        self.assertNotIn(canary, str(caught.exception))
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); (root/'input').write_bytes(b'x'*(P.MAX_BYTES+1))
            self.refusal('INPUT_LIMIT', lambda: P.read(root,'input'))
            (root/'link').symlink_to(root/'input')
            self.refusal('INPUT_INVALID', lambda: P.read(root,'link'))

    def test_missing_verifiers_and_fake_codec_never_enable_authority(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); (root/'ReleaseEvidenceCodec.cs').write_text('public class ReleaseEvidenceCodec {}')
            for kind in ('eventstore-package','platform-composed-host'):
                _, codes=P.missing_prerequisites(root,'a'*40,chain(kind)['records'])
                self.assertIn('RELEASE_EVIDENCE_CODEC_UNAVAILABLE',codes)
                self.assertTrue(set(P.FIXED_MISSING)<=set(codes))
                if kind=='platform-composed-host':
                    self.assertIn('PLATFORM_ISSUER_APPROVAL_VERIFIER_UNAVAILABLE',codes)
                    self.assertIn('EXTENSION_API_APPROVAL_VERIFIER_UNAVAILABLE',codes)

    def test_missing_inputs_are_named_in_an_isolated_fixture(self):
        with tempfile.TemporaryDirectory() as directory:
            result=subprocess.run([sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'evaluate','--root',directory],
                                  capture_output=True,text=True,timeout=20)
            self.assertEqual(1,result.returncode,result.stderr)
            document=json.loads(result.stdout)
            for code in ('PROFILE_MISSING','AUTHORITY_RECORDS_MISSING','COMPOSING_INVENTORY_MISSING'):
                self.assertIn(code,document['codes'])

    def test_real_repository_smoke_remains_non_authorizing_with_or_without_inputs(self):
        result=subprocess.run([sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'evaluate','--root',str(ROOT)],
                              capture_output=True,text=True,timeout=120)
        self.assertIn(result.returncode,(1,2),result.stderr)
        document=json.loads(result.stdout)
        self.assertEqual('FAIL',document['result']); self.assertIsNone(document['seal'])
        self.assertEqual('unproven',document['authority'])
        self.assertIn('RELEASE_EVIDENCE_CODEC_UNAVAILABLE',document['codes'])
        self.assertNotIn('Traceback',result.stderr)

    def test_live_malformed_records_preserve_exit_two(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); path=root/P.RECORDS; path.parent.mkdir(parents=True); path.write_text('{bad')
            result=subprocess.run([sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'evaluate','--root',directory],
                                  capture_output=True,text=True,timeout=20)
            self.assertEqual(2,result.returncode,result.stderr)
            self.assertIn('SCHEMA_INVALID',json.loads(result.stdout)['codes'])

    def test_symbolic_root_and_ancestor_refuse_in_the_real_cli(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); real=root/'real'; real.mkdir(); (real/'nested').mkdir()
            (real/'nested'/'records.json').write_bytes(P.canonical(chain()))
            (real/'records.json').write_bytes(P.canonical(chain()))
            alias=root/'alias'; alias.symlink_to(real,target_is_directory=True)
            for path in (alias/'records.json', alias/'nested'/'records.json', root/'direct.json'):
                if path.name=='direct.json': path.symlink_to(real/'nested'/'records.json')
                result=subprocess.run([sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'structural','--records',str(path)],
                                      capture_output=True,text=True,timeout=20)
                self.assertEqual(2,result.returncode,result.stderr)
                self.assertEqual(['INPUT_INVALID'],json.loads(result.stdout)['codes'])

    def test_traversal_output_cannot_create_the_profile_slot(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); repository=root/'repository'; outside=root/'outside'
            repository.mkdir(); outside.mkdir(); (repository/'deploy/dapr').mkdir(parents=True)
            source=outside/'records.json'; source.write_bytes(P.canonical(chain()))
            destination=outside/'..'/'repository'/P.PROFILE
            result=subprocess.run([sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'structural',
                                   '--root',str(repository),'--records',str(source),'--result',str(destination)],
                                  capture_output=True,text=True,timeout=20)
            self.assertEqual(2,result.returncode,result.stderr)
            self.assertFalse((repository/P.PROFILE).exists())

    def test_owner_result_requires_closed_success_and_both_matching_domains(self):
        resolved, infrastructure=H('resolved'),H('infrastructure')
        valid={'isStructurallyValid':True,'infrastructureDigest':infrastructure,'resolvedDigest':resolved,
               'issues':[],'authorization':'unevaluated','qualification':'unevaluated'}
        P.validate_owner_result(b'ordinary SDK diagnostics\n'+P.canonical(valid)+b'\n',resolved,infrastructure,infrastructure)
        for output in (b'',b'noop\n',P.canonical({}),P.canonical({**valid,'extra':True}),
                       *(P.canonical({**valid,key:value}) for key,value in (
                           ('isStructurallyValid',False),('issues',[{'path':'$','code':'bad'}]),('authorization','approved'),
                           ('qualification','approved'),('resolvedDigest',H('other')),('infrastructureDigest',H('other'))))):
            with self.subTest(output=output):
                self.refusal('PROFILE_OWNER_RESULT_INVALID',lambda:P.validate_owner_result(output,resolved,infrastructure,infrastructure))

    def test_profile_check_validates_owner_output_after_fresh_assembly(self):
        import phase_4_transition as T
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            fixture=json.loads((ROOT/'tests/Hexalith.EventStore.Contracts.Tests/Deployment/Fixtures/SyntheticProfile.v1.json').read_bytes())
            path=root/P.PROFILE; path.parent.mkdir(parents=True)
            path.write_bytes((ROOT/'tests/Hexalith.EventStore.Contracts.Tests/Deployment/Fixtures/SyntheticProfile.v1.json').read_bytes())
            copy_bridge_inputs(root)
            (root/'composition.json').write_bytes(b'independent fixture composition')
            (root/P.INVENTORY).write_bytes(P.canonical({'schema':'hexalith.eventstore.production-composing-inventory.v1',
                'source':{'path':'composition.json','sha256':P.digest(b'independent fixture composition')},
                'expectedApplicationIds':[a['id'] for a in fixture['applications']],
                'expectedComponentIds':[c['id'] for c in fixture['components']]}))
            valid={'isStructurallyValid':True,'infrastructureDigest':fixture['infrastructureDigest'],
                   'resolvedDigest':P.digest(path.read_bytes()),'issues':[],
                   'authorization':'unevaluated','qualification':'unevaluated'}
            for output in (P.canonical(valid), b'', b'{malformed',
                           P.canonical({**valid,'authorization':'approved'}),
                           P.canonical({**valid,'qualification':'approved'})):
                with self.subTest(output=output):
                    stages=[]
                    def child(command,scratch,filename,**kwargs):
                        stages.append(filename)
                        if filename=='build.log':
                            (scratch/'bin').mkdir()
                            (scratch/'bin/ProductionProfileBridge.dll').write_bytes(b'fresh child-produced assembly')
                        if filename=='profile-result.txt':
                            self.assertTrue((scratch/'bin/ProductionProfileBridge.dll').is_file())
                            return output
                        return b''
                    # Exercise capture, verified copies, fresh-output checks and
                    # the real caller; only the external process is substituted.
                    with mock.patch.object(T.Control,'bounded_process',side_effect=child), \
                         mock.patch.object(P.B.shutil,'which',return_value='/usr/bin/dotnet'):
                        if output==P.canonical(valid):
                            self.assertEqual('composition.json',P.profile_check(root,None))
                        else:
                            self.refusal('PROFILE_OWNER_RESULT_INVALID',lambda:P.profile_check(root,None))
                    self.assertEqual(['restore.log','build.log','profile-result.txt'],stages)

    def test_cli_structural_success_and_diagnostics_cannot_overwrite_input_or_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); source=root/'synthetic.json'; original=P.canonical(chain()); source.write_bytes(original)
            command=[sys.executable,'-B',str(ROOT/'tools/validate-publication-authority.py'),'structural','--records',str(source)]
            result=subprocess.run(command,capture_output=True,text=True,timeout=20)
            self.assertEqual(0,result.returncode,result.stderr)
            self.assertEqual('unevaluated',json.loads(result.stdout)['authority'])
            for target in (source, ROOT/P.PROFILE):
                result=subprocess.run(command+['--result',str(target)],capture_output=True,text=True,timeout=20)
                self.assertEqual(2,result.returncode)
            self.assertEqual(original,source.read_bytes())
            target=root/'link'; target.symlink_to(source)
            result=subprocess.run(command+['--result',str(target)],capture_output=True,text=True,timeout=20)
            self.assertEqual(2,result.returncode); self.assertEqual(original,source.read_bytes())
            broken=chain(); broken['records'][1]['predecessor_sha256']=H('wrong')
            source.write_bytes(P.canonical(broken))
            result=subprocess.run(command,capture_output=True,text=True,timeout=20)
            self.assertEqual(1,result.returncode)
            self.assertEqual(['CHAIN_PREDECESSOR'],json.loads(result.stdout)['codes'])
            source.write_bytes(b'{"schema":1,"schema":2}')
            result=subprocess.run(command,capture_output=True,text=True,timeout=20)
            self.assertEqual(2,result.returncode)
            self.assertEqual(['SCHEMA_INVALID'],json.loads(result.stdout)['codes'])
            self.assertNotIn('Traceback',result.stderr)

    def authenticated_control(self, document):
        comments={}
        registry={'identities':{'github:synthetic':{'kind':'human','github_id':123}},'roles':{role:['github:synthetic'] for role in P.ROLES}}
        control=types.SimpleNamespace(registry=registry, required='single-maintainer-attested',achieved='single-maintainer-attested',author_id=123,
                                      local=lambda _: b'registry',changed_push=lambda: dt.datetime(2026,1,1,tzinfo=dt.timezone.utc))
        for index, record in enumerate(document['records']):
            record['predecessor_sha256'] = P.record_digest(document['records'][index-1]) if index else None
            record['registry']['sha256']=P.digest(b'registry')
            body=P.canonical({'schema':'hexalith.eventstore.publication-authority-approval.v1','record_claim_sha256':P.claim_digest(record),
                              'role':record['issuer']['role'],'subject_sha256':record['subject']['subject_sha256'],'decision':'approved'}).decode()
            record['approval']['body_sha256']=record['issuer']['authentication_evidence_sha256']=P.digest(body.encode())
            comments['/issues/comments/'+str(record['approval']['comment_id'])]={'id':record['approval']['comment_id'],'body':body,
                     'user':{'id':123,'type':'User'},'performed_via_github_app':None,'created_at':'2026-01-02T00:00:00Z',
                     'updated_at':'2026-01-02T00:00:00Z','issue_url':'https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/42'}
        control.head = 'a'*40
        control.request = {'subjects': [{'path': P.RECORDS}]}
        control.api=types.SimpleNamespace(prefix='https://api.github.com/repos/Hexalith/Hexalith.EventStore',get=lambda endpoint: comments.get(endpoint,{}),
                     file=lambda commit,path,allow_absent=False: P.canonical(document) if commit==control.head else None,
                     iter_pages=lambda _: [[{'ref':'refs/heads/main','activity_type':'push','after':'a'*40,'before':'b'*40,'timestamp':'2026-01-01T00:00:00Z'}]])
        return control,comments

    def test_authenticated_comment_transport_control_and_changed_missing_edited_comments(self):
        document=chain(); control,comments=self.authenticated_control(document)
        P.authenticate_records(control,document['records'])
        _,codes=P.missing_prerequisites(ROOT,P.git(ROOT,'rev-parse','HEAD'),document['records'])
        self.assertIn('RELEASE_EVIDENCE_CODEC_UNAVAILABLE',codes)
        self.assertIn('BUILDS_RECORD_IDENTITY_MISMATCH',codes)
        endpoint='/issues/comments/1000'; original=copy.deepcopy(comments[endpoint])
        for update,code in (({'body':'changed'},'AUTHENTICATION_CHANGED'), ({'updated_at':'2026-01-03T00:00:00Z'},'AUTHENTICATION_EDITED'),
                            ({'user':{'id':124,'type':'User'}},'ISSUER_UNREGISTERED'),
                            ({'created_at':'2026-01-01T23:59:59Z','updated_at':'2026-01-01T23:59:59Z'},'AUTHENTICATION_WINDOW')):
            comments[endpoint]={**original,**update}
            with self.subTest(update=update): self.refusal(code,lambda: P.authenticate_records(control,document['records']))
        comments.pop(endpoint)
        self.refusal('AUTHENTICATION_CHANGED',lambda: P.authenticate_records(control,document['records']))

    def test_hash_consistent_comments_cannot_approve_different_semantics(self):
        for key,value in (('record_claim_sha256',H('other-claim')),('subject_sha256',H('other-subject')),
                          ('role','deployment-owner'),('decision','rejected')):
            document=chain(); control,comments=self.authenticated_control(document)
            comment=comments['/issues/comments/1000']; body=json.loads(comment['body']); body[key]=value
            comment['body']=P.canonical(body).decode()
            record=document['records'][0]
            record['approval']['body_sha256']=record['issuer']['authentication_evidence_sha256']=P.digest(comment['body'].encode())
            with self.subTest(key=key): self.refusal('AUTHENTICATION_INVALID',lambda:P.authenticate_records(control,document['records']))

    def test_receipt_attachment_preserves_claim_window_but_raw_seal_binding_changes(self):
        document = chain()
        document['records'] = document['records'][:1]
        control, _ = self.authenticated_control(document)
        before = copy.deepcopy(document)
        before['records'][0]['approval'] = {'comment_id': None, 'body_sha256': None}
        before['records'][0]['issuer']['authentication_evidence_sha256'] = None
        blobs = {'a'*40: P.canonical(document), 'b'*40: P.canonical(before)}
        control.api.file = lambda commit,path,allow_absent=False: blobs.get(commit)
        self.assertEqual(P.lifecycle_subject_at(control,'a'*40), P.lifecycle_subject_at(control,'b'*40))
        self.assertNotEqual(P.digest(blobs['a'*40]), P.digest(blobs['b'*40]))
        before['records'][0]['candidate']['sha256'] = H('authored-change')
        blobs['b'*40] = P.canonical(before)
        self.assertNotEqual(P.lifecycle_subject_at(control,'a'*40), P.lifecycle_subject_at(control,'b'*40))

    def test_successor_addition_retains_each_predecessor_authenticated_window(self):
        document = chain()
        control, comments = self.authenticated_control(document)
        old = {'schema': P.SCHEMA, 'records': copy.deepcopy(document['records'][:3])}
        blobs = {'a'*40: P.canonical(document), 'b'*40: P.canonical(old)}
        control.api.file = lambda commit,path,allow_absent=False: blobs.get(commit)
        control.api.iter_pages = lambda _: [[
            {'ref':'refs/heads/main','activity_type':'push','after':'a'*40,'before':'b'*40,'timestamp':'2026-01-10T00:00:00Z'},
            {'ref':'refs/heads/main','activity_type':'push','after':'b'*40,'before':'c'*40,'timestamp':'2026-01-01T00:00:00Z'}]]
        for index in (3, 4):
            comment=comments['/issues/comments/'+str(1000+index)]
            comment['created_at']=comment['updated_at']='2026-01-11T00:00:00Z'
        self.assertEqual(dt.datetime(2026,1,1,tzinfo=dt.timezone.utc), P.record_changed_push(control,document['records'][0]))
        self.assertEqual(dt.datetime(2026,1,10,tzinfo=dt.timezone.utc), P.record_changed_push(control,document['records'][4]))
        P.validate_chain(document, NOW)
        P.authenticate_records(control, document['records'])

    def test_changed_deployment_environment_and_inventory_are_explicit_bindings(self):
        document=chain(); record=document['records'][-1]
        before=P.record_digest(record)
        for key,value in (('environment','other'),('deployment_sha256',H('other'))):
            updated=copy.deepcopy(record); updated['promotion'][key]=value
            self.assertNotEqual(before,P.record_digest(updated))
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            self.refusal('PROFILE_MISSING',lambda:P.profile_check(root,record))
            (root/P.PROFILE).parent.mkdir(parents=True); (root/P.PROFILE).write_text('{}')
            self.refusal('COMPOSING_INVENTORY_MISSING',lambda:P.profile_check(root,record))
            inventory={'schema':'hexalith.eventstore.production-composing-inventory.v1','source':{'path':P.PROFILE,'sha256':H('x')},
                       'expectedApplicationIds':['app:eventstore'],'expectedComponentIds':['component:actor']}
            (root/P.INVENTORY).write_bytes(P.canonical(inventory))
            self.refusal('SELF_DERIVED_INVENTORY',lambda:P.profile_check(root,record))

    def test_explicit_inventory_versions_preserve_source_binding_and_owner_boundary(self):
        import phase_4_transition as T
        for version in (1, 2):
            with self.subTest(version=version), tempfile.TemporaryDirectory() as directory:
                root=Path(directory)
                content=(ROOT/f'tests/Hexalith.EventStore.Contracts.Tests/Deployment/Fixtures/SyntheticProfile.v{version}.json').read_bytes()
                fixture=json.loads(content)
                (root/P.PROFILE).parent.mkdir(parents=True)
                (root/P.PROFILE).write_bytes(content)
                copy_bridge_inputs(root)
                source=b'separately bound synthetic composing inventory'
                (root/'composition.json').write_bytes(source)
                inventory={'schema':f'hexalith.eventstore.production-composing-inventory.v{version}',
                    'source':{'path':'composition.json','sha256':P.digest(source)},
                    'expectedApplicationIds':['app:eventstore','app:tenants'],
                    'expectedComponentIds':['component:synthetic']}
                inventory_path=root/P.INVENTORY
                inventory_path.write_bytes(P.canonical(inventory))
                result={'isStructurallyValid':True,'infrastructureDigest':fixture['infrastructureDigest'],
                        'resolvedDigest':P.digest(content),'issues':[],'authorization':'unevaluated','qualification':'unevaluated'}
                # The genuine bridge separately exercises coverage and decoding. This
                # control isolates the fixed Python child's version/source bindings.
                with mock.patch.object(P.B,'execute',return_value=P.canonical(result)) as child:
                    self.assertEqual('composition.json',P.profile_check(root,None))
                    self.assertEqual(set(P.B.BUILD_INPUTS)|{P.PROFILE,P.INVENTORY,'composition.json'},
                                     set(dict(child.call_args.args[0].files)))
                    self.assertEqual(P.digest(inventory_path.read_bytes()),child.call_args.args[1])
                    inventory['source']['sha256']=H('wrong-source')
                    inventory_path.write_bytes(P.canonical(inventory))
                    self.refusal('COMPOSING_SOURCE_MISMATCH',lambda:P.profile_check(root,None))
                    inventory['schema']='hexalith.eventstore.production-composing-inventory.v3'
                    inventory_path.write_bytes(P.canonical(inventory))
                    self.refusal('SCHEMA_INVALID',lambda:P.profile_check(root,None))
                    self.assertEqual(1,child.call_count)
                _,codes=P.missing_prerequisites(root,'a'*40)
                self.assertTrue(set(P.FIXED_MISSING)<=set(codes))
                self.assertIn('RELEASE_EVIDENCE_CODEC_UNAVAILABLE',codes)

    def test_all_v2_and_shared_owner_sources_remain_required_execution_subjects(self):
        document=chain()
        document['records']=document['records'][:1]
        source='synthetic-composition.json'
        inventory={'schema':'hexalith.eventstore.production-composing-inventory.v2',
                   'source':{'path':source,'sha256':H('composition')},
                   'expectedApplicationIds':['app:eventstore'],'expectedComponentIds':['component:containerd']}
        required=set(P.CONTROL_FILES)|{P.RECORDS,P.PROFILE,P.INVENTORY,source}
        required|={p.relative_to(ROOT).as_posix() for p in (ROOT/'src/Hexalith.EventStore.Contracts/Deployment').glob('ProductionProfile*.cs')}
        values={P.INVENTORY:P.canonical(inventory),P.RECORDS:P.canonical(document)}
        def control(paths):
            return types.SimpleNamespace(root=ROOT,binding={'inputs':[{'path':p} for p in paths]},
                request={'subject_mode':'files','subjects':[{'path':p} for p in paths]},local=lambda path:values[path])
        self.assertEqual(document,P.transition_inputs(control(required)))
        import phase_4_transition as T
        candidate=control(required)
        candidate.request['transition']='built'
        # Even a passing authentication control cannot launch an unproven graph.
        with mock.patch.object(P,'authenticate_records'), mock.patch.object(T.Control,'bounded_process') as child:
            with self.assertRaises(T.Refusal) as caught:
                P.evaluate_transition(candidate)
            self.assertEqual('BUSINESS_PREREQUISITE_MISSING',caught.exception.code)
            self.assertEqual(P.BUILD_INPUT_PROOF,str(caught.exception))
            child.assert_not_called()
        for path in (*P.B.CONTROLS, 'docs/architecture/production-profile-v2.md',
                     'src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2Codec.cs',
                     'src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileCanonicalJson.cs',
                     'src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileValidationDocument.cs',source):
            with self.subTest(path=path):
                self.refusal('PUBLICATION_INPUT_UNBOUND',lambda:P.transition_inputs(control(required-{path})))
        incomplete=control(required)
        incomplete.request['subjects']=[item for item in incomplete.request['subjects'] if item['path']!=source]
        self.refusal('PUBLICATION_SUBJECT_UNBOUND',lambda:P.transition_inputs(incomplete))
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            copy_bridge_inputs(root)
            candidate=control(required)
            candidate.root=root
            extra=root/P.B.SOURCE_DIRECTORY/'ProductionProfileUnbound.cs'
            extra.write_text('#error unbound owner source')
            with mock.patch.object(P.B,'execute') as child:
                self.refusal('PROFILE_BUILD_INPUT_INVALID',lambda:P.transition_inputs(candidate))
                child.assert_not_called()

    def test_schema_is_closed_and_has_explicit_state_grammar(self):
        schema=json.loads((ROOT/'tools/publication-authority.schema.json').read_text())
        self.assertEqual(list(P.STATES),schema['$defs']['record']['properties']['state']['enum'])
        self.assertEqual(set(P.RECORD_KEYS),set(schema['$defs']['record']['required']))
        for value in schema['$defs'].values(): self.assertFalse(value['additionalProperties'])


if __name__=='__main__': unittest.main()
