// Independent surviving parent preimage constructor; no Python encoder is loaded.
import crypto from 'node:crypto';
const cat=(...parts)=>Buffer.concat(parts);
const hash=x=>crypto.createHash('sha256').update(x).digest();
const sized=x=>{const b=Buffer.isBuffer(x)?x:Buffer.from(x,'utf8');const n=Buffer.alloc(4);n.writeUInt32BE(b.length);return cat(n,b);};
const u64=x=>{const b=Buffer.alloc(8);b.writeBigUInt64BE(BigInt(x));return b;};
const record=(name,fields)=>{const count=Buffer.alloc(2);count.writeUInt16BE(fields.length);return cat(Buffer.from(name+'\0','ascii'),Buffer.from([1]),count,...fields.map((x,i)=>cat(Buffer.from([i+1]),x)));};
// Separate standard HMAC-SHA256 constructor; the preserved child prefix hash is historical model evidence.
if (process.argv[2]==='--cursor') {
    const canonical=x=>Buffer.from(JSON.stringify(x,(_,v)=>v && !Array.isArray(v) && typeof v==='object'
        ? Object.fromEntries(Object.keys(v).sort().map(k=>[k,v[k]])) : v),'utf8');
    const image=bytes=>({length:bytes.length,sha256:hash(bytes).toString('hex'),hex:bytes.toString('hex')});
    const generation=canonical([7,[['a',2],['é',3]]]);
    const absent=canonical([null,[]]);
    const payload={schema:'hexalith.eventstore.hold-cursor/1',scopeKind:'tenant',scopeId:'t',
        generation:hash(generation).toString('hex'),last:[1,'tenant','t','a'],expiry:9000000000};
    const key=Buffer.from(Array.from({length:32},(_,i)=>i));
    const signature=crypto.createHmac('sha256',key).update(canonical(payload)).digest('hex');
    const envelope={schema:'hexalith.eventstore.hold-cursor-envelope/1',keyId:'integration-fixture',payload,signature};
    console.log(JSON.stringify({generation:image(generation),absentGeneration:image(absent),
        keyHex:key.toString('hex'),payload:image(canonical(payload)),signature,envelope:image(canonical(envelope))}));
    process.exit(0);
}
const scope=hash(cat(...['t','d','counter','a','op'].map(sized)));
const tick=639000000000000000n;
const answers={
    'I08-outcome-prep':record('HX-EV-COMMAND-OUTCOME-PREP-1',[scope,sized('command-outcome:'+scope.toString('hex')+':0'),u64(0),hash(Buffer.from('outcome-0')),hash(Buffer.from('head-0')),u64(1),hash(Buffer.from('outcome-1')),hash(Buffer.from('head-1')),u64(7),sized('owner-1')]),
    'I08-key-preimage':cat(scope,u64(1),hash(Buffer.from('outcome-1'))),
    'I09-preparation-write':record('HX-EV-RESPONSE-PREPARATION-WRITE-1',[scope,u64(7),sized('owner-1'),...['response-input','response-record','outcome-0','response-cas-receipt','outcome-cas-receipt'].map(x=>hash(Buffer.from(x))),u64(tick),u64(8)]),
    'I17-destination-config':Buffer.from('{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"t.d.events"}'),
    'I33-preimage':cat(Buffer.from('HX-EV-ROUTE-DECISION-KEY-HASH-1\0','ascii'),Buffer.from([1]),sized('route-decision-key')),
};
console.log(JSON.stringify(Object.fromEntries(Object.entries(answers).map(([label,bytes])=>[label,{length:bytes.length,sha256:hash(bytes).toString('hex'),hex:bytes.toString('hex')}]))));
