"""Focused reviewed-input, codec and owner-approval checks; no runtime authority."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import ast
import contextlib
import hashlib
import hmac
import io
import importlib.util
import json
import re
import struct
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / '_bmad-output/implementation-artifacts'
DOC = ART / 'spec-event-versioning-upcasting.md'
BASE = 'cbbe41501ba722731bf36b2c343efdef4ac714fb'
ALLOW = {
    '_bmad-output/implementation-artifacts/spec-6-5-event-versioning-and-upcasting-spec.md',
    '_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md',
    '_bmad-output/implementation-artifacts/deferred-work.md',
    '_bmad-output/planning-artifacts/architecture.md',
}
PREFIX = '_bmad-output/implementation-artifacts/6-5-integration/'
CANDIDATES = {
    'a': '_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md',
    'b': '_bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md',
    'c': '_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md',
}

class Refusal(Exception):
    pass

def require(condition, owner):
    if not condition:
        raise Refusal(owner)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def run(args, **kwargs):
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=120, **kwargs)
    require(result.returncode == 0, 'command: ' + ' '.join(args) + ': ' + result.stderr[-1200:])
    return result.stdout

def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])

def allowed(path):
    return path in ALLOW or path.startswith(PREFIX)

def scope(manifest, extra_paths=()):
    require(manifest['baseline'] == BASE and set(manifest['allowedFiles']) == ALLOW
            and manifest['allowedDirectory'] == PREFIX, 'scope-configuration')
    # This describes the documentation change, not a permanent repository freeze.
    # inputs() separately checks only the actual reviewed source/model bytes.
    return {'documentationFiles':sorted(ALLOW),'integrationDirectory':PREFIX,
            'historicalBaseline':BASE,'repositoryFreeze':False,
            'ignoredUnrelatedPaths':sorted({p for p in extra_paths if not allowed(p)})}

def document_input_pins(manifest, doc):
    table=doc.split('### 11.4',1)[1].split('#### Current immutable D import pins',1)[0]
    raw_rows=re.findall(r'^\| 6\.5[^\n]*$',table,re.M)
    rows=re.findall(r'^\| (6\.5[abc]) \| `([^`]+)`, commit `([0-9a-f]{40})`, SHA-256 `([0-9a-f]{64})` \|.*?block \(SHA-256 `([0-9a-f]{64})`\)',table,re.M)
    require(len(raw_rows)==len(rows)==3 and {(label,path) for label,path,*_ in rows}==
            {('6.5'+label,path) for label,path in CANDIDATES.items()},'document-input-identities')
    for label,path,revision,file_hash,block_hash in rows:
        require(manifest['inputs'][path]['sha256']==file_hash and
                manifest['inputs'][path]['pythonBlocks']==[block_hash], 'document-input-pin')
        try:revision_bytes=git('show',revision+':'+path)
        except subprocess.CalledProcessError:raise Refusal('document-input-revision')
        require(sha(revision_bytes)==file_hash, 'document-input-revision')
    dtable=doc.split('#### Current immutable D import pins',1)[1].split('The current input manifest',1)[0]
    drows=re.findall(r'^\| `([^`]+)` \| `([0-9a-f]{40})` \| `([0-9a-f]{64})` \|$',dtable,re.M)
    expected={path:(row['revision'],row['sha256']) for path,row in manifest['inputs'].items()
              if '/6-5d-simplification/' in path or '/spec-6-5d-' in path}
    require(len(drows)==len(expected) and len({p for p,_,_ in drows})==len(expected)
            and {p:(r,h) for p,r,h in drows}==expected,'document-d-input-pins')
    return {'abc':len(rows),'d':len(drows)}

def inputs(manifest, doc):
    for path,row in manifest['inputs'].items():
        data=(ROOT/path).read_bytes()
        require(sha(data)==row['sha256'], 'immutable-input-pin')
        require(re.fullmatch('[0-9a-f]{40}',row['revision']), 'input-revision')
        require(sha(git('show',row['revision']+':'+path))==row['sha256'], 'committed-input-pin')
        if 'pythonBlocks' in row:
            blocks=re.findall(r'^```python\n(.*?)^```$',data.decode(),re.M|re.S)
            require([sha(b.encode()) for b in blocks]==row['pythonBlocks'], 'input-model-block-pin')
    document_input_pins(manifest,doc)
    d=(ART/'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md').read_text()
    contract='## D1.'+d.split('## D1.',1)[1].split('\nRun `python3 ',1)[0]
    contract=re.sub(r'^## D','#### D',contract,flags=re.M).replace('obligations.md','the imported wire reference in §11.6')
    imported=doc.split('<!-- imported-d-contract-start -->\n',1)[1].split('\n<!-- imported-d-contract-end -->',1)[0]
    require(imported==contract,'reviewed-d-contract')
    support=(ART/'6-5d-simplification/obligations.md').read_text()
    require(doc.split('<!-- imported-d-wire-reference-start -->\n',1)[1].split('\n<!-- imported-d-wire-reference-end -->',1)[0]==support,'reviewed-d-wire-schemas')
    literals=(ART/'6-5d-simplification/known-answers.json').read_text().rstrip()
    require(doc.split('<!-- imported-d-known-answers-start -->\n```json\n',1)[1].split('\n```\n<!-- imported-d-known-answers-end -->',1)[0]==literals,'reviewed-d-literals')
    return len(manifest['inputs'])

def held_predicates(doc, preserved=None):
    baseline=git('show',BASE+':_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode()
    i37=re.search(r'^\[I-37\].*?(?=^### )',baseline,re.M|re.S)[0]
    original=sorted(set(re.findall(r'`([A-Z][A-Za-z]+)`',i37.split('(2) *Entry.*',1)[0])))
    preserved=json.loads((HERE/'preserved-hold-predicates.json').read_text()) if preserved is None else preserved
    require(preserved==original,'hold-predicate-baseline')
    d8=doc.split('#### D8.',1)[1].split('#### D9.',1)[0]
    managed={name for name in re.findall(r'([A-Z][A-Za-z]+)→',d8)} & set(original)
    section=doc.split('#### Unrelated A/B/C holds retained under D8',1)[1].split('#### Exact application-owned metadata adapter',1)[0]
    rows=re.findall(r'^\| `([^`]+)` \| `([^`]+)` \| ([^|]+) \| (.*) \|$',section,re.M)
    expected={
        'ActorCommitEvidenceHold':('actor_commit_evidence_hold','`actor`'),
        'AppendPreparationHold':('append_preparation_hold','`coordinator` for §7 preparation; `actor` for A7 no-op'),
        'ConsumerMembershipFenceUnavailable':('consumer_membership_fence_unavailable','`subscriber`'),
        'HistoricalMessageIdCollision':('historical_message_id_collision','`operations`'),
        'KeySpaceMigrationHold':('key_space_migration_hold','`projection`'),
        'LegacyEvidenceConflict':('legacy_evidence_conflict','`operations` for A4/A6 offline evidence; `projection` for B2 retained projection reads'),
        'LegacyHandoffCapacityHold':('legacy_handoff_capacity_hold','`subscriber`'),
        'RawSourceUnavailable':('raw_source_unavailable','`gateway` for B2 gateway reads; `projection` for B2/B9 projection reads'),
        'ReplayCommitAmbiguous':('replay_commit_ambiguous','`coordinator`'),
        'RollbackReaderCapabilityHold':('rollback_reader_capability_hold','`gateway` for A9/C6 deployment routing; `projection` for B8 projection routes; `subscriber` for C6 subscriber routes'),
        'CommandOutcomeHold':('D4 aggregate: original authenticated cause','`gateway` for admission; `quota-coordinator` for pin capacity/quota generation; `subscriber` for first-send; `coordinator` for preparation/outcome/resume/terminal'),
    }
    require(len(rows)==len(expected) and {name:(reason,owner) for name,reason,owner,_ in rows}==expected,
            'hold-inventory-mappings')
    require(not (managed & set(expected)) and managed|set(expected)==set(original),'hold-predicate-accounting')
    closed={'actor','coordinator','gateway','subscriber','projection','operations','quota-coordinator'}
    require(all(set(re.findall(r'`([^`]+)`',owner))<=closed for _,_,owner,_ in rows),'hold-owner-kinds')
    return {'preserved':len(original),'d8':len(managed),'supplemental':len(rows)}

def cursor_known_answers():
    canonical=lambda value:json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()
    image=lambda raw:dict(length=len(raw),sha256=sha(raw),hex=raw.hex())
    generation=canonical([7,[['a',2],['é',3]]]);absent=canonical([None,[]])
    payload={'schema':'hexalith.eventstore.hold-cursor/1','scopeKind':'tenant','scopeId':'t',
             'generation':sha(generation),'last':[1,'tenant','t','a'],'expiry':9000000000}
    key=bytes(range(32));signature=hmac.new(key,canonical(payload),hashlib.sha256).hexdigest()
    envelope={'schema':'hexalith.eventstore.hold-cursor-envelope/1','keyId':'integration-fixture','payload':payload,'signature':signature}
    return {'generation':image(generation),'absentGeneration':image(absent),'keyHex':key.hex(),
            'payload':image(canonical(payload)),'signature':signature,'envelope':image(canonical(envelope))}

def cursor_answers(doc):
    actual=cursor_known_answers()
    independent=json.loads(run(['node',str(HERE/'independent-answers.mjs'),'--cursor']))
    require(independent==actual,'independent-inventory-cursor')
    literal=doc.split('<!-- inventory-cursor-known-answers-start -->\n```json\n',1)[1].split('\n```\n<!-- inventory-cursor-known-answers-end -->',1)[0]
    require(literal==json.dumps(actual,ensure_ascii=False,sort_keys=True,indent=2),'inventory-cursor-literals')
    return {'algorithm':'HS256','generationVectors':2,'positiveEnvelope':1}

def execute_model(label):
    path=ROOT/CANDIDATES[label]
    source=re.findall(r'^```python\n(.*?)^```$',path.read_text(),re.M|re.S)
    require(len(source)==1,'child-model-identity')
    output=io.StringIO();ns={}
    with contextlib.redirect_stdout(output):
        exec(compile(source[0],str(path),'exec'),ns)
    return source[0],ns,output.getvalue().splitlines()

def text_block(doc, label):
    marker=label+'\n\n```text\n'
    require(doc.count(marker)==1,'labeled-block-identity')
    return doc.split(marker,1)[1].split('\n```\n',1)[0].splitlines()

def vectors(doc):
    a_code,a,a_out=execute_model('a')
    require(len(a_out)==41 and a_out[-1]=='K06-K12 strict-codec and decision-model checks passed','a-model-count')
    require(text_block(doc,'A-series local codec answers (label, exact byte length, SHA-256):')==a_out[:-1],'a-labeled-vectors')
    pairs=[]
    for before in range(4):
        for after in range(4):
            prior=a['publication_set']([1,before]);current=a['publication_set']([1,after])
            try:
                a['reduce_set'](current,prior)
                accepted=True
            except (AssertionError,ValueError):
                accepted=False
            pairs.append([before,after,accepted])
    match=re.findall(r'^I11SameAttemptPairs=(\[.*\])$',doc,re.M)
    require(len(match)==1 and json.loads(match[0])==pairs,'same-attempt-pairs')
    i11=re.search(r'^\[I-11\].*?(?=^\[I-|^###)',doc,re.M|re.S)[0]
    require('accepted and failed rows are frozen' in i11 and 'same state, receipt hash and observation-proof hash' in i11,'same-attempt-row-freeze')
    b_code,b,b_out=execute_model('b')
    require(b_out==['LB-01..LB-18 passed: codec, bounds, ownership, query/root/TTL and quota models; no provider proof'],'b-model-count')
    expected_labels=[
        'LB-07 transition P (19 tags)','LB-07 transcript-v2 genesis','LB-07 transcript-v2 first step',
        *[label+str(k) for k in (0,7,'long.MaxValue') for label in ('LB-10 zero-event P, k=','LB-10 terminal transcript, k=')],
        'LB-11 timeline manifest (framing literal)','LB-14 transcript selector claim']
    # Capture the three original construction results at their actual assertion sites.
    baseline_doc=git('show',BASE+':_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode()
    baseline_lines=text_block(baseline_doc,'B-series local codec answers (SHA-256 of the complete encoded record):')
    markers={line.rsplit(' ',1)[1]:line.rsplit(' ',1)[0] for line in baseline_lines[:3]+baseline_lines[-2:-1]}
    tree=ast.parse(b_code);seen={}
    class Capture(ast.NodeTransformer):
        def visit_Assert(self,node):
            if isinstance(node.test,ast.Compare) and len(node.test.comparators)==1:
                other=node.test.comparators[0]
                if isinstance(other,ast.Constant) and other.value in markers:
                    call=ast.Expr(ast.Call(ast.Name('_capture',ast.Load()),[ast.Constant(markers[other.value]),node.test.left],[]))
                    return [node,ast.copy_location(call,node)]
            return node
    instrumented=ast.fix_missing_locations(Capture().visit(tree));bn={'_capture':lambda label,value:seen.__setitem__(label,value)}
    with contextlib.redirect_stdout(io.StringIO()):exec(compile(instrumented,'pinned-b-constructor','exec'),bn)
    computed=[]
    for label in expected_labels:
        if label in seen: value=seen[label]
        elif label.startswith('LB-10'):
            k=label.rsplit('=',1)[1];k=bn['MAX'] if k=='long.MaxValue' else int(k)
            encoded,terminal=bn['zero_completion'](k)
            value=bn['H'](encoded).hex() if 'zero-event' in label else terminal.hex()
        else: value=bn['H'](bn['selection']).hex()
        computed.append(label+' '+value)
    require(text_block(doc,'B-series local codec answers (SHA-256 of the complete encoded record):')==computed,'b-labeled-vectors')
    c_code,c,c_out=execute_model('c')
    baseline_c=re.findall(r'^```python\n(.*?)^```$',git('show',BASE+':_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md').decode(),re.M|re.S)[0]
    labels=re.findall(r"^print\('([^']+)'\)",baseline_c,re.M)
    require(c_out==labels and len(c_out)==42,'c-model-identities')
    offset_section=doc.split('#### BH37-9 original-offset rule and byte vectors\n',1)[1].split('\n### C2.',1)[0]
    offsets=[c['digest'](c['offset'](0)),c['digest'](c['offset'](60)),c['digest'](c['cloud_time'](0)),c['digest'](c['cloud_time'](60))]
    require(re.findall(r'`([0-9a-f]{64})`',offset_section)==offsets,'c-inline-offset-vectors')
    c1=doc.split('### C1.',1)[1].split('#### BH37-9',1)[0]
    dest=[]
    for component,topic,config in [(b'pubsub',b'orders',c['destination_config']),(b'pubsub.orders',b'events',c['destination_config']),(b'pubsub',b'orders.events',c['destination_config']),(b'pubsub',b'orders',c['changed_config'])]:
        dest.append(c['destination_id'](component,topic,b'\x00'+config,hashlib.sha256(config).digest()).decode())
    require(re.findall(r'`(hxdst1-[0-9a-f]{64})`',c1)==dest,'c-inline-destination-vectors')
    return {'aVectors':40,'bVectors':11,'cFamilies':42,'offsetVectors':4,'destinationVectors':4,'sameAttemptPairs':len(pairs),'sameAttemptAllowed':sum(p[2] for p in pairs)}

def parent_answers(doc):
    h=lambda x:hashlib.sha256(x).digest()
    u=lambda x:struct.pack('>I',len(x.encode()))+x.encode()
    n=lambda x:struct.pack('>Q',x)
    def record(name,values):
        return name.encode()+b'\0\1'+struct.pack('>H',len(values))+b''.join(bytes([i])+v for i,v in enumerate(values,1))
    scope_hash=h(b''.join(u(x) for x in ('t','d','counter','a','op')))
    values={
        'I08-outcome-prep':record('HX-EV-COMMAND-OUTCOME-PREP-1',[scope_hash,u('command-outcome:'+scope_hash.hex()+':0'),n(0),h(b'outcome-0'),h(b'head-0'),n(1),h(b'outcome-1'),h(b'head-1'),n(7),u('owner-1')]),
        'I08-key-preimage':scope_hash+n(1)+h(b'outcome-1'),
        'I09-preparation-write':record('HX-EV-RESPONSE-PREPARATION-WRITE-1',[scope_hash,n(7),u('owner-1'),*[h(x) for x in (b'response-input',b'response-record',b'outcome-0',b'response-cas-receipt',b'outcome-cas-receipt')],n(639000000000000000),n(8)]),
        'I17-destination-config':b'{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"t.d.events"}',
        'I33-preimage':b'HX-EV-ROUTE-DECISION-KEY-HASH-1\0\1'+u('route-decision-key'),
    }
    independent=json.loads(run(['node',str(HERE/'independent-answers.mjs')]))
    actual={label:dict(length=len(value),sha256=sha(value),hex=value.hex()) for label,value in values.items()}
    require(independent==actual,'independent-parent-constructor')
    require(text_block(doc,'Integration codec known answers (label, exact byte length, SHA-256):')==[f'{k} {len(v)} {sha(v)}' for k,v in values.items()],'parent-literal-vectors')
    ddir=ART/'6-5d-simplification'
    spec=importlib.util.spec_from_file_location('preserved_child_acceptance',ddir/'acceptance.py')
    acceptance=importlib.util.module_from_spec(spec)
    sys.dont_write_bytecode=True
    spec.loader.exec_module(acceptance)
    with tempfile.TemporaryDirectory(prefix='6-5-integration-independent-') as directory:
        converted=Path(directory)/'integer-safe-input.json'
        converted.write_text(json.dumps(acceptance.independent_input(json.loads((ddir/'known-answers.json').read_text())),ensure_ascii=False))
        independent_d=run(['node',str(ddir/'independent-answers.mjs'),str(converted),str(ddir/'obligations.md')]).strip()
        archive=run(['node',str(ddir/'prior-evidence/verify-6-5d-simplified-independent.mjs'),str(converted)]).strip()
    shared=run(['node',str(ddir/'independent-shared-keys.mjs'),str(ddir/'known-answers.json')]).strip()
    return {'parentAnswers':len(values),'independentD':independent_d,'independentDArchive':archive,'sharedKeys':shared}

def blocks(doc):
    actual=re.findall(r'^```bash\n(.*?)^```$',doc,re.M|re.S)
    old=re.findall(r'^```bash\n(.*?)^```$',git('show',BASE+':_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode(),re.M|re.S)
    expected=[old[0],old[1],'python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py\n',old[-1]]
    require([sha(x.encode()) for x in actual]==[sha(x.encode()) for x in expected],'verifier-identities-count')
    for code in actual[:2]:run(['bash'],input=code)
    signed=re.findall(r'^V(?:17|20|22|23)\w+=.*$',doc,re.M)
    old_signed=re.findall(r'^V(?:17|20|22|23)\w+=.*$',git('show',BASE+':_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').decode(),re.M)
    require(signed==old_signed,'immutable-public-signed-literals')
    return {'bashVerifiers':len(actual),'signedVerifierBlocksExecuted':2,'signedLiteralLines':len(signed)}

def owned_ledger(ledger):
    old=git('show',BASE+':_bmad-output/implementation-artifacts/deferred-work.md').decode()
    def records(text):return re.findall(r'^- source_spec:[^\n]*(?:\n  [^\n]*)*',text,re.M)
    def identity(row):
        return tuple(next((line for line in row.splitlines() if line.startswith(prefix)),None)
                     for prefix in ('- source_spec: ','  summary: ','  evidence: '))
    expected={identity(row):row for row in records(old)
              if re.search(r'^  status: dispositioned pending approval',row,re.M)}
    require(len(expected)==47 and all(None not in key for key in expected),'ledger-owned-baseline')
    current=records(ledger)
    for key,original in expected.items():
        matches=[row for row in current if identity(row)==key]
        require(len(matches)==1,'ledger-owned-record')
        statuses=re.findall(r'^  status: (.*)$',matches[0],re.M)
        old_status=re.search(r'^  status: (.*)$',original,re.M)[1]
        citation=re.search(r'O-\d\d',old_status) or re.search(r'\[I-\d+\]',old_status)
        require(len(statuses)==1 and statuses[0].startswith('open — accepted spec disposition') and
                'implementation/evidence follow-up remains open' in statuses[0] and
                citation is not None and citation[0] in statuses[0],'ledger-47-open-followups')
    return len(expected)

def dispositions(doc):
    pass1=doc.split('**Review pass 1.',1)[1].split('**Review pass 2:',1)[0]
    pass1_ids=[i.strip() for ids in re.findall(r'^\| ((?:VG|BH|E)[^|]+) \|',pass1,re.M) for i in ids.split(',')]
    execution=(ART/'spec-6-5-event-versioning-and-upcasting-spec.md').read_text()
    raw_pass1=re.search(r'^\| ID \| Verdict and evidence \| Route \|\n\| --- \| --- \| --- \|\n((?:\|[^\n]*\|\n)+)',
                        execution.split('Review pass 1 (2026-09-30), diff',1)[1],re.M)
    require(raw_pass1 is not None,'raw-pass1-dispositions')
    raw_pass1_ids=re.findall(r'^\| ((?:VG|BH|E)[^ |]+) \|',raw_pass1[1],re.M)
    require(len(pass1_ids)==len(set(pass1_ids))==len(raw_pass1_ids)==59 and set(pass1_ids)==set(raw_pass1_ids),'raw-pass1-dispositions')
    section=doc.split('**Review pass 2: accepted spec dispositions',1)[1].split('### 11.6',1)[0]
    ids=re.findall(r'^\| ((?:VG2|BH2|E2)-[^ |]+) \|',section,re.M)
    raw=(ART/'story-6-5-review-pass-2-findings.md').read_text()
    raw_ids=set(re.findall(r'\*\*((?:VG2|BH2|E2)-[^:* ]+)(?: \(confirmed\))?:',raw))
    require(len(ids)==67 and len(set(ids))==67 and set(ids)==raw_ids,'raw-pass2-dispositions')
    meanings={
        'VG2-1':['16','reduce_set'],'VG2-5':['B','label'],'VG2-6':['offset','destination'],'VG2-7':['identity','count'],
        'BH2-11':['open ledger','owner','O-row'],'BH2-12':['owner approval','conversation','D-RESUME','D-SPLIT'],
        'BH2-13':['288a61908f4661fed52bb791f928292fe7d90180',BASE],'BH2-14':['O-06','O-07','O-11','focused'],
        'BH2-15':['maintenance/security','manual','D-RELEASE'],'BH2-17':['independent Node','preimages'],
        'E2-27':['attach','refusal','unchanged'],'E2-33':['labeled B','swapped-label'],'E2-37':['exact-status','EventsStored','PublishFailed'],
    }
    for key,tokens in meanings.items():
        line=next(l for l in section.splitlines() if l.startswith('| '+key+' |'))
        require(all(t in line for t in tokens),'finding-meaning-'+key)
    register=doc.split('### 11.5',1)[1].split('**Review pass 1.',1)[0]
    require(set(re.findall(r'^\| (BH37-\d+) \|',register,re.M))=={'BH37-'+str(i) for i in range(1,11)},'bh37-accounting')
    obligations=doc.split('### 11.7',1)[1].split('## 12.',1)[0]
    rows=re.findall(r'^\| (O-\d\d) \| (.*)$',obligations,re.M)
    require([r[0] for r in rows]==[f'O-{i:02}' for i in range(1,21)],'obligation-accounting')
    require(all('Story 6.6' in l and 'blocking' in l and 'immutable' in l for _,l in rows),'obligation-owner-gate-evidence')
    require(all(len(l.split('|')) == 4 for _,l in rows),'obligation-table-cells')
    owned_ledger((ART/'deferred-work.md').read_text())
    require('FW1' in doc and 'rerun this gate' in doc,'fw1-rerun-gate')
    return {'pass1Findings':len(pass1_ids),'pass2Findings':len(ids),'childRouted':54,'parentFindings':13,'bh37Findings':10,'obligations':20,'openImplementationFollowups':47}

def normative_text(raw):
    marker=b'<!-- APPROVAL RECEIPT: mutable fields below -->\n'
    require(type(raw) is bytes and raw.count(marker)==1 and b'\r' not in raw
            and not raw.startswith(b'\xef\xbb\xbf'),'receipt-bytes')
    start=raw.index(marker)
    require(start==0 or raw[start-1:start]==b'\n','receipt-bytes')
    try:return raw.decode('utf-8')
    except UnicodeDecodeError:raise Refusal('receipt-bytes')

def approval(raw):
    normative_text(raw);marker=b'<!-- APPROVAL RECEIPT: mutable fields below -->\n'
    body,receipt=raw.split(marker)
    labels=['ApprovalDigest','Approver','ApprovalDateUtc','ApprovalScope','Authorization','ApprovalEvidence']
    lines=receipt.decode().splitlines()
    require(receipt.endswith(b'\n') and len(lines)==len(labels) and
            all(line.startswith(label+': ') for label,line in zip(labels,lines)),'approval-receipt-fields')
    fields={label:line[len(label)+2:] for label,line in zip(labels,lines)}
    require(re.fullmatch('[0-9a-f]{64}',fields['ApprovalDigest']) is not None and
            hmac.compare_digest(fields['ApprovalDigest'],sha(body)),'approval-digest')
    require(fields['ApprovalScope']=='Story 6.5 AD-13 normative artifact','approval-scope')
    require(fields['Approver']=='Jérôme Piquot' and
            fields['Authorization'] not in ('','UNAPPROVED') and
            'I Jérôme Piquot approve' in fields['ApprovalEvidence'],'owner-approval-record')
    try:recorded=datetime.fromisoformat(fields['ApprovalDateUtc'].replace('Z','+00:00'))
    except ValueError:raise Refusal('approval-recording-date')
    require(fields['ApprovalDateUtc'].endswith('Z') and recorded.tzinfo==timezone.utc,'approval-recording-date')
    return sha(body)

def current_pins(doc):
    rows=re.findall(r'^\| Current uncommitted `([^`]+)` \| `([0-9a-f]{64})` \|$',doc,re.M)
    expected={'verify.py','independent-answers.mjs','source-manifest.json','metadata-adapter-contract.md','build-integration.py','preserved-hold-predicates.json'}
    require({p for p,_ in rows}=={PREFIX+name for name in expected} and len(rows)==len(expected),'current-executable-pin-set')
    for path,expected_hash in rows:require(sha((ROOT/path).read_bytes())==expected_hash,'current-executable-pin')
    adapter=(HERE/'metadata-adapter-contract.md').read_text()
    require(doc.split('<!-- imported-adapter-contract-start -->\n',1)[1].split('\n<!-- imported-adapter-contract-end -->',1)[0]==adapter,'adapter-normative-bytes')
    require('jsonb' not in adapter.split('```sql')[1].split('```')[0] and 'registry_scope_hash' in adapter,'adapter-byte-domain')
    require(32+2+8+10+1024+1024+128==2228 and 2228<2704,'adapter-index-width')
    return len(rows)

def pragmatic_controls(doc,manifest):
    from unittest.mock import patch
    controls=[]
    # No repository-wide Git query can make ordinary owner changes fail this seam.
    unrelated=['README.md','tests/owner-change.cs','references/Hexalith.AI.Tools']
    def repository_query(*args):raise AssertionError('repository freeze queried')
    with patch.dict(globals(),{'git':repository_query}):
        descriptive=scope(manifest,unrelated)
    require(descriptive['ignoredUnrelatedPaths']==sorted(unrelated) and
            descriptive['repositoryFreeze'] is False,'unrelated-owner-changes')
    controls.append({'control':'unrelated owner commit/worktree/submodule changes',
                     'owningCheck':'unrelated-owner-changes','result':'accepted without repository queries'})
    def refuses(owner,action):
        try:action()
        except Refusal as error:
            require(str(error)==owner,'mutation-owning-failure-'+owner)
            return {'mutation':owner,'owningFailure':str(error),'result':'rejected'}
        raise Refusal('mutation-survived-'+owner)
    wrong=json.loads(json.dumps(manifest))
    wrong['inputs'][CANDIDATES['a']]['sha256']='0'*64
    controls.append(refuses('immutable-input-pin',lambda:inputs(wrong,doc)))
    raw=doc.encode()
    controls.append(refuses('approval-digest',lambda:approval(raw.replace(b'# Event Versioning And Upcasting',b'# Changed approval body',1))))
    controls.append(refuses('receipt-bytes',lambda:approval(raw.replace(b'\n',b'\r\n'))))
    controls.append(refuses('approval-receipt-fields',lambda:approval(raw.replace(b'ApprovalDigest: ',b'RelabelledDigest: ',1))))
    ledger=(ART/'deferred-work.md').read_text()
    appended=ledger+'\n- source_spec: `owner-unrelated.md`\n  summary: Unrelated owner follow-up.\n  evidence: Owner progress outside Story 6.5.\n  status: open\n'
    edited=ledger.replace('  status: open\n','  status: resolved by unrelated owner work\n',1)
    require(edited!=ledger and owned_ledger(appended)==owned_ledger(edited)==47,'unrelated-ledger-progress')
    controls.append({'control':'unrelated ledger append and status edit','owningCheck':'unrelated-ledger-progress','result':'accepted'})
    owned=next(row for row in re.findall(r'^- source_spec:[^\n]*(?:\n  [^\n]*)*',ledger,re.M)
               if '  status: open — accepted spec disposition' in row)
    controls.append(refuses('ledger-owned-record',lambda:owned_ledger(ledger.replace(owned,'',1))))
    controls.append(refuses('ledger-owned-record',lambda:owned_ledger(ledger.replace(owned,owned.replace('  summary: ','  summary: Changed owned identity: ',1),1))))
    controls.append(refuses('ledger-47-open-followups',lambda:owned_ledger(ledger.replace(owned,owned.replace('  status: open — accepted spec disposition','  status: resolved',1),1))))
    return controls

def review_fix_controls(doc,manifest):
    def refuses(owner,action):
        try:action()
        except Refusal as error:
            require(str(error)==owner,'mutation-owning-failure-'+owner)
            return {'mutation':owner,'owningFailure':str(error),'result':'rejected'}
        raise Refusal('mutation-survived-'+owner)
    controls=[]
    controls.append(refuses('receipt-bytes',lambda:normative_text(doc.encode().replace(b'\n',b'\r\n'))))
    controls.append(refuses('receipt-bytes',lambda:normative_text(b'\xef\xbb\xbf'+doc.encode())))
    controls.append(refuses('receipt-bytes',lambda:normative_text(doc.encode()+b'<!-- APPROVAL RECEIPT: mutable fields below -->\n')))
    marker=b'<!-- APPROVAL RECEIPT: mutable fields below -->\n'
    controls.append(refuses('receipt-bytes',lambda:normative_text(doc.encode().replace(marker,b'x'+marker,1))))
    pin_row=next(l for l in doc.splitlines() if l.startswith('| 6.5a |'))
    controls.append(refuses('document-input-identities',lambda:document_input_pins(manifest,doc.replace(pin_row+'\n','',1))))
    controls.append(refuses('document-input-identities',lambda:document_input_pins(manifest,doc.replace(pin_row,pin_row+'\n'+pin_row,1))))
    controls.append(refuses('document-input-identities',lambda:document_input_pins(manifest,doc.replace('| 6.5a |','| 6.5b |',1))))
    drow=next(l for l in doc.splitlines() if l.startswith('| `_bmad-output/implementation-artifacts/6-5d-simplification/'))
    wrong_d=re.sub(r'`[0-9a-f]{64}` \|$','`'+'0'*64+'` |',drow)
    controls.append(refuses('document-d-input-pins',lambda:document_input_pins(manifest,doc.replace(drow,wrong_d,1))))
    controls.append(refuses('document-d-input-pins',lambda:document_input_pins(manifest,doc.replace(drow+'\n','',1))))
    controls.append(refuses('document-d-input-pins',lambda:document_input_pins(manifest,doc.replace(drow,drow+'\n'+drow,1))))
    wrong=doc.replace('31f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444','01f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444',1)
    controls.append(refuses('document-input-pin',lambda:document_input_pins(manifest,wrong)))
    hold=next(l for l in doc.splitlines() if l.startswith('| `ActorCommitEvidenceHold` | `actor_commit_evidence_hold` |'))
    controls.append(refuses('hold-inventory-mappings',lambda:held_predicates(doc.replace(hold+'\n','',1))))
    preserved=json.loads((HERE/'preserved-hold-predicates.json').read_text())[:-1]
    controls.append(refuses('hold-predicate-baseline',lambda:held_predicates(doc,preserved)))
    for field in ('signature','generation'):
        actual=cursor_known_answers()
        value=actual['signature'] if field=='signature' else actual['generation']['sha256']
        changed=('0' if value[0]!='0' else '1')+value[1:]
        start=doc.index('<!-- inventory-cursor-known-answers-start -->')
        end=doc.index('<!-- inventory-cursor-known-answers-end -->',start)
        wrong=doc[:start]+doc[start:end].replace(value,changed,1)+doc[end:]
        controls.append(refuses('inventory-cursor-literals',lambda:cursor_answers(wrong)))
    from unittest.mock import patch
    for label,path in CANDIDATES.items():
        historical_first=sorted(ART.glob('spec-6-5'+label+'-*.md'))
        require(historical_first[0].name.endswith('-2.md'),'candidate-enumeration-fixture')
        with patch.object(Path,'glob',return_value=iter(historical_first)) as enumeration:
            code,_,_=execute_model(label)
            require(sha(code.encode())==manifest['inputs'][path]['pythonBlocks'][0]
                    and not enumeration.called,'candidate-enumeration-independent')
    controls.append({'mutation':'sorted directory enumeration with historical execution record first',
                     'owningCheck':'candidate-enumeration-independent','result':'passed for all three exact pinned candidates'})
    return controls

def mutation_controls(doc,manifest):
    def refuses(owner,action):
        try:action()
        except Refusal as error:
            require(str(error)==owner,'mutation-owning-failure-'+owner)
            return {'mutation':owner,'owningFailure':str(error),'result':'rejected'}
        raise Refusal('mutation-survived-'+owner)
    controls=review_fix_controls(doc,manifest)
    match=re.search(r'^I11SameAttemptPairs=(\[.*\])$',doc,re.M);pairs=json.loads(match[1]);pairs[4][2]=True
    changed=doc[:match.start(1)]+json.dumps(pairs,separators=(',',':'))+doc[match.end(1):]
    controls.append(refuses('same-attempt-pairs',lambda:vectors(changed)))
    lines=text_block(doc,'B-series local codec answers (SHA-256 of the complete encoded record):')
    a,b=lines[:2];wrong=doc.replace(a,a.rsplit(' ',1)[0]+' '+b.rsplit(' ',1)[1],1).replace(b,b.rsplit(' ',1)[0]+' '+a.rsplit(' ',1)[1],1)
    controls.append(refuses('b-labeled-vectors',lambda:vectors(wrong)))
    wrong=doc.replace('`fa9dcf5c6ce3b8fe79a1b4bb6fee665f65431c8be5e10ac4af34ced59e6fb02e`','`0a9dcf5c6ce3b8fe79a1b4bb6fee665f65431c8be5e10ac4af34ced59e6fb02e`',1)
    controls.append(refuses('c-inline-offset-vectors',lambda:vectors(wrong)))
    controls.append(refuses('verifier-identities-count',lambda:blocks(doc.replace('```bash\nnode','```python\nnode',1))))
    controls.extend(pragmatic_controls(doc,manifest))
    wrong=doc.replace('31f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444','01f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444',1)
    controls.append(refuses('document-input-pin',lambda:inputs(manifest,wrong)))
    return controls

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--mutations',action='store_true')
    parser.add_argument('--review-fixes',action='store_true')
    parser.add_argument('--pragmatic-policy',action='store_true');args=parser.parse_args()
    raw=DOC.read_bytes();doc=normative_text(raw);manifest=json.loads((HERE/'source-manifest.json').read_text())
    if args.pragmatic_policy:
        print(json.dumps({'result':'passed','authority':'Owner approved; Story 6.6 ready on owner request',
            'scope':scope(manifest),'immutableInputs':inputs(manifest,doc),'dispositions':dispositions(doc),
            'currentPins':current_pins(doc),'controls':pragmatic_controls(doc,manifest),
            'normativeDigest':approval(raw)},sort_keys=True))
        return
    if args.review_fixes:
        # Only the edited-byte/pin/hold/cursor/model-selection seams; full verification belongs to the parent.
        adapter=(HERE/'metadata-adapter-contract.md').read_text()
        require(all(token in adapter for token in ('fresh authenticated SQL readback','30-second recovery budget',
                'monotonic caller deadline','128 MiB aggregate live scratch','validated/read back sequentially',
                'No PostgreSQL-native receipt or signed proof record family')),'adapter-review-contract')
        c5=doc.split('### C5.',1)[1].split('#### Imported reviewed D1',1)[0]
        require('complete retained closure/authentication sources' not in c5 and
                'authenticated active execution control/current-window claim' in c5 and
                'never requires those reclaimed artifacts' in c5,'c5-reclaimed-history')
        print(json.dumps({'result':'passed','authority':'Owner approved; Story 6.6 ready on owner request',
            'pins':document_input_pins(manifest,doc),'heldPredicates':held_predicates(doc),
            'cursor':cursor_answers(doc),'currentPins':current_pins(doc),'controls':review_fix_controls(doc,manifest),
            'normativeDigest':approval(raw)},sort_keys=True))
        return
    result={'result':'passed','baseline':BASE,'scope':scope(manifest),'immutableInputs':inputs(manifest,doc),
            'vectors':vectors(doc),'constructors':parent_answers(doc),'blocks':blocks(doc),
            'heldPredicates':held_predicates(doc),'inventoryCursor':cursor_answers(doc),
            'dispositions':dispositions(doc),'currentPins':current_pins(doc),'normativeDigest':approval(raw),
            'authority':'Owner approved; Story 6.6 ready on owner request; fixtures do not prove provider behavior'}
    if args.mutations:result['mutations']=mutation_controls(doc,manifest)
    print(json.dumps(result,sort_keys=True))

if __name__=='__main__':
    try:main()
    except Refusal as error:
        print('REFUSED: '+str(error),file=sys.stderr);raise SystemExit(1)
