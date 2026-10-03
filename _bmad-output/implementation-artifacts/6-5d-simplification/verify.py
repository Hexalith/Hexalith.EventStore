#!/usr/bin/env python3
"""Executable documentation model. Fixture auth/transactions are not provider proof."""
import copy
import hashlib
import json
import re
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
MAX = 2**64 - 1
MIB = 1024**2
TICK = 10_000_000
DAY = 86400 * TICK
ZERO = '00' * 32
CAPS = {'execution': 768*1024, 'held': 128*1024, 'queue': 800*MIB+16384,
        'registry': 1024*MIB, 'cursor': 16384}
FIELDS = {
 'execution': 'schema tenant execution scope revision phase firstUtc updatedUtc source window windowClaim closedCount history ordinal limit drainBase roster accepted unresolved legacy request intent receipts outcomes reason nextUtc charge',
 'held': 'schema scopeKind scopeId deployment tenant component topic subscription policy revision phase reason firstUtc updatedUtc observations length carrier locator objectReceipt charge request attempt redrives nextUtc repair error intent receipts',
 'queue': 'schema deployment generation lastTicket rows',
 'registry': 'schema deployment shard generation rows',
 'cursor': 'schema scopeKind scopeId generation last expiry',
}
SCHEMAS = {k: 'hexalith.eventstore.'+v+'/1' for k,v in
           [('execution','execution-control'),('held','held-control'),
            ('queue','capacity-queue'),('registry','owner-registry'),('cursor','hold-cursor')]}

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
    return json.dumps(x, ensure_ascii=False, sort_keys=True, separators=(',',':'),
                      allow_nan=False).encode('utf-8')

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
    need(type(s) is str and 1 <= len(s.encode('utf-8')) <= cap, 'identifier')
    return s

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
        v=bytes.fromhex(v)
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
    need(len(b)<=CAPS[kind], 'control-byte-bound')
    d=decode(b); exact(d,FIELDS[kind]); need(d['schema']==SCHEMAS[kind],'schema')
    if kind=='execution':
        for f in ('tenant','execution'):textfield(d[f])
        for f in ('scope','source','windowClaim','history'):digest(d[f])
        for f in ('revision','window','closedCount','ordinal','limit','drainBase'):integer(d[f])
        need(d['phase'] in 'idle prepared disable reject closure window audit successor finalize invoke cleanup incident'.split(),'execution-phase')
        roster=d['roster'];need(type(roster) is list and len(roster)<=59,'roster-bound')
        for r in roster:
            exact(r,'position message digest');integer(r['position']);need(0<r['position']<2**32,'position');textfield(r['message']);digest(r['digest'])
        need(roster==sorted(roster,key=lambda r:r['position']), 'roster-order')
        need(len({r['position'] for r in roster})==len(roster) and len({r['message'] for r in roster})==len(roster),'roster-unique')
        a,u=d['accepted'],d['unresolved']
        need(type(a) is list and type(u) is list,'partition-shape')
        for n in a+u:integer(n);need(0<n<2**32,'partition-position')
        need(a==sorted(set(a)) and u==sorted(set(u)) and not set(a)&set(u)
             and set(a)|set(u)=={r['position'] for r in roster},'partition')
        need(type(d['outcomes']) is list and len(d['outcomes'])<=64,'outcome-bound')
        need(len({r['identity'] for r in d['outcomes']})==len(d['outcomes']),'outcome-identity')
        need([r['ordinal'] for r in d['outcomes']]==sorted({r['ordinal'] for r in d['outcomes']}),'outcome-order')
        for r in d['outcomes']:
            exact(r,'identity carrierHash ordinal window limit audit expiry deleteAfter result')
            for f in ('identity','carrierHash','audit'):digest(r[f])
            for f in ('ordinal','window','limit'):integer(r[f])
            utc(r['expiry']);utc(r['deleteAfter']);need(r['deleteAfter']==r['expiry']+30*DAY,'fixed-horizon')
            need(len(bytes.fromhex(r['result']))<=512,'result-bound')
        if d['request'] is not None:
            exact(d['request'],'identity carrier claim signature utc expiry eligibility source priorHash result')
            for f in ('identity','source','priorHash'):digest(d['request'][f])
            need(len(bytes.fromhex(d['request']['carrier']))<=2048,'carrier-bound')
            need(len(bytes.fromhex(d['request']['claim']))<=4096 and len(bytes.fromhex(d['request']['signature']))<=8192,'claim-bound')
            need(d['request']['utc']<d['request']['expiry']<=d['request']['utc']+900*TICK,'request-horizon')
    elif kind=='held':
        need(d['scopeKind'] in ('tenant','deployment') and (d['tenant'] is not None)==(d['scopeKind']=='tenant'),'held-scope')
        for f in ('scopeId','deployment','component','topic','subscription'):textfield(d[f])
        if d['tenant'] is not None:textfield(d['tenant']);need(d['scopeId']==d['tenant'],'held-tenant')
        for f in ('policy','carrier'):digest(d[f])
        for f in ('revision','observations','length','redrives'):integer(d[f])
        need(d['phase'] in 'observed capturing captured redriving cleanup quarantined incident closed'.split(),'held-phase')
        need(d['repair'] in ('none','absent','corrupt','required','repaired'),'repair')
        if d['locator'] is not None:
            exact(d['locator'],'backend key');textfield(d['locator']['backend']);textfield(d['locator']['key'],4096)
        if d['phase'] in ('captured','redriving','quarantined','closed'):need(d['locator'] is not None and d['objectReceipt'] is not None,'retained-locator')
        if d['request'] is not None:
            exact(d['request'],'claim signature expectedCount utc')
            need(len(bytes.fromhex(d['request']['claim']))<=3072 and len(bytes.fromhex(d['request']['signature']))<=8192,'redrive-request-bound')
        if d['attempt'] is not None:
            exact(d['attempt'],'count requestHash carrier utc result');integer(d['attempt']['count']);digest(d['attempt']['requestHash']);digest(d['attempt']['carrier'])
    elif kind=='queue':
        textfield(d['deployment']);integer(d['generation']);integer(d['lastTicket']);need(len(d['rows'])<=50000,'queue-count')
        need([r['ticket'] for r in d['rows']]==sorted({r['ticket'] for r in d['rows']}),'queue-order')
        need(len({r['subject'] for r in d['rows']})==len(d['rows']),'queue-subject')
        for r in d['rows']:
            exact(r,'subject tenant scope plan ticket firstUtc updatedUtc state candidate amount attempts charge owner')
            need(len(canonical(r))<=16384,'queue-row-bound')
            for f in ('subject','scope','plan'):digest(r[f])
            textfield(r['tenant']);textfield(r['owner']);integer(r['attempts'])
            need(0<integer(r['ticket'])<=d['lastTicket'],'ticket')
            need(r['state'] in ('reserved','queued','parked','cleanup'),'queue-state')
            need((r['candidate'] is None)==(r['amount'] is None),'queue-candidate')
            if r['amount'] is not None:digest(r['candidate']);need(integer(r['amount'])>0,'queue-amount')
            need(r['updatedUtc']>=r['firstUtc'],'queue-clock')
    elif kind=='registry':
        integer(d['generation']);need(0<=d['shard']<256,'registry-shard')
        rows=d['rows'];need(len({(r['scopeKind'],r['scopeId']) for r in rows})<=50000,'registry-scopes')
        need(len({(r['scopeKind'],r['scopeId'],r['subject']) for r in rows})==len(rows),'registry-unique')
        need(rows==sorted(rows,key=registry_sort),'registry-order')
        for r in rows:
            exact(r,'scopeKind scopeId subject owner address firstUtc state')
            need(r['scopeKind'] in ('tenant','deployment'),'registry-scope');textfield(r['scopeId']);textfield(r['subject'],4096);textfield(r['address'],256);textfield(r['owner']);utc(r['firstUtc'])
            need(r['state'] in ('reserved','present','cleanup') and len(canonical(r))<=65536,'registry-row')
            need(sum(x['scopeKind']==r['scopeKind'] and x['scopeId']==r['scopeId'] for x in rows)<=10000,'registry-subject-count')
    else:
        need(d['scopeKind'] in ('tenant','deployment'),'cursor-scope');textfield(d['scopeId']);digest(d['generation']);utc(d['expiry'])
        if d['last'] is not None:
            need(type(d['last']) is list and len(d['last'])==4,'cursor-position');utc(d['last'][0]);need(d['last'][1:3]==[d['scopeKind'],d['scopeId']],'cursor-position-scope');textfield(d['last'][3],4096)
    if kind in ('execution','held'):
        for f in ('firstUtc','updatedUtc','nextUtc'):utc(d[f])
        need(d['updatedUtc']>=d['firstUtc'],'owner-clock')
        need(type(d['receipts']) is dict and len(d['receipts'])<=8,'receipt-count')
        for v in d['receipts'].values():textfield(v,256)
        if d['intent'] is not None:
            exact(d['intent'],'kind address hash payload');textfield(d['intent']['address'],256);digest(d['intent']['hash']);need(len(canonical(d['intent']['payload']))<=(2048 if kind=='held' else 128*1024),'intent-bound')
    return d

def registry_sort(r):
    return (r['firstUtc'],r['scopeKind'].encode(),r['scopeId'].encode(),r['subject'].encode())

class Store:
    """Byte-only restart with authenticated fixture readback and staged local CAS."""
    def __init__(self, wire=None):
        self.rows=decode(wire) if wire else {}
        self.unavailable=set()
    def snapshot(self):return canonical(self.rows)
    def restart(self):return Store(self.snapshot())
    def read(self,key,owner=None):
        need(key not in self.unavailable,'evidence-unavailable')
        row=self.rows.get(key)
        if row is None:return None
        exact(row,'body generation owner receipt')
        need(row['receipt']==self.receipt(key,row['body'],row['generation'],row['owner']),'authenticated-readback')
        if owner is not None:need(row['owner']==owner,'owner')
        return bytes.fromhex(row['body'])
    @staticmethod
    def receipt(key,body,generation,owner):
        return hashbytes(b'fixture-provider-only\0'+canonical([key,body,generation,owner]))
    def write(self,key,b,owner,expected):
        old=self.read(key,owner)
        need(old==expected,'cas')
        gen=add(self.rows[key]['generation'],1) if old is not None else 1
        self.rows[key]={'body':b.hex(),'generation':gen,'owner':owner,'receipt':self.receipt(key,b.hex(),gen,owner)}
    def external(self,key,b,owner):
        old=self.read(key,owner)
        need(old is None or old==b,'external-conflict')
        if old is None:self.write(key,b,owner,None)
        return hashbytes(self.read(key,owner))
    def delete(self,key,owner):
        self.read(key,owner)
        self.rows.pop(key,None)
    def transaction(self, fn):
        staged=self.restart();staged.unavailable=set(self.unavailable)
        result=fn(staged)
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

def registry(db):
    return getcontrol(db,'registry','registry')

def bootstrap(db):
    install(db,'registry','registry',{'schema':SCHEMAS['registry'],'deployment':'dep','shard':0,'generation':1,'rows':[]})

def discover(db,address,scope='t',kind='tenant',now=0,owner='coordinator'):
    r=registry(db)
    row={'scopeKind':kind,'scopeId':scope,'subject':address,'owner':owner,'address':address,'firstUtc':now,'state':'reserved'}
    matches=[x for x in r['rows'] if x['scopeKind']==kind and x['scopeId']==scope and x['subject']==address]
    if matches:need(matches[0]==row or matches[0]['state']=='present','reservation-conflict');return
    r['rows'].append(row);r['rows'].sort(key=registry_sort);savecontrol(db,'registry','registry',r)

def registry_present(db,address):
    r=registry(db);row=next((x for x in r['rows'] if x['address']==address),None);need(row is not None,'discovery-before-owner')
    if row['state']!='present':row['state']='present';savecontrol(db,'registry','registry',r)

def owner_discovered(db,address):
    need(any(r['address']==address for r in registry(db)['rows']),'undiscovered-owner')

def reconcile_placeholder(db,address,absence=False):
    r=registry(db);row=next(x for x in r['rows'] if x['address']==address)
    if db.read(address,address) is not None:registry_present(db,address)
    elif absence:r['rows'].remove(row);savecontrol(db,'registry','registry',r)
    else:raise Refusal('placeholder-needs-authority')

# A small ledger fixture; configured production limits remain the binary capability.
def ledger_init(db,tenant=100,deploy=250,reserve=50,unidentified=100,overhead=1):
    d={'generation':1,'tenantCeiling':tenant,'deploymentCeiling':deploy,'reserve':reserve,'unidentifiedCeiling':unidentified,'overhead':overhead,'used':{},'charges':{},'reservations':{}}
    db.write('ledger',canonical(d),'ledger',None)
    q={'schema':SCHEMAS['queue'],'deployment':'dep','generation':1,'lastTicket':0,'rows':[]}
    install(db,'queue','queue',q)

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
    return add(own,amount)<=d['tenantCeiling'] and add(used.get(pool,0),amount)<=ceiling and add(dep,amount)<=d['deploymentCeiling']

def reserve_charge(db,key,kind,id,length,overhead=None):
    b=db.read('ledger','ledger');d=ledger_get(db);acct=account(kind,id);integer(length)
    if key in d['charges']:
        c=d['charges'][key];need((c['account'],c['length'],c['state'])==(acct,length,'active'),'charge-conflict');return c['amount']
    o=d['overhead'] if overhead is None else overhead;amount=add(length,o)
    need(ledger_fit(d,kind,id,amount),'capacity')
    c={'account':acct,'length':length,'overhead':o,'amount':amount,'state':'active'}
    d['charges'][key]=c
    for a in (acct,'tenant-pool' if kind=='tenant' else 'unidentified','deployment'):d['used'][a]=add(d['used'].get(a,0),amount)
    ledger_save(db,d,b);return amount

def refund(db,key,deleted):
    need(deleted,'delete-readback')
    b=db.read('ledger','ledger');d=ledger_get(db);c=d['charges'].get(key)
    if c is None:return
    kind,id=c['account'].split(':',1);acct=account(kind,id)
    for a in (acct,'tenant-pool' if kind=='tenant' else 'unidentified','deployment'):
        need(d['used'][a]>=c['amount'],'refund-underflow');d['used'][a]-=c['amount']
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

def queue_reserve(db,subject,tenant,plan,now,limit=50000):
    def tx(s):
        q=getcontrol(s,'queue','queue')
        old=next((r for r in q['rows'] if r['subject']==subject),None)
        if old:need((old['tenant'],old['plan'])==(tenant,plan),'queue-owner-conflict');return old['ticket']
        need(len(q['rows'])<limit,'queue-full');ticket=add(q['lastTicket'],1)
        reserve_charge(s,'wait:'+subject,'tenant',tenant,16384)
        q['lastTicket']=ticket
        q['rows'].append({'subject':subject,'tenant':tenant,'scope':hashbytes(b'scope'+tenant.encode()),'plan':plan,'ticket':ticket,'firstUtc':now,'updatedUtc':now,'state':'reserved','candidate':None,'amount':None,'attempts':0,'charge':'wait:'+subject,'owner':'owner:'+subject})
        savecontrol(s,'queue','queue',q);return ticket
    return db.transaction(tx)

def queue_materialize(db,subject,pins,now):
    q=getcontrol(db,'queue','queue');r=next(x for x in q['rows'] if x['subject']==subject)
    total=0
    for p in pins:total=add(total,add(integer(p['length']),ledger_get(db)['overhead']))
    need(total>0 and now>=r['updatedUtc'],'queue-rerender')
    r.update(candidate=hashbytes(canonical(pins)),amount=total,updatedUtc=now,state='queued')
    savecontrol(db,'queue','queue',q)

def queue_turn(db,pin_sources):
    q=getcontrol(db,'queue','queue');d=ledger_get(db);chosen=None
    for r in q['rows']:
        if r['state'] not in ('queued','parked') or r['amount'] is None:continue
        c=d['charges'].get(r['charge'])
        need(c is not None and c['account']==account('tenant',r['tenant']) and c['length']==16384 and c['state']=='active','wait-charge')
        net=copy.deepcopy(d)
        for a in (c['account'],'tenant-pool','deployment'):
            need(net['used'][a]>=c['amount'],'wait-refund');net['used'][a]-=c['amount']
        if add(net['used'].get(c['account'],0),r['amount'])>d['tenantCeiling']:continue
        if r['amount']>d['deploymentCeiling']-d['reserve']:continue
        chosen=r;break
    if chosen is None or not ledger_fit(net,'tenant',chosen['tenant'],chosen['amount']):return None
    need(hashbytes(canonical(pin_sources[chosen['subject']]))==chosen['candidate'],'rerender-authority')
    def tx(s):
        q=getcontrol(s,'queue','queue');q['rows']=[r for r in q['rows'] if r['subject']!=chosen['subject']]
        savecontrol(s,'queue','queue',q);refund(s,chosen['charge'],True)
        batch(s,chosen['subject'],chosen['tenant'],pin_sources[chosen['subject']])
        return chosen['ticket']
    return db.transaction(tx)

def execution_init(db,key='execution',eligibility='retry-exhausted',accepted=None):
    discover(db,key)
    roster=[{'position':i,'message':'event-'+str(i),'digest':hashbytes(('stored-'+str(i)).encode())} for i in (1,2,3)]
    a=accepted if accepted is not None else [1]
    d={'schema':SCHEMAS['execution'],'tenant':'t','execution':key,'scope':ZERO if eligibility=='legacy-publish-failed' else hashbytes(b'scope'),'revision':1,'phase':'idle','firstUtc':0,'updatedUtc':0,'source':hashbytes(b'exhaustion'),'window':0 if eligibility=='legacy-publish-failed' else 1,'windowClaim':hashbytes(b'original-window'),'closedCount':0,'history':ZERO,'ordinal':0,'limit':8,'drainBase':8,'roster':roster,'accepted':a,'unresolved':[r['position'] for r in roster if r['position'] not in a],'legacy':None,'request':None,'intent':None,'receipts':{},'outcomes':[],'reason':eligibility,'nextUtc':0,'charge':'old-window:'+key}
    install(db,key,'execution',d);registry_present(db,key)
    reserve_charge(db,'old-window:'+key,'tenant','t',2*MIB)
    return d

def resume_identity(tenant,handle,idkey):
    textfield(idkey,128);need(all(33<=ord(c)<=126 for c in idkey),'idempotency-key')
    return hashbytes(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+frame('U',tenant)+frame('U',handle)+frame('U',idkey))

def resume_audit(d,r,ordinal,window,limit):
    closure=None if r['eligibility'] in ('drain-limit','legacy-publish-failed') else hashbytes(canonical({'request':r['identity'],'action':'closure','unresolved':d['unresolved']}))
    return record('HX-EV-PUBLICATION-RESUME-AUDIT-4',[['U',d['tenant']],['U',d['execution']],['N',ordinal],['B32',r['identity']],['B32',hashbytes(bytes.fromhex(r['carrier']))],['B32',r['priorHash']],['O:B32',closure],['N',window],['N',limit],['Q',r['utc']]])

def begin_resume(db,idkey='r1',source=None,reason='repair',now=0,key='execution'):
    d=getcontrol(db,key,'execution');owner_discovered(db,key)
    source=d['source'] if source is None else source
    handle='hxrsm1-'+hashbytes(frame('U',d['tenant'])+frame('U',d['execution'])+bytes.fromhex(digest(source)))
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
    need(d['reason'] in ('retry-exhausted','drain-limit','drain-limit-and-retry-exhausted','legacy-publish-failed') and d['source']!=ZERO and d['unresolved'],'resume_not_eligible')
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
        x=getcontrol(s,key,'execution');x.update(request=request,phase='prepared',updatedUtc=now,nextUtc=now,reason='publication_resume_preparation_hold')
        savecontrol(s,key,'execution',x)
    db.transaction(tx)
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
    need(f[:3]==['admin',d['scopeKind'],d['tenant']] and f[3]==hashbytes(key.encode()) and f[5]=='operator','redrive-claim-owner')
    need(f[4]==request['expectedCount'] and add(f[4],1)==d['redrives'],'redrive-claim-count')
    need(f[6]==request['utc'] and f[6]>=d['firstUtc'],'redrive-claim-time')
    return raw

NEXT={'prepared':'disable','disable':'reject','reject':'closure','closure':'window','window':'audit','audit':'successor','successor':'finalize','finalize':'invoke','invoke':'idle'}

def resume_step(db,key='execution',crash=None):
    d=getcontrol(db,key,'execution');r=d['request'];need(r is not None,'resume-absent')
    resume_claim_binding(d,r);owner_discovered(db,key)
    phase=d['phase'];need(phase in NEXT,'resume-transition')
    if phase=='prepared' and r['eligibility'] in ('drain-limit','legacy-publish-failed'):action='audit'
    else:action=NEXT[phase]
    if phase=='audit':action='successor'
    result=decode(bytes.fromhex(r['result']))
    if action=='idle':
        d.update(phase='idle',request=None,intent=None,receipts={},reason=None)
        savecontrol(db,key,'execution',d);return bytes.fromhex(r['result'])
    address='action:'+r['identity']+':'+action
    # Payload is immutable small fields derived from retained original request/current roster.
    payload={'request':r['identity'],'action':action,'unresolved':d['unresolved']}
    external_bytes=resume_audit(d,r,result['resumeOrdinal'],result['window'],result['drainLimit']) if action=='audit' else canonical(payload)
    if action=='audit':need(hashbytes(external_bytes)==result['auditRecordHash'],'audit-result-hash')
    if d['intent'] is None or d['intent']['kind']!=action:
        d['intent']={'kind':action,'address':address,'hash':hashbytes(external_bytes),'payload':payload}
        d=savecontrol(db,key,'execution',d)
        if crash=='intent':raise Crash(action)
    else:
        need(d['intent']['address']==address and d['intent']['payload']==payload,'intent-binding')
    db.external(address,external_bytes,key)
    if crash=='effect':raise Crash(action)
    need(hashbytes(db.read(address,key))==d['intent']['hash'],'effect-readback')
    def advance(s):
        x=getcontrol(s,key,'execution');x['receipts'][action]=address
        if action=='successor':
            x['ordinal']=result['resumeOrdinal'];x['window']=result['window'];x['limit']=result['drainLimit'];x['source']=ZERO
            if r['eligibility'] not in ('drain-limit','legacy-publish-failed'):
                x['closedCount']=add(x['closedCount'],1);x['windowClaim']=hashbytes(canonical([r['identity'],'window']));x['history']=hashbytes(canonical([x['history'],r['identity']]))
            row={'identity':r['identity'],'carrierHash':hashbytes(bytes.fromhex(r['carrier'])),'ordinal':x['ordinal'],'window':x['window'],'limit':x['limit'],'audit':result['auditRecordHash'],'expiry':r['expiry'],'deleteAfter':r['expiry']+30*DAY,'result':r['result']}
            if not any(v['identity']==r['identity'] for v in x['outcomes']):x['outcomes'].append(row)
        if action=='finalize':refund(s,x['charge'],True);x['charge']='stage:'+r['identity']
        x['phase']=action;x['intent']=None;savecontrol(s,key,'execution',x)
    db.transaction(advance)
    if crash=='advance':raise Crash(action)
    return None

def finish_resume(db,key='execution'):
    for _ in range(12):
        d=getcontrol(db,key,'execution')
        if d['request'] is None:return
        resume_step(db,key)
    raise AssertionError('bounded-resume-completion')

def cancel_resume(db,key='execution',absence=True):
    d=getcontrol(db,key,'execution');r=d['request'];need(r is not None,'no-preparation')
    need(d['phase']=='prepared' and not any(db.read('action:'+r['identity']+':'+a,key) is not None for a in ('disable','reject','audit','successor')),'irreversible')
    need(absence,'absence-required')
    def tx(s):
        if d['intent'] is not None:s.delete(d['intent']['address'],key)
        refund(s,'stage:'+r['identity'],True)
        x=getcontrol(s,key,'execution');x.update(request=None,intent=None,phase='idle',reason=r['eligibility']);savecontrol(s,key,'execution',x)
    db.transaction(tx)

def reclaim(db,now,key='execution'):
    d=getcontrol(db,key,'execution');need(now>=d['updatedUtc'],'utc-regression')
    expired=[r for r in d['outcomes'] if now>=r['deleteAfter']]
    if not expired:return
    def tx(s):
        for r in expired:
            for a in NEXT.values():s.delete('action:'+r['identity']+':'+a,key)
        x=getcontrol(s,key,'execution');x['outcomes']=[r for r in x['outcomes'] if r not in expired];x['updatedUtc']=now;savecontrol(s,key,'execution',x)
    db.transaction(tx)

def held_init(db,carrier=b'exact carrier',key='held',now=0):
    discover(db,key,now=now,owner='operations')
    d={'schema':SCHEMAS['held'],'scopeKind':'tenant','scopeId':'t','deployment':'dep','tenant':'t','component':'pubsub','topic':'orders','subscription':'sub','policy':hashbytes(b'policy'),'revision':1,'phase':'observed','reason':'handler-capability-hold','firstUtc':now,'updatedUtc':now,'observations':1,'length':len(carrier),'carrier':hashbytes(carrier),'locator':None,'objectReceipt':None,'charge':'object:'+key,'request':None,'attempt':None,'redrives':0,'nextUtc':now,'repair':'none','error':None,'intent':None,'receipts':{}}
    old=db.read(key,key)
    if old is not None:
        prior=getcontrol(db,key,'held');need(prior['carrier']==d['carrier'] and prior['length']==d['length'] and prior['firstUtc']==now,'held-init-conflict');metadata_charge(db,prior,key);return prior
    def tx(s):
        reserve_charge(s,'metadata:'+key,'tenant','t',136*1024)
        install(s,key,'held',d);registry_present(s,key)
    db.transaction(tx);return d

def observe(db,now,key='held'):
    d=getcontrol(db,key,'held');need(now>=d['updatedUtc'],'observation-clock');d['observations']=add(d['observations'],1);d['updatedUtc']=now;savecontrol(db,key,'held',d)

def metadata_charge(db,d,key):
    c=ledger_get(db)['charges'].get('metadata:'+key)
    need(c is not None and c['account']==account('tenant',d['tenant']) and c['length']==136*1024 and c['state']=='active' and c['amount']==add(c['length'],c['overhead']),'metadata-charge')

def capture(db,carrier,key='held',crash=None):
    d=getcontrol(db,key,'held');owner_discovered(db,key);metadata_charge(db,d,key)
    need(hashbytes(carrier)==d['carrier'] and len(carrier)==d['length'] and len(carrier)<=193*MIB,'capture-carrier')
    if d['phase']=='captured':verify_retained(db,d,key);return True
    need(d['phase'] in ('observed','capturing'),'capture-transition');address='object:'+key
    def admit(s):
        reserve_charge(s,d['charge'],'tenant',d['tenant'],len(carrier))
        if d['phase']=='observed':
            x=getcontrol(s,key,'held');x.update(phase='capturing',intent={'kind':'capture','address':address,'hash':d['carrier'],'payload':{'length':d['length'],'carrier':d['carrier']}});savecontrol(s,key,'held',x)
    db.transaction(admit)
    if crash=='charge':raise Crash('capture-charge')
    receipt=db.external(address,carrier,key)
    if crash=='object':raise Crash('capture-object')
    d=getcontrol(db,key,'held');d.update(phase='captured',locator={'backend':'held-delivery-store','key':address},objectReceipt=receipt,intent=None)
    savecontrol(db,key,'held',d)
    if crash=='control':raise Crash('capture-control')
    return True

def verify_retained(db,d,key):
    metadata_charge(db,d,key)
    need(d['locator'] is not None,'locator')
    b=db.read(d['locator']['key'],key);need(b is not None and hashbytes(b)==d['carrier'] and len(b)==d['length'],'retained-bytes')
    c=ledger_get(db)['charges'].get(d['charge']);need(c is not None and c['account']==account('tenant',d['tenant']) and c['length']==d['length'],'retained-charge')
    need(db.read(d['locator']['key'],key) is not None and d['objectReceipt']==hashbytes(b),'object-receipt');owner_discovered(db,key)
    return b

def redrive(db,key='held',now=0,expected=None,crash=None):
    d=getcontrol(db,key,'held');verify_retained(db,d,key)
    need(d['phase']=='captured' and d['repair'] in ('none','repaired'),'redrive-phase')
    need(now>=d['firstUtc'] and now>=d['nextUtc'],'redrive-time')
    count=d['redrives'] if expected is None else expected;need(count==d['redrives'],'redrive-count')
    claim=record('HX-EV-REDRIVE-REQUEST-2',[['U','admin'],['U',d['scopeKind']],['O:U',d['tenant']],['B32',hashbytes(key.encode())],['N',count],['U','operator'],['Q',now]])
    n=add(count,1)
    d.update(redrives=n,phase='redriving',repair='none',request={'claim':claim.hex(),'signature':fixture_sign(claim),'expectedCount':count,'utc':now},attempt={'count':n,'requestHash':hashbytes(claim),'carrier':d['carrier'],'utc':now,'result':None},intent={'kind':'send','address':'delivery:'+key+':'+str(n),'hash':d['carrier'],'payload':{'count':n,'carrier':d['carrier']}},updatedUtc=now)
    savecontrol(db,key,'held',d)
    if crash=='commit':raise Crash('redrive-commit')
    return send_held(db,key,crash)

def send_held(db,key='held',crash=None):
    d=getcontrol(db,key,'held');b=verify_retained(db,d,key)
    need(d['phase']=='redriving' and d['request'] is not None and d['attempt'] is not None,'send-attempt')
    claim=held_claim_binding(d,key,d['request'])
    need(d['attempt']['count']==d['redrives'] and d['attempt']['requestHash']==hashbytes(claim),'send-binding')
    need(route_authority(db,d,key)!='terminal','terminal-no-send')
    db.external(d['intent']['address'],b,key)
    if crash=='effect':raise Crash('redrive-effect')
    return d['redrives']

def route_authority(db,d,key):
    raw=db.read('route-terminal:'+key+':'+str(d['redrives']),key)
    if raw is None:return 'unknown'
    a=decode(raw);exact(a,'count requestHash carrier outcome')
    need(a['count']==d['redrives'] and a['carrier']==d['carrier'] and d['request'] is not None and a['requestHash']==hashbytes(bytes.fromhex(d['request']['claim'])) and a['outcome'] in ('terminal','nonterminal','unknown'),'route-authority')
    return a['outcome']

def redrive_reconcile(db,key='held',now=0,terminal=False):
    d=getcontrol(db,key,'held');need(d['phase']=='redriving','reconcile-phase')
    outcome=route_authority(db,d,key);repair='none'
    if d['request'] is None or d['attempt'] is None:repair='absent'
    else:
        try:
            held_claim_binding(d,key,d['request'])
            need(d['attempt']['count']==d['redrives'] and d['attempt']['requestHash']==hashbytes(bytes.fromhex(d['request']['claim'])),'attempt-count')
        except (Refusal,ValueError):repair='corrupt'
    if terminal:need(outcome=='terminal','route-terminal-readback')
    terminal=outcome=='terminal'
    if terminal:need(repair=='none','terminal-evidence')
    d.update(phase='cleanup' if terminal else 'captured',repair=repair,nextUtc=now+(900 if repair!='none' else backoff(d['redrives']))*TICK,error=None if terminal else {'reason':'evidence' if repair!='none' else 'nonterminal','hash':hashbytes(b'error'),'utc':now},updatedUtc=now,intent=None)
    savecontrol(db,key,'held',d)

def backoff(n):return min(900,60*2**min(max(n-1,0),4))

def repair_held(db,key='held',claim=None):
    d=getcontrol(db,key,'held');need(d['repair'] in ('absent','corrupt','required'),'repair-required');verify_retained(db,d,key)
    need(claim is not None,'repair-authority')
    held_claim_binding(d,key,claim)
    need(claim['expectedCount']+1==d['redrives'],'repair-count')
    d['request']=claim;d['attempt']={'count':d['redrives'],'requestHash':hashbytes(bytes.fromhex(claim['claim'])),'carrier':d['carrier'],'utc':claim['utc'],'result':None};d['repair']='repaired';savecontrol(db,key,'held',d)

def erase_held(db,key='held',authority=True,crash=None):
    d=getcontrol(db,key,'held');need(authority,'erasure-authority')
    if d['phase']!='cleanup':
        d['phase']='cleanup';d['intent']={'kind':'erase','address':'object:'+key,'hash':d['carrier'],'payload':{'delivered':False}};savecontrol(db,key,'held',d)
    db.delete('object:'+key,key)
    if crash=='object':raise Crash('erase-object')
    need(db.read('object:'+key,key) is None,'object-deletion-readback')
    for address,row in list(db.rows.items()):
        if row['owner']==key and address!=key:
            db.delete(address,key);need(db.read(address,key) is None,'provider-deletion-readback')
            if crash=='source':raise Crash('erase-source')
    # Original stable discovery persists through external delete and transactional refund.
    def tx(s):
        refund(s,d['charge'],True);refund(s,'metadata:'+key,True)
        s.delete(key,key)
        r=registry(s);r['rows']=[x for x in r['rows'] if x['address']!=key];savecontrol(s,'registry','registry',r)
    db.transaction(tx)

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

def inventory_page(db,kind,scope,size=50,cursor=None,now=0):
    need(1<=size<=200,'page-size')
    rows=[r for r in registry(db)['rows'] if (r['scopeKind'],r['scopeId'])==(kind,scope)]
    versions=[];views=[]
    for r in rows:
        b=db.read(r['address'],r['address'])
        versions.append([r['address'],None if b is None else hashbytes(b)])
        v=decode(b) if b is not None else {'reason':'placeholder','phase':'reserved'}
        views.append({'subject':r['subject'],'reason':v.get('reason'),'phase':v.get('phase'),'firstUtc':r['firstUtc']})
    generation=hashbytes(canonical([registry(db)['generation'],versions]));start=0
    if cursor:
        d=typed('cursor',bytes.fromhex(cursor['body']));signed(bytes.fromhex(cursor['body']),cursor['sig'])
        need((d['scopeKind'],d['scopeId'])==(kind,scope),'cursor-scope');need(now<d['expiry'],'cursor-expired');need(d['generation']==generation,'hold_inventory_generation_changed')
        if d['last'] is not None:
            positions=[i for i,r in enumerate(rows) if [r['firstUtc'],r['scopeKind'],r['scopeId'],r['subject']]==d['last']]
            need(len(positions)==1,'cursor-position');start=positions[0]+1
    page=views[start:start+size];last_index=start+len(page)
    last=None if not page else [rows[last_index-1]['firstUtc'],kind,scope,rows[last_index-1]['subject']]
    d={'schema':SCHEMAS['cursor'],'scopeKind':kind,'scopeId':scope,'generation':generation,'last':last,'expiry':now+900*TICK};b=canonical(d)
    return page,{'body':b.hex(),'sig':fixture_sign(b)} if last_index<len(views) else None

# No provider or production crypto claim follows from these scenario assertions.
def refused(fn, why=None):
    try:fn()
    except Refusal as e:
        if why is not None:assert str(e)==why,(str(e),why)
        return
    raise AssertionError('expected owning refusal')

def unchanged(db,fn,why=None):
    b=db.snapshot();refused(fn,why);assert db.snapshot()==b,'refusal changed persisted state'

def newdb():
    db=Store();bootstrap(db);ledger_init(db,64*MIB,160*MIB,32*MIB,64*MIB);return db

def drain_fixture(events,classification):
    return canonical({'schema':'fixture-unpublished-events/1','tenant':'t','domain':'d','aggregate':'a','tracking':'tracking','execution':'op','correlation':'correlation','commandType':'increment','events':events,'isRejection':classification=='rejection-events'})

def capsule_make(db,events,classification='success-events',key='capsule',cleanup=False):
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
    member_bytes=b''.join(frame('N',r['sequence'])+frame('U',r['message'])+bytes.fromhex(r['digest']) for r in events)
    descriptors=[]
    for ordinal,offset in enumerate(range(0,len(events),61)):
        part=events[offset:offset+61]
        rows=b''.join(frame('N',r['sequence'])+frame('U',r['message'])+bytes.fromhex(r['digest']) for r in part)
        root=hashbytes(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+frame('B',rows.hex()))
        chunk=record('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',[['B32',cid],['N',ordinal],['N',len(part)],['B',rows.hex()],['B32',root]])
        address='legacy-resume-capsule-chunk:'+hashbytes(b'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0\x01'+bytes.fromhex(cid)+frame('N',ordinal))
        reserve_charge(db,address,'tenant','t',len(chunk));db.external(address,chunk,'legacy')
        descriptors.append(frame('N',ordinal)+frame('N',part[0]['sequence'])+frame('N',len(part))+bytes.fromhex(hashbytes(chunk))+frame('N',len(chunk))+frame('U',address))
    manifest=frame('P',len(descriptors))+b''.join(descriptors)
    root=hashbytes(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'+frame('B',(frame('P',len(events))+member_bytes).hex()))
    fields=[['U','t'],['U','d'],['U','a'],['U','tracking'],['O:U',original['execution']],['U',original['correlation']],['U',original['commandType']],['U',classification],['N',events[0]['sequence']],['N',events[-1]['sequence']],['I',len(events)],['B32',root],['B',manifest.hex()],['U','drain-exhaustion'],['B32',source_hash],['Q',0]]
    b=record('HX-EV-LEGACY-RESUME-CAPSULE-2',fields);need(len(b)<=128*1024,'capsule-bound')
    reserve_charge(db,key,'tenant','t',len(b));db.external(key,b,'legacy')
    need(db.read(key,'legacy')==b,'capsule-readback')
    capsule_restore(db,events,key)
    if cleanup:need(db.read('drain','legacy')==source,'drain-cleanup-source');db.delete('drain','legacy')
    return b

def capsule_restore(db,events,key='capsule'):
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

def legacy_transition(db,key,edge,ordinal,owner='legacy-resume',repair=False):
    d=getcontrol(db,key,'execution');legacy=d['legacy'];need(legacy is not None,'legacy-fence')
    need(legacy['owner']==owner,'legacy-owner')
    old=legacy['state']
    need((old,edge) in {('claimed','draining'),('draining','completed'),('draining','failed'),('failed','claimed')},'legacy-edge')
    if old=='failed':
        need(ordinal>legacy['ordinal'] and ordinal==d['ordinal'] and (legacy['failure']=='transport-retryable' or repair),'legacy-reclaim')
        row=next((r for r in d['outcomes'] if r['ordinal']==ordinal),None);need(row is not None,'legacy-success-authority')
        audit=db.read('action:'+row['identity']+':audit',key);need(audit is not None and hashbytes(audit)==row['audit'],'legacy-success-audit')
        f=[v for _,v in read_record(audit,ANSWERS['records']['D45-audit'])]
        need(f[:3]==[d['tenant'],d['execution'],ordinal] and f[3]==row['identity'] and f[4]==row['carrierHash'],'legacy-success-owner')
    legacy.update(state=edge,generation=add(legacy['generation'],1),ordinal=ordinal)
    if edge=='failed':legacy['failure']='transport-retryable'
    savecontrol(db,key,'execution',d)

def scope_admit(db,tenant,execution,input_hash,now=0):
    key='command-execution-scope:'+hashbytes(frame('U',tenant)+frame('U',execution));shard=hashlib.sha256(frame('U',tenant)+frame('U',execution)).digest()[0]
    old=db.read(key,key)
    if old is not None:
        d=decode(old)
        need(d['input']==input_hash,'CommandIdentityConflict')
        need(d['state']!='tombstone','idempotency-expired')
        return key
    def tx(s):
        usagekey='scope-usage:'+tenant+':'+str(shard)
        u=s.read(usagekey,usagekey);usage=0 if u is None else decode(u)['used'];need(usage+4096+2048<=262144,'scope-capacity')
        s.write(key,canonical({'tenant':tenant,'execution':execution,'input':input_hash,'state':'required','shard':shard,'expiry':None}),key,None)
        s.write(usagekey,canonical({'used':usage+4096,'shard':shard,'tenant':tenant}),usagekey,u)
    db.transaction(tx);return key

def scope_compact(db,key,now,closed):
    b=db.read(key,key);d=decode(b);need(closed,'scope-open-obligation')
    if d['state']=='tombstone':return
    need(d['state']=='required','scope-predecessor')
    d.update(state='tombstone',expiry=now+30*DAY);db.write(key,canonical(d),key,b)

def scope_status(db,key):
    d=decode(db.read(key,key));return 410 if d['state']=='tombstone' else 200

def scope_expire(db,key,now,closed):
    b=db.read(key,key)
    if b is None:return
    d=decode(b);need(d['state']=='tombstone' and now>=d['expiry'] and closed,'scope-expiry')
    def tx(s):
        ukey='scope-usage:'+d['tenant']+':'+str(d['shard']);u=s.read(ukey,ukey);usage=decode(u);usage['used']-=4096
        s.delete(key,key);s.write(ukey,canonical(usage),ukey,u)
    db.transaction(tx)

ANSWERS = json.loads((HERE/'known-answers.json').read_text())

def verify_answers():
    for name,a in ANSWERS['records'].items():
        raw=bytes.fromhex(a['hex'])
        assert len(raw)==a['length'] and hashbytes(raw)==a['sha256'],name
        produced=record(a['domain'],a['fields']) if 'domain' in a else canonical(a['json'])
        assert produced==raw,name
        if 'domain' in a:
            assert read_record(raw,a)==a['fields']
            refused(lambda:read_record(raw+b'\x00',a))
            broken=bytearray(raw);broken[len(a['domain'])+4]=255
            refused(lambda:read_record(bytes(broken),a))
    for name,a in ANSWERS['keys'].items():
        material=a['domain'].encode()+b'\x00\x01'+b''.join(frame(k,v) for k,v in a['fields'])
        assert material.hex()==a['materialHex'] and a['prefix']+hashbytes(material)==a['value'],name
    for kind,a in ANSWERS['controls'].items():
        raw=bytes.fromhex(a['hex']);assert canonical(a['json'])==raw and len(raw)==a['length'] and hashbytes(raw)==a['sha256'];typed(kind,raw)
    bad=copy.deepcopy(ANSWERS['records']['D45-window']);bad['sha256']=ZERO
    try:assert hashbytes(bytes.fromhex(bad['hex']))==bad['sha256']
    except AssertionError:pass
    else:raise AssertionError('mutated answer did not fail')

def focused_corrections():
    # Field-specific U widths preserve literals and ordinary identifier bounds.
    for name,tag,sizes in [('D36-quarantine',8,(4095,4096,4097)),('D29-counter',2,(1030,1031,1032))]:
        a=ANSWERS['records'][name]
        for size in sizes:
            f=copy.deepcopy(a['fields']);f[tag][1]='x'*size
            if size==sizes[-1]:
                refused(lambda:record(a['domain'],f),'identifier')
                raw=a['domain'].encode()+b'\x00\x01'+len(f).to_bytes(2,'big')+b''.join(bytes([i])+frame(k,v,8192) for i,(k,v) in enumerate(f,1))
                refused(lambda:read_record(raw,a),'identifier')
            else:assert read_record(record(a['domain'],f),a)==f
        refused(lambda:frame('U','x'*1025),'identifier')
    # Admission failures retain only the original discoverable reservation.
    db=newdb();discover(db,'held',owner='operations');b=db.read('ledger','ledger');d=ledger_get(db);d['tenantCeiling']=1;db.write('ledger',canonical(d),'ledger',b)
    unchanged(db,lambda:held_init(db),'capacity');assert db.read('held','held') is None and registry(db)['rows'][0]['state']=='reserved'
    b=db.read('ledger','ledger');d=ledger_get(db);d['tenantCeiling']=64*MIB;db.write('ledger',canonical(d),'ledger',b);held_init(db);b=db.snapshot();held_init(db);assert db.snapshot()==b
    for operation in ('capture','redrive'):
        probe=newdb();held_init(probe)
        if operation=='redrive':capture(probe,b'exact carrier')
        refund(probe,'metadata:held',True)
        unchanged(probe,lambda:capture(probe,b'exact carrier') if operation=='capture' else redrive(probe),'metadata-charge')
    # Object-capacity refusal is atomic; successful partial captures still restart.
    probe=newdb();held_init(probe);b=probe.read('ledger','ledger');d=ledger_get(probe);d['tenantCeiling']=d['used']['tenant:t'];probe.write('ledger',canonical(d),'ledger',b)
    unchanged(probe,lambda:capture(probe,b'exact carrier'),'capacity')
    for point in ('charge','object'):
        probe=newdb();held_init(probe)
        try:capture(probe,b'exact carrier',crash=point)
        except Crash:pass
        probe=probe.restart();capture(probe,b'exact carrier');assert getcontrol(probe,'held','held')['phase']=='captured'
    # Original route authority determines outcome; forged/unavailable cannot advance.
    for outcome in ('absent','unknown','nonterminal','terminal','unavailable','forged'):
        probe=newdb();held_init(probe);capture(probe,b'exact carrier');redrive(probe)
        d=getcontrol(probe,'held','held');address='route-terminal:held:1'
        if outcome not in ('absent','unavailable'):
            authority={'count':1,'requestHash':d['attempt']['requestHash'],'carrier':d['carrier'],'outcome':outcome}
            if outcome=='forged':authority.update(outcome='terminal',count=2)
            probe.external(address,canonical(authority),'held')
        if outcome=='unavailable':probe.unavailable.add(address)
        if outcome in ('unavailable','forged'):
            unchanged(probe,lambda:redrive_reconcile(probe));unchanged(probe,lambda:send_held(probe))
        elif outcome=='terminal':
            probe=probe.restart();redrive_reconcile(probe,terminal=False);assert getcontrol(probe,'held','held')['phase']=='cleanup'
            try:erase_held(probe,crash='source')
            except Crash:pass
            probe=probe.restart();assert ledger_get(probe)['charges'] and registry(probe)['rows'];erase_held(probe)
            assert not any(r['owner']=='held' for r in probe.rows.values()) and not ledger_get(probe)['charges']
        else:
            unchanged(probe,lambda:redrive_reconcile(probe,terminal=True),'route-terminal-readback')
            redrive_reconcile(probe);assert getcontrol(probe,'held','held')['phase']=='captured'
    # Read-backed original drain range/classification is mandatory and immutable.
    events=[{'sequence':1,'message':'m','digest':ZERO}]
    for failure in ('missing','unavailable','events','classification'):
        probe=newdb()
        if failure!='missing':probe.external('drain',drain_fixture(events,'rejection-events'),'legacy')
        if failure=='unavailable':probe.unavailable.add('drain')
        changed=copy.deepcopy(events)
        if failure=='events':changed[0]['message']='different'
        unchanged(probe,lambda:capsule_make(probe,changed,'success-events' if failure=='classification' else 'rejection-events'))
    probe=newdb();probe.external('drain',drain_fixture(events,'rejection-events'),'legacy');capsule_make(probe,events,'rejection-events',cleanup=True);probe=probe.restart();assert capsule_restore(probe,events)==(['m'],'Rejected')
    # Actual successful resume audit earns the reclaimed ordinal.
    d=execution_init(probe,eligibility='legacy-publish-failed');d['source']=hashbytes(probe.read('capsule','legacy'));d['legacy']={'capsule':d['source'],'identity':ZERO,'owner':'legacy-resume','state':'failed','generation':1,'ordinal':0,'failure':'transport-retryable','repaired':None};savecontrol(probe,'execution','execution',d)
    unchanged(probe,lambda:legacy_transition(probe,'execution','claimed',1),'legacy-reclaim')
    begin_resume(probe,'earned');finish_resume(probe);probe=probe.restart();d=getcontrol(probe,'execution','execution');row=d['outcomes'][0];actual=probe.read('action:'+row['identity']+':audit','execution')
    assert hashbytes(actual)==row['audit']==decode(bytes.fromhex(row['result']))['auditRecordHash'];legacy_transition(probe,'execution','claimed',1)
    # Near exhaustion refuses before producer disable; a legal pending request survives.
    for field in ('owner','provider'):
        probe=newdb();execution_init(probe);d=getcontrol(probe,'execution','execution')
        if field=='owner':d['revision']=MAX-2;probe.write('execution',canonical(d),'execution',probe.read('execution','execution'))
        else:
            r=probe.rows['execution'];r['generation']=MAX-2;r['receipt']=Store.receipt('execution',r['body'],r['generation'],r['owner'])
        unchanged(probe,lambda:begin_resume(probe),'resume_arithmetic_exhausted');assert not any(k.endswith(':disable') for k in probe.rows)
    probe=newdb();execution_init(probe);d=getcontrol(probe,'execution','execution');d['revision']=MAX-20;probe.write('execution',canonical(d),'execution',probe.read('execution','execution'))
    begin_resume(probe)
    try:resume_step(probe,crash='intent')
    except Crash:pass
    original=getcontrol(probe,'execution','execution')['request'];probe=probe.restart();assert getcontrol(probe,'execution','execution')['request']==original;finish_resume(probe)
    # Supplied net-grant boundary: remove/refund row storage in the same transaction.
    for extra in (0,1):
        probe=newdb();b=probe.read('ledger','ledger');d=ledger_get(probe);d.update(tenantCeiling=1024*MIB,deploymentCeiling=3*1024*MIB);probe.write('ledger',canonical(d),'ledger',b)
        subject=hashbytes(b'net-fit');pins=[{'message':'p','length':449*MIB}];queue_reserve(probe,subject,'t',ZERO,0);reserve_charge(probe,'fill','tenant','t',602947584+extra-16385-1);queue_materialize(probe,subject,pins,0);probe=probe.restart();prior=probe.snapshot()
        result=queue_turn(probe,{subject:pins})
        if extra:assert result is None and probe.snapshot()==prior
        else:assert result==1 and ledger_get(probe)['used']['tenant:t']==1024*MIB
    print('Focused corrections: authority, charge/refusal/restart, U bounds, audit bytes, generations and net queue fit passed')

def scenarios():
    # All 14 historical status cases plus mixed and unavailable precedence.
    expected=[({},('EventsStored',None,1)),({'state':'unknown','maximum':True},('EventsStored',None,1)),({'drain':True},('EventsStored','publication_drain_limit_hold',60)),({'state':'failed','classes':['class-01'],'maximum':True},('EventsStored','publication_retry_exhausted_hold',60)),({'state':'failed','classes':['class-02'],'maximum':True},('CommandOutcomeHold','terminal_evidence_hold',30)),({'state':'failed','classes':['class-03']},('CommandOutcomeHold','terminal_evidence_hold',30)),({'state':'failed','classes':['class-01'],'automatic':True},('EventsStored','publication_retry_pending',1)),({'state':'failed','terminal':True},('PublishFailed','publication_terminal_failed',None)),({'conflict':True},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'published'},('Completed',None,None)),({'state':'published','classification':'rejection'},('Rejected',None,None)),({'state':'published','classification':'unknown'},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'not-applicable'},('Completed',None,None)),({'state':'failed','classes':['unknown'],'maximum':True},('CommandOutcomeHold','outcome_evidence_conflict',30)),({'state':'failed','classes':['class-01','class-02'],'maximum':True},('CommandOutcomeHold','terminal_evidence_hold',30)),({'terminal':True,'unavailable':True},('CommandOutcomeHold','outcome_evidence_hold',30))]
    for args,out in expected:assert status(**args)==out
    # Permanent old-window fence admits only unresolved members in a valid successor.
    claim={'window':2,'unresolved':[2,3]}
    assert broker_accept(2,2,claim,{1})=='accepted'
    refused(lambda:broker_accept(1,2,{'window':1,'unresolved':[2,3]},{1}),'window-fence')
    refused(lambda:broker_accept(2,1,claim,{1}),'window-admission')
    refused(lambda:broker_accept(2,2,claim,{1},terminal=True,duplicate=True),'operation-terminal-fence')
    # Membership fresh proof and changed configuration cannot override immutable bytes.
    assert membership(b'pin',b'pin','EmptyNamespace',True)=='ContinueSamePin'
    assert membership(b'pin',b'changed','InitialRowOnly',True)=='FirstSendMembershipChangedHold'
    refused(lambda:membership(b'pin',b'pin','EmptyNamespace',False))
    refused(lambda:membership(b'pin',b'pin','EmptyNamespace',True,True))
    # Replay below/at/above accounting activation and hard bounds, idle capability exit.
    assert [replay(n,0) for n in (24575,24576,24577)]==['continue-full-replay','hold','hold']
    assert [replay(n,0,activation=False) for n in (32767,32768,32769)]==['full-replay','full-replay','LegacyArrayLimit']
    assert [replay(1,n) for n in (48*MIB-1,48*MIB,48*MIB+1)]==['continue-full-replay','hold','hold']
    assert [replay(1,n,activation=False) for n in (64*MIB-1,64*MIB,64*MIB+1)]==['full-replay','full-replay','LegacyArrayLimit']
    assert replay(24576,0,incremental=True)=='incremental-bootstrap'
    refused(lambda:replay(MAX,0));refused(lambda:replay(-1,0))
    raw=bytes.fromhex(ANSWERS['records']['D17-destination-config']['hex']);assert destination(raw,'pubsub','orders')['metadata']=={}
    refused(lambda:destination(raw+b' ','pubsub','orders'));refused(lambda:destination(raw,'different','orders'))
    refused(lambda:decode(b'{"a":1,"a":2}'))
    # Registry reservation crash remains discoverable; release requires authority.
    db=newdb();discover(db,'missing');db=db.restart();unchanged(db,lambda:reconcile_placeholder(db,'missing'))
    reconcile_placeholder(db,'missing',True);assert not registry(db)['rows']
    # Resume refuses before mutation and persisted restart at every irreversible phase.
    restarts=0
    for mode in ('retry-exhausted','drain-limit','drain-limit-and-retry-exhausted'):
        for crash in ('intent','effect','advance'):
            db=newdb();original=execution_init(db,eligibility=mode);source=original['source']
            unchanged(db,lambda:begin_resume(db,source=ZERO),'resume_hold_changed')
            begin_resume(db,source=source);db=db.restart()
            for _ in range(10):
                if getcontrol(db,'execution','execution')['request'] is None:break
                try:resume_step(db,crash=crash)
                except Crash:pass
                db=db.restart();restarts+=1
            finish_resume(db)
            d=getcontrol(db,'execution','execution');assert d['accepted']==[1] and d['unresolved']==[2,3] and d['roster']==original['roster']
            assert d['source']==ZERO and d['ordinal']==1 and len(d['outcomes'])==1
            if mode=='drain-limit':assert d['windowClaim']==original['windowClaim'] and d['window']==original['window'] and d['closedCount']==0
            old=db.snapshot();reply=begin_resume(db,source=source);assert decode(reply)['resumeOrdinal']==1 and db.snapshot()==old
            unchanged(db,lambda:begin_resume(db,source=source,reason='changed'),'resume_request_conflict')
            unchanged(db,lambda:begin_resume(db,'new',source=source),'resume_hold_changed')
            unchanged(db,lambda:begin_resume(db,source=source,now=900*TICK),'resume_request_expired')
    # Pre-irreversible cleanup and distinct disable/reject crash outcomes.
    db=newdb();execution_init(db);begin_resume(db);before=ledger_get(db)['used']['deployment'];cancel_resume(db)
    assert ledger_get(db)['used']['deployment']<before and getcontrol(db,'execution','execution')['request'] is None
    for target in ('disable','reject','audit'):
        db=newdb();execution_init(db);begin_resume(db)
        while getcontrol(db,'execution','execution')['phase']!=target:resume_step(db)
        unchanged(db,lambda:cancel_resume(db),'irreversible');db=db.restart();finish_resume(db)
    db=newdb();execution_init(db,accepted=[1,2,3]);unchanged(db,lambda:begin_resume(db),'resume_not_eligible')
    db=newdb();execution_init(db);d=getcontrol(db,'execution','execution');d['ordinal']=MAX;savecontrol(db,'execution','execution',d);unchanged(db,lambda:begin_resume(db),'arithmetic')
    # One owner supports 67 actual successes after deterministic outcome reclamation.
    db=newdb();execution_init(db);now=0
    for i in range(67):
        if i:
            d=getcontrol(db,'execution','execution');d.update(source=hashbytes(str(i).encode()),reason='retry-exhausted',updatedUtc=now);savecontrol(db,'execution','execution',d)
        src=getcontrol(db,'execution','execution')['source'];begin_resume(db,'life'+str(i),source=src,now=now);finish_resume(db)
        assert getcontrol(db,'execution','execution')['ordinal']==i+1
        now+=31*DAY;reclaim(db,now);db=db.restart();assert not getcontrol(db,'execution','execution')['outcomes']
        assert len(ledger_get(db)['charges'])==1
    # Legacy capsule before cleanup and exact range/MessageIds for success and rejection.
    for classification in ('success-events','rejection-events'):
        db=newdb();events=[{'sequence':10+i,'message':'m'+str(i),'digest':hashbytes(('event'+str(i)).encode())} for i in range(65)]
        db.external('drain',drain_fixture(events,classification),'legacy');capsule_make(db,events,classification,cleanup=True);db=db.restart()
        assert db.read('drain','legacy') is None
        ids,out=capsule_restore(db,events);assert ids==[r['message'] for r in events] and out==('Rejected' if classification=='rejection-events' else 'Completed')
        changed=copy.deepcopy(events);changed[0]['message']='wrong';unchanged(db,lambda:capsule_restore(db,changed))
        d=execution_init(db,eligibility='legacy-publish-failed');d['source']=hashbytes(db.read('capsule','legacy'));d['legacy']={'capsule':hashbytes(db.read('capsule','legacy')),'identity':hashbytes(b'cid'),'owner':'legacy-resume','state':'claimed','generation':1,'ordinal':1,'failure':None,'repaired':None};savecontrol(db,'execution','execution',d)
        unchanged(db,lambda:legacy_transition(db,'execution','completed',1),'legacy-edge');legacy_transition(db,'execution','draining',1);legacy_transition(db,'execution','failed',1)
        unchanged(db,lambda:legacy_transition(db,'execution','claimed',1),'legacy-reclaim');unchanged(db,lambda:legacy_transition(db,'execution','claimed',2),'legacy-reclaim')
        d=getcontrol(db,'execution','execution');d['ordinal']=1;savecontrol(db,'execution','execution',d);begin_resume(db,'legacy-earned');finish_resume(db);legacy_transition(db,'execution','claimed',2)
    db=newdb();unchanged(db,lambda:capsule_restore(db,[{'sequence':1,'message':'m','digest':ZERO}]),'legacy_resume_evidence_unavailable')
    # Capture restarts preserve first observation and complete charge-only/object-written work.
    for point in ('charge','object','control'):
        db=newdb();held_init(db)
        try:capture(db,b'exact carrier',crash=point)
        except Crash:pass
        db=db.restart();observe(db,20*TICK);capture(db,b'exact carrier');d=getcontrol(db,'held','held')
        assert d['phase']=='captured' and d['firstUtc']==0 and d['observations']==2
        old=db.snapshot();capture(db,b'exact carrier');assert db.snapshot()==old
        unchanged(db,lambda:capture(db,b'changed'))
    # Committed attempt/count survives lost acknowledgement; unknown completion schedules retry.
    db=newdb();held_init(db);capture(db,b'exact carrier')
    try:redrive(db,crash='commit')
    except Crash:pass
    db=db.restart();d=getcontrol(db,'held','held');assert d['redrives']==1 and d['attempt']['count']==1
    send_held(db);redrive_reconcile(db,now=TICK);assert getcontrol(db,'held','held')['nextUtc']==61*TICK
    assert [backoff(i) for i in range(1,8)]==[60,120,240,480,900,900,900]
    for i in range(130):
        d=getcontrol(db,'held','held');redrive(db,now=d['nextUtc']);db=db.restart();redrive_reconcile(db,now=getcontrol(db,'held','held')['updatedUtc'])
        assert len(canonical(getcontrol(db,'held','held')))<CAPS['held'] and len(ledger_get(db)['charges'])==2
    # Valid fixture signature cannot substitute another owner or original count.
    for tag,value in ((2,'foreign-tenant'),(4,0)):
        probe=newdb();held_init(probe);capture(probe,b'exact carrier')
        try:redrive(probe,crash='commit')
        except Crash:pass
        d=getcontrol(probe,'held','held');fields=read_record(bytes.fromhex(d['request']['claim']),ANSWERS['records']['D36-redrive'])
        if tag==2:fields[tag][1]=value
        else:fields[tag][1]=1
        raw=record('HX-EV-REDRIVE-REQUEST-2',fields);d['request']['claim']=raw.hex();d['request']['signature']=fixture_sign(raw);d['attempt']['requestHash']=hashbytes(raw)
        savecontrol(probe,'held','held',d);unchanged(probe,lambda:send_held(probe))
    probe=newdb();execution_init(probe);begin_resume(probe);d=getcontrol(probe,'execution','execution')
    fields=read_record(bytes.fromhex(d['request']['claim']),ANSWERS['records']['D45-request']);fields[1][1]='foreign-tenant'
    raw=record('HX-EV-PUBLICATION-RESUME-3',fields);d['request']['claim']=raw.hex();d['request']['signature']=fixture_sign(raw);savecontrol(probe,'execution','execution',d)
    unchanged(probe,lambda:resume_step(probe),'resume-claim-owner')
    # Explicit absent/corrupt repair, no send on unavailable or forged evidence.
    d=getcontrol(db,'held','held');redrive(db,now=d['nextUtc']);d=getcontrol(db,'held','held');original=copy.deepcopy(d['request']);d['request']=None;savecontrol(db,'held','held',d);redrive_reconcile(db,now=d['updatedUtc'])
    assert getcontrol(db,'held','held')['repair']=='absent';unchanged(db,lambda:redrive(db,now=d['updatedUtc']+900*TICK));unchanged(db,lambda:repair_held(db))
    repair_held(db,claim=original);d=getcontrol(db,'held','held');assert d['reason']=='handler-capability-hold' and d['repair']=='repaired'
    db.unavailable.add('object:held');unchanged(db,lambda:redrive(db,now=d['nextUtc']));db.unavailable.clear()
    # Refund last, partial deletion remains discoverable after restart.
    try:erase_held(db,crash='object')
    except Crash:pass
    db=db.restart();assert any(r['address']=='held' for r in registry(db)['rows']) and ledger_get(db)['charges']
    erase_held(db);assert db.read('held','held') is None and not ledger_get(db)['charges'] and not registry(db)['rows']
    # Fair queue: old tenant-blocked entry remains in one queue, another tenant progresses.
    db=newdb();
    reserve_charge(db,'tenantblock','tenant','a',64*MIB-16385-6)
    sources={}
    for i,t in enumerate(('a','b','c'),1):
        s=hashbytes(str(i).encode());sources[s]=[{'message':'p'+str(i),'length':20000 if i==1 else 10}];queue_reserve(db,s,t,ZERO,0);queue_materialize(db,s,sources[s],0)
    assert queue_turn(db,sources)==2
    assert [r['ticket'] for r in getcontrol(db,'queue','queue')['rows']]==[1,3]
    refund(db,'tenantblock',True);assert queue_turn(db,sources)==1 and queue_turn(db,sources)==3
    assert not getcontrol(db,'queue','queue')['rows'] and getcontrol(db,'queue','queue')['lastTicket']==3
    unchanged(db,lambda:batch(db,hashbytes(b'2'),'other',sources[hashbytes(b'2')]),'batch-conflict')
    unchanged(db,lambda:batch(db,'invalid','t',[{'message':'p','length':-1}]))
    unchanged(db,lambda:batch(db,'too-big','t',[{'message':'p','length':449*MIB+1}]))
    s=hashbytes(b'ticket');q=getcontrol(db,'queue','queue');q['lastTicket']=MAX;savecontrol(db,'queue','queue',q);unchanged(db,lambda:queue_reserve(db,s,'t',ZERO,0),'arithmetic')
    # Scope availability, unchanged closed-obligation refusal, tombstone 410 and exact refund.
    db=newdb();key=scope_admit(db,'t','e',ZERO);db=db.restart();unchanged(db,lambda:scope_compact(db,key,0,False),'scope-open-obligation');scope_compact(db,key,0,True);assert scope_status(db,key)==410
    unchanged(db,lambda:scope_admit(db,'t','e',ZERO),'idempotency-expired');scope_expire(db,key,30*DAY,True);scope_expire(db,key,30*DAY,True);assert scope_admit(db,'t','e',hashbytes(b'new'))==key
    # Paging scope isolation and generation conflict reflects authoritative owner changes.
    db=newdb();execution_init(db,'e1');execution_init(db,'e2');page,cursor=inventory_page(db,'tenant','t',1);assert len(page)==1 and cursor
    unchanged(db,lambda:inventory_page(db,'deployment','t',1,cursor),'cursor-scope')
    d=getcontrol(db,'e1','execution');d['reason']='changed';savecontrol(db,'e1','execution',d)
    unchanged(db,lambda:inventory_page(db,'tenant','t',1,cursor),'hold_inventory_generation_changed')
    # Max escaped legal inputs fit fixed control/registry caps, no silent truncation.
    db=newdb();e=execution_init(db);e['roster']=[{'position':i,'message':'\x01'*1023+chr(64+i),'digest':ZERO} for i in range(1,60)];e['accepted']=[];e['unresolved']=list(range(1,60));typed('execution',canonical(e));assert len(canonical(e))<CAPS['execution']
    h=held_init(db);h.update(component='\x01'*1024,topic='\x01'*1024,subscription='\x01'*1024,deployment='\x01'*1024,scopeId='\x01'*1024,tenant='\x01'*1024,locator={'backend':'\x01'*1024,'key':'\x01'*4096},request={'claim':('ab'*3072),'signature':('ab'*8192),'expectedCount':0,'utc':0});typed('held',canonical(h));assert len(canonical(h))<CAPS['held']
    row={'scopeKind':'tenant','scopeId':'\x01'*1024,'subject':'\x01'*4096,'owner':'\x01'*1024,'address':'\x01'*256,'firstUtc':0,'state':'reserved'};assert len(canonical(row))<=65536
    refused(lambda:typed('execution',b'x'*(CAPS['execution']+1)),'control-byte-bound')
    badq={'schema':SCHEMAS['queue'],'deployment':'dep','generation':1,'lastTicket':0,'rows':[{'subject':ZERO,'tenant':'t','scope':ZERO,'plan':ZERO,'ticket':1,'firstUtc':0,'updatedUtc':0,'state':'queued','candidate':ZERO,'amount':1,'attempts':0,'charge':'c','owner':'o'}]};refused(lambda:typed('queue',canonical(badq)),'ticket')
    # Meaningful model mutation: wrong accepted partition fails its owning decoder.
    bad=copy.deepcopy(e);bad['accepted']=[1];refused(lambda:typed('execution',canonical(bad)),'partition')
    return restarts

def dispositions():
    text=(HERE/'obligations.md').read_text();section=text.split('<!-- pass2-dispositions-start -->')[1].split('<!-- pass2-dispositions-end -->')[0]
    ids=re.findall(r'^\| ((?:VG2|BH2|E2)-[^ |]+) \|',section,re.M)
    expected={'VG2-'+x for x in ['2','3','4','O1','O2','O3']}|{'BH2-'+str(i) for i in [1,2,3,4,5,6,7,8,9,10,16,18,19]}|{'E2-'+str(i) for i in list(range(1,27))+[28,29,30,31,32,34,35,36,38]}
    assert len(ids)==54 and len(set(ids))==54 and set(ids)==expected,'54 routed dispositions'
    assert 'obsolete' in text and 'provider' in text

def main():
    verify_answers();focused_corrections();restarts=scenarios();dispositions()
    print(f'6.5d simplified verifier: {len(ANSWERS["records"])} retained wire answers, {len(ANSWERS["keys"])} key answers, {len(ANSWERS["controls"])} control answers; 54 dispositions; 16 status cases; four matrix rows; {restarts} persisted resume restarts; 67 lifetime resumes; 131 bounded redrives; framing/bound/transition/queue/model corruptions refused. Fixture crypto and transactions only; provider gate remains unproved.')

if __name__=='__main__':main()
