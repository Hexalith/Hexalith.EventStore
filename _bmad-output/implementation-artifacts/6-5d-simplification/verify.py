#!/usr/bin/env python3
"""Executable documentation model. Fixture auth/transactions are not provider proof."""
import copy
import hashlib
import json
import re
import struct
import secrets
from pathlib import Path

HERE = Path(__file__).resolve().parent
MAX = 2**64 - 1
MIB = 1024**2
TICK = 10_000_000
DAY = 86400 * TICK
ZERO = '00' * 32
CAPS = {'execution': 768*1024, 'held': 128*1024, 'queue': 16384, 'queue-shard': 100*MIB,
        'registry': 16384, 'registry-entry': 65536, 'registry-scope': 16384, 'epoch': 16384, 'cursor': 16384}
FIELDS = {
 'execution': 'schema tenant execution scope revision phase firstUtc updatedUtc source window windowClaim closedCount history ordinal limit drainBase roster accepted unresolved legacy request intent receipts outcomes reason nextUtc charge',
 'held': 'schema scopeKind scopeId deployment tenant component topic subscription policy revision phase reason firstUtc updatedUtc observations length carrier locator objectReceipt charge request attempt redrives nextUtc repair error intent receipts',
 'queue': 'schema deployment generation lastTicket count',
 'queue-shard': 'schema deployment shard generation rows',
 'registry': 'schema deployment shard generation entryCount scopeCount',
 'registry-entry': 'schema deployment shard generation scopeKind scopeId subject owner address firstUtc state',
 'registry-scope': 'schema deployment scopeKind scopeId generation count',
 'epoch': 'schema deployment holder epoch generation ownerFence acquiredUtc expiresUtc renewedUtc',
 'cursor': 'schema scopeKind scopeId generation last expiry',
}
SCHEMAS = {k: 'hexalith.eventstore.'+v+'/1' for k,v in
           [('execution','execution-control'),('held','held-control'),
            ('queue','capacity-queue'),('queue-shard','capacity-queue-shard'),('registry','owner-registry'),('registry-entry','owner-registry-entry'),('registry-scope','owner-registry-scope'),('epoch','operations-epoch'),('cursor','hold-cursor')]}
ELIGIBILITY = ('retry-exhausted','drain-limit','drain-limit-and-retry-exhausted','legacy-publish-failed')
OWNERS = ('actor','coordinator','gateway','subscriber','projection','operations','quota-coordinator')
HELD_REASONS = ('handler-capability-hold','raw-source-unavailable','delivery-carrier-limit-hold','invalid-header-value','invalid-carrier','oversize-carrier','delivery_above_advertised_max')
EXECUTION_REASONS = ELIGIBILITY + ('publication_resume_preparation_hold','resume_evidence_hold','legacy_resume_evidence_unavailable','outcome_evidence_hold','outcome_evidence_conflict','quota_generation_exhausted')
COUNTS = {}

class Refusal(Exception):
    pass

class Crash(Exception):
    pass

def need(test, why):
    if not test:
        raise Refusal(why)

def hashbytes(b):
    return hashlib.sha256(b).hexdigest()

def canonical(x):
    try:
        return json.dumps(x, ensure_ascii=False, sort_keys=True, separators=(',',':'),
                          allow_nan=False).encode('utf-8')
    except (ValueError, UnicodeError, TypeError) as e:raise Refusal('invalid-json') from e

def pairs(rows):
    d = {}
    for k,v in rows:
        need(k not in d, 'duplicate-json-key')
        d[k] = v
    return d

def decode(b):
    try:
        x = json.loads(b.decode('utf-8'), object_pairs_hook=pairs)
    except (ValueError, UnicodeError) as e:
        raise Refusal('invalid-json') from e
    need(canonical(x) == b, 'canonical-json')
    return x

def integer(n):
    need(type(n) is int and 0 <= n <= MAX, 'u64')
    return n

def add(a,b):
    integer(a); integer(b)
    need(a <= MAX-b, 'arithmetic')
    return a+b

def utc(n):
    need(type(n) is int and -(2**63) <= n < 2**63, 'utc')
    return n

def textfield(s, cap=1024):
    try:need(type(s) is str and 1 <= len(s.encode('utf-8')) <= cap, 'identifier')
    except UnicodeError as e:raise Refusal('identifier') from e
    return s

def hexbytes(s, cap=None):
    need(type(s) is str and re.fullmatch('(?:[0-9a-f]{2})*',s) is not None,'hex')
    b=bytes.fromhex(s);need(cap is None or len(b)<=cap,'hex-bound');return b

def subtract(a,b):
    integer(a);integer(b);need(a>=b,'refund-underflow');return a-b

def digest(d):
    need(type(d) is str and re.fullmatch('[0-9a-f]{64}',d) is not None,'digest')
    return d

def exact(d, names):
    need(type(d) is dict and set(d) == set(names.split()),'fields')

def ucap(domain,tag,fields):
    if domain=='HX-EV-CARRIER-QUARANTINE-2' and tag==9:return 4096
    if domain=='HX-EV-PUBLICATION-COUNTER-1' and tag==3 and fields[1][1]=='tenant':return 1031
    return 1024

def frame(k,v,cap=1024):
    if k.startswith('O:'):
        return b'\x00' if v is None else b'\x01'+frame(k[2:],v,cap)
    if k == 'U':
        v=textfield(v,cap).encode('utf-8')
        return len(v).to_bytes(4,'big')+v
    if k == 'B':
        v=hexbytes(v)
        need(len(v)<2**32,'blob-bound')
        return len(v).to_bytes(4,'big')+v
    if k == 'B32': return bytes.fromhex(digest(v))
    if k == 'N': return integer(v).to_bytes(8,'big')
    if k == 'P':
        need(type(v) is int and 0<=v<2**32,'u32')
        return v.to_bytes(4,'big')
    if k == 'Q': return utc(v).to_bytes(8,'big',signed=True)
    if k == 'I':
        need(type(v) is int and -(2**31)<=v<2**31,'i32')
        return v.to_bytes(4,'big',signed=True)
    raise Refusal('type')

def record(domain, fields):
    need(len(fields)<=255,'tag-count')
    return domain.encode('ascii')+b'\x00\x01'+len(fields).to_bytes(2,'big')+b''.join(
        bytes([i])+frame(k,v,ucap(domain,i,fields)) for i,(k,v) in enumerate(fields,1))

def read_record(b, answer):
    need(len(b)<=answer['maxBytes'],'record-cap')
    prefix=answer['domain'].encode()+b'\x00\x01'+len(answer['fields']).to_bytes(2,'big')
    need(b.startswith(prefix),'record-framing')
    pos=len(prefix)
    def get(k,cap):
        nonlocal pos
        if k.startswith('O:'):
            need(pos<len(b),'optional-short')
            flag=b[pos]; pos+=1
            need(flag in (0,1),'optional-flag')
            return None if flag==0 else get(k[2:],cap)
        if k in ('U','B'):
            need(pos+4<=len(b),'length-short')
            n=int.from_bytes(b[pos:pos+4],'big');pos+=4
            need(n<=len(b)-pos,'allocation-bound')
            v=b[pos:pos+n];pos+=n
            if k=='B':return v.hex()
            try: return textfield(v.decode('utf-8'),cap)
            except UnicodeError as e:raise Refusal('utf8') from e
        n={'B32':32,'N':8,'Q':8,'I':4,'P':4}[k]
        need(pos+n<=len(b),'fixed-short')
        v=b[pos:pos+n];pos+=n
        return v.hex() if k=='B32' else int.from_bytes(v,'big',signed=k in ('Q','I'))
    result=[]
    for i,(k,_) in enumerate(answer['fields'],1):
        need(pos<len(b) and b[pos]==i,'record-tag');pos+=1
        result.append([k,get(k,ucap(answer['domain'],i,result))])
    need(pos==len(b),'record-trailing')
    need(record(answer['domain'],result)==b,'record-roundtrip')
    return result

def typed(kind, b):
    try:return validate_control(kind,b)
    except (ValueError,UnicodeError,TypeError,KeyError,IndexError,OverflowError) as e:
        raise Refusal('control-shape') from e

def validate_control(kind,b):
    need(kind in CAPS and len(b)<=CAPS[kind], 'control-byte-bound')
    d=decode(b);exact(d,FIELDS[kind]);need(d['schema']==SCHEMAS[kind],'schema')
    if kind=='execution':
        for f in ('tenant','execution','charge'):textfield(d[f])
        need(re.fullmatch('publication-charge:[0-9a-f]{64}',d['charge']) is not None,'charge-address')
        for f in ('scope','source','windowClaim','history'):digest(d[f])
        for f in ('revision','window','closedCount','ordinal','limit','drainBase'):integer(d[f])
        need(d['limit']>0 and d['drainBase']>0,'drain-limit')
        need(d['phase'] in 'idle prepared disable reject closure window audit successor finalize invoke cleanup incident'.split(),'execution-phase')
        need(d['reason'] is None or d['reason'] in EXECUTION_REASONS,'execution-reason')
        roster=d['roster'];need(type(roster) is list and len(roster)<=59,'roster-bound')
        for r in roster:
            exact(r,'position message digest');need(0<integer(r['position'])<2**32,'position');textfield(r['message']);digest(r['digest'])
        need(roster==sorted(roster,key=lambda r:r['position']), 'roster-order')
        need(len({r['position'] for r in roster})==len(roster) and len({r['message'] for r in roster})==len(roster),'roster-unique')
        a,u=d['accepted'],d['unresolved'];need(type(a) is list and type(u) is list,'partition-shape')
        for n in a+u:need(0<integer(n)<2**32,'partition-position')
        need(a==sorted(set(a)) and u==sorted(set(u)) and not set(a)&set(u) and set(a)|set(u)=={r['position'] for r in roster},'partition')
        need(type(d['outcomes']) is list and len(d['outcomes'])<=64,'outcome-bound')
        for r in d['outcomes']:
            exact(r,'identity carrierHash ordinal window limit audit expiry deleteAfter result oldCharge artifacts')
            for f in ('identity','carrierHash','audit'):digest(r[f])
            for f in ('ordinal','window','limit'):integer(r[f])
            need(0<r['ordinal']<=d['ordinal'],'outcome-ordinal');utc(r['expiry']);utc(r['deleteAfter'])
            need(r['deleteAfter']==r['expiry']+30*DAY,'fixed-horizon');response(r['result']);textfield(r['oldCharge']);need(re.fullmatch('publication-charge:[0-9a-f]{64}',r['oldCharge']) is not None,'charge-address');need(type(r['artifacts']) is list and len(r['artifacts'])<=12 and len(set(r['artifacts']))==len(r['artifacts']),'outcome-artifacts')
            for address in r['artifacts']:textfield(address,256)
        need(len({r['identity'] for r in d['outcomes']})==len(d['outcomes']),'outcome-identity')
        need([r['ordinal'] for r in d['outcomes']]==sorted({r['ordinal'] for r in d['outcomes']}),'outcome-order')
        if d['legacy'] is not None:
            l=d['legacy'];exact(l,'capsule identity owner state generation ordinal failure repaired')
            digest(l['capsule']);digest(l['identity']);need(integer(l['generation'])>0,'legacy-generation');integer(l['ordinal'])
            need(l['owner'] in ('legacy-resume','dead-letter-admin') and l['state'] in ('claimed','draining','completed','failed'),'legacy-schema')
            need(l['failure'] in (None,'transport-retryable','evidence-unavailable','evidence-contradictory'),'legacy-failure')
            need((l['state']=='failed')==(l['failure'] is not None),'legacy-failure-phase')
            if l['repaired'] is not None:digest(l['repaired']);need(l['state'] in ('claimed','draining'),'legacy-repaired-phase')
        if d['request'] is not None:
            r=d['request'];exact(r,'identity carrier claim signature utc expiry eligibility source priorHash result')
            for f in ('identity','source','priorHash'):digest(r[f])
            hexbytes(r['carrier'],2048);hexbytes(r['claim'],4096);hexbytes(r['signature'],8192)
            utc(r['utc']);utc(r['expiry']);need(r['utc']<r['expiry']<=r['utc']+900*TICK,'request-horizon')
            need(r['eligibility'] in ELIGIBILITY,'eligibility');response(r['result'])
        need(d['request'] is not None or d['phase'] in ('idle','cleanup','incident'),'request-phase')
        if d['phase']=='idle':need(d['request'] is None and d['intent'] is None and not d['receipts'],'idle-fields')
        if d['intent'] is not None and d['phase'] not in ('cleanup','incident'):
            next_action='audit' if d['phase']=='prepared' and d['request']['eligibility'] in ('drain-limit','legacy-publish-failed') else NEXT.get(d['phase'])
            need(d['intent']['kind']==next_action,'intent-phase')
    elif kind=='held':
        need(d['scopeKind'] in ('tenant','deployment') and (d['tenant'] is not None)==(d['scopeKind']=='tenant'),'held-scope')
        for f in ('scopeId','deployment','component','topic','subscription','charge'):textfield(d[f])
        need(re.fullmatch('publication-charge:[0-9a-f]{64}',d['charge']) is not None,'charge-address')
        if d['tenant'] is not None:textfield(d['tenant']);need(d['scopeId']==d['tenant'],'held-tenant')
        else:need(d['scopeId']==d['deployment'],'held-deployment')
        for f in ('policy','carrier'):digest(d[f])
        for f in ('revision','observations','length','redrives'):integer(d[f])
        need(d['phase'] in 'observed capturing captured redriving cleanup quarantined incident'.split(),'held-phase')
        need(d['reason'] in HELD_REASONS,'held-reason');need(d['repair'] in ('none','absent','corrupt','required','repaired'),'repair')
        if d['locator'] is not None:
            exact(d['locator'],'backend key');textfield(d['locator']['backend']);textfield(d['locator']['key'],4096)
        need((d['locator'] is None)==(d['objectReceipt'] is None),'locator-pair')
        if d['objectReceipt'] is not None:digest(d['objectReceipt'])
        if d['phase'] in ('captured','redriving','quarantined'):need(d['locator'] is not None,'retained-locator')
        if d['request'] is not None:
            r=d['request'];exact(r,'claim signature expectedCount utc');hexbytes(r['claim'],3072);hexbytes(r['signature'],8192);integer(r['expectedCount']);utc(r['utc'])
        if d['attempt'] is not None:
            r=d['attempt'];exact(r,'count requestHash carrier utc result');integer(r['count']);digest(r['requestHash']);digest(r['carrier']);utc(r['utc'])
            need(r['result'] in (None,'terminal','nonterminal','unknown'),'attempt-result')
        if d['error'] is not None:
            r=d['error'];exact(r,'reason hash utc');textfield(r['reason'],128);digest(r['hash']);utc(r['utc'])
        if d['phase']=='observed':need(d['locator'] is None and d['request'] is None and d['attempt'] is None and d['intent'] is None,'observed-fields')
        if d['phase']=='capturing':need(d['intent'] is not None and d['intent']['kind']=='capture','capture-intent')
        if d['phase']=='cleanup':need(d['intent'] is not None and d['intent']['kind'] in ('delivered-cleanup','erase'),'cleanup-intent')
        if d['phase'] in ('captured','quarantined'):need(d['intent'] is None,'held-idle-intent')
        if d['phase']=='redriving':need(d['intent'] is not None and d['intent']['kind']=='send','send-intent')
    elif kind in ('queue','queue-shard'):
        textfield(d['deployment']);integer(d['generation'])
        if kind=='queue':
            integer(d['lastTicket']);need(integer(d['count'])<=50000,'queue-count');return d
        need(0<=integer(d['shard'])<8,'queue-shard');need(type(d['rows']) is list and len(d['rows'])<=6400,'queue-count')
        for r in d['rows']:
            exact(r,'subject tenant scope plan ticket firstUtc updatedUtc state candidate amount attempts charge owner')
            need(len(canonical(r))<=16384,'queue-row-bound')
            for f in ('subject','scope','plan'):digest(r[f])
            for f in ('tenant','owner','charge'):textfield(r[f])
            integer(r['attempts']);need(0<integer(r['ticket']) and (r['ticket']-1)%8==d['shard'],'ticket');utc(r['firstUtc']);utc(r['updatedUtc'])
            need(r['state'] in ('reserved','queued','parked','cleanup'),'queue-state')
            need((r['candidate'] is None)==(r['amount'] is None),'queue-candidate')
            if r['amount'] is not None:digest(r['candidate']);need(integer(r['amount'])>0,'queue-amount')
            need(r['state']!='reserved' or r['amount'] is None,'reserved-fields');need(r['state']!='queued' or r['amount'] is not None,'queued-fields')
            need(r['updatedUtc']>=r['firstUtc'],'queue-clock')
        need([r['ticket'] for r in d['rows']]==sorted({r['ticket'] for r in d['rows']}),'queue-order')
        need(len({r['subject'] for r in d['rows']})==len(d['rows']),'queue-subject')
    elif kind in ('registry','registry-entry','registry-scope'):
        textfield(d['deployment']);integer(d['generation'])
        if kind!='registry-scope':need(0<=integer(d['shard'])<256,'registry-shard')
        if kind=='registry':
            need(integer(d['scopeCount'])<=50000 and integer(d['entryCount'])<=50000*10000,'registry-count')
        elif kind=='registry-scope':
            need(d['scopeKind'] in ('tenant','deployment'),'registry-scope');textfield(d['scopeId']);need(integer(d['count'])<=10000,'registry-subject-count')
        else:
            need(d['scopeKind'] in ('tenant','deployment'),'registry-scope')
            for f,cap in (('scopeId',1024),('subject',1024),('address',256)):textfield(d[f],cap)
            need(d['owner'] in OWNERS,'registry-owner');utc(d['firstUtc']);need(d['state'] in ('reserved','present','cleanup'),'registry-state')
    elif kind=='epoch':
        textfield(d['deployment']);textfield(d['holder'])
        need(integer(d['epoch'])>0 and integer(d['generation'])>0,'epoch-generation')
        need(type(d['ownerFence']) is str and re.fullmatch('[0-7][0-9A-HJKMNP-TV-Z]{25}',d['ownerFence']) is not None,'owner-fence')
        for f in ('acquiredUtc','expiresUtc','renewedUtc'):utc(d[f])
        need(d['acquiredUtc']<=d['renewedUtc']<d['expiresUtc'] and d['expiresUtc']==d['renewedUtc']+60*TICK,'epoch-clock')
    else:
        need(d['scopeKind'] in ('tenant','deployment'),'hold_inventory_cursor_scope_mismatch');textfield(d['scopeId']);digest(d['generation']);utc(d['expiry'])
        if d['last'] is not None:
            need(type(d['last']) is list and len(d['last'])==4,'cursor-position');utc(d['last'][0]);need(d['last'][1:3]==[d['scopeKind'],d['scopeId']],'cursor-position-scope');textfield(d['last'][3],1024)
    if kind in ('execution','held'):
        for f in ('firstUtc','updatedUtc','nextUtc'):utc(d[f])
        need(d['updatedUtc']>=d['firstUtc'],'owner-clock');need(type(d['receipts']) is dict and len(d['receipts'])<=8,'receipt-count')
        for k,v in d['receipts'].items():textfield(k,128);textfield(v,256)
        if d['intent'] is not None:
            i=d['intent'];exact(i,'kind address hash payload');textfield(i['address'],256);digest(i['hash'])
            allowed=tuple(a for a in NEXT.values() if a!='idle')+('capsule','erase','cleanup') if kind=='execution' else ('capture','send','delivered-cleanup','erase')
            need(i['kind'] in allowed,'intent-kind');need(type(i['payload']) is dict and len(canonical(i['payload']))<=(2048 if kind=='held' else 128*1024),'intent-bound')
    return d

def response(s):
    r=decode(hexbytes(s,512));exact(r,'resumeHandle resumeOrdinal window drainLimit auditRecordHash')
    textfield(r['resumeHandle']);digest(r['auditRecordHash'])
    for f in ('resumeOrdinal','window','drainLimit'):integer(r[f])
    return r

def mint_fence():
    alphabet='0123456789ABCDEFGHJKMNPQRSTVWXYZ';value=secrets.randbits(128)
    return ''.join(alphabet[(value>>(5*i))&31] for i in reversed(range(26)))

def registry_sort(r):
    return (r['firstUtc'],r['scopeKind'].encode(),r['scopeId'].encode(),r['subject'].encode())

class Store:
    """Byte-only restart with authenticated fixture readback and staged local CAS."""
    def __init__(self, wire=None):
        self.before_transaction=None
        self.rows=decode(wire) if wire else {}
        self.unavailable=set()
    def snapshot(self):return canonical(self.rows)
    def restart(self):return Store(self.snapshot())
    def read(self,key,owner=None):
        need(key not in self.unavailable,'evidence-unavailable')
        row=self.rows.get(key)
        if row is None:return None
        exact(row,'body generation owner receipt fence index' if 'index' in row else 'body generation owner receipt fence')
        need(row['receipt']==self.receipt(key,row['body'],row['generation'],row['owner'],row.get('index'),row['fence']),'authenticated-readback')
        if owner is not None:need(row['owner']==owner,'owner')
        return bytes.fromhex(row['body'])
    @staticmethod
    def receipt(key,body,generation,owner,index=None,fence=None):
        return hashbytes(b'fixture-provider-only\0'+canonical([key,body,generation,owner]+([] if index is None else [index])+([] if fence is None else [fence])))
    def write(self,key,b,owner,expected):
        old=self.read(key,owner)
        need(old==expected,'cas')
        gen=add(self.rows[key]['generation'],1) if old is not None else 1
        index=None
        if key.startswith('owner-registry-entry:'):
            d=typed('registry-entry',b);index={f:d[f] for f in ('deployment','shard','scopeKind','scopeId','subject','owner','address','firstUtc')}
            if old is not None:need(index==self.rows[key]['index'],'registry-index-binding')
        fence=self.rows[key]['fence'] if old is not None else mint_fence()
        self.rows[key]={'body':b.hex(),'generation':gen,'owner':owner,'fence':fence,'receipt':self.receipt(key,b.hex(),gen,owner,index,fence)}
        if index is not None:self.rows[key]['index']=index
    def external(self,key,b,owner):
        old=self.read(key,owner)
        need(old is None or old==b,'external-conflict')
        if old is None:self.write(key,b,owner,None)
        return hashbytes(self.read(key,owner))
    def delete(self,key,owner):
        self.read(key,owner)
        self.rows.pop(key,None)
    def transaction(self, fn):
        expected=copy.deepcopy(self.rows)
        if self.before_transaction is not None:
            hook=self.before_transaction;self.before_transaction=None;hook(self)
        staged=self.restart();staged.unavailable=set(self.unavailable)
        result=fn(staged)
        # The admitted generation/fence cannot be refreshed by rereading equal payloads.
        # Check every changed participant against its pre-admission provider token.
        for key in set(staged.rows)|set(self.rows):
            if staged.rows.get(key)!=self.rows.get(key):
                old=expected.get(key);current=self.rows.get(key)
                token=lambda r:None if r is None else (r['generation'],r['fence'])
                need(token(old)==token(current),'cas')
        self.rows=staged.rows
        return result

def getcontrol(db,key,kind):
    b=db.read(key,key);need(b is not None,'control-absent');return typed(kind,b)

def savecontrol(db,key,kind,d):
    prior=db.read(key,key)
    if prior is not None:
        p=typed(kind,prior)
        field='revision' if kind in ('execution','held') else 'generation'
        need(d[field]==p[field],'predecessor-generation')
        need(d.get('updatedUtc',0)>=p.get('updatedUtc',0),'utc-regression')
        if kind in ('execution','held'):
            need(d['firstUtc']==p['firstUtc'],'first-observation-identity')
        if kind=='execution':
            need(d['roster']==p['roster'] and set(p['accepted'])<=set(d['accepted']),'execution-progress')
            need(d['tenant']==p['tenant'] and d['execution']==p['execution'],'execution-identity')
            if p['legacy'] is not None:
                need(d['legacy'] is not None and d['legacy']['capsule']==p['legacy']['capsule'] and d['legacy']['identity']==p['legacy']['identity'],'legacy-capsule-binding')
                need(d['legacy']['generation']>=p['legacy']['generation'] and d['legacy']['ordinal']>=p['legacy']['ordinal'],'legacy-generation-regression')
        if kind in ('execution','held') and p['intent'] is not None and d['phase']==p['phase']:
            if d['intent'] is None:
                artifact=db.read(p['intent']['address'],key);need(artifact is not None and hashbytes(artifact)==p['intent']['hash'],'intent-binding')
            else:need(d['intent']==p['intent'],'intent-binding')
        if kind=='held':
            need(d['observations']>=p['observations'] and d['redrives']>=p['redrives'],'count-regression')
            need(all(d[f]==p[f] for f in ('scopeKind','scopeId','deployment','tenant','component','topic','subscription','carrier','length')),'held-identity')
        d=copy.deepcopy(d);d[field]=add(d[field],1)
    b=canonical(d);typed(kind,b);db.write(key,b,key,prior)
    return d

def install(db,key,kind,d):
    typed(kind,canonical(d));db.write(key,canonical(d),key,None)

def fixture_sign(b):return hashbytes(b'fixture-purpose-2d-only\0'+b)

def signed(b,sig):need(fixture_sign(b)==sig,'fixture-signature')

def keyed(domain,prefix,fields):
    return prefix+hashbytes(domain.encode()+b'\x00\x01'+b''.join(frame(k,v) for k,v in fields))

def provider_address(kind,owner,identity=ZERO):
    # Fixture provider sources stand in for existing broker/actor artifacts, not new wire families.
    return keyed('FIXTURE-PROVIDER-SOURCE-1','provider-source:',[['U',kind],['U',owner],['B32',identity]])

def authority(db,subject,action,create=False):
    address=provider_address('authority-'+action,subject)
    expected=canonical({'subject':subject,'action':action})
    if create:db.external(address,expected,'fixture-authority')
    need(db.read(address,'fixture-authority')==expected,'authority-readback')

def registry_key(kind,scope,subject):
    return keyed('HX-EV-OWNER-REGISTRY-ENTRY-KEY-1','owner-registry-entry:',[['U','dep'],['U',kind],['U',scope],['U',subject]])

def registry_scope_key(kind,scope):
    return keyed('HX-EV-OWNER-REGISTRY-SCOPE-KEY-1','owner-registry-scope:',[['U','dep'],['U',kind],['U',scope]])

def registry_shard_key(shard):
    return keyed('HX-EV-OWNER-REGISTRY-SHARD-KEY-1','owner-registry-shard:',[['U','dep'],['N',shard]])

def registry_shard(kind,scope):return hashlib.sha256(frame('U',kind)+frame('U',scope)).digest()[0]

def registry_rows(db,kind=None,scope=None,incidents=False):
    # Persisted provider index metadata selects addressed rows before owner readback.
    result=[]
    for address,row in sorted(db.rows.items()):
        index=row.get('index')
        if not address.startswith('owner-registry-entry:') or index is None:continue
        if kind is not None and (index['scopeKind'],index['scopeId'])!=(kind,scope):continue
        try:result.append(getcontrol(db,address,'registry-entry'))
        except Refusal:
            if incidents:result.append({**index,'generation':None,'state':'incident'})
            else:raise
    return sorted(result,key=registry_sort)

def registry(db,kind=None,scope=None):
    return {'rows':registry_rows(db,kind,scope,incidents=True)}

def bootstrap(db):
    for shard in range(256):
        install(db,registry_shard_key(shard),'registry',{'schema':SCHEMAS['registry'],'deployment':'dep','shard':shard,'generation':1,'entryCount':0,'scopeCount':0})

def discover(db,address,scope='t',kind='tenant',now=0,owner='coordinator',scope_limit=10000,shard_limit=50000):
    textfield(address,256);need(owner in OWNERS,'registry-owner')
    key=registry_key(kind,scope,address);old=db.read(key,key)
    if old is not None:
        row=typed('registry-entry',old);need(row['address']==address and row['owner']==owner and row['firstUtc']==now,'reservation-conflict');return
    def tx(s):
        scope_key=registry_scope_key(kind,scope);raw=s.read(scope_key,scope_key)
        sh=None if raw is None else typed('registry-scope',raw)
        shard=registry_shard(kind,scope);header_key=registry_shard_key(shard);h=getcontrol(s,header_key,'registry')
        need((0 if sh is None else sh['count'])<scope_limit and (sh is not None or h['scopeCount']<shard_limit),'registry_capacity_hold')
        charge_kind='tenant' if kind=='tenant' else 'capture-scope'
        if sh is None:
            reserve_charge(s,scope_key,charge_kind,scope,CAPS['registry-scope'],objects=(scope_key,))
            install(s,scope_key,'registry-scope',{'schema':SCHEMAS['registry-scope'],'deployment':'dep','scopeKind':kind,'scopeId':scope,'generation':1,'count':1})
        else:
            sh['count']=add(sh['count'],1);savecontrol(s,scope_key,'registry-scope',sh)
        reserve_charge(s,key,charge_kind,scope,65536,objects=(key,))
        row={'schema':SCHEMAS['registry-entry'],'deployment':'dep','shard':shard,'generation':1,'scopeKind':kind,'scopeId':scope,'subject':address,'owner':owner,'address':address,'firstUtc':now,'state':'reserved'}
        install(s,key,'registry-entry',row)
        h['entryCount']=add(h['entryCount'],1);h['scopeCount']=add(h['scopeCount'],1 if sh is None else 0);savecontrol(s,header_key,'registry',h)
    try:db.transaction(tx)
    except Refusal as e:
        if str(e)=='capacity':raise Refusal('registry_capacity_hold') from e
        raise

def registry_owner(db,address):
    index=next((r['index'] for k,r in db.rows.items() if k.startswith('owner-registry-entry:') and r.get('index',{}).get('address')==address),None)
    if index is None:return None
    return getcontrol(db,registry_key(index['scopeKind'],index['scopeId'],index['subject']),'registry-entry')

def registry_present(db,address):
    row=registry_owner(db,address);need(row is not None,'discovery-before-owner')
    need(db.read(address,address) is not None,'owner-readback')
    if row['state']!='present':
        row['state']='present';savecontrol(db,registry_key(row['scopeKind'],row['scopeId'],row['subject']),'registry-entry',row)

def owner_discovered(db,address):need(registry_owner(db,address) is not None,'undiscovered-owner')

def registry_release(db,address):
    row=registry_owner(db,address)
    if row is None:return
    need(db.read(address,address) is None and not any(r['owner']==address for r in db.rows.values()),'owner-deletion-readback')
    def tx(s):
        k=registry_key(row['scopeKind'],row['scopeId'],row['subject']);s.delete(k,k);refund(s,k)
        scope_key=registry_scope_key(row['scopeKind'],row['scopeId']);sh=getcontrol(s,scope_key,'registry-scope');sh['count']=subtract(sh['count'],1)
        empty=sh['count']==0
        if empty:s.delete(scope_key,scope_key);refund(s,scope_key)
        else:savecontrol(s,scope_key,'registry-scope',sh)
        hk=registry_shard_key(row['shard']);h=getcontrol(s,hk,'registry');h['entryCount']=subtract(h['entryCount'],1);h['scopeCount']=subtract(h['scopeCount'],1 if empty else 0);savecontrol(s,hk,'registry',h)
    db.transaction(tx)

def reconcile_placeholder(db,address):
    if db.read(address,address) is not None:registry_present(db,address)
    else:
        authority(db,address,'no-commit-no-effect')
        need(not any(r['owner']==address for r in db.rows.values()),'placeholder-effect')
        registry_release(db,address)

# A small ledger fixture; configured production limits remain the binary capability.
def ledger_init(db,tenant=100,deploy=250,reserve=50,unidentified=100,overhead=1):
    d={'generation':1,'tenantCeiling':tenant,'deploymentCeiling':deploy,'reserve':reserve,'unidentifiedCeiling':unidentified,'overhead':overhead,'used':{},'charges':{},'reservations':{}}
    db.write('ledger',canonical(d),'ledger',None)
    q={'schema':SCHEMAS['queue'],'deployment':'dep','generation':1,'lastTicket':0,'count':0}
    install(db,queue_key(),'queue',q)
    for shard in range(8):install(db,queue_shard_key(shard),'queue-shard',{'schema':SCHEMAS['queue-shard'],'deployment':'dep','shard':shard,'generation':1,'rows':[]})

def ledger_get(db):
    b=db.read('ledger','ledger');need(b is not None,'ledger-absent');return decode(b)

def ledger_save(db,d,prior):
    d['generation']=add(d['generation'],1);db.write('ledger',canonical(d),'ledger',prior)

def account(kind,id):
    need(kind in ('tenant','capture-scope'),'account-kind');return kind+':'+id

def ledger_fit(d,kind,id,amount):
    integer(amount);acct=account(kind,id)
    used=d['used'];own=used.get(acct,0);dep=used.get('deployment',0)
    pool='tenant-pool' if kind=='tenant' else 'unidentified'
    ceiling=d['deploymentCeiling']-d['reserve'] if kind=='tenant' else d['unidentifiedCeiling']
    return add(own,amount)<=(d['tenantCeiling'] if kind=='tenant' else d['unidentifiedCeiling']) and add(used.get(pool,0),amount)<=ceiling and add(dep,amount)<=d['deploymentCeiling']

def retention_capability(tenant,deploy,reserve,unidentified,overhead,quarantine):
    for n in (tenant,deploy,reserve,unidentified,overhead,quarantine):integer(n)
    need(overhead<=1114112 and 193*MIB<=quarantine<=256*MIB,'publication_retention_capability_invalid')
    fixed=add(800*MIB+16384+256*CAPS['registry']+CAPS['epoch'],266*overhead)
    tenant_fixed=add(216*1024+CAPS['registry-scope'],4*overhead)
    need(tenant>=add(add(1024*MIB,overhead),tenant_fixed) and add(tenant,reserve)<=deploy,'publication_retention_capability_invalid')
    need(195*MIB<=reserve<=unidentified<=deploy and add(fixed,add(quarantine,overhead))<=unidentified and reserve>=add(fixed,add(quarantine,overhead)),'publication_retention_capability_invalid')
    return fixed

def capture_interval(length,advertised=256*MIB,valid=False):
    integer(length);need(193*MIB<=advertised<=256*MIB,'capture-capability')
    if length<=193*MIB:return 'ordinary'
    need(not valid,'valid-carrier-oversize')
    return 'provider-quarantine' if length<=advertised else 'incident'

def charge_address(kind,id,object_key):
    if object_key.startswith('publication-charge:'):return object_key
    return keyed('HX-EV-PUBLICATION-CHARGE-KEY-1','publication-charge:',[['U','dep'],['U',kind],['U',id],['B32',hashbytes(frame('B',object_key.encode().hex()))]])

def resolve_charge(db,key):
    if key.startswith('publication-charge:'):return key
    rows=[k for k,c in ledger_get(db)['charges'].items() if c['objectKey']==hashbytes(frame('B',key.encode().hex()))]
    need(len(rows)<=1,'charge-address-ambiguous');return rows[0] if rows else key

def reserve_charge(db,key,kind,id,length,overhead=None,objects=(),locator=None):
    b=db.read('ledger','ledger');d=ledger_get(db);acct=account(kind,id);integer(length);object_key=hashbytes(frame('B',(key if locator is None else locator).encode().hex()));derived=keyed('HX-EV-PUBLICATION-CHARGE-KEY-1','publication-charge:',[['U','dep'],['U',kind],['U',id],['B32',object_key]]);need(not key.startswith('publication-charge:') or key==derived,'charge-address');key=derived
    if key in d['charges']:
        c=d['charges'][key];need((c['account'],c['length'],c['state'])==(acct,length,'active') and c['objectKey']==object_key and c['objects']==list(objects),'charge-conflict');return c['amount']
    o=d['overhead'] if overhead is None else overhead;amount=add(length,o)
    need(ledger_fit(d,kind,id,amount),'capacity')
    c={'objectKey':object_key,'account':acct,'length':length,'overhead':o,'amount':amount,'state':'active','objects':list(objects)}
    d['charges'][key]=c
    for a in (acct,'tenant-pool' if kind=='tenant' else 'unidentified','deployment'):d['used'][a]=add(d['used'].get(a,0),amount)
    ledger_save(db,d,b);return amount

def refund(db,key):
    b=db.read('ledger','ledger');d=ledger_get(db);key=resolve_charge(db,key);c=d['charges'].get(key)
    if c is None:return
    for address in c['objects']:need(db.read(address) is None,'delete-readback')
    kind,id=c['account'].split(':',1);acct=account(kind,id)
    for a in (acct,'tenant-pool' if kind=='tenant' else 'unidentified','deployment'):
        d['used'][a]=subtract(d['used'][a],c['amount'])
    del d['charges'][key];ledger_save(db,d,b)

def batch(db,subject,tenant,pins):
    need(1<=len(pins)<=59,'batch-count')
    need(len({p['message'] for p in pins})==len(pins),'batch-duplicate')
    total=0
    for p in pins:need(0<=integer(p['length'])<=449*MIB,'pin-length');total=add(total,add(p['length'],ledger_get(db)['overhead']))
    def tx(s):
        b=s.read('ledger','ledger');d=ledger_get(s)
        if subject in d['reservations']:
            need(d['reservations'][subject]['pins']==pins and d['reservations'][subject]['tenant']==tenant and d['reservations'][subject]['binding']==hashbytes(canonical([subject,tenant,pins])),'batch-conflict');return d['reservations'][subject]['amount']
        need(ledger_fit(d,'tenant',tenant,total),'capacity')
        for i,p in enumerate(pins):reserve_charge(s,subject+':pin:'+str(i),'tenant',tenant,p['length'])
        b=s.read('ledger','ledger');d=ledger_get(s)
        d['reservations'][subject]={'tenant':tenant,'binding':hashbytes(canonical([subject,tenant,pins])),'pins':pins,'amount':total,'state':'reserved'};ledger_save(s,d,b)
        return total
    return db.transaction(tx)

def pin_admit(db,subject,tenant,pins):
    try:return batch(db,subject,tenant,pins)
    except Refusal as e:raise Refusal('publication_pin_capacity_hold') from e

def queue_key():
    return keyed('HX-EV-PIN-CAPACITY-QUEUE-KEY-1','pin-capacity-queue:',[['U','dep'],['U','deployment']])

def queue_shard_key(shard):
    return keyed('HX-EV-PIN-CAPACITY-QUEUE-SHARD-KEY-1','pin-capacity-queue-shard:',[['U','dep'],['N',shard]])

def queue_view(db):
    q=getcontrol(db,queue_key(),'queue');q['_shards']={};q['rows']=[]
    for shard in range(8):
        key=queue_shard_key(shard);raw=db.read(key,key);d=typed('queue-shard',raw)
        q['_shards'][shard]=raw.hex();q['rows'].extend(d['rows'])
    q['rows'].sort(key=lambda r:r['ticket'])
    need(len(q['rows'])==q['count'] and len({r['subject'] for r in q['rows']})==q['count'],'queue-count-readback')
    for r in q['rows']:need(r['ticket']<=q['lastTicket'],'ticket')
    return q

def queue_save(db,q):
    hk=queue_key();header=getcontrol(db,hk,'queue');need(header['generation']==q['generation'],'cas')
    for shard in range(8):
        key=queue_shard_key(shard);old=db.read(key,key);need(old.hex()==q['_shards'][shard],'cas')
        d=typed('queue-shard',old);rows=[r for r in q['rows'] if (r['ticket']-1)%8==shard]
        if d['rows']!=rows:d['rows']=rows;savecontrol(db,key,'queue-shard',d)
    header.update(lastTicket=q['lastTicket'],count=len(q['rows']));savecontrol(db,hk,'queue',header)

def queue_reserve(db,subject,tenant,plan,now,limit=50000,shard_limit=6400):
    def tx(s):
        q=queue_view(s);old=next((r for r in q['rows'] if r['subject']==subject),None)
        if old:need((old['tenant'],old['plan'])==(tenant,plan),'queue-owner-conflict');return old['ticket']
        need(len(q['rows'])<limit,'AppendPreparationLimit')
        need(q['lastTicket']<MAX,'AppendPreparationLimit');ticket=q['lastTicket']+1;shard=(ticket-1)%8
        rows=[r for r in q['rows'] if (r['ticket']-1)%8==shard]
        need(len(rows)<shard_limit,'AppendPreparationLimit')
        charge=charge_address('tenant',tenant,'wait:'+subject)
        row={'subject':subject,'tenant':tenant,'scope':hashbytes(b'scope'+tenant.encode()),'plan':plan,'ticket':ticket,'firstUtc':now,'updatedUtc':now,'state':'reserved','candidate':None,'amount':None,'attempts':0,'charge':charge,'owner':'owner:'+subject}
        image=typed('queue-shard',hexbytes(q['_shards'][shard]));image['rows']=rows+[row]
        need(len(canonical(image))<=CAPS['queue-shard'],'AppendPreparationLimit')
        reserve_charge(s,charge,'tenant',tenant,16384,locator='wait:'+subject);q['lastTicket']=ticket;q['rows'].append(row);queue_save(s,q);return ticket
    return db.transaction(tx)

def validate_pins(pins,overhead):
    need(type(pins) is list and 1<=len(pins)<=59,'batch-count');total=0
    for p in pins:
        exact(p,'message length');textfield(p['message']);need(0<=integer(p['length'])<=449*MIB,'pin-length');total=add(total,add(p['length'],overhead))
    need(len({p['message'] for p in pins})==len(pins),'batch-duplicate');return total

def operation_plan_fixture(db,subject,pins):
    row=next(r for r in queue_view(db)['rows'] if r['subject']==subject)
    db.external(provider_address('operation-plan',row['owner'],row['plan']),canonical(pins),'fixture-operation')

def queue_materialize(db,subject,pins,now):
    q=queue_view(db);r=next(x for x in q['rows'] if x['subject']==subject)
    need(r['state'] in ('reserved','queued','parked'),'queue-materialize-phase')
    total=validate_pins(pins,ledger_get(db)['overhead']);need(total>0 and now>=r['updatedUtc'],'queue-rerender')
    address=provider_address('operation-plan',r['owner'],r['plan'])
    need(db.read(address,'fixture-operation')==canonical(pins),'rerender-authority')
    r.update(candidate=hashbytes(canonical(pins)),amount=total,updatedUtc=now,state='queued')
    db.transaction(lambda t:queue_save(t,q))

def queue_turn(db,pin_sources):
    initial=queue_view(db)
    def tx(s):
        need(queue_view(s)==initial,'cas');q=queue_view(s);d=ledger_get(s);changed=False
        for r in q['rows']:
            if r['state'] not in ('queued','parked'):continue
            try:
                pins=pin_sources[r['subject']]
                need(s.read(provider_address('operation-plan',r['owner'],r['plan']),'fixture-operation')==canonical(pins),'rerender-authority')
                amount=validate_pins(pins,d['overhead']);candidate=hashbytes(canonical(pins))
                reservation=d['reservations'].get(r['subject'])
                need(reservation is None or (reservation['tenant']==r['tenant'] and reservation['pins']==pins and reservation['binding']==hashbytes(canonical([r['subject'],r['tenant'],pins]))),'batch-conflict')
            except (Refusal,KeyError):
                if r['state']!='parked' or r['amount'] is not None:r.update(state='parked',amount=None,candidate=None);changed=True
                continue
            state='parked' if amount>d['tenantCeiling'] or amount>d['deploymentCeiling']-d['reserve'] else 'queued'
            if (r['candidate'],r['amount'],r['state'])!=(candidate,amount,state):r.update(candidate=candidate,amount=amount,state=state);changed=True
            if state=='parked':continue
            try:
                c=d['charges'].get(r['charge']);need(c is not None and c['account']==account('tenant',r['tenant']) and c['length']==16384 and c['state']=='active' and c['amount']==add(c['length'],c['overhead']),'wait-charge')
                net=copy.deepcopy(d)
                for a in (c['account'],'tenant-pool','deployment'):net['used'][a]=subtract(net['used'][a],c['amount'])
                if add(net['used'].get(c['account'],0),amount)>d['tenantCeiling']:continue
                fits=ledger_fit(net,'tenant',r['tenant'],amount)
            except Refusal:
                r.update(state='parked',amount=None,candidate=None);changed=True;continue
            if not fits:
                if changed:queue_save(s,q)
                return None
            # Stage the complete grant on a disposable transaction image. Any selected-row
            # refusal parks only this subject; no deletion, refund or partial grant survives.
            grant=s.restart();grant.unavailable=set(s.unavailable);trial=queue_view(grant)
            trial['rows']=[v for v in trial['rows'] if v['subject']!=r['subject']]
            try:
                queue_save(grant,trial);refund(grant,r['charge']);batch(grant,r['subject'],r['tenant'],pins)
            except Refusal:
                r.update(state='parked',amount=None,candidate=None);changed=True;continue
            if changed:
                # Include earlier parked/rerendered rows in the successful grant image.
                updated=queue_view(grant);updated['rows']=[v for v in q['rows'] if v['subject']!=r['subject']];queue_save(grant,updated)
            s.rows=grant.rows;return r['ticket']
        if changed:queue_save(s,q)
        return None
    return db.transaction(tx)

def execution_init(db,key='execution',eligibility='retry-exhausted',accepted=None):
    discover(db,key)
    roster=[{'position':i,'message':'event-'+str(i),'digest':hashbytes(('stored-'+str(i)).encode())} for i in (1,2,3)]
    a=accepted if accepted is not None else [1]
    d={'schema':SCHEMAS['execution'],'tenant':'t','execution':key,'scope':ZERO if eligibility=='legacy-publish-failed' else hashbytes(frame('U','t')+frame('U',key)),'revision':1,'phase':'idle','firstUtc':0,'updatedUtc':0,'source':hashbytes(b'exhaustion'),'window':0 if eligibility=='legacy-publish-failed' else 1,'windowClaim':hashbytes(b'original-window'),'closedCount':0,'history':ZERO,'ordinal':0,'limit':8,'drainBase':8,'roster':roster,'accepted':a,'unresolved':[r['position'] for r in roster if r['position'] not in a],'legacy':None,'request':None,'intent':None,'receipts':{},'outcomes':[],'reason':eligibility,'nextUtc':0,'charge':charge_address('tenant','t','old-window:'+key)}
    def tx(t):
        reserve_charge(t,d['charge'],'tenant','t',2*MIB,locator='old-window:'+key);install(t,key,'execution',d);registry_present(t,key)
    db.transaction(tx)
    if eligibility!='legacy-publish-failed':
        db.external(window_address(d),canonical({'tenant':d['tenant'],'scope':d['scope'],'window':d['window'],'admission':d['unresolved']}),key)
    return d

def resume_identity(tenant,handle,idkey):
    textfield(idkey,128);need(all(33<=ord(c)<=126 for c in idkey),'idempotency-key')
    return hashbytes(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+frame('U',tenant)+frame('U',handle)+frame('U',idkey))

def window_address(d,window=None):
    return keyed('HX-EV-PUBLICATION-WINDOW-KEY-1','publication-window:',[['B32',d['scope']],['N',d['window'] if window is None else window]])

def unresolved_root(d):
    members=[r for r in d['roster'] if r['position'] in d['unresolved']]
    return hashbytes(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+frame('P',len(members))+b''.join(frame('P',r['position'])+frame('U',r['message'])+frame('B32',r['digest']) for r in members))

def action_address(d,r,action,payload=None):
    result=response(r['result'])
    if action=='audit':return keyed('HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2','publication-resume-audit:',[['U',d['tenant']],['U',d['execution']],['B32',r['identity']]])
    if action=='closure':return keyed('HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1','publication-window-closure:',[['B32',d['scope']],['N',result['window']-1]])
    if action=='window':return window_address(d,result['window'])
    if action=='invoke':
        root=payload['root'];claim=ZERO if r['eligibility']=='legacy-publish-failed' else d['windowClaim']
        identity=hashbytes(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+frame('B32',claim)+frame('N',result['resumeOrdinal'])+frame('N',result['drainLimit'])+frame('B32',r['identity'])+frame('B32',root))
        return keyed('HX-EV-PUBLICATION-INVOCATION-KEY-1','publication-invocation:',[['U',d['tenant']],['U',d['execution']],['B32',identity]])
    return provider_address(action,d['execution'],r['identity'])

def closure_payload(d,r):
    # Window admission remains immutable while current progress accepts members.
    return {'request':r['identity'],'action':'closure','tenant':d['tenant'],'scope':d['scope'],'window':response(r['result'])['window']-1 if r['result'] else d['window']}

def resume_audit(d,r,ordinal,window,limit):
    closure=None if r['eligibility'] in ('drain-limit','legacy-publish-failed') else hashbytes(canonical({'request':r['identity'],'action':'closure','tenant':d['tenant'],'scope':d['scope'],'window':window-1}))
    return record('HX-EV-PUBLICATION-RESUME-AUDIT-4',[['U',d['tenant']],['U',d['execution']],['N',ordinal],['B32',r['identity']],['B32',hashbytes(hexbytes(r['carrier']))],['B32',r['priorHash']],['O:B32',closure],['N',window],['N',limit],['Q',r['utc']]])

def legacy_authority(db,d,source=None):
    l=d['legacy'];need(l is not None,'legacy_resume_evidence_unavailable')
    key=keyed('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2','legacy-resume-capsule:',[['B32',l['identity']]])
    raw=db.read(key,'legacy');need(raw is not None,'legacy_resume_evidence_unavailable')
    need(hashbytes(raw)==l['capsule'] and (source is None or source==l['capsule']),'legacy-capsule-binding')
    fields,events=capsule_read(db,key)
    need(fields[0]==d['tenant'],'legacy-owner')
    return fields,events

def begin_resume(db,idkey='r1',source=None,reason='repair',now=0,key='execution'):
    d=getcontrol(db,key,'execution');owner_discovered(db,key)
    source=d['source'] if source is None else source
    handle=resume_handle(d,source)
    rid=resume_identity(d['tenant'],handle,idkey)
    carrier=record('HX-EV-PUBLICATION-RESUME-CARRIER-1',[['U',d['tenant']],['U',handle],['B32',source],['U',idkey],['U',textfield(reason,512)]])
    ch=hashbytes(carrier)
    for row in d['outcomes']:
        if row['identity']==rid:
            need(row['carrierHash']==ch,'resume_request_conflict')
            need(now<row['expiry'],'resume_request_expired')
            return bytes.fromhex(row['result'])
    if d['request'] is not None:
        need(d['request']['identity']==rid,'resume_capacity_hold')
        need(hashbytes(bytes.fromhex(d['request']['carrier']))==ch,'resume_request_conflict')
        return None
    need(source==d['source'],'resume_hold_changed')
    need(d['phase']=='idle' and d['intent'] is None,'resume_capacity_hold')
    need(d['reason'] in ELIGIBILITY and d['source']!=ZERO and (d['unresolved'] or d['reason']=='legacy-publish-failed'),'resume_not_eligible')
    if d['reason']=='legacy-publish-failed':
        legacy_authority(db,d,source);need(d['legacy']['state']!='completed','resume_not_eligible')
    need(len(d['outcomes'])<64,'resume_capacity_hold')
    need(d['revision']<=MAX-20 and db.rows[key]['generation']<=MAX-20 and ledger_get(db)['generation']<=MAX-2 and db.rows['ledger']['generation']<=MAX-2,'resume_arithmetic_exhausted')
    ordinal=add(d['ordinal'],1);window=0 if d['reason']=='legacy-publish-failed' else d['window'] if d['reason']=='drain-limit' else add(d['window'],1)
    limit=add(d['limit'],d['drainBase']);need(d['drainBase']>0,'resume_arithmetic_exhausted')
    expiry=now+900*TICK;utc(expiry);utc(expiry+30*DAY)
    claim=record('HX-EV-PUBLICATION-RESUME-3',[['U','admin'],['U',d['tenant']],['U',d['execution']],['B32',d['scope']],['U',d['reason']],['B32',d['source']],['B32',ZERO if d['reason']=='legacy-publish-failed' else hashbytes(b'head')],['N',ordinal],['B32',ZERO],['U',handle],['B32',rid],['B32',ch],['U','operator'],['Q',now],['Q',expiry]])
    request={'identity':rid,'carrier':carrier.hex(),'claim':claim.hex(),'signature':fixture_sign(claim),'utc':now,'expiry':expiry,'eligibility':d['reason'],'source':source,'priorHash':hashbytes(db.read(key,key)),'result':None}
    audit=resume_audit(d,request,ordinal,window,limit)
    result={'resumeHandle':handle,'resumeOrdinal':ordinal,'window':window,'drainLimit':limit,'auditRecordHash':hashbytes(audit)}
    request['result']=canonical(result).hex()
    def tx(s):
        reserve_charge(s,'stage:'+rid,'tenant',d['tenant'],3*MIB)
        x=getcontrol(s,key,'execution');need(canonical(x)==canonical(d),'cas');x.update(request=request,phase='prepared',updatedUtc=now,nextUtc=now,reason='publication_resume_preparation_hold')
        savecontrol(s,key,'execution',x)
    try:db.transaction(tx)
    except Refusal as e:
        if str(e)=='capacity':raise Refusal('resume_capacity_hold') from e
        raise
    return None


def resume_claim_binding(d,r):
    raw=bytes.fromhex(r['claim']);signed(raw,r['signature'])
    f=[v for _,v in read_record(raw,ANSWERS['records']['D45-request'])]
    carrier=bytes.fromhex(r['carrier']);c=[v for _,v in read_record(carrier,ANSWERS['records']['D45-carrier'])]
    result=decode(bytes.fromhex(r['result']))
    need(f[0]=='admin' and f[12]=='operator' and f[1:4]==[d['tenant'],d['execution'],d['scope']],'resume-claim-owner')
    need(f[4]==r['eligibility'] and f[5]==r['source'] and f[7]==result['resumeOrdinal'] and f[10]==r['identity'] and f[11]==hashbytes(carrier),'resume-claim-request')
    need(f[13:15]==[r['utc'],r['expiry']],'resume-claim-time')
    need(c[0]==d['tenant'] and c[1]==f[9] and c[2]==r['source'] and resume_identity(c[0],c[1],c[3])==r['identity'],'resume-carrier-owner')
    if r['eligibility']=='legacy-publish-failed':need(f[3]==ZERO and f[6]==ZERO,'legacy-no-A8')
    return f

def held_claim_binding(d,key,request):
    raw=bytes.fromhex(request['claim']);signed(raw,request['signature'])
    f=[v for _,v in read_record(raw,ANSWERS['records']['D36-redrive'])]
    need(f[:3]==['admin',d['scopeKind'],d['tenant']] and f[3]==held_identity(d) and f[5]=='operator','redrive-claim-owner')
    need(f[4]==request['expectedCount'] and add(f[4],1)==d['redrives'],'redrive-claim-count')
    need(f[6]==request['utc'] and f[6]>=d['firstUtc'],'redrive-claim-time')
    return raw

NEXT={'prepared':'disable','disable':'reject','reject':'closure','closure':'window','window':'audit','audit':'successor','successor':'finalize','finalize':'invoke','invoke':'idle'}

def resume_step(db,now,key='execution',crash=None):
    utc(now)
    d=getcontrol(db,key,'execution');r=d['request'];need(r is not None,'resume-absent')
    resume_claim_binding(d,r);owner_discovered(db,key)
    if r['eligibility']=='legacy-publish-failed':legacy_authority(db,d,r['source'])
    phase=d['phase'];need(phase in NEXT,'resume-transition')
    if phase=='prepared' and now>=r['expiry']:
        irreversible=any(db.read(action_address(d,r,a),key) is not None for a in ('disable','reject','audit','successor'))
        if not irreversible:cancel_resume(db,key);return None
    action='audit' if phase=='prepared' and r['eligibility'] in ('drain-limit','legacy-publish-failed') else NEXT[phase]
    result=response(r['result'])
    if action=='idle':
        d.update(phase='idle',request=None,intent=None,receipts={},reason=None)
        savecontrol(db,key,'execution',d);return hexbytes(r['result'])
    if d['intent'] is not None:
        intent=d['intent'];need(intent['kind']==action,'intent-binding');external_bytes=hexbytes(intent['payload']['bytes']);address=intent['address']
        need(hashbytes(external_bytes)==intent['hash'],'intent-binding')
    else:
        payload={'request':r['identity'],'action':action}
        if action=='closure':payload=closure_payload(d,r)
        elif action=='window':payload.update(tenant=d['tenant'],scope=d['scope'],window=result['window'],admission=d['unresolved'])
        elif action=='invoke':
            if r['eligibility']=='legacy-publish-failed':
                fields,events=legacy_authority(db,d,r['source']);payload.update(root=fields[11],range=[fields[8],fields[9]],capsule=d['legacy']['capsule'],classification=fields[7])
            else:payload.update(root=unresolved_root(d),unresolved=d['unresolved'])
        address=action_address(d,r,action,payload)
        external_bytes=resume_audit(d,r,result['resumeOrdinal'],result['window'],result['drainLimit']) if action=='audit' else canonical(payload)
        need(action!='audit' or hashbytes(external_bytes)==result['auditRecordHash'],'audit-result-hash')
        d['intent']={'kind':action,'address':address,'hash':hashbytes(external_bytes),'payload':{'bytes':external_bytes.hex()}}
        d=savecontrol(db,key,'execution',d)
        if crash=='intent':raise Crash(action)
    admitted_token=(db.rows[key]['generation'],db.rows[key]['fence'])
    db.external(address,external_bytes,key)
    if crash=='effect':raise Crash(action)
    need(hashbytes(db.read(address,key))==d['intent']['hash'],'effect-readback')
    def advance(s):
        need((s.rows[key]['generation'],s.rows[key]['fence'])==admitted_token,'cas')
        x=getcontrol(s,key,'execution');need(canonical(x)==canonical(d),'cas');x['receipts'][action]=address
        if action=='successor':
            oldwindow=window_address(x);oldcharge=x['charge']
            x['ordinal']=result['resumeOrdinal'];x['window']=result['window'];x['limit']=result['drainLimit'];x['source']=ZERO
            if r['eligibility'] not in ('drain-limit','legacy-publish-failed'):
                x['closedCount']=add(x['closedCount'],1);x['windowClaim']=hashbytes(s.read(window_address(x),key));x['history']=hashbytes(canonical([x['history'],s.read(action_address(d,r,'closure'),key).hex()]))
            retained=[a for a in x['receipts'].values() if a!=address]
            if r['eligibility'] not in ('drain-limit','legacy-publish-failed'):retained.append(oldwindow)
            row={'identity':r['identity'],'carrierHash':hashbytes(hexbytes(r['carrier'])),'ordinal':x['ordinal'],'window':x['window'],'limit':x['limit'],'audit':result['auditRecordHash'],'expiry':r['expiry'],'deleteAfter':r['expiry']+30*DAY,'result':r['result'],'oldCharge':oldcharge,'artifacts':sorted(set(retained))}
            if not any(v['identity']==r['identity'] for v in x['outcomes']):x['outcomes'].append(row)
        if action=='finalize':x['charge']=charge_address('tenant',x['tenant'],'stage:'+r['identity'])
        if action=='invoke':
            row=next(v for v in x['outcomes'] if v['identity']==r['identity'])
            row['artifacts']=sorted(set(row['artifacts']+[address]+[v for a,v in x['receipts'].items() if a in ('successor','finalize')]))
        x['phase']=action;x['intent']=None;savecontrol(s,key,'execution',x)
    db.transaction(advance)
    if crash=='advance':raise Crash(action)
    return None

def finish_resume(db,now,key='execution'):
    utc(now)
    for _ in range(12):
        d=getcontrol(db,key,'execution')
        if d['request'] is None:return
        resume_step(db,now,key)
    raise AssertionError('bounded-resume-completion')

def invocation_messages(db,d,payload):
    if 'capsule' in payload:
        fields,events=legacy_authority(db,d,payload['capsule'])
        need(payload['root']==fields[11] and payload['range']==[fields[8],fields[9]] and payload['classification']==fields[7],'legacy-invocation-binding')
        return [r['message'] for r in events]
    members=[r for r in d['roster'] if r['position'] in payload['unresolved']]
    admission=copy.deepcopy(d);admission['unresolved']=payload['unresolved']
    need(unresolved_root(admission)==payload['root'],'invocation-admission')
    return [r['message'] for r in members if r['position'] not in d['accepted']]

def cancel_resume(db,key='execution'):
    d=getcontrol(db,key,'execution');r=d['request'];need(r is not None,'no-preparation')
    need(d['phase']=='prepared' and not any(db.read(action_address(d,r,a),key) is not None for a in ('disable','reject','audit','successor')),'irreversible')
    def tx(s):
        x=getcontrol(s,key,'execution');need(canonical(x)==canonical(d),'cas')
        if d['intent'] is not None:s.delete(d['intent']['address'],key);need(s.read(d['intent']['address'],key) is None,'delete-readback')
        refund(s,'stage:'+r['identity'])
        x.update(request=None,intent=None,phase='idle',reason=r['eligibility']);savecontrol(s,key,'execution',x)
    db.transaction(tx)

def reclaim(db,now,key='execution'):
    d=getcontrol(db,key,'execution');need(now>=d['updatedUtc'],'utc-regression')
    pending=None if d['request'] is None else d['request']['identity']
    expired=[r for r in d['outcomes'] if now>=r['deleteAfter'] and r['identity']!=pending]
    if not expired:return
    def tx(s):
        x=getcontrol(s,key,'execution');need(canonical(x)==canonical(d),'cas')
        active=window_address(x)
        for r in expired:
            for address in r['artifacts']:
                if address!=active:s.delete(address,key);need(s.read(address,key) is None,'delete-readback')
            refund(s,r['oldCharge'])
        x['outcomes']=[r for r in x['outcomes'] if r not in expired];x['updatedUtc']=now;savecontrol(s,key,'execution',x)
    db.transaction(tx)

def held_init(db,carrier=b'exact carrier',key='held',now=0,kind='tenant',scope='t'):
    discover(db,key,scope=scope,kind=kind,now=now,owner='operations')
    d={'schema':SCHEMAS['held'],'scopeKind':kind,'scopeId':scope,'deployment':'dep' if kind=='tenant' else scope,'tenant':scope if kind=='tenant' else None,'component':'pubsub','topic':'orders','subscription':'sub','policy':hashbytes(b'policy'),'revision':1,'phase':'observed','reason':'handler-capability-hold','firstUtc':now,'updatedUtc':now,'observations':1,'length':len(carrier),'carrier':hashbytes(carrier),'locator':None,'objectReceipt':None,'charge':charge_address('tenant' if kind=='tenant' else 'capture-scope',scope,'object:'+key),'request':None,'attempt':None,'redrives':0,'nextUtc':now,'repair':'none','error':None,'intent':None,'receipts':{}}
    old=db.read(key,key)
    if old is not None:
        prior=getcontrol(db,key,'held');need(prior['carrier']==d['carrier'] and prior['length']==d['length'] and prior['firstUtc']==now,'held-init-conflict');metadata_charge(db,prior,key);return prior
    def tx(s):
        reserve_charge(s,'metadata:'+key,*held_account(d),136*1024,objects=(key,))
        install(s,key,'held',d);registry_present(s,key)
    db.transaction(tx);return d

def observe(db,now,key='held'):
    d=getcontrol(db,key,'held');need(now>=d['updatedUtc'],'observation-clock');d['observations']=add(d['observations'],1);d['updatedUtc']=now;savecontrol(db,key,'held',d)

def metadata_charge(db,d,key):
    c=ledger_get(db)['charges'].get(charge_address(*held_account(d),'metadata:'+key))
    need(c is not None and c['account']==account(*held_account(d)) and c['length']==136*1024 and c['state']=='active' and c['amount']==add(c['length'],c['overhead']),'metadata-charge')

def capture(db,carrier,key='held',crash=None):
    d=getcontrol(db,key,'held');owner_discovered(db,key);metadata_charge(db,d,key)
    need(capture_interval(len(carrier))=='ordinary','capture-carrier')
    need(hashbytes(carrier)==d['carrier'] and len(carrier)==d['length'],'capture-carrier')
    if d['phase']=='captured':verify_retained(db,d,key);return True
    need(d['phase'] in ('observed','capturing'),'capture-transition');address=held_object(d)
    def admit(s):
        reserve_charge(s,d['charge'],*held_account(d),len(carrier),objects=(held_object(d),),locator='object:'+key)
        x=getcontrol(s,key,'held');need(canonical(x)==canonical(d),'cas')
        if d['phase']=='observed':
            x.update(phase='capturing',intent={'kind':'capture','address':address,'hash':d['carrier'],'payload':{'length':d['length'],'carrier':d['carrier']}});savecontrol(s,key,'held',x)
    db.transaction(admit)
    admitted=db.read(key,key);fence=db.rows[key]['fence'];generation=db.rows[key]['generation']
    if crash=='charge':raise Crash('capture-charge')
    receipt=db.external(address,carrier,key)
    if crash=='object':raise Crash('capture-object')
    current=db.read(key,key)
    if current is None or current!=admitted or db.rows[key]['fence']!=fence or db.rows[key]['generation']!=generation:
        replacement=None if current is None else typed('held',current)
        owned=False
        if replacement is not None and replacement['carrier']==d['carrier'] and replacement['length']==d['length']:
            if replacement['locator'] is not None and replacement['locator']['key']==address:
                verify_retained(db,replacement,key);owned=True
                completed=typed('held',admitted)
                completed.update(phase='captured',locator={'backend':'held-delivery-store','key':address},objectReceipt=receipt,intent=None,revision=add(completed['revision'],1))
                if replacement['phase']=='captured' and db.rows[key]['fence']==fence and db.rows[key]['generation']==generation+1 and current==canonical(completed):return True
            elif replacement['phase']=='capturing' and replacement['intent']['address']==address:
                c=ledger_get(db)['charges'].get(replacement['charge'])
                need(c is not None and c['account']==account(*held_account(replacement)) and c['length']==replacement['length'],'retained-charge')
                owner_discovered(db,key);owned=True
        if not owned:
            db.delete(address,key);need(db.read(address,key) is None,'object-deletion-readback')
        raise Refusal('capture-predecessor')
    prior=typed('held',current);need(prior['phase']=='capturing' and prior['intent']['address']==address and prior['carrier']==d['carrier'],'capture-predecessor')
    prior.update(phase='captured',locator={'backend':'held-delivery-store','key':address},objectReceipt=receipt,intent=None)
    savecontrol(db,key,'held',prior)
    if crash=='control':raise Crash('capture-control')
    return True

def verify_retained(db,d,key):
    metadata_charge(db,d,key)
    need(capture_interval(d['length'])=='ordinary','retained-interval')
    need(d['locator'] is not None,'locator')
    b=db.read(d['locator']['key'],key);need(b is not None and hashbytes(b)==d['carrier'] and len(b)==d['length'],'retained-bytes')
    c=ledger_get(db)['charges'].get(d['charge']);need(c is not None and c['account']==account(*held_account(d)) and c['length']==d['length'],'retained-charge')
    need(db.read(d['locator']['key'],key) is not None and d['objectReceipt']==hashbytes(b),'object-receipt');owner_discovered(db,key)
    return b

def redrive(db,key='held',now=0,expected=None,crash=None):
    d=getcontrol(db,key,'held');verify_retained(db,d,key)
    need(d['phase']=='captured' and d['repair'] in ('none','repaired'),'redrive-phase')
    need(now>=d['firstUtc'] and now>=d['nextUtc'],'redrive-time')
    count=d['redrives'] if expected is None else expected;need(count==d['redrives'],'held_redrive_count_changed')
    claim=record('HX-EV-REDRIVE-REQUEST-2',[['U','admin'],['U',d['scopeKind']],['O:U',d['tenant']],['B32',held_identity(d)],['N',count],['U','operator'],['Q',now]])
    n=add(count,1)
    old_route=db.read(provider_address('route-terminal',key),key)
    if old_route is not None:need(route_authority(db,d,key)!='terminal','terminal-no-send')
    d.update(redrives=n,phase='redriving',repair='none',request={'claim':claim.hex(),'signature':fixture_sign(claim),'expectedCount':count,'utc':now},attempt={'count':n,'requestHash':hashbytes(claim),'carrier':d['carrier'],'utc':now,'result':None},intent={'kind':'send','address':provider_address('delivery',key),'hash':d['carrier'],'payload':{'count':n,'carrier':d['carrier']}},updatedUtc=now)
    def commit(t):
        t.delete(provider_address('route-terminal',key),key);savecontrol(t,key,'held',d)
    db.transaction(commit)
    if crash=='commit':raise Crash('redrive-commit')
    return send_held(db,key,crash)

def send_held(db,key='held',crash=None):
    d=getcontrol(db,key,'held');b=verify_retained(db,d,key)
    need(d['phase']=='redriving' and d['request'] is not None and d['attempt'] is not None,'send-attempt')
    claim=held_claim_binding(d,key,d['request'])
    need(d['attempt']['count']==d['redrives'] and d['attempt']['requestHash']==hashbytes(claim),'send-binding')
    need(route_authority(db,d,key)!='terminal','terminal-no-send')
    db.delete(d['intent']['address'],key);db.external(d['intent']['address'],b,key)
    if crash=='effect':raise Crash('redrive-effect')
    return d['redrives']

def route_authority(db,d,key):
    raw=db.read(provider_address('route-terminal',key),key)
    if raw is None:return 'unknown'
    a=decode(raw);exact(a,'count requestHash carrier outcome')
    need(a['count']==d['redrives'] and a['carrier']==d['carrier'] and d['request'] is not None and a['requestHash']==hashbytes(bytes.fromhex(d['request']['claim'])) and a['outcome'] in ('terminal','nonterminal','unknown'),'route-authority')
    return a['outcome']

def held_account(d):
    return ('tenant',d['tenant']) if d['scopeKind']=='tenant' else ('capture-scope',d['scopeId'])

def held_identity(d):
    return keyed('HX-EV-HELD-DELIVERY-KEY-2','',[['U',d['scopeKind']],['U',d['deployment']],['O:U',d['tenant']],['U',d['component']],['U',d['topic']],['U',d['subscription']],['B32',d['carrier']]])

def held_object(d):
    return 'held/'+held_identity(d)

def cleanup_intent(d,key,kind):
    payload={'carrier':d['carrier'],'count':d['redrives'],'delivered':kind=='delivered-cleanup'}
    return {'kind':kind,'address':held_object(d),'hash':hashbytes(canonical(payload)),'payload':payload}

def redrive_reconcile(db,key='held',now=0):
    raw=db.read(key,key);d=decode(raw);repair='none';disputed=canonical([d.get('request'),d.get('attempt')])
    try:typed('held',raw)
    except Refusal:
        d.update(request=None,repair='corrupt')
        try:typed('held',canonical(d))
        except Refusal:d['attempt']=None;typed('held',canonical(d))
        repair='corrupt'
    need(d['phase'] in ('observed','redriving'),'reconcile-phase')
    if d['phase']=='observed':
        a=db.read(provider_address('original-route',key),key)
        need(a==canonical({'carrier':d['carrier'],'outcome':'terminal'}),'route-terminal-readback')
        outcome='terminal'
    else:
        if d['request'] is None or d['attempt'] is None:repair='absent' if repair=='none' else repair
        else:
            try:
                held_claim_binding(d,key,d['request']);need(d['attempt']['count']==d['redrives'] and d['attempt']['requestHash']==hashbytes(hexbytes(d['request']['claim'])),'attempt-count')
            except Refusal:repair='corrupt'
        # Corrupt/absent pair cannot authorize completion; source availability still reads back.
        if repair=='none':outcome=route_authority(db,d,key)
        else:db.read(provider_address('route-terminal',key),key);outcome='unknown'
    need(now>=d['updatedUtc'],'utc-regression');terminal=outcome=='terminal'
    d.update(phase='cleanup' if terminal else 'captured',repair=repair,nextUtc=now+(900 if repair!='none' else backoff(d['redrives']))*TICK,error=None if terminal else {'reason':'evidence' if repair!='none' else 'nonterminal','hash':hashbytes(disputed),'utc':now},updatedUtc=now,intent=cleanup_intent(d,key,'delivered-cleanup') if terminal else None)
    d['revision']=add(d['revision'],1);typed('held',canonical(d));db.write(key,canonical(d),key,raw)

def backoff(n):return min(900,60*2**min(max(n-1,0),4))

def repair_held(db,key='held',claim=None):
    d=getcontrol(db,key,'held');need(d['repair'] in ('absent','corrupt','required'),'repair-required');verify_retained(db,d,key)
    need(claim is not None,'repair-authority')
    held_claim_binding(d,key,claim)
    need(claim['expectedCount']+1==d['redrives'],'repair-count')
    raw=db.read(provider_address('route-terminal',key),key)
    source=decode(raw) if raw is not None else d['attempt']
    need(type(source) is dict and source.get('requestHash')==hashbytes(hexbytes(claim['claim'])) and source.get('count')==d['redrives'] and source.get('carrier')==d['carrier'],'repair-original-request')
    d['request']=claim;d['attempt']={'count':d['redrives'],'requestHash':hashbytes(bytes.fromhex(claim['claim'])),'carrier':d['carrier'],'utc':claim['utc'],'result':None};d['repair']='repaired';savecontrol(db,key,'held',d)

def cleanup_held(db,key='held',crash=None):
    d=getcontrol(db,key,'held');need(d['phase']=='cleanup' and d['intent'] is not None,'cleanup-intent')
    kind=d['intent']['kind'];need(kind in ('erase','delivered-cleanup'),'cleanup-kind')
    # Authority is retained by the owner intent, including after terminal source deletion.
    if kind=='erase':authority(db,key,'erase')
    db.delete(held_object(d),key)
    if crash=='object':raise Crash('cleanup-object')
    need(db.read(held_object(d),key) is None,'object-deletion-readback')
    for address,row in list(db.rows.items()):
        if row['owner']==key and address!=key:
            db.delete(address,key);need(db.read(address,key) is None,'provider-deletion-readback')
            if crash=='source':raise Crash('cleanup-source')
    def tx(s):
        need(s.read(key,key)==canonical(d),'cas')
        s.delete(key,key);refund(s,d['charge']);refund(s,'metadata:'+key);registry_release(s,key)
    db.transaction(tx)

def delivered_cleanup(db,key='held',crash=None):
    d=getcontrol(db,key,'held');need(d['intent'] is not None and d['intent']['kind']=='delivered-cleanup','delivered-cleanup-intent');cleanup_held(db,key,crash)

def erase_held(db,key='held',crash=None):
    d=getcontrol(db,key,'held');authority(db,key,'erase')
    if d['phase']!='cleanup' or d['intent']['kind']!='erase':
        d.update(phase='cleanup',intent=cleanup_intent(d,key,'erase'));savecontrol(db,key,'held',d)
    cleanup_held(db,key,crash)

# Policy functions are deliberately small and checked by concrete behavior cases.
def status(state='pending',conflict=False,unavailable=False,preparation=False,terminal=False,classification='success',drain=False,classes=(),maximum=False,automatic=False):
    if conflict or unavailable or preparation:return ('CommandOutcomeHold','outcome_evidence_conflict' if conflict else 'response_preparation_hold' if preparation else 'outcome_evidence_hold',30)
    if terminal:return ('PublishFailed','publication_terminal_failed',None)
    if state=='published':return ('Completed' if classification=='success' else 'Rejected',None,None) if classification in ('success','rejection') else ('CommandOutcomeHold','outcome_evidence_conflict',30)
    if state=='not-applicable':return ('Completed',None,None)
    if drain:return ('EventsStored','publication_drain_limit_hold',60)
    c=set(classes)
    if state=='failed' and c&{'class-02','class-03'}:return ('CommandOutcomeHold','terminal_evidence_hold',30)
    if state=='failed' and c=={'class-01'} and maximum:return ('EventsStored','publication_retry_exhausted_hold',60)
    if state=='failed' and c=={'class-01'} and automatic:return ('EventsStored','publication_retry_pending',1)
    if state in ('pending','unknown'):return ('EventsStored',None,1)
    return ('CommandOutcomeHold','outcome_evidence_conflict',30)

def replay(count,readable,incremental=False,activation=True):
    accounting=integer(count)*8192;need(accounting<=MAX,'replay-overflow');integer(readable)
    bounds=(100000,64*MIB,256*MIB);values=(count,readable,accounting)
    if incremental:return 'incremental-bootstrap'
    if activation:return 'continue-full-replay' if all(v*4<b*3 for v,b in zip(values,bounds)) else 'hold'
    return 'full-replay' if all(v<=b for v,b in zip(values,bounds)) else 'LegacyArrayLimit'


def broker_accept(window,member,claim,closed_windows,terminal=False,duplicate=False):
    # Terminal operation fence precedes duplicates; a closed-window fence is permanent.
    need(not terminal,'operation-terminal-fence')
    need(window not in closed_windows,'window-fence')
    need(claim is not None and claim['window']==window and member in claim['unresolved'],'window-admission')
    return 'duplicate' if duplicate else 'accepted'

def membership(old,new,zero,fresh,sent=False):
    need(fresh and zero in ('EmptyNamespace','InitialRowOnly') and not sent,'zero-send-proof')
    return 'ContinueSamePin' if old==new else 'FirstSendMembershipChangedHold'

def destination(raw,component,topic):
    need(len(raw)<=65536,'destination-bound');d=decode(raw);exact(d,'component metadata schema topic')
    need(d['schema']=='hexalith.eventstore.destination/1','destination-schema')
    need(textfield(d['component'])==component and textfield(d['topic'])==topic,'destination-outbox')
    need(type(d['metadata']) is dict and len(d['metadata'])<=64,'metadata-count')
    total=0
    for k,v in d['metadata'].items():
        need(type(k) is str and type(v) is str and len(k.encode())<=16384 and len(v.encode())<=16384,'metadata-field');total+=len(k.encode())+len(v.encode())
    need(total<=16384,'metadata-total');return d

def resume_handle(d,source=None):
    return 'hxrsm1-'+hashbytes(frame('U',d['tenant'])+frame('U',d['execution'])+frame('B32',d['source'] if source is None else source))

def inventory_actor(kind,scope):
    need(kind in ('tenant','deployment'),'inventory-scope');return kind+':'+hashbytes(frame('U',scope))

def resolve_handle(db,tenant,handle):
    matches=[]
    for row in registry(db,'tenant',tenant)['rows']:
        if row['state']!='incident':
            raw=db.read(row['address'],row['address'])
            if raw is not None and type(decode(raw)) is dict and decode(raw).get('schema')==SCHEMAS['execution']:
                d=typed('execution',raw)
                sources={d['source']}|{read_record(hexbytes(d['request']['carrier']),ANSWERS['records']['D45-carrier'])[2][1]} if d['request'] else {d['source']}
                # Retained retry responses keep previously exposed handles resolvable.
                handles={resume_handle(d,source) for source in sources}|{response(r['result'])['resumeHandle'] for r in d['outcomes']}
                if handle in handles:matches.append(row['address'])
    need(len(matches)==1,'resume_hold_changed');return matches[0]

def cursor_sign(payload):
    return hashbytes(b'fixture-inventory-cursor-only\0'+canonical(payload))

def inventory_page(db,kind,scope,size=50,cursor=None,now=0):
    need(type(size) is int and 1<=size<=200,'page-size');need(kind in ('tenant','deployment'),'hold_inventory_cursor_scope_mismatch');textfield(scope)
    rows=registry(db,kind,scope)['rows'];header_key=registry_scope_key(kind,scope);raw=db.read(header_key,header_key)
    header=None if raw is None else typed('registry-scope',raw)
    generations=[[r['subject'],db.rows[registry_key(kind,scope,r['subject'])]['generation']] for r in rows]
    generation=hashbytes(canonical([None if header is None else header['generation'],generations]));start=0
    expiry=now+900*TICK
    if cursor is not None:
        try:
            need(len(canonical(cursor))<=16384,'cursor-envelope-bound');exact(cursor,'schema keyId payload signature')
            need(cursor['schema']=='hexalith.eventstore.hold-cursor-envelope/1' and cursor['keyId']=='fixture','hold_inventory_cursor_invalid')
            d=typed('cursor',canonical(cursor['payload']));need(cursor['signature']==cursor_sign(d),'hold_inventory_cursor_invalid')
        except Refusal as e:raise Refusal('hold_inventory_cursor_invalid') from e
        need((d['scopeKind'],d['scopeId'])==(kind,scope),'hold_inventory_cursor_scope_mismatch');need(now<d['expiry'],'hold_inventory_cursor_expired');need(d['generation']==generation,'hold_inventory_generation_changed');expiry=d['expiry']
        if d['last'] is not None:
            positions=[i for i,r in enumerate(rows) if list(registry_sort(r)[:1])+[r['scopeKind'],r['scopeId'],r['subject']]==d['last']]
            need(len(positions)==1,'hold_inventory_cursor_invalid');start=positions[0]+1
    views=[]
    for r in rows[start:start+size]:
        view={'subject':r['subject'],'owner':r['owner'],'reason':'placeholder','phase':'reserved','firstUtc':r['firstUtc'],'revision':None,'stale':False,'resumeHandle':None}
        try:
            need(r['state']!='incident','registry-entry-incident');raw=db.read(r['address'],r['address'])
            if raw is not None:
                decoded=decode(raw);need(type(decoded) is dict,'control-shape')
                owner_kind=next((k for k in ('execution','held') if decoded.get('schema')==SCHEMAS[k]),None);need(owner_kind is not None,'owner-schema')
                v=typed(owner_kind,raw);view.update(reason=v['reason'],phase=v['phase'],revision=v['revision'],updatedUtc=v['updatedUtc'],count=v.get('observations'))
                if owner_kind=='execution' and v['reason'] in ELIGIBILITY:view['resumeHandle']=resume_handle(v)
        except Refusal:view.update(reason='outcome_evidence_hold',phase='incident',stale=True)
        views.append(view)
    end=start+len(views);last=None if not views else [rows[end-1]['firstUtc'],kind,scope,rows[end-1]['subject']]
    d={'schema':SCHEMAS['cursor'],'scopeKind':kind,'scopeId':scope,'generation':generation,'last':last,'expiry':expiry}
    envelope={'schema':'hexalith.eventstore.hold-cursor-envelope/1','keyId':'fixture','payload':d,'signature':cursor_sign(d)}
    need(len(canonical(envelope))<=16384,'hold_inventory_cursor_invalid');return views,envelope if end<len(rows) else None

# No provider or production crypto claim follows from these scenario assertions.
def refused(fn, why=None):
    try:fn()
    except Refusal as e:
        if why is not None:assert str(e)==why,(str(e),why)
        COUNTS['refusals']=COUNTS.get('refusals',0)+1
        return
    raise AssertionError('expected owning refusal: '+str(why))

def unchanged(db,fn,why=None):
    b=db.snapshot();refused(fn,why);assert db.snapshot()==b,'refusal changed persisted state'

def newdb():
    db=Store();bootstrap(db);ledger_init(db,64*MIB,160*MIB,32*MIB,64*MIB);return db

def drain_fixture(events,classification):
    return canonical({'schema':'fixture-unpublished-events/1','tenant':'t','domain':'d','aggregate':'a','tracking':'tracking','execution':'op','correlation':'correlation','commandType':'increment','events':events,'isRejection':classification=='rejection-events'})

def capsule_cleanup(db,key,crash=None):
    d=getcontrol(db,key,'execution');owner_discovered(db,key)
    need(d['phase']=='cleanup' and d['intent'] is not None and d['intent']['kind']=='cleanup','capsule-cleanup-intent')
    fields,events=legacy_authority(db,d,d['intent']['payload'].get('capsule'))
    need(d['source']==d['legacy']['capsule'] and d['intent']=={'kind':'cleanup','address':'drain','hash':fields[14],'payload':{'capsule':d['legacy']['capsule']}},'capsule-cleanup-intent')
    source=db.read(d['intent']['address'],'legacy')
    if source is not None:
        expected=canonical(dict(zip(('schema','tenant','domain','aggregate','tracking','execution','correlation','commandType','events','isRejection'),('fixture-unpublished-events/1',*fields[:7],events,fields[7]=='rejection-events'))))
        need(hashbytes(source)==d['intent']['hash'] and source==expected,'drain-cleanup-source')
    if crash=='cleanup-intent':raise Crash('capsule-cleanup-intent')
    db.delete(d['intent']['address'],'legacy');need(db.read(d['intent']['address'],'legacy') is None,'drain-deletion-readback')
    if crash=='drain':raise Crash('capsule-cleanup')
    d.update(phase='idle',intent=None);savecontrol(db,key,'execution',d)

def capsule_make(db,events,classification='success-events',key='execution',cleanup=False,crash=None):
    existing=db.read(key,key)
    if existing is not None:
        d=typed('execution',existing)
        need(d['request'] is None,'capsule-request-pending')
        if d['legacy'] is not None:
            f,original_events=legacy_authority(db,d)
            need(original_events==events and f[7]==classification,'legacy-drain-authority')
            if d['phase']!='cleanup':return db.read(keyed('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2','legacy-resume-capsule:',[['B32',d['legacy']['identity']]]),'legacy')
        if d['phase']=='cleanup' and d['intent'] is not None and d['intent']['kind']=='cleanup':
            capsule_cleanup(db,key,crash)
            return db.read(keyed('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2','legacy-resume-capsule:',[['B32',d['legacy']['identity']]]),'legacy')
    source=db.read('drain','legacy');need(source is not None,'legacy_resume_evidence_unavailable')
    original=decode(source);exact(original,'schema tenant domain aggregate tracking execution correlation commandType events isRejection')
    need(original['schema']=='fixture-unpublished-events/1' and original['events']==events and type(original['isRejection']) is bool and classification==('rejection-events' if original['isRejection'] else 'success-events'),'legacy-drain-authority')
    need([original[x] for x in ('tenant','domain','aggregate','tracking')]==['t','d','a','tracking'],'legacy-drain-scope')
    source_hash=hashbytes(source)
    need(classification in ('success-events','rejection-events'),'rejection-classification')
    need(1<=len(events)<=1000,'legacy-range-bound')
    need([r['sequence'] for r in events]==list(range(events[0]['sequence'],events[0]['sequence']+len(events))),'legacy-sequence')
    need(len({r['message'] for r in events})==len(events),'legacy-message-unique')
    cid=hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01'+frame('U','t')+frame('U','d')+frame('U','a')+frame('U','tracking')+bytes.fromhex(source_hash))
    manifest_key=keyed('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2','legacy-resume-capsule:',[['B32',cid]])
    if db.read(key,key) is None:execution_init(db,key,eligibility='legacy-publish-failed')
    owner_discovered(db,key);d=getcontrol(db,key,'execution')
    if d['intent'] is not None:need(d['intent']['kind']=='capsule' and d['intent']['payload']['sourceHash']==source_hash,'capsule-intent-binding')
    elif d['phase']!='cleanup':d.update(phase='cleanup');savecontrol(db,key,'execution',d)
    member_bytes=b''.join(frame('N',r['sequence'])+frame('U',r['message'])+bytes.fromhex(r['digest']) for r in events)
    descriptors=[]
    for ordinal,offset in enumerate(range(0,len(events),61)):
        part=events[offset:offset+61]
        rows=b''.join(frame('N',r['sequence'])+frame('U',r['message'])+bytes.fromhex(r['digest']) for r in part)
        root=hashbytes(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+frame('B',rows.hex()))
        chunk=record('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',[['B32',cid],['N',ordinal],['N',len(part)],['B',rows.hex()],['B32',root]])
        address='legacy-resume-capsule-chunk:'+hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0\x01'+bytes.fromhex(cid)+frame('N',ordinal))
        d=getcontrol(db,key,'execution')
        if d['intent'] is not None and d['intent']['address']!=address:
            need(db.read(address,'legacy')==chunk and charge_address('tenant','t',address) in ledger_get(db)['charges'],'capsule-prior-chunk')
        else:
            if d['intent'] is None:
                d['intent']={'kind':'capsule','address':address,'hash':hashbytes(chunk),'payload':{'sourceHash':source_hash,'capsule':cid,'ordinal':ordinal}};savecontrol(db,key,'execution',d)
            need(d['intent']['hash']==hashbytes(chunk),'capsule-intent-binding')
        reserve_charge(db,address,'tenant','t',len(chunk),objects=(address,));
        if crash=='charge':raise Crash('capsule-charge')
        db.external(address,chunk,'legacy')
        if crash=='chunk':raise Crash('capsule-chunk')
        d=getcontrol(db,key,'execution')
        if d['intent'] is not None and d['intent']['address']==address:
            # Legacy provider owns capsule artifacts; explicit readback consumes this owner intent.
            need(db.read(address,'legacy')==chunk,'capsule-chunk-readback');prior=canonical(d);d['intent']=None
            d['revision']=add(d['revision'],1);typed('execution',canonical(d));db.write(key,canonical(d),key,prior)
        descriptors.append(frame('N',ordinal)+frame('N',part[0]['sequence'])+frame('N',len(part))+bytes.fromhex(hashbytes(chunk))+frame('N',len(chunk))+frame('U',address))
    manifest=frame('P',len(descriptors))+b''.join(descriptors)
    root=hashbytes(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'+frame('B',(frame('P',len(events))+member_bytes).hex()))
    fields=[['U','t'],['U','d'],['U','a'],['U','tracking'],['O:U',original['execution']],['U',original['correlation']],['U',original['commandType']],['U',classification],['N',events[0]['sequence']],['N',events[-1]['sequence']],['I',len(events)],['B32',root],['B',manifest.hex()],['U','drain-exhaustion'],['B32',source_hash],['Q',0]]
    b=record('HX-EV-LEGACY-RESUME-CAPSULE-2',fields);need(len(b)<=128*1024,'capsule-bound')
    d=getcontrol(db,key,'execution')
    if d['intent'] is None:d['intent']={'kind':'capsule','address':manifest_key,'hash':hashbytes(b),'payload':{'sourceHash':source_hash,'capsule':cid}};savecontrol(db,key,'execution',d)
    need(d['intent']['address']==manifest_key and d['intent']['hash']==hashbytes(b),'capsule-intent-binding')
    reserve_charge(db,manifest_key,'tenant','t',len(b),objects=(manifest_key,));db.external(manifest_key,b,'legacy')
    need(db.read(manifest_key,'legacy')==b,'capsule-readback');capsule_restore(db,events,manifest_key)
    d=getcontrol(db,key,'execution');d.update(phase='idle',intent=None,source=hashbytes(b),legacy={'capsule':hashbytes(b),'identity':cid,'owner':'legacy-resume','state':'failed','generation':1,'ordinal':0,'failure':'transport-retryable','repaired':None});savecontrol(db,key,'execution',d)
    if cleanup:
        owner_discovered(db,key);need(getcontrol(db,key,'execution')['source']==hashbytes(b),'capsule-owner-readback')
        d=getcontrol(db,key,'execution');d.update(phase='cleanup',intent={'kind':'cleanup','address':'drain','hash':source_hash,'payload':{'capsule':hashbytes(b)}});savecontrol(db,key,'execution',d)
        capsule_cleanup(db,key,crash)
    return b

def capsule_restore(db,events,key):
    b=db.read(key,'legacy');need(b is not None,'legacy_resume_evidence_unavailable')
    a=ANSWERS['records']['D46-capsule'];f=[v for _,v in read_record(b,a)]
    need(f[0:4]==['t','d','a','tracking'] and f[7] in ('success-events','rejection-events'),'capsule-identity')
    need(f[10]==len(events) and f[8]==events[0]['sequence'] and f[9]==events[-1]['sequence'],'capsule-range')
    # Independently construct descriptor bytes from freshly addressed chunk readback.
    cid=hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01'+frame('U',f[0])+frame('U',f[1])+frame('U',f[2])+frame('U',f[3])+bytes.fromhex(f[14]))
    parts=[];allrows=b''
    for ordinal,offset in enumerate(range(0,len(events),61)):
        address='legacy-resume-capsule-chunk:'+hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0\x01'+bytes.fromhex(cid)+frame('N',ordinal))
        cb=db.read(address,'legacy');need(cb is not None,'legacy-chunk-absent')
        cf=[v for _,v in read_record(cb,ANSWERS['records']['D46-chunk'])]
        rows=b''.join(frame('N',r['sequence'])+frame('U',r['message'])+bytes.fromhex(r['digest']) for r in events[offset:offset+61])
        need(cf[:3]==[cid,ordinal,len(events[offset:offset+61])] and cf[3]==rows.hex() and cf[4]==hashbytes(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+frame('B',rows.hex())),'legacy-original-range')
        parts.append(frame('N',ordinal)+frame('N',events[offset]['sequence'])+frame('N',cf[2])+bytes.fromhex(hashbytes(cb))+frame('N',len(cb))+frame('U',address));allrows+=rows
    need(bytes.fromhex(f[12])==frame('P',len(parts))+b''.join(parts),'capsule-manifest')
    need(f[11]==hashbytes(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'+frame('B',(frame('P',len(events))+allrows).hex())),'capsule-event-root')
    return ([r['message'] for r in events], 'Rejected' if f[7]=='rejection-events' else 'Completed')

def capsule_read(db,key):
    raw=db.read(key,'legacy');need(raw is not None,'legacy_resume_evidence_unavailable')
    f=[v for _,v in read_record(raw,ANSWERS['records']['D46-capsule'])]
    need(1<=f[10]<=1000 and f[9]-f[8]+1==f[10],'legacy-range-bound')
    cid=hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01'+frame('U',f[0])+frame('U',f[1])+frame('U',f[2])+frame('U',f[3])+frame('B32',f[14]))
    events=[]
    for ordinal in range((f[10]+60)//61):
        address=keyed('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1','legacy-resume-capsule-chunk:',[['B32',cid],['N',ordinal]])
        cb=db.read(address,'legacy');need(cb is not None,'legacy-chunk-absent')
        cf=[v for _,v in read_record(cb,ANSWERS['records']['D46-chunk'])];need(cf[:2]==[cid,ordinal] and 1<=cf[2]<=61,'legacy-chunk-order')
        rows=hexbytes(cf[3]);offset=0
        for _ in range(cf[2]):
            need(offset+12<=len(rows),'legacy-row-short');seq=int.from_bytes(rows[offset:offset+8],'big');length=int.from_bytes(rows[offset+8:offset+12],'big');offset+=12
            need(1<=length<=1024 and offset+length+32<=len(rows),'legacy-row-length')
            message=rows[offset:offset+length].decode('utf-8');offset+=length;dg=rows[offset:offset+32].hex();offset+=32
            events.append({'sequence':seq,'message':textfield(message),'digest':dg})
        need(offset==len(rows),'legacy-row-trailing')
    need([r['sequence'] for r in events]==list(range(f[8],f[9]+1)) and len({r['message'] for r in events})==len(events),'legacy-original-range')
    capsule_restore(db,events,key)
    return f,events

def legacy_failure_address(key,legacy):
    return provider_address('drain-failure',key,hashbytes(canonical([legacy['capsule'],legacy['generation'],legacy['ordinal']])))

def legacy_failure_fixture(db,failure,key='execution'):
    d=getcontrol(db,key,'execution');l=d['legacy'];f,_=legacy_authority(db,d)
    reason={'transport-retryable':'drain_publish_failed','evidence-unavailable':'drain_missing_event','evidence-contradictory':'drain_event_count_mismatch'}.get(failure,failure)
    payload={'capsule':l['capsule'],'generation':l['generation'],'ordinal':l['ordinal'],'eventRoot':f[11],'range':[f[8],f[9]],'classification':f[7],'reason':reason}
    db.external(legacy_failure_address(key,l),canonical(payload),key)

def legacy_repair_address(key,legacy):
    return provider_address('legacy-repaired-range',key,hashbytes(canonical([legacy['capsule'],legacy['generation']])))

def legacy_repair_payload(legacy,fields):
    return {'capsule':legacy['capsule'],'failedGeneration':legacy['generation'],'eventRoot':fields[11],'range':[fields[8],fields[9]],'classification':fields[7]}

def legacy_transition(db,key,edge,ordinal,owner='legacy-resume',failure=None,crash=None):
    d=getcontrol(db,key,'execution');legacy=d['legacy'];need(legacy is not None,'legacy-fence')
    need(legacy['owner']==owner,'legacy-owner');old=legacy['state']
    need((old,edge) in {('claimed','draining'),('draining','draining'),('draining','completed'),('draining','failed'),('failed','claimed')},'legacy-edge')
    fields,events=legacy_authority(db,d)
    if old=='failed':
        need(ordinal>legacy['ordinal'] and ordinal==d['ordinal'],'legacy-reclaim')
        need(d['phase'] in ('idle','cleanup') and d['request'] is None,'legacy-success-authority')
        row=next((r for r in d['outcomes'] if r['ordinal']==ordinal),None);need(row is not None,'legacy-success-authority')
        r={'result':row['result'],'identity':row['identity']};audit=db.read(action_address(d,r,'audit'),key)
        need(audit is not None and hashbytes(audit)==row['audit'],'legacy-success-audit')
        f=[v for _,v in read_record(audit,ANSWERS['records']['D45-audit'])]
        need(f[:3]==[d['tenant'],d['execution'],ordinal] and f[3]==row['identity'] and f[4]==row['carrierHash'],'legacy-success-owner')
        if legacy['failure']!='transport-retryable':
            address=legacy_repair_address(key,legacy);expected=canonical(legacy_repair_payload(legacy,fields))
            intent={'kind':'cleanup','address':address,'hash':hashbytes(expected),'payload':{'action':'consume-legacy-repair','capsule':legacy['capsule'],'failedGeneration':legacy['generation'],'ordinal':ordinal}}
            if d['phase']=='cleanup':need(d['intent']==intent,'legacy-repair-intent')
            else:
                need(db.read(address,'fixture-authority')==expected,'legacy-repair-readback')
                d.update(phase='cleanup',intent=intent);d=savecontrol(db,key,'execution',d);legacy=d['legacy']
            raw=db.read(address,'fixture-authority');need(raw in (None,expected),'legacy-repair-readback')
            if crash=='intent':raise Crash('legacy-repair-intent')
            db.delete(address,'fixture-authority');need(db.read(address,'fixture-authority') is None,'legacy-repair-deletion-readback')
            if crash=='effect':raise Crash('legacy-repair-effect')
            legacy['repaired']=hashbytes(expected);d.update(phase='idle',intent=None)
        legacy['failure']=None
    else:need(ordinal==legacy['ordinal'],'legacy-ordinal')
    if edge=='draining':
        need(db.read('drain','legacy') is None,'legacy-live-drain')
        # Existing actor restore is represented by its exact original range readback.
        payload={'capsule':legacy['capsule'],'owner':owner,'ordinal':ordinal,'range':[fields[8],fields[9]],'eventRoot':fields[11],'classification':fields[7]}
        address=provider_address('live-drain',key);raw=canonical(payload)
        intent={'kind':'invoke','address':address,'hash':hashbytes(raw),'payload':payload}
        if old=='draining':
            need(d['phase']=='idle' and d['intent'] is None and d['request'] is None and db.read(address,key)==raw,'legacy-restore-readback')
            return
        if d['phase']=='cleanup':
            need(d['request'] is None and d['intent']==intent,'legacy-restore-intent')
            need(db.read(address,key) in (None,raw),'legacy-restore-readback')
        else:
            need(d['phase']=='idle' and d['intent'] is None and d['request'] is None,'legacy-restore-intent')
            need(db.read(address,key) is None,'legacy-live-drain')
            d.update(phase='cleanup',intent=intent);d=savecontrol(db,key,'execution',d)
        if crash=='intent':raise Crash('legacy-restore-intent')
        admitted_token=(db.rows[key]['generation'],db.rows[key]['fence'])
        db.external(address,raw,key)
        if crash=='effect':raise Crash('legacy-restore-effect')
        need(db.read(address,key)==raw,'legacy-restore-readback')
        def advance(s):
            need((s.rows[key]['generation'],s.rows[key]['fence'])==admitted_token,'cas')
            need(s.read(key,key)==canonical(d),'cas');x=copy.deepcopy(d)
            x.update(phase='idle',intent=None);x['legacy'].update(state=edge,generation=add(legacy['generation'],1),ordinal=ordinal)
            savecontrol(s,key,'execution',x)
        db.transaction(advance)
        if crash=='advance':raise Crash('legacy-restore-advance')
        return
    if edge in ('failed','completed'):
        if edge=='failed':
            raw=db.read(legacy_failure_address(key,legacy),key);need(raw is not None,'legacy-failure-readback')
            evidence=decode(raw);exact(evidence,'capsule generation ordinal eventRoot range classification reason')
            need({k:evidence[k] for k in ('capsule','generation','ordinal','eventRoot','range','classification')}=={'capsule':legacy['capsule'],'generation':legacy['generation'],'ordinal':legacy['ordinal'],'eventRoot':fields[11],'range':[fields[8],fields[9]],'classification':fields[7]},'legacy-failure-readback')
            classes={'drain_publish_failed':'transport-retryable','drain_state_store_failure':'transport-retryable','drain_dapr_unavailable':'transport-retryable','drain_attempts_exhausted':'transport-retryable','drain_event_count_mismatch':'evidence-contradictory','drain_missing_event':'evidence-unavailable','unknown':'evidence-unavailable'}
            need(evidence['reason'] in classes,'legacy-failure');derived=classes[evidence['reason']]
            need(failure is None or failure==derived,'legacy-failure-class')
        else:
            raw=db.read(provider_address('drain-completed',key),key)
            need(raw==canonical({'capsule':legacy['capsule'],'outcome':'Rejected' if fields[7]=='rejection-events' else 'Completed'}),'legacy-completion-readback');derived=None
        address=provider_address('live-drain',key)
        intent={'kind':'cleanup','address':address,'hash':hashbytes(raw),'payload':{'action':edge,'capsule':legacy['capsule'],'generation':legacy['generation'],'ordinal':ordinal,'failure':derived}}
        if d['phase']=='cleanup':need(d['intent']==intent,'legacy-failure-intent')
        else:
            need(d['phase']=='idle' and d['intent'] is None and d['request'] is None,'legacy-failure-intent')
            d.update(phase='cleanup',intent=intent);d=savecontrol(db,key,'execution',d);legacy=d['legacy']
        if crash=='intent':raise Crash('legacy-end-intent')
        db.delete(address,key);need(db.read(address,key) is None,'legacy-drain-deletion-readback')
        if crash=='effect':raise Crash('legacy-end-effect')
        d.update(phase='idle',intent=None);legacy['failure']=derived;legacy['repaired']=None
        if edge=='failed':d.update(source=legacy['capsule'],reason='legacy-publish-failed')
    legacy.update(state=edge,generation=add(legacy['generation'],1),ordinal=ordinal)
    savecontrol(db,key,'execution',d)

def scope_usage(tenant,shard):
    return keyed('HX-EV-SCOPE-SHARD-USAGE-KEY-1','scope-shard-usage:',[['U',tenant],['N',shard]])

def scope_admit(db,tenant,execution,input_hash,now=0):
    textfield(tenant);textfield(execution);digest(input_hash)
    material=frame('U',tenant)+frame('U',execution);key='command-execution-scope:'+hashbytes(material);shard=hashlib.sha256(material).digest()[0]
    try:
        old=db.read(key,key)
        if old is not None:
            d=decode(old);exact(d,'tenant execution input state shard expiry');need(d['tenant']==tenant and d['execution']==execution and d['state'] in ('required','tombstone'),'scope-authority');need(d['input']==input_hash,'CommandIdentityConflict');need(d['state']!='tombstone','idempotency-expired');return key
        def tx(s):
            usagekey=scope_usage(tenant,shard);u=s.read(usagekey,usagekey);usage=0 if u is None else decode(u)['used']
            need(add(add(usage,4096),2048)<=262144,'scope_retention_capacity_hold')
            s.write(key,canonical({'tenant':tenant,'execution':execution,'input':input_hash,'state':'required','shard':shard,'expiry':None}),key,None)
            s.write(usagekey,canonical({'used':add(usage,4096),'shard':shard,'tenant':tenant}),usagekey,u)
        db.transaction(tx);return key
    except Refusal as e:
        if str(e) not in ('CommandIdentityConflict','idempotency-expired','scope_retention_capacity_hold'):raise Refusal('admission_evidence_hold') from e
        raise

def scope_closed(db,key,d,create=False):
    address=provider_address('scope-obligations',key)
    expected=canonical({'scope':key,'input':d['input'],'obligations':'closed'})
    if create:db.external(address,expected,'fixture-authority')
    need(db.read(address,'fixture-authority')==expected,'scope-open-obligation')

def scope_compact(db,key,now):
    b=db.read(key,key);need(b is not None,'scope-absent');d=decode(b);scope_closed(db,key,d)
    if d['state']=='tombstone':return
    need(d['state']=='required','scope-predecessor');utc(now);utc(now+30*DAY)
    d.update(state='tombstone',expiry=now+30*DAY);db.write(key,canonical(d),key,b)

def scope_status(db,key):
    d=decode(db.read(key,key));return 410 if d['state']=='tombstone' else 200

def scope_expire(db,key,now):
    b=db.read(key,key)
    if b is None:return
    d=decode(b);need(d['state']=='tombstone' and now>=d['expiry'],'scope-expiry');scope_closed(db,key,d)
    def tx(s):
        need(s.read(key,key)==b,'cas');ukey=scope_usage(d['tenant'],d['shard']);u=s.read(ukey,ukey);usage=decode(u)
        need((usage['tenant'],usage['shard'])==(d['tenant'],d['shard']),'scope-usage-owner');usage['used']=subtract(usage['used'],4096)
        s.delete(key,key);s.write(ukey,canonical(usage),ukey,u);s.delete(provider_address('scope-obligations',key),'fixture-authority')
    db.transaction(tx)

ANSWERS = json.loads((HERE/'known-answers.json').read_text())

RECORD_CAPS = dict(zip(('D06-activation D12-legacy-claim D12-cutover D12-usage D12-tombstone D14-drain-limit D14-drain-resolution D16-membership-resolution D29-capability D29-charge D29-counter D29-pin-batch D36-policy D36-quarantine D36-redrive D45-carrier D45-request D45-attempt-set D45-closure D45-window D45-audit D46-chunk D46-capsule D17-destination-config').split(),(1048576,8192,4096,2048,4096,4096,4096,16384,65536,4096,4096,65536,16384,131072,3072,2048,4096,67108864,65536,16384,4096,65536,131072,65536)))

def normative_caps():
    rows=re.findall(r'^\| ((?:record|control|public)/[^ |]+) \| ([0-9]+) \|$',(HERE/'obligations.md').read_text(),re.M)
    need(len(rows)==len({k for k,_ in rows}),'normative-cap-duplicate');return {k:int(v) for k,v in rows}

def verify_public(name,raw):
    d=decode(raw)
    if name=='cursor-envelope':
        exact(d,'schema keyId payload signature');need(d['schema']=='hexalith.eventstore.hold-cursor-envelope/1','cursor-envelope-schema');textfield(d['keyId']);typed('cursor',canonical(d['payload']));digest(d['signature']);need(d['signature']==cursor_sign(d['payload']),'cursor-envelope-signature')
    elif name=='held-redrive-request':exact(d,'expectedRedriveCount');integer(d['expectedRedriveCount'])
    else:
        exact(d,'entryKey redriveCount state');digest(d['entryKey']);need(integer(d['redriveCount'])>0 and d['state']=='redriving','redrive-response')
    return d

def verify_answers(answers=None):
    caps=normative_caps()
    expected={**{'record/'+k:v for k,v in RECORD_CAPS.items()},**{'control/'+k:v for k,v in CAPS.items()},**{'public/'+k:v for k,v in {'cursor-envelope':16384,'held-redrive-request':4096,'held-redrive-202':4096}.items()}}
    assert set(caps)==set(expected),'normative-cap-set'
    for name,value in expected.items():assert caps[name]==value,'normative-cap:'+name
    answers=ANSWERS if answers is None else answers
    assert set(answers['records'])==set(RECORD_CAPS),'record-answer-set'
    for name,a in answers['records'].items():
        assert a['maxBytes']==RECORD_CAPS[name],name+' cap'
        raw=bytes.fromhex(a['hex'])
        assert len(raw)==a['length'] and hashbytes(raw)==a['sha256'],name
        produced=record(a['domain'],a['fields']) if 'domain' in a else canonical(a['json'])
        assert produced==raw,name
        if 'domain' in a:
            assert read_record(raw,a)==a['fields']
            refused(lambda:read_record(raw+b'\x00',a))
            broken=bytearray(raw);broken[len(a['domain'])+4]=255
            refused(lambda:read_record(bytes(broken),a))
    for name,a in answers['keys'].items():
        material=a['domain'].encode()+b'\x00\x01'+b''.join(frame(k,v) for k,v in a['fields'])
        assert material.hex()==a['materialHex'] and a['prefix']+hashbytes(material)==a['value'],name
    for name,a in answers['sharedKeys'].items():
        material=b''.join(frame(k,v) for k,v in a['fields'])
        assert material.hex()==a['materialHex'] and a['prefix']+hashbytes(material)==a['value'],name
    assert set(answers['controls'])==set(CAPS),'control-answer-set'
    for kind,a in answers['controls'].items():
        assert a['maxBytes']==CAPS[kind],kind+' cap'
        raw=bytes.fromhex(a['hex']);assert canonical(a['json'])==raw and len(raw)==a['length'] and hashbytes(raw)==a['sha256'],kind+' bytes';typed(kind,raw)
    assert set(answers['public'])=={'cursor-envelope','held-redrive-request','held-redrive-202'},'public-answer-set'
    for name,a in answers['public'].items():
        assert a['maxBytes']==caps['public/'+name],name+' cap'
        raw=hexbytes(a['hex']);assert canonical(a['json'])==raw and len(raw)==a['length']<=a['maxBytes'] and hashbytes(raw)==a['sha256'],name+' bytes';verify_public(name,raw)
    entry=answers['controls']['registry-entry']['json'];assert entry['shard']==registry_shard(entry['scopeKind'],entry['scopeId']),'registry literal shard'

def answer_mutations():
    for family in ('records','controls','public'):
        for name in ANSWERS[family]:
            bad=copy.deepcopy(ANSWERS);bad[family][name]['maxBytes']+=1
            try:verify_answers(bad)
            except AssertionError as e:assert str(e)==name+' cap',(name,str(e))
            else:raise AssertionError('mutated cap did not fail: '+name)
    bad=copy.deepcopy(ANSWERS);bad['records']['D45-window']['sha256']=ZERO
    try:verify_answers(bad)
    except AssertionError as e:assert str(e)=='D45-window'
    else:raise AssertionError('mutated digest did not fail')

def change_ledger(db,**values):
    prior=db.read('ledger','ledger');d=ledger_get(db);d.update(values);db.write('ledger',canonical(d),'ledger',prior)

def route_fixture(db,key='held',outcome='terminal',original=False):
    d=getcontrol(db,key,'held')
    payload={'carrier':d['carrier'],'outcome':outcome} if original else {'count':d['redrives'],'requestHash':d['attempt']['requestHash'],'carrier':d['carrier'],'outcome':outcome}
    db.external(provider_address('original-route' if original else 'route-terminal',key),canonical(payload),key)

def asserted_failure(fn):
    try:fn()
    except (AssertionError,Refusal):COUNTS['owningCorruptions']=COUNTS.get('owningCorruptions',0)+1;return
    raise AssertionError('owning corruption did not fail')

def codec_cases():
    verify_answers();answer_mutations()
    for raw in (b'NaN',b'Infinity',b'1e400',b'"\\ud800"',b'{"a":1,"a":2}',b'\xff'):
        refused(lambda:decode(raw))
    refused(lambda:frame('B','not-hex'));refused(lambda:textfield('\ud800'))
    for name,tag,sizes in [('D36-quarantine',8,(4095,4096,4097)),('D29-counter',2,(1030,1031,1032))]:
        answer=ANSWERS['records'][name]
        for size in sizes:
            fields=copy.deepcopy(answer['fields']);fields[tag][1]='x'*size
            if size==sizes[-1]:refused(lambda:record(answer['domain'],fields),'identifier')
            else:assert read_record(record(answer['domain'],fields),answer)==fields
    e=copy.deepcopy(ANSWERS['controls']['execution']['json']);h=copy.deepcopy(ANSWERS['controls']['held']['json'])
    invalid=[('execution',{**e,'reason':'x'*100000}),('execution',{**e,'legacy':{'arbitrary':1}}),('execution',{**e,'accepted':[1]}),('execution',{**e,'roster':e['roster']*2}),('execution',{**e,'intent':{'kind':'fabricated','address':'a','hash':ZERO,'payload':{}}}),('held',{**h,'reason':'unknown'}),('held',{**h,'error':{'reason':'x'*129,'hash':ZERO,'utc':0}}),('held',{**h,'attempt':{'count':1,'requestHash':ZERO,'carrier':ZERO,'utc':True,'result':None}}),('held',{**h,'tenant':None}),('held',{**h,'phase':'captured'}),('held',{**h,'request':{'claim':'zz','signature':'00','expectedCount':0,'utc':0}})]
    r=copy.deepcopy(ANSWERS['controls']['registry-entry']['json']);invalid += [('registry-entry',{**r,'shard':True}),('registry-entry',{**r,'owner':'quota'})]
    for subject in ('x'*1024,'\u00e9'*512):
        assert typed('registry-entry',canonical({**r,'subject':subject}))['subject']==subject
        assert registry_key(r['scopeKind'],r['scopeId'],subject).startswith('owner-registry-entry:')
    for subject in ('x'*1025,'\u00e9'*512+'x'):
        refused(lambda:typed('registry-entry',canonical({**r,'subject':subject})),'identifier')
        refused(lambda:registry_key(r['scopeKind'],r['scopeId'],subject),'identifier')
    epoch=copy.deepcopy(ANSWERS['controls']['epoch']['json'])
    for field,value in [('epoch',True),('generation',0),('ownerFence','8'+'0'*25),('ownerFence','0'*25),('renewedUtc',-1),('expiresUtc',60*TICK+1),('holder','x'*1025),('extra',None)]:
        refused(lambda:typed('epoch',canonical({**epoch,field:value})))
    refused(lambda:typed('epoch',b'x'*(CAPS['epoch']+1)),'control-byte-bound')
    for kind,value in invalid:refused(lambda:typed(kind,canonical(value)))
    refused(lambda:typed('execution',b'x'*(CAPS['execution']+1)),'control-byte-bound')
    q=copy.deepcopy(ANSWERS['controls']['queue-shard']['json']);q['rows'][0]['ticket']=2;refused(lambda:typed('queue-shard',canonical(q)),'ticket')
    q=copy.deepcopy(ANSWERS['controls']['queue-shard']['json']);q['rows']=q['rows']*2;refused(lambda:typed('queue-shard',canonical(q)),'queue-order')
    db=newdb();execution_init(db);begin_resume(db);pending=getcontrol(db,'execution','execution')
    bad=copy.deepcopy(pending);bad['request']['expiry']=bad['request']['utc']+901*TICK;refused(lambda:typed('execution',canonical(bad)),'request-horizon')
    finish_resume(db,0);bad=getcontrol(db,'execution','execution');bad['outcomes'][0]['deleteAfter']+=1;refused(lambda:typed('execution',canonical(bad)),'fixed-horizon')
    raw=bytes.fromhex(ANSWERS['records']['D17-destination-config']['hex']);assert destination(raw,'pubsub','orders')['metadata']=={}
    refused(lambda:destination(raw+b' ','pubsub','orders'))
    for metadata in ({str(i):'' for i in range(65)},{'a':'v'*16384,'b':'v'}):
        value=decode(raw);value['metadata']=metadata;refused(lambda:destination(canonical(value),'pubsub','orders'))
    # A real behavior assertion, rather than a self-test of a detached hash expression.
    bad=copy.deepcopy(ANSWERS);bad['records']['D45-window']['hex']='00';asserted_failure(lambda:verify_answers(bad))

def status_replay_cases():
    expected=[({},('EventsStored',None,1)),({'state':'unknown','maximum':True},('EventsStored',None,1)),({'drain':True},('EventsStored','publication_drain_limit_hold',60)),({'state':'failed','classes':['class-01'],'maximum':True},('EventsStored','publication_retry_exhausted_hold',60)),({'state':'failed','classes':['class-02'],'maximum':True},('CommandOutcomeHold','terminal_evidence_hold',30)),({'state':'failed','classes':['class-03']},('CommandOutcomeHold','terminal_evidence_hold',30)),({'state':'failed','classes':['class-01'],'automatic':True},('EventsStored','publication_retry_pending',1)),({'state':'failed','terminal':True},('PublishFailed','publication_terminal_failed',None)),({'conflict':True},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'published'},('Completed',None,None)),({'state':'published','classification':'rejection'},('Rejected',None,None)),({'state':'published','classification':'unknown'},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'not-applicable'},('Completed',None,None)),({'state':'failed','classes':['unknown'],'maximum':True},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'failed','classes':['class-01','class-02'],'maximum':True},('CommandOutcomeHold','terminal_evidence_hold',30)),({'terminal':True,'unavailable':True},('CommandOutcomeHold','outcome_evidence_hold',30))]
    # Each adjacent precedence pair has a case with both predicates active.
    expected += [({'terminal':True,'state':'published'},('PublishFailed','publication_terminal_failed',None)),({'state':'published','drain':True},('Completed',None,None)),({'state':'not-applicable','drain':True},('Completed',None,None)),({'drain':True,'state':'failed','classes':['class-02']},('EventsStored','publication_drain_limit_hold',60)),({'state':'failed','classes':['class-01','class-03'],'maximum':True},('CommandOutcomeHold','terminal_evidence_hold',30)),({'state':'failed','classes':['class-01'],'maximum':True,'automatic':True},('EventsStored','publication_retry_exhausted_hold',60)),({'preparation':True,'terminal':True},('CommandOutcomeHold','response_preparation_hold',30)),({'state':'failed','classes':['class-01']},('CommandOutcomeHold','outcome_evidence_conflict',30))]
    for args,out in expected:assert status(**args)==out,'status-precedence'
    COUNTS['statusCases']=len(expected)
    for n in (24575,24576,24577):assert replay(n,0)==('continue-full-replay' if n<24576 else 'hold')
    for n in (32767,32768,32769):assert replay(n,0,activation=False)==('full-replay' if n<=32768 else 'LegacyArrayLimit')
    assert [replay(1,n) for n in (48*MIB-1,48*MIB,48*MIB+1)]==['continue-full-replay','hold','hold']
    assert [replay(1,n,activation=False) for n in (64*MIB-1,64*MIB,64*MIB+1)]==['full-replay','full-replay','LegacyArrayLimit']
    assert replay(0,0,incremental=True)=='incremental-bootstrap';COUNTS['matrixEvidence']['idle-replay']='incremental-bootstrap at zero events';refused(lambda:replay(MAX,0))
    assert membership(b'pin',b'pin','EmptyNamespace',True)=='ContinueSamePin'
    assert membership(b'pin',b'changed','InitialRowOnly',True)=='FirstSendMembershipChangedHold'
    refused(lambda:membership(b'pin',b'pin','EmptyNamespace',False));refused(lambda:membership(b'pin',b'pin','EmptyNamespace',True,True))
    claim={'window':2,'unresolved':[2,3]};assert broker_accept(2,2,claim,{1})=='accepted'
    refused(lambda:broker_accept(1,2,{'window':1,'unresolved':[2,3]},{1}),'window-fence')
    refused(lambda:broker_accept(2,1,claim,{1}),'window-admission')
    refused(lambda:broker_accept(2,2,claim,{1},terminal=True,duplicate=True),'operation-terminal-fence')
    assert capture_interval(193*MIB)=='ordinary' and capture_interval(193*MIB+1)=='provider-quarantine' and capture_interval(256*MIB+1)=='incident'
    refused(lambda:capture_interval(193*MIB+1,valid=True))
    retention_capability(2*1024*MIB,4*1024*MIB,1200*MIB,1200*MIB,1,256*MIB)
    refused(lambda:retention_capability(1024*MIB,2*1024*MIB,195*MIB,195*MIB,1,256*MIB),'publication_retention_capability_invalid')

def resume_cases():
    restarts=0
    for mode in ELIGIBILITY[:3]:
        for crash in ('intent','effect','advance'):
            db=newdb();original=execution_init(db,eligibility=mode);source=original['source']
            unchanged(db,lambda:begin_resume(db,source=ZERO),'resume_hold_changed')
            begin_resume(db,source=source)
            for _ in range(10):
                if getcontrol(db,'execution','execution')['request'] is None:break
                try:resume_step(db,0,crash=crash)
                except Crash:pass
                db=db.restart();restarts+=1
            finish_resume(db,0);d=getcontrol(db,'execution','execution')
            assert d['accepted']==[1] and d['unresolved']==[2,3] and d['roster']==original['roster'] and d['limit']==16 and d['ordinal']==1
            assert d['window']==(original['window'] if mode=='drain-limit' else original['window']+1)
            if mode=='drain-limit':assert d['windowClaim']==original['windowClaim'] and d['closedCount']==0
            prior=db.snapshot();reply=begin_resume(db,source=source);assert response(reply.hex())['resumeOrdinal']==1 and db.snapshot()==prior
            unchanged(db,lambda:begin_resume(db,source=source,reason='changed'),'resume_request_conflict')
            unchanged(db,lambda:begin_resume(db,source=source,now=900*TICK),'resume_request_expired')
            assert original['charge'] in ledger_get(db)['charges']
            reclaim(db,900*TICK+30*DAY-1);assert getcontrol(db,'execution','execution')['outcomes']
            reclaim(db,900*TICK+30*DAY);assert not getcontrol(db,'execution','execution')['outcomes'] and original['charge'] not in ledger_get(db)['charges']
    COUNTS['persistedResumeRestarts']=restarts;COUNTS['matrixEvidence']['eligible-resume']='serialized restart, stable roster and unresolved dispatch'
    db=newdb();execution_init(db);begin_resume(db);resume_step(db,now=900*TICK)
    assert getcontrol(db,'execution','execution')['request'] is None and getcontrol(db,'execution','execution')['ordinal']==0
    for crash in ('intent','effect'):
        db=newdb();execution_init(db);begin_resume(db)
        try:resume_step(db,0,crash=crash)
        except Crash:pass
        if crash=='intent':cancel_resume(db);assert getcontrol(db,'execution','execution')['request'] is None
        else:unchanged(db,lambda:cancel_resume(db),'irreversible');resume_step(db,now=901*TICK);finish_resume(db,0)
    # The pending reject bytes survive acceptance progress after producer-disable.
    db=newdb();execution_init(db);begin_resume(db);resume_step(db,0)
    try:resume_step(db,0,crash='intent')
    except Crash:pass
    d=getcontrol(db,'execution','execution');frozen=copy.deepcopy(d['intent']);d.update(accepted=[1,2],unresolved=[3]);savecontrol(db,'execution','execution',d)
    db=db.restart();assert getcontrol(db,'execution','execution')['intent']==frozen;finish_resume(db,0)
    invocations=[decode(bytes.fromhex(row['body'])) for address,row in db.rows.items() if address.startswith('publication-invocation:')]
    assert invocation_messages(db,getcontrol(db,'execution','execution'),invocations[0])==['event-3']
    for field in ('owner','provider'):
        db=newdb();execution_init(db)
        if field=='owner':
            d=getcontrol(db,'execution','execution');d['revision']=MAX-2;db.write('execution',canonical(d),'execution',db.read('execution','execution'))
        else:
            r=db.rows['execution'];r['generation']=MAX-2;r['receipt']=Store.receipt('execution',r['body'],r['generation'],r['owner'],r.get('index'),r['fence'])
        unchanged(db,lambda:begin_resume(db),'resume_arithmetic_exhausted')
    # Pending identities are never reclaimed, even after the retention deadline.
    db=newdb();execution_init(db);begin_resume(db)
    while getcontrol(db,'execution','execution')['phase']!='successor':resume_step(db,0)
    before=db.snapshot();reclaim(db,32*DAY);assert db.snapshot()==before;db=db.restart();finish_resume(db,0)
    # 64 concurrent outcomes are bounded; refusal changes neither ordinal nor charge.
    db=newdb();change_ledger(db,tenantCeiling=1024*MIB,deploymentCeiling=3*1024*MIB)
    execution_init(db)
    for i in range(64):
        if i:
            d=getcontrol(db,'execution','execution');d.update(reason='retry-exhausted',source=hashbytes(str(i).encode()));savecontrol(db,'execution','execution',d)
        begin_resume(db,'bounded'+str(i));finish_resume(db,0)
    d=getcontrol(db,'execution','execution');d.update(reason='retry-exhausted',source=hashbytes(b'65'));savecontrol(db,'execution','execution',d)
    unchanged(db,lambda:begin_resume(db,'bounded65'),'resume_capacity_hold')
    COUNTS['concurrentOutcomes']=len(d['outcomes'])
    db=newdb();execution_init(db);now=0;successes=0
    for i in range(67):
        if i:
            d=getcontrol(db,'execution','execution');d.update(source=hashbytes(str(i).encode()),reason='retry-exhausted',updatedUtc=now);savecontrol(db,'execution','execution',d)
        begin_resume(db,'life'+str(i),now=now);finish_resume(db,now);successes=getcontrol(db,'execution','execution')['ordinal'];now+=31*DAY;reclaim(db,now);db=db.restart()
        assert not getcontrol(db,'execution','execution')['outcomes'] and len(ledger_get(db)['charges'])==3
    COUNTS['lifetimeResumes']=successes

def legacy_cases():
    events=[{'sequence':10+i,'message':'m'+str(i),'digest':hashbytes(str(i).encode())} for i in range(65)]
    db=newdb();execution_init(db,eligibility='legacy-publish-failed');unchanged(db,lambda:begin_resume(db),'legacy_resume_evidence_unavailable')
    for classification in ('success-events','rejection-events'):
        db=newdb();db.external('drain',drain_fixture(events,classification),'legacy')
        capsule_make(db,events,classification,cleanup=True);db=db.restart();d=getcontrol(db,'execution','execution')
        fields,restored=legacy_authority(db,d);assert restored==events and fields[7]==classification and db.read('drain','legacy') is None
        begin_resume(db,'legacy');finish_resume(db,0)
        unchanged(db,lambda:legacy_transition(db,'execution','claimed',1,owner='dead-letter-admin'),'legacy-owner')
        legacy_transition(db,'execution','claimed',1)
        unchanged(db,lambda:legacy_transition(db,'execution','draining',0),'legacy-ordinal')
        legacy_transition(db,'execution','draining',1);unchanged(db,lambda:legacy_transition(db,'execution','failed',0,failure='transport-retryable'),'legacy-ordinal')
        legacy_failure_fixture(db,'evidence-contradictory');legacy_transition(db,'execution','failed',1,failure='evidence-contradictory')
        assert getcontrol(db,'execution','execution')['reason']=='legacy-publish-failed'
        begin_resume(db,'legacy2');finish_resume(db,0)
        unchanged(db,lambda:legacy_transition(db,'execution','claimed',2),'legacy-repair-readback')
        d=getcontrol(db,'execution','execution');db.external(legacy_repair_address('execution',d['legacy']),canonical(legacy_repair_payload(d['legacy'],legacy_authority(db,d)[0])),'fixture-authority')
        legacy_transition(db,'execution','claimed',2);legacy_transition(db,'execution','draining',2)
        db.external(provider_address('drain-completed','execution'),canonical({'capsule':d['legacy']['capsule'],'outcome':'Rejected' if classification=='rejection-events' else 'Completed'}),'execution')
        legacy_transition(db,'execution','completed',2);unchanged(db,lambda:legacy_transition(db,'execution','failed',2,failure='transport-retryable'),'legacy-edge')
        invocations=[decode(bytes.fromhex(row['body'])) for address,row in db.rows.items() if address.startswith('publication-invocation:')]
        assert all(v['range']==[10,74] for v in invocations)
        # Verify dispatch before terminal completion consumes the stored capsule, not copied IDs.
        complete=getcontrol(db,'execution','execution')
        assert all(invocation_messages(db,complete,v)==[e['message'] for e in events] for v in invocations)
    for point in ('charge','chunk','cleanup-intent','drain'):
        db=newdb();db.external('drain',drain_fixture(events,'success-events'),'legacy')
        try:capsule_make(db,events,cleanup=True,crash=point)
        except Crash:pass
        db=db.restart();owner_discovered(db,'execution');assert ledger_get(db)['charges']
        if point in ('cleanup-intent','drain'):
            pending=getcontrol(db,'execution','execution');assert pending['phase']=='cleanup' and pending['intent']['address']=='drain'
            assert (db.read('drain','legacy') is None)==(point=='drain')
        if point=='cleanup-intent':
            original=db.read('drain','legacy');db.write('drain',drain_fixture(events,'rejection-events'),'legacy',original)
            unchanged(db,lambda:capsule_make(db,events,cleanup=True),'drain-cleanup-source')
            db.write('drain',original,'legacy',db.read('drain','legacy'))
        capsule_make(db,events,cleanup=True);assert getcontrol(db,'execution','execution')['phase']=='idle' and db.read('drain','legacy') is None
    for classification in ('success-events','rejection-events'):
        for point in ('intent','effect','advance'):
            db=newdb();db.external('drain',drain_fixture(events,classification),'legacy');capsule_make(db,events,classification,cleanup=True)
            begin_resume(db,'restore');finish_resume(db,0);legacy_transition(db,'execution','claimed',1)
            claimed=getcontrol(db,'execution','execution');generation=claimed['legacy']['generation'];revision=claimed['revision']
            address=provider_address('live-drain','execution');payload={'capsule':claimed['legacy']['capsule'],'owner':'legacy-resume','ordinal':1,'range':[10,74],'eventRoot':legacy_authority(db,claimed)[0][11],'classification':classification}
            db.external(address,canonical({'unrelated':True}),'execution')
            unchanged(db,lambda:legacy_transition(db,'execution','draining',1),'legacy-live-drain');db.delete(address,'execution')
            try:legacy_transition(db,'execution','draining',1,crash=point)
            except Crash:pass
            db=db.restart();pending=getcontrol(db,'execution','execution')
            assert (db.read(address,'execution') is None)==(point=='intent')
            unchanged(db,lambda:legacy_transition(db,'execution','draining',1,owner='dead-letter-admin'),'legacy-owner')
            unchanged(db,lambda:legacy_transition(db,'execution','draining',0),'legacy-ordinal')
            if point!='advance':
                assert pending['phase']=='cleanup' and pending['legacy']['state']=='claimed' and pending['intent']=={'kind':'invoke','address':address,'hash':hashbytes(canonical(payload)),'payload':payload}
                db.delete(address,'execution');db.external(address,canonical({**payload,'classification':'contradictory'}),'execution')
                unchanged(db,lambda:legacy_transition(db,'execution','draining',1),'legacy-restore-readback');db.delete(address,'execution')
                if point=='effect':db.external(address,canonical(payload),'execution')
                for field,value in [('address',provider_address('other-live-drain','execution')),('hash',ZERO),('payload',{**payload,'ordinal':2})]:
                    corrupt=copy.deepcopy(pending);corrupt['intent'][field]=value;db.write('execution',canonical(corrupt),'execution',db.read('execution','execution'))
                    unchanged(db,lambda:legacy_transition(db,'execution','draining',1),'legacy-restore-intent')
                    db.write('execution',canonical(pending),'execution',db.read('execution','execution'))
            legacy_transition(db,'execution','draining',1);done=getcontrol(db,'execution','execution')
            assert db.read(address,'execution')==canonical(payload) and legacy_authority(db,done)[1]==events
            assert done['phase']=='idle' and done['intent'] is None and done['legacy']['generation']==generation+1 and done['revision']==revision+2
            db=db.restart();before=db.snapshot();legacy_transition(db,'execution','draining',1);assert db.snapshot()==before
    db=newdb();db.external('drain',drain_fixture(events,'success-events'),'legacy')
    try:capsule_make(db,events,crash='chunk')
    except Crash:pass
    change_ledger(db,tenantCeiling=ledger_get(db)['used']['tenant:t']+20)
    refused(lambda:capsule_make(db,events));assert registry(db)['rows'] and db.read('drain','legacy') is not None
    db=newdb();too_many=events*16;db.external('drain',drain_fixture(too_many,'success-events'),'legacy')
    unchanged(db,lambda:capsule_make(db,too_many),'legacy-range-bound')
    db=newdb();maximum=[{'sequence':i+1,'message':str(i).zfill(4)+'x'*1020,'digest':ZERO} for i in range(1000)]
    db.external('drain',drain_fixture(maximum,'success-events'),'legacy');capsule_make(db,maximum,cleanup=True);begin_resume(db);finish_resume(db,0)
    legacy_transition(db,'execution','claimed',1)
    try:legacy_transition(db,'execution','draining',1,crash='effect')
    except Crash:pass
    db=db.restart();legacy_transition(db,'execution','draining',1);d=getcontrol(db,'execution','execution');assert d['legacy']['state']=='draining' and legacy_authority(db,d)[1]==maximum and len(canonical(d))<CAPS['execution']
    invocation=next(decode(hexbytes(r['body'])) for k,r in db.rows.items() if k.startswith('publication-invocation:'));assert invocation_messages(db,d,invocation)==[r['message'] for r in maximum],'maximum-range-dispatch'
    COUNTS['matrixEvidence']['legacy-resume']='maximum-width original range reaches draining after byte restart'

def held_cases():
    for kind,scope in (('tenant','deployment'),('deployment','deployment')):
        for point in ('charge','object','control'):
            db=newdb();held_init(db,kind=kind,scope=scope)
            try:capture(db,b'exact carrier',crash=point)
            except Crash:pass
            db=db.restart();observe(db,20*TICK);capture(db,b'exact carrier');d=getcontrol(db,'held','held')
            assert d['firstUtc']==0 and d['observations']==2 and d['phase']=='captured';unchanged(db,lambda:capture(db,b'changed'))
            try:redrive(db,now=20*TICK,crash='commit')
            except Crash:pass
            db=db.restart();send_held(db);route_fixture(db,outcome='nonterminal');redrive_reconcile(db,now=20*TICK)
            d=getcontrol(db,'held','held');assert d['nextUtc']==80*TICK and d['redrives']==1
            unchanged(db,lambda:redrive(db,now=79*TICK),'redrive-time');unchanged(db,lambda:redrive(db,now=80*TICK,expected=0),'held_redrive_count_changed')
            redrive(db,now=80*TICK);route_fixture(db);redrive_reconcile(db,now=80*TICK);assert getcontrol(db,'held','held')['intent']['kind']=='delivered-cleanup'
            try:delivered_cleanup(db,crash='source')
            except Crash:pass
            db=db.restart();delivered_cleanup(db);assert not registry(db)['rows'] and not ledger_get(db)['charges']
    # Local success before capture follows delivered cleanup without fabricating a carrier object.
    db=newdb();held_init(db);route_fixture(db,original=True);redrive_reconcile(db);delivered_cleanup(db);assert not registry(db)['rows']
    db=newdb();held_init(db);change_ledger(db,tenantCeiling=ledger_get(db)['used']['tenant:t']);unchanged(db,lambda:capture(db,b'exact carrier'),'capacity')
    # A valid fixture signature cannot authorize a different held identity.
    db=newdb();held_init(db);capture(db,b'exact carrier')
    try:redrive(db,crash='commit')
    except Crash:pass
    d=getcontrol(db,'held','held');foreign=copy.deepcopy(d);foreign['topic']='foreign-orders'
    fields=read_record(hexbytes(d['request']['claim']),ANSWERS['records']['D36-redrive']);fields[3][1]=held_identity(foreign)
    raw=record(ANSWERS['records']['D36-redrive']['domain'],fields);claim={**d['request'],'claim':raw.hex(),'signature':fixture_sign(raw)}
    signed(raw,claim['signature']);assert read_record(raw,ANSWERS['records']['D36-redrive'])==fields and fields[3][1]!=held_identity(d)
    d['request']=claim;d['attempt']['requestHash']=hashbytes(raw);savecontrol(db,'held','held',d)
    unchanged(db,lambda:send_held(db),'redrive-claim-owner')
    d=getcontrol(db,'held','held');d.update(phase='captured',repair='required',request=None,attempt=None,intent=None);savecontrol(db,'held','held',d)
    unchanged(db,lambda:repair_held(db,claim=claim),'redrive-claim-owner')
    db=newdb();held_init(db);capture(db,b'exact carrier')
    try:redrive(db,crash='commit')
    except Crash:pass
    d=getcontrol(db,'held','held');request=copy.deepcopy(d['request'])
    # Simulate authenticated stored corruption at the boundary; repair uses the same owner.
    d['request']['claim']='zz';db.write('held',canonical(d),'held',db.read('held','held'));refused(lambda:getcontrol(db,'held','held'),'hex')
    redrive_reconcile(db);assert getcontrol(db,'held','held')['repair']=='corrupt'
    unchanged(db,lambda:redrive(db,now=900*TICK),'redrive-phase');repair_held(db,claim=request)
    d=getcontrol(db,'held','held');db.unavailable.add(held_object(d));unchanged(db,lambda:redrive(db,now=d['nextUtc']));db.unavailable.clear()
    d=getcontrol(db,'held','held');d['redrives']=0;unchanged(db,lambda:savecontrol(db,'held','held',d),'count-regression')
    d=getcontrol(db,'held','held');d['firstUtc']=1;unchanged(db,lambda:savecontrol(db,'held','held',d),'first-observation-identity')
    d=getcontrol(db,'held','held');d['updatedUtc']=-1;unchanged(db,lambda:savecontrol(db,'held','held',d),'utc-regression')
    d=getcontrol(db,'held','held');d['carrier']=ZERO;unchanged(db,lambda:savecontrol(db,'held','held',d),'held-identity')
    unchanged(db,lambda:erase_held(db),'authority-readback');authority(db,'held','erase',create=True)
    try:erase_held(db,crash='object')
    except Crash:pass
    db=db.restart();assert registry(db)['rows'] and ledger_get(db)['charges'];erase_held(db);assert not ledger_get(db)['charges']
    db=newdb();held_init(db);capture(db,b'exact carrier');attempts=0
    for _ in range(131):
        d=getcontrol(db,'held','held');redrive(db,now=d['nextUtc']);attempts+=1;db=db.restart();redrive_reconcile(db,now=getcontrol(db,'held','held')['updatedUtc'])
        assert len(canonical(getcontrol(db,'held','held')))<CAPS['held'] and len(ledger_get(db)['charges'])==4
    COUNTS['matrixEvidence']['held-delivery']='capture restart, route outcome, exact charge and cleanup'
    COUNTS['boundedRedrives']=getcontrol(db,'held','held')['redrives']
    assert COUNTS['boundedRedrives']==attempts,'observed-redrive-count'
    assert [backoff(i) for i in range(1,8)]==[60,120,240,480,900,900,900]

def queue_scope_inventory_cases():
    db=newdb();reserve_charge(db,'tenantblock','tenant','a',64*MIB-16385-6);sources={}
    for i,t in enumerate(('a','b','c'),1):
        subject=hashbytes(str(i).encode());sources[subject]=[{'message':'p'+str(i),'length':20000 if i==1 else 10}];queue_reserve(db,subject,t,ZERO,0);operation_plan_fixture(db,subject,sources[subject]);queue_materialize(db,subject,sources[subject],0)
    assert queue_turn(db,sources)==2;assert [r['ticket'] for r in queue_view(db)['rows']]==[1,3]
    refund(db,'tenantblock');assert queue_turn(db,sources)==1 and queue_turn(db,sources)==3
    # Rerender current overhead, park above-ceiling oldest rows, then grant younger tenant.
    db=newdb();sources={}
    for i,t in enumerate(('a','b'),1):
        subject=hashbytes(('park'+str(i)).encode());sources[subject]=[{'message':'p','length':64*MIB-100 if i==1 else 1}];queue_reserve(db,subject,t,ZERO,0);operation_plan_fixture(db,subject,sources[subject]);queue_materialize(db,subject,sources[subject],0)
    change_ledger(db,overhead=200);assert queue_turn(db,sources)==2;assert queue_view(db)['rows'][0]['state']=='parked'
    # Oldest tenant-eligible deployment-blocked work cannot be bypassed.
    db=newdb();reserve_charge(db,'fill','capture-scope','dep',64*MIB-1);sources={}
    for i in (1,2):
        subject=hashbytes(('deploy'+str(i)).encode());sources[subject]=[{'message':'p','length':64*MIB-1 if i==1 else 1}];queue_reserve(db,subject,str(i),ZERO,0);operation_plan_fixture(db,subject,sources[subject]);queue_materialize(db,subject,sources[subject],0)
    reserve_charge(db,'other','tenant','other',40*MIB);before=db.snapshot();assert queue_turn(db,sources) is None and db.snapshot()==before
    refund(db,'fill');assert queue_turn(db,sources)==1
    # Refused materialization cannot leave an invalid row blocking fairness.
    db=newdb();subject=hashbytes(b'invalid');queue_reserve(db,subject,'t',ZERO,0)
    for pins in ([{'message':'p','length':1}]*60,[{'message':'p','length':1}]*2):
        unchanged(db,lambda:queue_materialize(db,subject,pins,0))
    q=queue_view(db);q['lastTicket']=MAX;queue_save(db,q);unchanged(db,lambda:queue_reserve(db,hashbytes(b'next'),'t',ZERO,0),'AppendPreparationLimit')
    for failure in ('negative','overflow','unavailable'):
        db=newdb();pins=[{'message':'p','length':-1 if failure=='negative' else 1}]
        if failure=='overflow':change_ledger(db,overhead=MAX)
        if failure=='unavailable':db.unavailable.add('ledger')
        unchanged(db,lambda:pin_admit(db,'invalid','t',pins),'publication_pin_capacity_hold')
    for delta in (0,1):
        db=newdb();change_ledger(db,tenantCeiling=1024*MIB,deploymentCeiling=3*1024*MIB)
        subject=hashbytes(b'net');pins=[{'message':'p','length':449*MIB}];queue_reserve(db,subject,'t',ZERO,0);operation_plan_fixture(db,subject,pins);queue_materialize(db,subject,pins,0)
        reserve_charge(db,'fill','tenant','t',1024*MIB-(449*MIB+1)-1+delta)
        before=db.snapshot();result=queue_turn(db,{subject:pins})
        assert result==1 if delta==0 else result is None and db.snapshot()==before
    COUNTS['matrixEvidence']['capacity-wait']='fair grants, no bypass, parking and net refund'
    # Kind-qualified capture accounts obey the unidentified pool independently of tenants.
    db=newdb();reserve_charge(db,'tenant','tenant','same',1);reserve_charge(db,'capture','capture-scope','same',64*MIB-1)
    unchanged(db,lambda:reserve_charge(db,'plus','capture-scope','same',0),'capacity');refund(db,'capture');assert ledger_get(db)['used']['tenant:same']==2
    # Scope unavailable admission, conflicting input, full shard, closed evidence and checked refund.
    db=newdb();key=scope_admit(db,'t','e',ZERO);unchanged(db,lambda:scope_admit(db,'t','e',hashbytes(b'changed')),'CommandIdentityConflict')
    limited=newdb();material=frame('U','t')+frame('U','full');shard=hashlib.sha256(material).digest()[0];usagekey=scope_usage('t',shard)
    limited.external(usagekey,canonical({'used':63*4096,'tenant':'t','shard':shard}),usagekey)
    unchanged(limited,lambda:scope_admit(limited,'t','full',ZERO),'scope_retention_capacity_hold')
    db.unavailable.add(key);unchanged(db,lambda:scope_admit(db,'t','e',ZERO),'admission_evidence_hold');db.unavailable.clear()
    unchanged(db,lambda:scope_compact(db,key,0),'scope-open-obligation');scope_closed(db,key,decode(db.read(key,key)),create=True);scope_compact(db,key,0);assert scope_status(db,key)==410
    unchanged(db,lambda:scope_admit(db,'t','e',ZERO),'idempotency-expired')
    ukey=scope_usage('t',decode(db.read(key,key))['shard']);u=decode(db.read(ukey,ukey));u['used']=1;db.write(ukey,canonical(u),ukey,db.read(ukey,ukey))
    unchanged(db,lambda:scope_expire(db,key,30*DAY),'refund-underflow');u['used']=4096;db.write(ukey,canonical(u),ukey,db.read(ukey,ukey));scope_expire(db,key,30*DAY);scope_expire(db,key,30*DAY);assert scope_admit(db,'t','e',hashbytes(b'new'))==key
    db=newdb();discover(db,'placeholder');db=db.restart();unchanged(db,lambda:reconcile_placeholder(db,'placeholder'),'authority-readback');authority(db,'placeholder','no-commit-no-effect',create=True);reconcile_placeholder(db,'placeholder');assert not registry(db)['rows']
    assert inventory_actor('tenant','deployment')!=inventory_actor('deployment','deployment')
    discover(db,'slot');unchanged(db,lambda:discover(db,'too-many',scope_limit=1),'registry_capacity_hold');authority(db,'slot','no-commit-no-effect',create=True);reconcile_placeholder(db,'slot');discover(db,'too-many',scope_limit=1)
    # Owner churn permits cursor continuation; registry changes alone invalidate it.
    db=newdb();execution_init(db,'e1');execution_init(db,'e2');page,cursor=inventory_page(db,'tenant','t',1);assert page[0]['resumeHandle'] and resolve_handle(db,'t',page[0]['resumeHandle'])=='e1'
    unchanged(db,lambda:inventory_page(db,'deployment','t',1,cursor),'hold_inventory_cursor_scope_mismatch');unchanged(db,lambda:inventory_page(db,'tenant','t',1,cursor,900*TICK),'hold_inventory_cursor_expired')
    bad=copy.deepcopy(cursor);bad['signature']=ZERO;unchanged(db,lambda:inventory_page(db,'tenant','t',1,bad),'hold_inventory_cursor_invalid')
    d=getcontrol(db,'e1','execution');d['updatedUtc']=TICK;savecontrol(db,'e1','execution',d);page,end=inventory_page(db,'tenant','t',1,cursor);assert page[0]['subject']=='e2' and end is None
    db.unavailable.add('e1');page,_=inventory_page(db,'tenant','t');assert len(page)==2 and page[0]['stale'] and page[0]['phase']=='incident';db.unavailable.clear()
    discover(db,'e3');unchanged(db,lambda:inventory_page(db,'tenant','t',1,cursor),'hold_inventory_generation_changed')
    # Concurrent decision predecessor loses rather than overwriting a newer control.
    db=newdb();execution_init(db)
    def concurrent(t):
        d=getcontrol(t,'execution','execution');d['updatedUtc']=TICK;savecontrol(t,'execution','execution',d)
    db.before_transaction=concurrent;refused(lambda:begin_resume(db),'cas');assert getcontrol(db,'execution','execution')['request'] is None

def capture_replacement_redrive_case():
    db=newdb();held_init(db);authority(db,'held','erase',create=True);original_external=db.external;replacement={}
    def raced(address,body,owner):
        result=original_external(address,body,owner)
        if address.startswith('held/'):
            db.external=original_external;erase_held(db);held_init(db);capture(db,b'exact carrier')
            try:redrive(db,crash='commit')
            except Crash:pass
            replacement['snapshot']=db.snapshot()
        return result
    db.external=raced;refused(lambda:capture(db,b'exact carrier'),'capture-predecessor')
    assert db.snapshot()==replacement['snapshot'],'stale-capture-preserves-redriving-replacement'
    db=db.restart();d=getcontrol(db,'held','held')
    assert d['phase']=='redriving' and d['redrives']==1 and d['intent']['kind']=='send','replacement-redrive-commit'
    assert verify_retained(db,d,'held')==b'exact carrier','redriving-replacement-object-preserved'

def legacy_restore_token_cases():
    for transfer_kind in ('generation','fence'):
        db=newdb();events=[{'sequence':1,'message':'original','digest':ZERO}]
        db.external('drain',drain_fixture(events,'success-events'),'legacy');capsule_make(db,events,cleanup=True)
        begin_resume(db);finish_resume(db,0);legacy_transition(db,'execution','claimed',1)
        capsule_rows={k:copy.deepcopy(r) for k,r in db.rows.items() if k.startswith('legacy-resume-capsule')}
        original_external=db.external;address=provider_address('live-drain','execution');transferred={}
        def transferred_effect(target,body,owner):
            result=original_external(target,body,owner)
            if target==address:
                row=db.rows['execution']
                if transfer_kind=='generation':row['generation']+=1
                else:row['fence']=mint_fence()
                row['receipt']=Store.receipt('execution',row['body'],row['generation'],row['owner'],row.get('index'),row['fence'])
                transferred.update(snapshot=db.snapshot(),body=body,token=(row['generation'],row['fence']))
            return result
        db.external=transferred_effect;refused(lambda:legacy_transition(db,'execution','draining',1),'cas')
        assert db.snapshot()==transferred['snapshot'],'stale-legacy-restore-preserves-transferred-owner'
        pending=getcontrol(db,'execution','execution')
        assert pending['phase']=='cleanup' and pending['legacy']['state']=='claimed' and pending['intent']['address']==address,'stale-legacy-restore-intent-retained'
        assert db.read(address,'execution')==transferred['body'],'stale-legacy-restore-effect-retained'
        db=db.restart();assert (db.rows['execution']['generation'],db.rows['execution']['fence'])==transferred['token'],'legacy-transfer-token-persisted'
        legacy_transition(db,'execution','draining',1);done=getcontrol(db,'execution','execution')
        assert done['phase']=='idle' and done['intent'] is None and done['legacy']['state']=='draining' and done['legacy']['generation']==pending['legacy']['generation']+1,'fresh-legacy-holder-completes'
        assert db.read(address,'execution')==transferred['body'] and legacy_authority(db,done)[1]==events,'fresh-legacy-holder-original-readback'
        assert {k:r for k,r in db.rows.items() if k.startswith('legacy-resume-capsule')}==capsule_rows,'legacy-restore-preserves-capsule'
        db=db.restart();before=db.snapshot();legacy_transition(db,'execution','draining',1);assert db.snapshot()==before,'legacy-restore-completes-once'

def inventory_continuation_expiry_case():
    db=newdb()
    for i in range(1,5):execution_init(db,'expiry-'+str(i))
    cursor=None;expiry=900*TICK
    for i,now in enumerate((0,300*TICK,899*TICK),1):
        db=db.restart();before=db.snapshot();page,cursor=inventory_page(db,'tenant','t',1,cursor,now)
        assert db.snapshot()==before and [v['subject'] for v in page]==['expiry-'+str(i)],'three-page-inventory-order'
        assert cursor is not None and cursor['payload']['expiry']==expiry,'continuation-preserves-original-expiry'
    unchanged(db,lambda:inventory_page(db,'tenant','t',1,cursor,expiry),'hold_inventory_cursor_expired')

def queue_shard_placement_case():
    db=newdb();sources={};subjects={}
    for ticket in range(1,17):
        subject=hashbytes(('shard-ticket-'+str(ticket)).encode());pins=[{'message':'pin-'+str(ticket),'length':1}]
        sources[subject]=pins;subjects[ticket]=subject
        assert queue_reserve(db,subject,'t',ZERO,0)==ticket,'sixteen-global-tickets'
        operation_plan_fixture(db,subject,pins);queue_materialize(db,subject,pins,0);db=db.restart()
    hk=queue_key();header=typed('queue',db.read(hk,hk))
    assert header['lastTicket']==16 and header['count']==16,'persisted-queue-header'
    shard_keys=[queue_shard_key(shard) for shard in range(8)];assert len(set(shard_keys))==8,'eight-addressed-shards'
    for shard,key in enumerate(shard_keys):
        image=typed('queue-shard',db.read(key,key));expected=[shard+1,shard+9]
        assert image['deployment']==header['deployment'] and image['shard']==shard and [r['ticket'] for r in image['rows']]==expected,'persisted-addressed-shard-placement'
        assert all(r['subject']==subjects[r['ticket']] and r['state']=='queued' for r in image['rows']),'persisted-shard-owner'
    grants=[]
    for ticket in range(1,17):
        db=db.restart();grants.append(queue_turn(db,sources));header=typed('queue',db.read(hk,hk))
        assert grants[-1]==ticket and header['lastTicket']==16 and header['count']==16-ticket,'global-grant-order-across-shards'
        assert ledger_get(db)['reservations'][subjects[ticket]]['pins']==sources[subjects[ticket]],'persisted-global-grant'
    assert grants==list(range(1,17)) and all(typed('queue-shard',db.read(key,key))['rows']==[] for key in shard_keys),'sixteen-grants-drain-eight-shards'

def retention_readiness_boundary_cases():
    # Independent literal precharge: eight 100 MiB shards, queue header, 256
    # registry headers, epoch, and 266 recorded provider overheads.
    overhead=37;quarantine=193*MIB
    fixed=800*MIB+16384+256*16384+16384+266*overhead
    tenant=1024*MIB+216*1024+16384+5*overhead
    reserve=fixed+quarantine+overhead;deploy=tenant+reserve
    assert retention_capability(tenant,deploy,reserve,reserve,overhead,quarantine)==fixed,'readiness-exact-bootstrap-precharge-fit'
    for args in ((tenant-1,deploy,reserve,reserve),(tenant,deploy-1,reserve,reserve),(tenant,deploy,reserve-1,reserve),(tenant,deploy,reserve,reserve-1)):
        refused(lambda:retention_capability(*args,overhead,quarantine),'publication_retention_capability_invalid')

def correction_cases():
    exercised=[]
    def done(label):exercised.append(label)
    def legacy_ready():
        db=newdb();events=[{'sequence':1,'message':'original','digest':ZERO}]
        db.external('drain',drain_fixture(events,'success-events'),'legacy');capsule_make(db,events,cleanup=True)
        return db,events
    def held_ready():
        db=newdb();held_init(db);capture(db,b'exact carrier')
        try:redrive(db,crash='commit')
        except Crash:pass
        return db
    # Approved stage headroom choice and actual staged refunds on explicit/expiry cancel.
    db=newdb();change_ledger(db,tenantCeiling=6*MIB);execution_init(db);begin_resume(db);finish_resume(db,0)
    d=getcontrol(db,'execution','execution');d.update(source=hashbytes(b'second'),reason='retry-exhausted');savecontrol(db,'execution','execution',d)
    unchanged(db,lambda:begin_resume(db,'second'),'resume_capacity_hold');done('RD1-stage-headroom')
    for expiry in (False,True):
        db=newdb();execution_init(db);begin_resume(db);rid=getcontrol(db,'execution','execution')['request']['identity'];stage=charge_address('tenant','t','stage:'+rid)
        if expiry:finish_resume(db,901*TICK)
        else:
            try:resume_step(db,0,crash='intent')
            except Crash:pass
            cancel_resume(db)
        assert stage not in ledger_get(db)['charges'] and getcontrol(db,'execution','execution')['ordinal']==0,'stage-refund'
    done('RP7-expiry-and-RP16-stage-refunds')
    # Signed foreign scope/request/time must fail their owning binding before any effect.
    for tag,value,why in ((1,'foreign','resume-claim-owner'),(10,ZERO,'resume-claim-request'),(13,1,'resume-claim-time')):
        db=newdb();execution_init(db);begin_resume(db);d=getcontrol(db,'execution','execution');f=read_record(hexbytes(d['request']['claim']),ANSWERS['records']['D45-request']);f[tag][1]=value
        raw=record(ANSWERS['records']['D45-request']['domain'],f);d['request'].update(claim=raw.hex(),signature=fixture_sign(raw));savecontrol(db,'execution','execution',d)
        unchanged(db,lambda:resume_step(db,0),why)
    db=newdb();execution_init(db,accepted=[1,2,3]);unchanged(db,lambda:begin_resume(db),'resume_not_eligible');done('RP16-signed-resume-and-zero-unresolved')
    # Dispatch excludes acceptance occurring after invocation admission; old handle resolves.
    db=newdb();execution_init(db);oldhandle=resume_handle(getcontrol(db,'execution','execution'));begin_resume(db);finish_resume(db,0)
    d=getcontrol(db,'execution','execution');inv=next(decode(hexbytes(row['body'])) for key,row in db.rows.items() if key.startswith('publication-invocation:'))
    d.update(accepted=[1,2],unresolved=[3]);savecontrol(db,'execution','execution',d)
    assert invocation_messages(db,d,inv)==['event-3'],'accepted-dispatch-exclusion'
    assert resolve_handle(db,'t',oldhandle)=='execution','retained-handle-resolution'
    outcome=d['outcomes'][0];retained=set(outcome['artifacts']);active=window_address(d)
    assert retained=={a for a,r in db.rows.items() if r['owner']=='execution' and a!='execution'},'complete-retained-artifact-set'
    reclaim(db,outcome['deleteAfter']);assert all(db.read(a,'execution') is None for a in retained if a!=active) and db.read(active,'execution') is not None,'retained-artifact-deletion'
    done('RP17-dispatch-handle-artifact-reclamation')
    # Capsule reentry cannot reset a claim and cannot disturb pending resume.
    db,events=legacy_ready();begin_resume(db);finish_resume(db,0);legacy_transition(db,'execution','claimed',1)
    db.external('drain',drain_fixture(events,'success-events'),'legacy');before=db.snapshot();capsule_make(db,events);assert db.snapshot()==before,'capsule-idempotent-owner'
    db,events=legacy_ready();begin_resume(db);unchanged(db,lambda:capsule_make(db,events),'capsule-request-pending');done('RP5-capsule-reentry')
    # Before-success/stale reclaim, forged success-audit, and owner-bound audit readback.
    db,_=legacy_ready();unchanged(db,lambda:legacy_transition(db,'execution','claimed',1),'legacy-reclaim')
    begin_resume(db);finish_resume(db,0);unchanged(db,lambda:legacy_transition(db,'execution','claimed',0),'legacy-reclaim')
    d=getcontrol(db,'execution','execution');r=d['outcomes'][0];address=action_address(d,r,'audit');original=db.read(address,'execution')
    db.delete(address,'execution');unchanged(db,lambda:legacy_transition(db,'execution','claimed',1),'legacy-success-audit')
    f=read_record(original,ANSWERS['records']['D45-audit']);f[1][1]='foreign';forged=record(ANSWERS['records']['D45-audit']['domain'],f);db.external(address,forged,'execution');r['audit']=hashbytes(forged);d['outcomes'][0]=r;savecontrol(db,'execution','execution',d)
    unchanged(db,lambda:legacy_transition(db,'execution','claimed',1),'legacy-success-owner');done('RP16-17-legacy-success-authority')
    # Authenticated failure class overrides callers, and each failure consumes new repair proof.
    db,events=legacy_ready();begin_resume(db);finish_resume(db,0);legacy_transition(db,'execution','claimed',1);legacy_transition(db,'execution','draining',1)
    unchanged(db,lambda:legacy_transition(db,'execution','failed',1),'legacy-failure-readback')
    legacy_failure_fixture(db,'evidence-contradictory');unchanged(db,lambda:legacy_transition(db,'execution','failed',1,failure='transport-retryable'),'legacy-failure-class')
    try:legacy_transition(db,'execution','failed',1,crash='effect')
    except Crash:pass
    db=db.restart();legacy_transition(db,'execution','failed',1);begin_resume(db,'new');finish_resume(db,0)
    d=getcontrol(db,'execution','execution');address=legacy_repair_address('execution',d['legacy']);proof=canonical(legacy_repair_payload(d['legacy'],legacy_authority(db,d)[0]));db.external(address,proof,'fixture-authority')
    try:legacy_transition(db,'execution','claimed',2,crash='effect')
    except Crash:pass
    db=db.restart();legacy_transition(db,'execution','claimed',2);assert db.read(address,'fixture-authority') is None,'consumed-repair-proof'
    legacy_transition(db,'execution','draining',2);legacy_failure_fixture(db,'evidence-unavailable');legacy_transition(db,'execution','failed',2);begin_resume(db,'third');finish_resume(db,0)
    db.external(address,proof,'fixture-authority');unchanged(db,lambda:legacy_transition(db,'execution','claimed',3),'legacy-repair-readback');done('RD2-RP6-generation-repair-and-failure-intent')
    # Unknown authenticated failure reason is closed; observed/completion readbacks own exits.
    db,_=legacy_ready();begin_resume(db);finish_resume(db,0);legacy_transition(db,'execution','claimed',1);legacy_transition(db,'execution','draining',1)
    legacy_failure_fixture(db,'fabricated');unchanged(db,lambda:legacy_transition(db,'execution','failed',1),'legacy-failure')
    unchanged(db,lambda:legacy_transition(db,'execution','completed',1),'legacy-completion-readback')
    db=newdb();held_init(db);unchanged(db,lambda:redrive_reconcile(db),'route-terminal-readback');done('RP17-closed-failure-and-terminal-readbacks')
    # Original drain and capsule source tamper, unavailable authority, and bound 1001.
    db=newdb();events=[{'sequence':1,'message':'a','digest':ZERO}];db.external('drain',drain_fixture(events,'success-events'),'legacy')
    unchanged(db,lambda:capsule_make(db,events,'rejection-events'),'legacy-drain-authority')
    changed=[{**events[0],'digest':hashbytes(b'changed')}];unchanged(db,lambda:capsule_make(db,changed),'legacy-drain-authority')
    db.unavailable.add('drain');unchanged(db,lambda:capsule_make(db,events),'evidence-unavailable');db.unavailable.clear();db.delete('drain','legacy');unchanged(db,lambda:capsule_make(db,events),'legacy_resume_evidence_unavailable')
    large=[{'sequence':i+1,'message':str(i),'digest':ZERO} for i in range(1001)];db.external('drain',drain_fixture(large,'success-events'),'legacy');unchanged(db,lambda:capsule_make(db,large),'legacy-range-bound')
    db,stored_events=legacy_ready();d=getcontrol(db,'execution','execution');key=keyed('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2','legacy-resume-capsule:',[['B32',d['legacy']['identity']]])
    different=[{**stored_events[0],'digest':hashbytes(b'changed-stored-digest')}];unchanged(db,lambda:capsule_restore(db,different,key),'legacy-original-range')
    raw=db.read(key,'legacy');f=read_record(raw,ANSWERS['records']['D46-capsule']);f[14][1]=hashbytes(b'tamper');db.write(key,record(ANSWERS['records']['D46-capsule']['domain'],f),'legacy',raw)
    unchanged(db,lambda:begin_resume(db),'legacy-capsule-binding');done('RP16-17-legacy-source-and-1001-bound')
    db,_=legacy_ready();begin_resume(db);finish_resume(db,0);legacy_transition(db,'execution','claimed',1)
    db.external('drain',b'original still present','legacy');unchanged(db,lambda:legacy_transition(db,'execution','draining',1),'legacy-live-drain');done('RP17-drain-absence')
    # Terminal/no-send and forged/unavailable route sources on actual send path.
    db=held_ready();route_fixture(db);unchanged(db,lambda:send_held(db),'terminal-no-send')
    address=provider_address('route-terminal','held');raw=db.read(address,'held');db.delete(address,'held');bad=decode(raw);bad['requestHash']=ZERO;db.external(address,canonical(bad),'held')
    unchanged(db,lambda:send_held(db),'route-authority');db.unavailable.add(address);unchanged(db,lambda:send_held(db),'evidence-unavailable');done('RP16-17-terminal-send-authority')
    # Absence repair retains original route hash; re-signing time cannot substitute it.
    db=held_ready();route_fixture(db,outcome='nonterminal');d=getcontrol(db,'held','held');original=copy.deepcopy(d['request']);d.update(request=None,attempt=None);savecontrol(db,'held','held',d);redrive_reconcile(db)
    assert getcontrol(db,'held','held')['repair']=='absent','absent-repair-state';repair_held(db,claim=original)
    d=getcontrol(db,'held','held');d['repair']='required';savecontrol(db,'held','held',d);f=read_record(hexbytes(original['claim']),ANSWERS['records']['D36-redrive']);f[6][1]=1;raw=record(ANSWERS['records']['D36-redrive']['domain'],f);changed={**original,'claim':raw.hex(),'signature':fixture_sign(raw),'utc':1}
    unchanged(db,lambda:repair_held(db,claim=changed),'repair-original-request');done('RP11-RP16-original-absent-repair')
    for tag,value,why in ((2,'foreign','redrive-claim-owner'),(4,1,'redrive-claim-count')):
        db=held_ready();d=getcontrol(db,'held','held');f=read_record(hexbytes(d['request']['claim']),ANSWERS['records']['D36-redrive']);f[tag][1]=value;raw=record(ANSWERS['records']['D36-redrive']['domain'],f);d['request'].update(claim=raw.hex(),signature=fixture_sign(raw));d['attempt']['requestHash']=hashbytes(raw);savecontrol(db,'held','held',d)
        unchanged(db,lambda:send_held(db),why)
    db=held_ready();d=getcontrol(db,'held','held');key=charge_address(*held_account(d),'metadata:held');b=db.read('ledger','ledger');ledger=ledger_get(db);ledger['charges'][key]['amount']+=1;db.write('ledger',canonical(ledger),'ledger',b)
    unchanged(db,lambda:send_held(db),'metadata-charge');done('RP16-held-signed-tags-and-metadata-charge')
    # Capture erase/recreation races: late orphan deleted, valid replacement preserved.
    for replacement in (False,True):
        db=newdb();held_init(db);authority(db,'held','erase',create=True);original_external=db.external
        def raced(address,body,owner):
            result=original_external(address,body,owner)
            if address.startswith('held/'):
                db.external=original_external;erase_held(db)
                if replacement:held_init(db);capture(db,b'exact carrier')
            return result
        db.external=raced;refused(lambda:capture(db,b'exact carrier'),'capture-predecessor')
        if replacement:assert verify_retained(db,getcontrol(db,'held','held'),'held')==b'exact carrier','replacement-capture-preserved'
        else:assert not registry(db)['rows'] and not ledger_get(db)['charges'] and not any(k.startswith('held/') for k in db.rows),'late-capture-orphan'
    for transfer_kind in ('generation','fence'):
        db=newdb();held_init(db);original_external=db.external
        def transferred_write(address,body,owner):
            result=original_external(address,body,owner)
            if address.startswith('held/'):
                row=db.rows['held']
                if transfer_kind=='generation':row['generation']+=1
                else:row['fence']=mint_fence()
                row['receipt']=Store.receipt('held',row['body'],row['generation'],row['owner'],row.get('index'),row['fence'])
            return result
        db.external=transferred_write;refused(lambda:capture(db,b'exact carrier'),'capture-predecessor')
        d=getcontrol(db,'held','held');assert d['phase']=='capturing' and db.read(held_object(d),'held')==b'exact carrier','stale-capture-token-preserves-owner'
        db.external=original_external;capture(db,b'exact carrier');assert verify_retained(db,getcontrol(db,'held','held'),'held')==b'exact carrier'
    done('RP12-capture-erase-and-recreation')
    # Refund cannot erase charges while an addressed object still exists.
    db=newdb();db.external('charged-object',b'exact','object');reserve_charge(db,'object-charge','tenant','t',5,objects=('charged-object',));unchanged(db,lambda:refund(db,'object-charge'),'delete-readback')
    db=newdb();discover(db,'placeholder');authority(db,'placeholder','no-commit-no-effect',create=True);db.external('effect',b'existing','placeholder')
    unchanged(db,lambda:reconcile_placeholder(db,'placeholder'),'placeholder-effect');unchanged(db,lambda:registry_release(db,'placeholder'),'owner-deletion-readback');done('RP17-refund-and-placeholder-readbacks')
    # Scope widths, shard-local capacity and independent deployment account.
    for kind in ('registry-scope','registry-entry','epoch'):
        d=copy.deepcopy(ANSWERS['controls'][kind]['json'])
        for field in (('deployment','scopeId') if kind!='epoch' else ('deployment','holder')):d[field]='\x01'*1024
        typed(kind,canonical(d));assert len(canonical(d))<=CAPS[kind],'escaped-width-cap'
    db=newdb();discover(db,'first',scope='t');other=next('s'+str(i) for i in range(10000) if registry_shard('tenant','s'+str(i))==registry_shard('tenant','t'))
    unchanged(db,lambda:discover(db,'second',scope=other,shard_limit=1),'registry_capacity_hold')
    foreign=next('f'+str(i) for i in range(10000) if registry_shard('tenant','f'+str(i))!=registry_shard('tenant','t'));discover(db,'foreign',scope=foreign,shard_limit=1)
    discover(db,'deployment-owner',kind='deployment',scope='dep');key=registry_key('deployment','dep','deployment-owner');assert ledger_get(db)['charges'][charge_address('capture-scope','dep',key)]['account']=='capture-scope:dep','deployment-registry-account'
    limited=newdb();change_ledger(limited,tenantCeiling=1);unchanged(limited,lambda:discover(limited,'quota-full'),'registry_capacity_hold')
    widest='\x01'*1024;discover(db,'max-width',scope=widest);assert typed('registry-scope',db.read(registry_scope_key('tenant',widest),registry_scope_key('tenant',widest)))['scopeId']==widest,'maximum-scope-discovery'
    done('RP4-RP13-RP16-17-registry-bounds-and-accounts')
    # Foreign unavailable entries cannot block discovery/paging; corrupt owner is per row.
    db=newdb();execution_init(db,'one');execution_init(db,'two');_,cursor=inventory_page(db,'tenant','t',1);discover(db,'foreign',scope='other');foreignkey=registry_key('tenant','other','foreign');db.unavailable.add(foreignkey);discover(db,'another',scope='third')
    page,_=inventory_page(db,'tenant','t',1,cursor);assert page[0]['subject']=='two','foreign-cursor-isolation'
    d=getcontrol(db,'one','execution');d['reason']='fabricated-reason';db.write('one',canonical(d),'one',db.read('one','one'));db.write('two',canonical([]),'two',db.read('two','two'));page,_=inventory_page(db,'tenant','t');assert len(page)==2 and all(v['stale'] and v['phase']=='incident' for v in page),'typed-owner-incidents'
    unchanged(db,lambda:inventory_page(db,'tenant','t',cursor={}),'hold_inventory_cursor_invalid');done('RP4-RP8-RP19-isolated-typed-inventory')
    db=newdb();execution_init(db,'one');execution_init(db,'two');_,cursor=inventory_page(db,'tenant','t',1);d=getcontrol(db,'two','execution');d['updatedUtc']=1;savecontrol(db,'two','execution',d);page,_=inventory_page(db,'tenant','t',1,cursor);assert page[0]['revision']==d['revision']+1,'fresh-page-revision';done('RP17-page-revision')
    # Full target shard refuses without consuming an allocator ticket or charge.
    db=newdb();subject=hashbytes(b'full-shard');before=db.snapshot();unchanged(db,lambda:queue_reserve(db,subject,'t',ZERO,0,shard_limit=0),'AppendPreparationLimit');assert queue_view(db)['lastTicket']==0 and db.snapshot()==before,'refused-ticket-consumption'
    # Missing/mismatched admitted plans and conflicting existing batches park only that subject.
    for badplan in ('absent','mismatch','batch-conflict','wait-charge'):
        db=newdb();sources={}
        for i in (1,2):
            subject=hashbytes((badplan+str(i)).encode());pins=[{'message':'p'+str(i),'length':1}];sources[subject]=pins;queue_reserve(db,subject,str(i),ZERO,0);operation_plan_fixture(db,subject,pins);queue_materialize(db,subject,pins,0)
        first=queue_view(db)['rows'][0];address=provider_address('operation-plan',first['owner'],first['plan'])
        if badplan=='absent':db.delete(address,'fixture-operation')
        elif badplan=='mismatch':db.write(address,canonical([{'message':'foreign','length':1}]),'fixture-operation',db.read(address,'fixture-operation'))
        elif badplan=='batch-conflict':batch(db,first['subject'],first['tenant'],[{'message':'foreign','length':1}])
        else:
            prior=db.read('ledger','ledger');ledger=ledger_get(db);ledger['charges'][first['charge']]['length']+=1;db.write('ledger',canonical(ledger),'ledger',prior)
        assert queue_turn(db,sources)==2 and queue_view(db)['rows'][0]['state']=='parked','selected-refusal-parks'
    db=newdb();subject=hashbytes(b'no-authority');queue_reserve(db,subject,'t',ZERO,0);unchanged(db,lambda:queue_materialize(db,subject,[{'message':'p','length':1}],0),'rerender-authority')
    done('RD4-RP9-RP10-RP17-queue-shards-and-authority')
    # Owning 193 MiB+1 capture refuses unchanged before an object allocation/write.
    class OversizeCarrier:
        def __len__(self):return 193*MIB+1
    db=newdb();held_init(db);d=getcontrol(db,'held','held');d['length']=193*MIB+1;db.write('held',canonical(d),'held',db.read('held','held'))
    unchanged(db,lambda:capture(db,OversizeCarrier()),'capture-carrier');unchanged(db,lambda:verify_retained(db,getcontrol(db,'held','held'),'held'),'retained-interval');done('RP17-retained-object-interval')
    # Every transaction consumes its captured predecessor; concurrent updates survive CAS loss.
    for path in ('capture','queue_turn','reclaim','cancel'):
        db=newdb()
        if path=='capture':held_init(db);key='held';kind='held';action=lambda:capture(db,b'exact carrier')
        elif path=='queue_turn':
            subject=hashbytes(b'casqueue');pins=[{'message':'p','length':1}];queue_reserve(db,subject,'t',ZERO,0);operation_plan_fixture(db,subject,pins);queue_materialize(db,subject,pins,0)
            def advance(t):queue_save(t,queue_view(t))
            db.before_transaction=advance;refused(lambda:queue_turn(db,{subject:pins}),'cas');assert queue_view(db)['rows'];continue
        else:
            execution_init(db);begin_resume(db);key='execution';kind='execution'
            if path=='reclaim':finish_resume(db,0);action=lambda:reclaim(db,32*DAY)
            else:action=lambda:cancel_resume(db)
        def advance(t):
            d=getcontrol(t,key,kind);d['updatedUtc']=1;savecontrol(t,key,kind,d)
        db.before_transaction=advance;refused(action,'cas');assert getcontrol(db,key,kind)['updatedUtc']==1,'concurrent-predecessor-preserved'
    done('RP17-all-predecessor-CAS')
    # Malformed scope authority maps to the closed public admission hold.
    for raw in (canonical([]),b'not-json'):
        db=newdb();key=scope_admit(db,'t','e',ZERO);db.write(key,raw,key,db.read(key,key));unchanged(db,lambda:scope_admit(db,'t','e',ZERO),'admission_evidence_hold')
    done('RP18-scope-public-reasons')
    for transfer_kind in ('generation','fence','both'):
        db=newdb();execution_init(db);transferred={}
        def transfer(t):
            row=t.rows['execution']
            if transfer_kind!='fence':row['generation']+=1
            if transfer_kind!='generation':row['fence']=mint_fence()
            row['receipt']=Store.receipt('execution',row['body'],row['generation'],row['owner'],row.get('index'),row['fence']);transferred.update(snapshot=t.snapshot())
        db.before_transaction=transfer;refused(lambda:begin_resume(db),'cas');assert db.snapshot()==transferred['snapshot'] and getcontrol(db,'execution','execution')['phase']=='idle','stale-fence-refusal'
    for transfer_kind in ('generation','fence'):
        db=newdb();execution_init(db);begin_resume(db);original_external=db.external
        def transferred_effect(address,body,owner):
            result=original_external(address,body,owner);row=db.rows['execution']
            if transfer_kind=='generation':row['generation']+=1
            else:row['fence']=mint_fence()
            row['receipt']=Store.receipt('execution',row['body'],row['generation'],row['owner'],row.get('index'),row['fence']);return result
        db.external=transferred_effect;refused(lambda:resume_step(db,0),'cas');d=getcontrol(db,'execution','execution')
        assert d['phase']=='prepared' and d['intent']['kind']=='disable' and db.read(d['intent']['address'],'execution') is not None,'stale-resume-effect-retained'
        db=db.restart();finish_resume(db,901*TICK);assert getcontrol(db,'execution','execution')['ordinal']==1,'fresh-holder-finishes-original-effect'
    done('RD3-provider-generation-and-fence-CAS')
    capture_replacement_redrive_case();done('RP12-redriving-replacement-retention')
    legacy_restore_token_cases();done('RD3-legacy-restore-provider-token')
    inventory_continuation_expiry_case();done('RP19-three-page-original-cursor-expiry')
    queue_shard_placement_case();done('RD4-sixteen-ticket-eight-shard-placement')
    retention_readiness_boundary_cases();done('RP16-bootstrap-precharge-boundaries')
    COUNTS['correctionEvidence']=exercised

def scenarios():
    COUNTS.clear();COUNTS['matrixEvidence']={};codec_cases();status_replay_cases();resume_cases();legacy_cases();held_cases();queue_scope_inventory_cases()
    correction_cases();COUNTS['matrixRows']=len(COUNTS['matrixEvidence'])
    return COUNTS

def dispositions():
    text=(HERE/'obligations.md').read_text();section=text.split('<!-- pass2-dispositions-start -->')[1].split('<!-- pass2-dispositions-end -->')[0]
    ids=re.findall(r'^\| ((?:VG2|BH2|E2)-[^ |]+) \|',section,re.M)
    expected={'VG2-'+x for x in ['2','3','4','O1','O2','O3']}|{'BH2-'+str(i) for i in [1,2,3,4,5,6,7,8,9,10,16,18,19]}|{'E2-'+str(i) for i in list(range(1,27))+[28,29,30,31,32,34,35,36,38]}
    assert len(ids)==54 and len(set(ids))==54 and set(ids)==expected,'54 routed dispositions'
    assert 'obsolete' in text and 'provider' in text
    return len(ids)

def main():
    counts=scenarios();routed=dispositions()
    print(json.dumps({'result':'passed','wireAnswers':len(ANSWERS['records']),'keyAnswers':len(ANSWERS['keys']),'sharedKeyAnswers':len(ANSWERS['sharedKeys']),'controlAnswers':len(ANSWERS['controls']),'routedDispositions':routed,'behaviorEvidence':counts,'authority':'fixture authorization and transactions only; D9 provider gate unproved'},sort_keys=True))

if __name__=='__main__':main()
